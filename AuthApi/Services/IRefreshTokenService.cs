using AuthApi.Models;

namespace AuthApi.Services;

public interface IRefreshTokenService
{
    Task<(RefreshToken PersistedToken, string RawToken)> CreateAsync(ApplicationUser user, string ipAddress);
    Task<RefreshToken?> FindByRawTokenAsync(string rawToken);
    Task<(RefreshToken NewToken, string NewRawToken)> RotateAsync(RefreshToken currentToken, string ipAddress);
}