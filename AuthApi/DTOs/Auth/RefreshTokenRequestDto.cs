using System.ComponentModel.DataAnnotations;

namespace AuthApi.DTOs.Auth;

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}