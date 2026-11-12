using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.UnitTests.Catalog;

// PROC-UNPUBLISH-01: xuất bản tách khỏi công khai; chỉ thủ tục chưa từng công khai mới hủy xuất bản được.
public sealed class UnpublishTests
{
    [Fact]
    public async Task Publishing_new_draft_creates_hidden_procedure()
    {
        var store = new Store();
        var input = new ReviewedProcedureInput { CategoryId = 1, ProcedureCode = "P-NEW", Title = "Thủ tục mới" };
        var result = await new PublishReviewedProcedureHandler(store, new ProcedureInputValidator())
            .Handle(new PublishReviewedProcedureCommand(input), default);
        Assert.False(result.Value!.IsActive);
        Assert.Null(result.Value.FirstPublishedAt);
        Assert.True(store.Added!.CanUnpublish);
    }

    [Fact]
    public async Task First_activation_sets_first_published_at_once()
    {
        var store = new Store { Locked = new Procedure { Id = Guid.NewGuid(), IsActive = false } };
        var handler = new ToggleProcedureStatusCommandHandler(store);
        await handler.Handle(new ToggleProcedureStatusCommand(store.Locked.Id, true, null), default);
        var first = store.Locked.FirstPublishedAt;
        Assert.NotNull(first);
        await handler.Handle(new ToggleProcedureStatusCommand(store.Locked.Id, false, "Tạm ẩn"), default);
        await handler.Handle(new ToggleProcedureStatusCommand(store.Locked.Id, true, null), default);
        Assert.Equal(first, store.Locked.FirstPublishedAt);
        Assert.False(store.Locked.CanUnpublish);
    }

    [Theory]
    [InlineData(false, false, true)]   // xuất bản nhưng chưa công khai
    [InlineData(true, false, false)]   // đang công khai
    [InlineData(false, true, false)]   // từng công khai rồi ẩn
    public void Only_never_public_procedure_can_be_unpublished(bool active, bool everPublic, bool expected) =>
        Assert.Equal(expected, new Procedure { IsActive = active, FirstPublishedAt = everPublic ? DateTime.UtcNow : null }.CanUnpublish);

    [Fact]
    public void Unpublished_draft_returns_to_review()
    {
        var draft = new ProcedureDraft { Status = "Published", PublishedProcedureId = Guid.NewGuid(), ReviewedBy = "manager" };
        var revision = draft.Revision;
        draft.ReturnToReview(DateTime.UtcNow);
        Assert.Equal("NeedsReview", draft.Status);
        Assert.Null(draft.PublishedProcedureId);
        Assert.Null(draft.ReviewedBy);
        Assert.NotEqual(revision, draft.Revision);
    }

    private sealed class Store : IProcedureManagementStore
    {
        public Procedure? Locked { get; init; }
        public Procedure? Added { get; private set; }
        public Task<ProcedureCategory?> FindCategory(int id, CancellationToken ct) =>
            Task.FromResult<ProcedureCategory?>(new ProcedureCategory { Id = id, CategoryName = "Hộ tịch" });
        public Task<Procedure?> LockProcedureByCode(string code, CancellationToken ct) => Task.FromResult<Procedure?>(null);
        public Task<Procedure?> LockProcedure(Guid id, CancellationToken ct) => Task.FromResult(Locked);
        public void Add(Procedure procedure) => Added = procedure;
        public void AddVersion(ProcedureVersion version) { }
        public Task<ProcedureResult<T>> Transaction<T>(Func<Task<ProcedureResult<T>>> action, CancellationToken ct) => action();
        public Task<bool> CodeExists(string code, Guid? excludingId, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> NextVersion(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> Exists(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProcedureVersion>> ListVersions(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
}
