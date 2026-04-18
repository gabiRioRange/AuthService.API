using AuthApi.DTOs.Users;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AuthApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new UserProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty
        });
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequestDto request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var emailChanged = !string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase);
        if (emailChanged)
        {
            var existingUser = await userManager.FindByEmailAsync(request.Email);
            if (existingUser is not null && existingUser.Id != user.Id)
            {
                return Conflict(new { message = "Email ja esta em uso." });
            }

            user.Email = request.Email;
            user.UserName = request.Email;
            user.NormalizedEmail = userManager.NormalizeEmail(request.Email);
            user.NormalizedUserName = userManager.NormalizeName(request.Email);
            user.EmailConfirmed = false;
        }

        user.FullName = request.FullName;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Falha ao atualizar perfil.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        return Ok(new { message = "Perfil atualizado com sucesso." });
    }

    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Falha ao alterar senha.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        return Ok(new { message = "Senha alterada com sucesso." });
    }
}