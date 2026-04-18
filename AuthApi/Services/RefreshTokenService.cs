using System.Security.Cryptography;
using System.Text;
using AuthApi.Data;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public class RefreshTokenService(AppDbContext dbContext) : IRefreshTokenService
{
    private const int RefreshTokenExpirationDays = 7;

    public async Task<(RefreshToken PersistedToken, string RawToken)> CreateAsync(ApplicationUser user, string ipAddress)
    {
        var rawToken = GenerateSecureToken();
        var tokenHash = HashToken(rawToken);

        var refreshToken = new RefreshToken
        {
            TokenHash = tokenHash,
            UserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByIp = ipAddress,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays)
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync();

        return (refreshToken, rawToken);
    }

    public Task<RefreshToken?> FindByRawTokenAsync(string rawToken)
    {
        var tokenHash = HashToken(rawToken);
        return dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
    }

    public async Task<(RefreshToken NewToken, string NewRawToken)> RotateAsync(RefreshToken currentToken, string ipAddress)
    {
        var newRawToken = GenerateSecureToken();
        var newTokenHash = HashToken(newRawToken);

        currentToken.RevokedAtUtc = DateTime.UtcNow;
        currentToken.RevokedByIp = ipAddress;
        currentToken.ReplacedByTokenHash = newTokenHash;

        var newToken = new RefreshToken
        {
            TokenHash = newTokenHash,
            UserId = currentToken.UserId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByIp = ipAddress,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays)
        };

        dbContext.RefreshTokens.Add(newToken);
        await dbContext.SaveChangesAsync();

        return (newToken, newRawToken);
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}