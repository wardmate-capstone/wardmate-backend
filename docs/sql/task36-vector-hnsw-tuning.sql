-- =============================================================================
-- TASK-36: Vector Tuning & HNSW Indexing for AIOCR Knowledge Base
-- Target Database: WardMate AIOCR / PostgreSQL 15+ with pgvector
-- Model: Gemini text-embedding-004 / gemini-embedding-001 (768 dimensions)
-- Date: 2026-11-03
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 1. Kích hoạt extension pgvector
-- Lưu ý: Supabase mặc định hỗ trợ pgvector, cần quyền superuser hoặc postgres role.
-- ---------------------------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS vector;

-- ---------------------------------------------------------------------------
-- 2. Chuyển đổi hoặc bổ sung cột vector chuẩn (768 chiều)
-- Cột hiện tại 'embedding' kiểu 'real[]' được chuyển sang kiểu 'vector(768)'.
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    -- Nếu cột embedding_vector chưa có, tạo mới
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'knowledge_chunks' 
          AND column_name = 'embedding_vector'
    ) THEN
        ALTER TABLE public.knowledge_chunks 
        ADD COLUMN embedding_vector vector(768);
    END IF;
END $$;

-- Đồng bộ dữ liệu từ real[] sang vector(768) nếu có dữ liệu sẵn
UPDATE public.knowledge_chunks 
SET embedding_vector = (embedding)::text::vector 
WHERE embedding IS NOT NULL 
  AND embedding_vector IS NULL 
  AND array_length(embedding, 1) = 768;

-- ---------------------------------------------------------------------------
-- 3. Tạo chỉ mục HNSW (Hierarchical Navigable Small World)
-- Cấu hình Tuning:
--   - metric: vector_cosine_ops (khoảng cách Cosine phù hợp với embedding chuẩn hóa)
--   - m = 16 (số kết nối tối đa mỗi node: cân bằng giữa tốc độ tìm kiếm và bộ nhớ)
--   - ef_construction = 64 (kích thước danh sách ứng viên khi xây dựng index)
-- ---------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS "ix_knowledge_chunks_embedding_hnsw"
    ON public.knowledge_chunks 
    USING hnsw (embedding_vector vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);

-- ---------------------------------------------------------------------------
-- 4. Function tìm kiếm tương đồng ngữ nghĩa (Cosine Similarity Search)
-- Tham số tìm kiếm:
--   - query_embedding: vector câu hỏi người dùng (768d)
--   - match_threshold: ngưỡng tương đồng tối thiểu (mặc định 0.65)
--   - match_count: số lượng chunks tối đa trả về (topK, mặc định 5)
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.search_knowledge_chunks(
    query_embedding vector(768),
    match_threshold float DEFAULT 0.65,
    match_count int DEFAULT 5,
    filter_source_type varchar(20) DEFAULT NULL
)
RETURNS TABLE (
    chunk_id uuid,
    document_id uuid,
    document_title varchar(500),
    source_type varchar(20),
    procedure_id uuid,
    heading varchar(500),
    content text,
    similarity float
)
LANGUAGE plpgsql
AS $$
BEGIN
    -- Tinh chỉnh tham số tìm kiếm trong phiên truy vấn
    -- ef_search = 40: cân bằng chính xác recall > 98% và thời gian phản hồi < 5ms
    SET LOCAL hnsw.ef_search = 40;

    RETURN QUERY
    SELECT 
        kc.id AS chunk_id,
        kd.id AS document_id,
        kd.title AS document_title,
        kd.source_type,
        kd.procedure_id,
        kc.heading,
        kc.content,
        (1 - (kc.embedding_vector <=> query_embedding))::float AS similarity
    FROM public.knowledge_chunks kc
    JOIN public.knowledge_documents kd ON kc.document_id = kd.id
    WHERE kd.is_active = true
      AND kc.embedding_vector IS NOT NULL
      AND (filter_source_type IS NULL OR kd.source_type = filter_source_type)
      AND (1 - (kc.embedding_vector <=> query_embedding)) >= match_threshold
    ORDER BY kc.embedding_vector <=> query_embedding ASC
    LIMIT match_count;
END;
$$;

-- ---------------------------------------------------------------------------
-- 5. Cấu hình Runtime Parameter cho PostgreSQL Session
-- Khuyến nghị đặt trong postgresql.conf hoặc connection pooling:
--   maintenance_work_mem = '128MB' (giúp tạo index HNSW nhanh hơn)
--   hnsw.ef_search = 40 (đảm bảo độ chính xác recall cao trong các truy vấn)
-- ---------------------------------------------------------------------------

