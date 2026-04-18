using AuthApi.Models;

namespace AuthApi.Services;

public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(ApplicationUser user, IList<string> roles);
}