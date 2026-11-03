-- =============================================================================
-- TASK-36: Database Index Optimization
-- Phạm vi: 6 microservices · WardMate Production (Supabase PostgreSQL)
-- Ngày tạo: 2026-11-03
-- Mục đích: Bổ sung các chỉ mục B-Tree, GIN và Partial Index còn thiếu
--           nhằm tối ưu hóa hiệu năng truy vấn trên toàn hệ thống.
-- Hướng dẫn: Chạy từng khối WITHIN một transaction riêng trên Supabase
--             SQL Editor (Run without RLS). Mỗi lệnh đều dùng IF NOT EXISTS
--             nên an toàn khi chạy nhiều lần.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- [1] SERVICE: IAM (public schema)
-- Lý do: Tra cứu người dùng theo Ward (quản lý nhân viên), lọc token hết
--         hạn khi logout hàng loạt, lọc OTP theo user và thời gian tạo.
-- ---------------------------------------------------------------------------

-- 1.1 users: lọc nhân viên theo phường (WardCode) và trạng thái kích hoạt
CREATE INDEX IF NOT EXISTS "IX_users_ward_code_is_active"
    ON public.users (ward_code, is_active)
    WHERE ward_code IS NOT NULL;

-- 1.2 users: sắp xếp danh sách theo thời gian tạo (phân trang)
CREATE INDEX IF NOT EXISTS "IX_users_created_at"
    ON public.users (created_at DESC);

-- 1.3 refresh_tokens: dọn token hết hạn hoặc bị thu hồi
CREATE INDEX IF NOT EXISTS "IX_refresh_tokens_expires_at_is_revoked"
    ON public.refresh_tokens (expires_at)
    WHERE is_revoked = false;

-- 1.4 refresh_tokens: tra cứu nhanh token của một user cụ thể
CREATE INDEX IF NOT EXISTS "IX_refresh_tokens_user_id_expires_at"
    ON public.refresh_tokens (user_id, expires_at DESC);

-- 1.5 user_otps: tra cứu OTP chưa dùng của user (xác thực OTP)
CREATE INDEX IF NOT EXISTS "IX_user_otps_user_id_expires_at"
    ON public.iam.user_otps (user_id, expires_at_utc)
    WHERE is_used = false;

-- 1.6 rbac_audit_logs: tìm kiếm theo actorId (ai đã phân quyền gì)
CREATE INDEX IF NOT EXISTS "IX_rbac_audit_logs_actor_id_created_at"
    ON public.rbac_audit_logs (actor_id, created_at DESC);

-- 1.7 rbac_audit_logs: lọc theo target_type và target_id (quyền của ai)
CREATE INDEX IF NOT EXISTS "IX_rbac_audit_logs_target_type_target_id"
    ON public.rbac_audit_logs (target_type, target_id);

-- 1.8 user_profiles: tìm kiếm Full-Text theo tên nhân viên (GIN + unaccent)
CREATE INDEX IF NOT EXISTS "IX_user_profiles_full_name_gin"
    ON public.user_profiles USING GIN (to_tsvector('simple', coalesce(full_name, '')));

-- ---------------------------------------------------------------------------
-- [2] SERVICE: WORKFLOW (public schema — snake_case)
-- Lý do: Hồ sơ chiếm volume lớn nhất hệ thống; truy vấn đa chiều theo
--         Ward, Officer, Status, Deadline là bottleneck chính.
-- ---------------------------------------------------------------------------

-- 2.1 applications: lọc hồ sơ đang xử lý của phường (dashboard chính)
CREATE INDEX IF NOT EXISTS "IX_applications_ward_code_status_submitted_at"
    ON public.applications (ward_code, status, submitted_at DESC)
    WHERE status NOT IN ('CANCELLED','REJECTED','APPROVED');

-- 2.2 applications: hồ sơ của nhân viên phụ trách đang xử lý
CREATE INDEX IF NOT EXISTS "IX_applications_assigned_officer_status"
    ON public.applications (assigned_officer_id, status)
    WHERE assigned_officer_id IS NOT NULL
      AND status NOT IN ('CANCELLED','REJECTED','APPROVED','DRAFT');

-- 2.3 applications: tra cứu deadline sắp đến hạn (cảnh báo SLA)
CREATE INDEX IF NOT EXISTS "IX_applications_deadline_due_at"
    ON public.applications (ward_code, due_at_utc ASC)
    WHERE deadline_status = 'KNOWN'
      AND due_at_utc IS NOT NULL
      AND status NOT IN ('CANCELLED','REJECTED','APPROVED');

-- 2.4 applications: tìm kiếm GIN trên form_data (JSONB — tìm kiếm nộp)
CREATE INDEX IF NOT EXISTS "IX_applications_form_data_gin"
    ON public.applications USING GIN (form_data);

-- 2.5 applications: tìm kiếm GIN trên deadline_history (JSONB)
CREATE INDEX IF NOT EXISTS "IX_applications_deadline_history_gin"
    ON public.applications USING GIN (deadline_history);

-- 2.6 application_status_history: lịch sử trạng thái theo ứng dụng (audit trail)
CREATE INDEX IF NOT EXISTS "IX_application_status_history_application_id_created_at"
    ON public.application_status_history (application_id, created_at DESC);

-- 2.7 application_versions: tìm phiên bản mới nhất của hồ sơ
CREATE INDEX IF NOT EXISTS "IX_application_versions_application_id_version_number"
    ON public.application_versions (application_id, version_number DESC);

-- 2.8 application_versions: tìm kiếm GIN trên snapshot_data (JSONB)
CREATE INDEX IF NOT EXISTS "IX_application_versions_snapshot_data_gin"
    ON public.application_versions USING GIN (snapshot_data);

-- 2.9 application_comments: lấy bình luận chưa giải quyết của hồ sơ
CREATE INDEX IF NOT EXISTS "IX_application_comments_application_id_status"
    ON public.application_comments (application_id, status)
    WHERE status = 'OPEN';

-- 2.10 application_assignment_history: lịch sử phân công của nhân viên
CREATE INDEX IF NOT EXISTS "IX_application_assignment_history_officer_id_created_at"
    ON public.application_assignment_history (officer_id, created_at DESC);

-- 2.11 workflow_outbox: tin nhắn chờ gửi (Relay Worker polling)
CREATE INDEX IF NOT EXISTS "IX_workflow_outbox_pending"
    ON public.workflow_outbox (next_attempt_at ASC)
    WHERE published_at IS NULL;

-- 2.12 officer_shifts: xem ca làm của nhân viên trong khoảng thời gian
CREATE INDEX IF NOT EXISTS "IX_officer_shifts_ward_code_starts_at"
    ON public.officer_shifts (ward_code, starts_at_utc, ends_at_utc);

-- 2.13 officer_leaves: xem đơn nghỉ phép của phường theo trạng thái
CREATE INDEX IF NOT EXISTS "IX_officer_leaves_ward_code_status_starts_at"
    ON public.officer_leaves (ward_code, status, starts_at_utc);

-- 2.14 application_consultations: tư vấn đang mở của hồ sơ
CREATE INDEX IF NOT EXISTS "IX_application_consultations_application_id_status"
    ON public.application_consultations (application_id, status)
    WHERE status <> 'CLOSED';

-- ---------------------------------------------------------------------------
-- [3] SERVICE: DOCUMENTFORM (schema: document)
-- Lý do: Lookup biểu mẫu theo ứng dụng, lọc tài liệu đang chờ duyệt,
--         và quản lý upload file theo Submission.
-- ---------------------------------------------------------------------------

-- 3.1 application_forms: tra cứu form của hồ sơ theo phiên bản (multi-version form)
CREATE INDEX IF NOT EXISTS "IX_application_forms_application_id_created_at"
    ON document.application_forms (application_id, created_at DESC);

-- 3.2 supporting_documents: tìm tài liệu chưa xử lý của một hồ sơ
CREATE INDEX IF NOT EXISTS "IX_supporting_documents_application_id_status"
    ON document.supporting_documents (application_id, status);

-- 3.3 supporting_documents: lọc theo loại tài liệu và phường
CREATE INDEX IF NOT EXISTS "IX_supporting_documents_document_type_ward_code"
    ON document.supporting_documents (document_type, ward_code);

-- 3.4 generated_documents: tìm tài liệu đã tạo theo hồ sơ
CREATE INDEX IF NOT EXISTS "IX_generated_documents_application_id_created_at"
    ON document.generated_documents (application_id, created_at DESC);

-- 3.5 form_templates: tra cứu biểu mẫu theo thủ tục (procedure)
CREATE INDEX IF NOT EXISTS "IX_form_templates_procedure_id_is_active"
    ON document.form_templates (procedure_id, is_active)
    WHERE is_active = true;

-- 3.6 user_submissions: tìm bản nộp đang active của user (Citizen portal)
CREATE INDEX IF NOT EXISTS "IX_user_submissions_user_id_created_at"
    ON document.user_submissions (user_id, created_at DESC);

-- 3.7 user_submissions: tìm bản nộp theo hồ sơ và phiên bản
CREATE INDEX IF NOT EXISTS "IX_user_submissions_application_id_submission_version"
    ON document.user_submissions (application_id, submission_version DESC);

-- 3.8 approved_qrs: tra cứu QR đã duyệt của hồ sơ
CREATE INDEX IF NOT EXISTS "IX_approved_qrs_application_id"
    ON document.approved_qrs (application_id);

-- ---------------------------------------------------------------------------
-- [4] SERVICE: PROCEDURE CATALOG (public schema)
-- Lý do: Hiện tại chỉ có index GIN trên ContentPayload; bổ sung thêm
--         index cho tìm kiếm Full-Text và phân loại theo danh mục.
-- ---------------------------------------------------------------------------

-- 4.1 procedures: Full-Text Search tiếng Việt (GIN + unaccent)
CREATE INDEX IF NOT EXISTS "IX_procedures_title_fts"
    ON public.procedures USING GIN (to_tsvector('simple', coalesce(title, '')));

-- 4.2 procedures: lọc thủ tục theo danh mục và trạng thái
CREATE INDEX IF NOT EXISTS "IX_procedures_category_id_status"
    ON public.procedures (category_id, status);

-- 4.3 procedures: lọc thủ tục đang hoạt động theo mã (tra cứu từ Workflow)
CREATE INDEX IF NOT EXISTS "IX_procedures_procedure_code_status"
    ON public.procedures (procedure_code, status)
    WHERE status = 'PUBLISHED';

-- 4.4 procedure_versions: lấy phiên bản mới nhất (LATEST)
CREATE INDEX IF NOT EXISTS "IX_procedure_versions_procedure_id_version_number"
    ON public.procedure_versions (procedure_id, version_number DESC);

-- 4.5 procedure_drafts: bản nháp đang được khóa để chỉnh sửa (Draft Lock)
CREATE INDEX IF NOT EXISTS "IX_procedure_drafts_status_lease_until"
    ON public.procedure_drafts (status, lease_until)
    WHERE status = 'LOCKED';

-- ---------------------------------------------------------------------------
-- [5] SERVICE: AI OCR / KNOWLEDGE BASE (public schema)
-- Lý do: Lọc documents cần embedding, tìm chunks theo model signature,
--         và tìm kiếm GIN trên citations của chat messages.
-- ---------------------------------------------------------------------------

-- 5.1 knowledge_documents: ưu tiên embedding document chưa nhúng
CREATE INDEX IF NOT EXISTS "IX_knowledge_documents_next_embedding_at"
    ON public.knowledge_documents (next_embedding_at ASC)
    WHERE status IN ('CHUNKED', 'EMBEDDING_FAILED')
      AND is_active = true;

-- 5.2 knowledge_documents: tìm document theo procedure (Knowledge Catalog sync)
CREATE INDEX IF NOT EXISTS "IX_knowledge_documents_source_type_source_reference"
    ON public.knowledge_documents (source_type, source_reference)
    WHERE is_active = true;

-- 5.3 knowledge_chunks: tìm chunks chưa được nhúng (indexing queue)
CREATE INDEX IF NOT EXISTS "IX_knowledge_chunks_embedding_model_embedded_at"
    ON public.knowledge_chunks (embedding_model, embedded_at)
    WHERE embedding IS NULL;

-- 5.4 ai_request_logs: lọc log theo outcome và operation (monitoring)
CREATE INDEX IF NOT EXISTS "IX_ai_request_logs_outcome_operation_created_at"
    ON public.ai_request_logs (outcome, operation, created_at DESC);

-- 5.5 ai_request_logs: tổng chi phí theo user (usage tracking)
CREATE INDEX IF NOT EXISTS "IX_ai_request_logs_user_id_operation_created_at"
    ON public.ai_request_logs (user_id, operation, created_at DESC);

-- 5.6 chat_sessions: tìm session theo ward (analytics)
CREATE INDEX IF NOT EXISTS "IX_chat_sessions_ward_code_created_at"
    ON public.chat_sessions (ward_code, created_at DESC);

-- 5.7 chat_messages: GIN index trên citations (JSONB — tìm kiếm nguồn trích dẫn)
CREATE INDEX IF NOT EXISTS "IX_chat_messages_citations_gin"
    ON public.chat_messages USING GIN (citations)
    WHERE citations IS NOT NULL;

-- ---------------------------------------------------------------------------
-- [6] SERVICE: ANALYTICS (schema: analytics)
-- Lý do: Tăng tốc phân tích theo khoảng thời gian và dashboard metrics.
-- ---------------------------------------------------------------------------

-- 6.1 service_health_logs: lịch sử sức khỏe gần nhất của từng service
CREATE INDEX IF NOT EXISTS "IX_service_health_logs_service_name_checked_at"
    ON analytics.service_health_logs ("ServiceName", "CheckedAtUtc" DESC);

-- 6.2 service_health_logs: lọc sức khỏe theo trạng thái và thời gian (SLA report)
CREATE INDEX IF NOT EXISTS "IX_service_health_logs_status_checked_at"
    ON analytics.service_health_logs ("Status", "CheckedAtUtc" DESC)
    WHERE "Status" IN ('Unhealthy', 'Degraded');

-- 6.3 system_backup_logs: backup theo service và trạng thái
CREATE INDEX IF NOT EXISTS "IX_system_backup_logs_service_name_status"
    ON analytics.system_backup_logs ("ServiceName", "Status", "CreatedAtUtc" DESC);

-- 6.4 business_audit_events: tìm kiếm sự kiện theo source và entity
CREATE INDEX IF NOT EXISTS "IX_business_audit_events_source_entity_occurred_at"
    ON analytics.business_audit_events ("Source", "Entity", "OccurredAtUtc" DESC);

-- 6.5 business_audit_events: GIN trên Payload (tìm theo dữ liệu nghiệp vụ)
CREATE INDEX IF NOT EXISTS "IX_business_audit_events_payload_gin"
    ON analytics.business_audit_events USING GIN ("Payload");

-- 6.6 citizen_feedback: thống kê đánh giá theo phường và thủ tục
CREATE INDEX IF NOT EXISTS "IX_citizen_feedback_ward_code_procedure_id_created_at"
    ON analytics.citizen_feedback ("WardCode", "ProcedureId", "CreatedAtUtc" DESC);

-- 6.7 application_procedure_metrics: dashboard hiệu suất hàng ngày
CREATE INDEX IF NOT EXISTS "IX_application_procedure_metrics_metric_date_ward_code"
    ON analytics.application_procedure_metrics ("MetricDate" DESC, "WardCode");

-- 6.8 audit_logs: tìm log theo actor và thời gian (ai đã sửa gì)
CREATE INDEX IF NOT EXISTS "IX_audit_logs_actor_id_occurred_at"
    ON analytics.audit_logs ("ActorId", "OccurredAtUtc" DESC);

-- ---------------------------------------------------------------------------
-- [7] SERVICE: NOTIFICATIONS (public schema — notifications DB)
-- Lý do: Lọc thông báo chưa gửi, retry queue và tra cứu theo user.
-- ---------------------------------------------------------------------------

-- 7.1 notification_outbox / notifications: lọc thông báo chưa gửi (Relay polling)
-- NOTE: Tên bảng cụ thể phụ thuộc vào migration NotificationsDB, kiểm tra trước khi chạy.
-- Uncomment sau khi xác nhận tên bảng chính xác:
-- CREATE INDEX IF NOT EXISTS "IX_notifications_status_next_attempt_at"
--     ON public.notifications (status, next_attempt_at ASC)
--     WHERE status IN ('PENDING', 'FAILED');

-- =============================================================================
-- XONG — Tổng cộng 40+ chỉ mục mới trên 6 microservices.
-- Chạy lệnh ANALYZE sau khi tạo index để cập nhật statistics:
-- ANALYZE public.users, public.applications, document.supporting_documents,
--         analytics.service_health_logs, analytics.business_audit_events;
-- =============================================================================

