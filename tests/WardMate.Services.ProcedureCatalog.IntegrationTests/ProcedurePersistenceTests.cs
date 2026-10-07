using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Application.Queries;
using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class ProcedurePersistenceTests(ProcedureFixture fixture) : IClassFixture<ProcedureFixture>
{
    private static Procedure NewProcedure()
    {
        var procedure = ProcedureSeed.CreateSample();
        procedure.Id = Guid.Empty;
        procedure.ProcedureCode = Guid.NewGuid().ToString("N");
        return procedure;
    }

    [Fact]
    public async Task MigrationCreatesProcedureAndDraftTablesJsonbColumnsIndexesAndRepeatableSeed()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        var tables = await db.Database.SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory'").ToArrayAsync();
        Assert.Equal(new[] { "procedure_categories", "procedure_drafts", "procedure_versions", "procedures" }, tables.Order().ToArray());
        var columns = await db.Database.SqlQueryRaw<string>("SELECT table_name || '.' || column_name || ':' || is_nullable AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public' AND udt_name = 'jsonb'").ToArrayAsync();
        Assert.Equal(new[] { "procedure_drafts.payload_json:NO", "procedure_drafts.warnings_json:NO", "procedure_versions.snapshot_data:NO", "procedures.checklist_schema:YES", "procedures.content_payload:NO", "procedures.form_definitions:YES" }, columns.Order().ToArray());
        var indexes = await db.Database.SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'").ToArrayAsync();
        Assert.Contains(indexes, x => x.Contains("USING gin (content_payload)", StringComparison.Ordinal));
        Assert.Contains(indexes, x => x.Contains("USING gin (checklist_schema)", StringComparison.Ordinal));
        Assert.Contains(indexes, x => x.Contains("UNIQUE", StringComparison.Ordinal) && x.Contains("USING btree (procedure_code)", StringComparison.Ordinal));
        Assert.Contains(indexes, x => x.Contains("UNIQUE", StringComparison.Ordinal) && x.Contains("(procedure_id, version_number)", StringComparison.Ordinal));
        await db.Database.MigrateAsync();
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
        var categories = await db.ProcedureCategories.Where(x => x.Id <= 3).OrderBy(x => x.Id).Select(x => x.CategoryName).ToArrayAsync();
        Assert.Equal(new[] { "Hộ tịch", "Đất đai", "Quản lý công sản" }, categories);
        var seed = await db.Procedures.SingleAsync(x => x.Id == ProcedureSeed.SampleProcedureId);
        Assert.NotEmpty(seed.ContentPayload.Cases);
        Assert.NotEmpty(seed.ChecklistSchema!);
        Assert.NotEmpty(seed.FormDefinitions!);
    }

    [Fact]
    public async Task ComplexJsonRoundTripsThroughRepositoryMediatorAndHttp()
    {
        var procedure = NewProcedure();
        var externalTemplate = Guid.NewGuid();
        procedure.ContentPayload.Cases.Add(new() { CaseCode = "BO_SUNG", CaseName = "Bổ sung hồ sơ", Steps = [new() { StepOrder = 1, StepName = "Bổ sung", Executor = "Công dân", ActionDetails = "Nội dung có dấu \"trích dẫn\"\nvà dòng mới" }] });
        procedure.ContentPayload.SubmissionMethods.Add(new() { MethodName = "Trực tuyến", FeeAmount = 1234.50m, FeeUnit = "VND", EstimatedDays = 0.5m, Note = null });
        procedure.ChecklistSchema!.Add(new() { ChecklistId = "BO_SUNG_01", CaseCode = "BO_SUNG", SubmissionType = "NOP", ItemName = "Giấy tờ bổ sung", DocumentCopyType = "CERTIFIED_COPY", Quantity = 2, IsMandatory = false });
        procedure.FormDefinitions![1].FormTemplateId = externalTemplate;
        using (var write = fixture.Factory.Services.CreateScope())
        {
            var repository = write.ServiceProvider.GetRequiredService<IProcedureRepository>();
            repository.Add(procedure);
            await repository.SaveChanges();
            Assert.NotEqual(Guid.Empty, procedure.Id);
        }
        using var read = fixture.Factory.Services.CreateScope();
        var dto = await read.ServiceProvider.GetRequiredService<ISender>().Send(new GetProcedureByIdQuery(procedure.Id));
        Assert.NotNull(dto);
        Assert.Equal(procedure.ProcedureCode, dto.ProcedureCode);
        Assert.Equal("Hộ tịch", dto.CategoryName);
        Assert.Equal(JsonSerializer.Serialize(procedure.ContentPayload), JsonSerializer.Serialize(dto.ContentPayload));
        Assert.Equal(JsonSerializer.Serialize(procedure.ChecklistSchema), JsonSerializer.Serialize(dto.ChecklistSchema));
        Assert.Equal(JsonSerializer.Serialize(procedure.FormDefinitions), JsonSerializer.Serialize(dto.FormDefinitions));
        Assert.Equal(externalTemplate, dto.FormDefinitions![1].FormTemplateId);
        using var client = fixture.Factory.CreateClient();
        var http = await client.GetFromJsonAsync<ProcedureDetailDto>($"/api/v1/procedures/{procedure.Id}");
        Assert.Equal("Bổ sung hồ sơ", http!.ContentPayload.Cases[1].CaseName);
        Assert.Equal(2, http.ChecklistSchema![2].Quantity);
        Assert.False(http.ChecklistSchema[2].IsMandatory);
    }

    [Fact]
    public async Task NestedChangesArePersistedWithoutReplacingJsonRoot()
    {
        var procedure = NewProcedure();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
            db.Procedures.Add(procedure);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var loaded = await db.Procedures.SingleAsync(x => x.Id == procedure.Id);
            loaded.ContentPayload.Cases[0].Steps[0].ActionDetails = "Nội dung đã cập nhật";
            loaded.ChecklistSchema![0].Quantity = 3;
            loaded.FormDefinitions![0].IsMandatory = false;
            await db.SaveChangesAsync();
        }
        using var read = fixture.Factory.Services.CreateScope();
        var result = await read.ServiceProvider.GetRequiredService<IProcedureRepository>().GetById(procedure.Id);
        Assert.Equal("Nội dung đã cập nhật", result!.ContentPayload.Cases[0].Steps[0].ActionDetails);
        Assert.Equal(3, result.ChecklistSchema![0].Quantity);
        Assert.False(result.FormDefinitions![0].IsMandatory);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OptionalJsonDistinguishesSqlNullFromEmptyArrays(bool useNull)
    {
        var procedure = NewProcedure();
        procedure.ChecklistSchema = useNull ? null : [];
        procedure.FormDefinitions = useNull ? null : [];
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        db.Procedures.Add(procedure);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var loaded = await db.Procedures.SingleAsync(x => x.Id == procedure.Id);
        if (useNull) { Assert.Null(loaded.ChecklistSchema); Assert.Null(loaded.FormDefinitions); }
        else { Assert.Empty(loaded.ChecklistSchema!); Assert.Empty(loaded.FormDefinitions!); }
        var isNull = await db.Database.SqlQuery<bool>($"SELECT checklist_schema IS NULL AS \"Value\" FROM procedures WHERE id = {procedure.Id}").SingleAsync();
        Assert.Equal(useNull, isNull);
    }

    [Fact]
    public async Task DuplicateProcedureCodeIsRejectedByDatabase()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        var first = NewProcedure();
        db.Procedures.Add(first);
        await db.SaveChangesAsync();
        var duplicate = NewProcedure();
        duplicate.ProcedureCode = first.ProcedureCode;
        db.Procedures.Add(duplicate);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task VersionsAreUniquePerProcedureAndSnapshotIsFrozen()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        var first = NewProcedure();
        var second = NewProcedure();
        db.Procedures.AddRange(first, second);
        await db.SaveChangesAsync();
        var version = ProcedureVersion.Capture(first, 1, new DateOnly(2026, 9, 30), "DEMO-QD-02", DateTime.UtcNow);
        var oldTitle = first.Title;
        first.Title = "Thủ tục đã đổi tên";
        first.ContentPayload.Cases.Clear();
        db.ProcedureVersions.Add(version);
        db.ProcedureVersions.Add(ProcedureVersion.Capture(second, 1, new DateOnly(2026, 9, 30), null, DateTime.UtcNow));
        await db.SaveChangesAsync();
        Assert.NotEqual(Guid.Empty, version.Id);
        db.ChangeTracker.Clear();
        var stored = await db.ProcedureVersions.SingleAsync(x => x.Id == version.Id);
        using var snapshot = JsonDocument.Parse(stored.SnapshotData);
        Assert.Equal(oldTitle, snapshot.RootElement.GetProperty("title").GetString());
        Assert.NotEmpty(snapshot.RootElement.GetProperty("contentPayload").GetProperty("cases").EnumerateArray());
        Assert.Equal(new DateOnly(2026, 9, 30), stored.EffectiveDate);
        db.ProcedureVersions.Add(ProcedureVersion.Capture(first, 1, new DateOnly(2026, 10, 1), null, DateTime.UtcNow));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task DatabaseDefaultsGenerateIdsAndSeedDoesNotBreakCategoryIdentity()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        var code = Guid.NewGuid().ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO procedures (category_id, procedure_code, title, content_payload) VALUES (1, {code}, 'Mẫu mặc định', '{{}}'::jsonb)");
        var result = await db.Procedures.SingleAsync(x => x.ProcedureCode == code);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.True(result.IsActive);
        Assert.Equal("Cấp Xã", result.LevelOfImplementation);
        Assert.Equal("Công dân Việt Nam", result.TargetAudience);
        Assert.Equal("Miễn phí", result.FeeSummary);
        Assert.Equal("1 ngày", result.ProcessingTimeSummary);
        Assert.Equal(DateTimeKind.Utc, result.CreatedAt.Kind);
        Assert.NotEqual(default, result.UpdatedAt);
        var category = new ProcedureCategory { CategoryName = "Nhóm mới" };
        db.ProcedureCategories.Add(category);
        await db.SaveChangesAsync();
        Assert.True(category.Id > 3);
    }

    [Fact]
    public async Task RequiredJsonCannotBeSqlNull()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        var procedure = NewProcedure();
        procedure.ContentPayload = null!;
        db.Procedures.Add(procedure);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.NotNullViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicQueryHidesMissingAndInactiveProcedures(bool inactive)
    {
        var id = Guid.NewGuid();
        if (inactive)
        {
            using var scope = fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
            var procedure = NewProcedure();
            procedure.IsActive = false;
            db.Procedures.Add(procedure);
            await db.SaveChangesAsync();
            id = procedure.Id;
        }
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/procedures/{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("procedure.not_found", problem.GetProperty("code").GetString());
        Assert.Equal("Không tìm thấy thủ tục đang hoạt động.", problem.GetProperty("title").GetString());
    }
}
