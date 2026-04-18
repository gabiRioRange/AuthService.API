using Microsoft.AspNetCore.Identity;

namespace AuthApi.Models;

public class ApplicationUser : IdentityUser
{
    public required string FullName { get; set; }
}