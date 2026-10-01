using Microsoft.EntityFrameworkCore;
using Npgsql;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;

public sealed class ProcedureManagementStore(ProcedureDbContext db) : IProcedureManagementStore
{
    public Task<ProcedureCategory?> FindCategory(int id, CancellationToken ct) => db.ProcedureCategories.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> CodeExists(string code, Guid? excludingId, CancellationToken ct) =>
        db.Procedures.AnyAsync(x => x.ProcedureCode == code && (!excludingId.HasValue || x.Id != excludingId.Value), ct);

    public async Task<Procedure?> LockProcedure(Guid id, CancellationToken ct)
    {
        // All mutations serialize on the same row; next-version allocation is protected by this lock.
        var rows = await db.Procedures.FromSqlInterpolated($"SELECT * FROM procedures WHERE id = {id} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public async Task<Procedure?> LockProcedureByCode(string code, CancellationToken ct)
    {
        // Serialize concurrent publish requests even when the code has no row to lock yet.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({code}, 9009))", ct);
        var rows = await db.Procedures.FromSqlInterpolated($"SELECT * FROM procedures WHERE procedure_code = {code} FOR UPDATE").ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public async Task<int> NextVersion(Guid id, CancellationToken ct) =>
        (await db.ProcedureVersions.Where(x => x.ProcedureId == id).MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0) + 1;
    public void Add(Procedure procedure) => db.Procedures.Add(procedure);
    public void AddVersion(ProcedureVersion version) => db.ProcedureVersions.Add(version);
    public Task<bool> Exists(Guid id, CancellationToken ct) => db.Procedures.AnyAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<ProcedureVersion>> ListVersions(Guid id, CancellationToken ct) =>
        await db.ProcedureVersions.AsNoTracking().Where(x => x.ProcedureId == id).OrderByDescending(x => x.VersionNumber).ToListAsync(ct);

    public async Task<ProcedureResult<T>> Transaction<T>(Func<Task<ProcedureResult<T>>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action();
            if (!result.IsSuccess)
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return result;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_procedures_procedure_code" })
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return ProcedureResult<T>.Fail("procedure.code_exists", "Mã thủ tục đã tồn tại.", 409);
        }
    }
}
