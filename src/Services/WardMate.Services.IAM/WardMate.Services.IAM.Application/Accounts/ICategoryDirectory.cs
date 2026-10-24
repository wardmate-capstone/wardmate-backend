namespace WardMate.Services.IAM.Application.Accounts;

public interface ICategoryDirectory
{
    Task<int[]> ReadIds(CancellationToken ct);
}
