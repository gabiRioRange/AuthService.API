namespace AuthApi.DTOs.Users;

public class UserProfileDto
{
    public required string Id { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
}