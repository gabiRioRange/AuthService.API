namespace AuthApi.Models;

public class AccessTokenResult
{
    public required string AccessToken { get; set; }
    public required DateTime ExpiresAtUtc { get; set; }
}