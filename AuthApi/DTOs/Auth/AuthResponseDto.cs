namespace AuthApi.DTOs.Auth;

public class AuthResponseDto
{
    public required string AccessToken { get; set; }
    public required DateTime AccessTokenExpiresAtUtc { get; set; }
    public required string RefreshToken { get; set; }
    public required DateTime RefreshTokenExpiresAtUtc { get; set; }
}