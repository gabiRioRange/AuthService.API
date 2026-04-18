namespace AuthApi.Models;

public class RefreshToken
{
    public int Id { get; set; }
    public required string TokenHash { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public required string CreatedByIp { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsRevoked => RevokedAtUtc.HasValue;
    public bool IsActive => !IsExpired && !IsRevoked;
}