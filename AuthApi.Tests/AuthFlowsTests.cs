using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuthApi.DTOs.Auth;
using AuthApi.Tests.Infrastructure;

namespace AuthApi.Tests;

public class AuthFlowsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_ThenConfirmEmail_ThenLogin_ShouldReturnTokens()
    {
        var email = $"user_{Guid.NewGuid():N}@mail.com";
        var password = "Senha@1234";

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "Usuario Teste",
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var registerJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = registerJson.GetProperty("userId").GetString();
        var emailConfirmationToken = ExtractTokenFromLatestEmail(factory, "Confirme seu email");

        var confirmResponse = await _client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequestDto
        {
            UserId = userId!,
            Token = emailConfirmationToken
        });

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var authJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(authJson.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(authJson.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task Login_WithoutConfirmedEmail_ShouldReturnUnauthorized()
    {
        var email = $"pending_{Guid.NewGuid():N}@mail.com";

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "Usuario Pendente",
            Email = email,
            Password = "Senha@1234"
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = "Senha@1234"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ShouldRotateRefreshToken()
    {
        var email = $"refresh_{Guid.NewGuid():N}@mail.com";
        const string password = "Senha@1234";

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "Usuario Refresh",
            Email = email,
            Password = password
        });
        var registerJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();

        await _client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequestDto
        {
            UserId = registerJson.GetProperty("userId").GetString()!,
            Token = ExtractTokenFromLatestEmail(factory, "Confirme seu email")
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });

        var loginJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginJson.GetProperty("refreshToken").GetString();

        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto
        {
            RefreshToken = refreshToken!
        });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshJson = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        var newRefreshToken = refreshJson.GetProperty("refreshToken").GetString();

        Assert.NotEqual(refreshToken, newRefreshToken);
    }

    [Fact]
    public async Task ForgotPassword_ThenResetPassword_ShouldAllowLoginWithNewPassword()
    {
        var email = $"reset_{Guid.NewGuid():N}@mail.com";
        const string initialPassword = "Senha@1234";
        const string newPassword = "NovaSenha@1234";

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "Usuario Reset",
            Email = email,
            Password = initialPassword
        });
        var registerJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();

        await _client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequestDto
        {
            UserId = registerJson.GetProperty("userId").GetString()!,
            Token = ExtractTokenFromLatestEmail(factory, "Confirme seu email")
        });

        var forgotResponse = await _client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto
        {
            Email = email
        });

        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);

        var resetToken = ExtractTokenFromLatestEmail(factory, "Recuperacao de senha");

        var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequestDto
        {
            Email = email,
            Token = resetToken,
            NewPassword = newPassword
        });

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var loginWithOldPassword = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = initialPassword
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginWithOldPassword.StatusCode);

        var loginWithNewPassword = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = newPassword
        });

        Assert.Equal(HttpStatusCode.OK, loginWithNewPassword.StatusCode);
    }

    private static string ExtractTokenFromLatestEmail(CustomWebApplicationFactory factory, string subject)
    {
        var email = factory.EmailSender.Emails.LastOrDefault(e => e.Subject.Contains(subject, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Email com assunto '{subject}' nao foi enviado.");

        var start = email.BodyHtml.IndexOf("<strong>", StringComparison.OrdinalIgnoreCase);
        var end = email.BodyHtml.IndexOf("</strong>", StringComparison.OrdinalIgnoreCase);
        if (start < 0 || end < 0 || end <= start)
        {
            throw new InvalidOperationException("Token nao encontrado no corpo do email.");
        }

        start += "<strong>".Length;
        return email.BodyHtml[start..end];
    }
}