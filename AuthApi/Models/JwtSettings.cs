namespace AuthApi.Models;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string Key { get; set; }
    public int ExpirationMinutes { get; set; } = 60;
}