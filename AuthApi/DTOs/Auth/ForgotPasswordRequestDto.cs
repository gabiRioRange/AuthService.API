using System.ComponentModel.DataAnnotations;

namespace AuthApi.DTOs.Auth;

public class ForgotPasswordRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}