using System.Text;
using WardMate.Services.IAM.Application.Interfaces;

namespace WardMate.Services.IAM.Infrastructure.Security;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        if (string.IsNullOrEmpty(password) || Encoding.UTF8.GetByteCount(password) > 72)
            throw new ArgumentException("BCrypt passwords must contain 1–72 UTF-8 bytes.", nameof(password));
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || Encoding.UTF8.GetByteCount(password) > 72) return false;
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
