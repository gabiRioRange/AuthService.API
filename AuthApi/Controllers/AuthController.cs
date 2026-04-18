using AuthApi.DTOs.Auth;
using AuthApi.Models;
using AuthApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AuthApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IEmailSender emailSender,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            return Conflict(new { message = "Email ja esta em uso." });
        }

        var user = new ApplicationUser
        {
            FullName = request.FullName,
            Email = request.Email,
            UserName = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Falha ao cadastrar usuario.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        await userManager.AddToRoleAsync(user, "User");

        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailSender.SendAsync(
            user.Email!,
            "Confirme seu email",
            $"<p>Seu token de confirmacao e:</p><p><strong>{confirmationToken}</strong></p>"
        );

        return Ok(new
        {
            message = "Usuario cadastrado com sucesso. Verifique seu email para confirmar a conta.",
            userId = user.Id
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            logger.LogWarning("Tentativa de login com email inexistente: {Email}", request.Email);
            return Unauthorized(new { message = "Email ou senha invalidos." });
        }

        if (!user.EmailConfirmed)
        {
            return Unauthorized(new { message = "Email ainda nao confirmado." });
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (signInResult.IsLockedOut)
        {
            logger.LogWarning("Usuario bloqueado por excesso de tentativas: {Email}", request.Email);
            return Unauthorized(new { message = "Usuario temporariamente bloqueado por excesso de tentativas." });
        }

        if (!signInResult.Succeeded)
        {
            logger.LogWarning("Tentativa de login com senha invalida: {Email}", request.Email);
            return Unauthorized(new { message = "Email ou senha invalidos." });
        }

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenService.CreateAccessToken(user, roles);
        var (persistedRefreshToken, rawRefreshToken) = await refreshTokenService.CreateAsync(user, GetIpAddress());

        var response = new AuthResponseDto
        {
            AccessToken = accessToken.AccessToken,
            AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
            RefreshToken = rawRefreshToken,
            RefreshTokenExpiresAtUtc = persistedRefreshToken.ExpiresAtUtc
        };

        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        var storedToken = await refreshTokenService.FindByRawTokenAsync(request.RefreshToken);
        if (storedToken is null || storedToken.User is null)
        {
            return Unauthorized(new { message = "Refresh token invalido." });
        }

        if (!storedToken.IsActive)
        {
            return Unauthorized(new { message = "Refresh token expirado ou revogado." });
        }

        var user = storedToken.User;
        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenService.CreateAccessToken(user, roles);
        var (newToken, newRawToken) = await refreshTokenService.RotateAsync(storedToken, GetIpAddress());

        return Ok(new AuthResponseDto
        {
            AccessToken = accessToken.AccessToken,
            AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
            RefreshToken = newRawToken,
            RefreshTokenExpiresAtUtc = newToken.ExpiresAtUtc
        });
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequestDto request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            return NotFound(new { message = "Usuario nao encontrado." });
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Falha ao confirmar email.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        return Ok(new { message = "Email confirmado com sucesso." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Ok(new { message = "Se o email existir, as instrucoes foram enviadas." });
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        await emailSender.SendAsync(
            user.Email!,
            "Recuperacao de senha",
            $"<p>Seu token de recuperacao de senha e:</p><p><strong>{resetToken}</strong></p>"
        );

        return Ok(new
        {
            message = "Se o email existir, as instrucoes foram enviadas."
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return BadRequest(new { message = "Token ou email invalidos." });
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Falha ao redefinir senha.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        return Ok(new { message = "Senha redefinida com sucesso." });
    }

    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation([FromBody] ForgotPasswordRequestDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Ok(new { message = "Se o email existir, um novo token foi gerado." });
        }

        if (user.EmailConfirmed)
        {
            return Ok(new { message = "Email ja confirmado." });
        }

        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailSender.SendAsync(
            user.Email!,
            "Reenvio de confirmacao de email",
            $"<p>Seu novo token de confirmacao e:</p><p><strong>{confirmationToken}</strong></p>"
        );

        return Ok(new
        {
            message = "Novo token de confirmacao enviado por email.",
            userId = user.Id
        });
    }

    private string GetIpAddress()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? "unknown" : ip;
    }
}