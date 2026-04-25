using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using AuthApi.DTOs.Auth;
using AuthApi.DTOs.Users;
using AuthApi.Models;
using AuthApi.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnConflict()
    {
        var email = $"dup_{Guid.NewGuid():N}@mail.com";

        var first = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "Usuario 1",
            Email = email,
            Password = "Senha@1234"
        });

        var second = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "Usuario 2",
            Email = email,
            Password = "Senha@1234"
        });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_WithUnknownUser_ShouldReturnNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequestDto
        {
            UserId = Guid.NewGuid().ToString("N"),
            Token = "invalid-token"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_WithInvalidToken_ShouldReturnBadRequest()
    {
        var email = $"confirm_{Guid.NewGuid():N}@mail.com";
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "Usuario Confirm",
            Email = email,
            Password = "Senha@1234"
        });

        var registerJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var response = await _client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequestDto
        {
            UserId = registerJson.GetProperty("userId").GetString()!,
            Token = "token-invalido"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ShouldReturnUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto
        {
            RefreshToken = "refresh-token-invalido"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_ShouldReturnOk()
    {
        var before = factory.EmailSender.Emails.Count;

        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto
        {
            Email = $"unknown_{Guid.NewGuid():N}@mail.com"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, factory.EmailSender.Emails.Count);
    }

    [Fact]
    public async Task ResetPassword_WithUnknownEmail_ShouldReturnBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequestDto
        {
            Email = $"unknown_{Guid.NewGuid():N}@mail.com",
            Token = "invalid-token",
            NewPassword = "NovaSenha@1234"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmation_WithUnknownEmail_ShouldReturnOk()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/resend-confirmation", new ForgotPasswordRequestDto
        {
            Email = $"unknown_{Guid.NewGuid():N}@mail.com"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmation_WithConfirmedEmail_ShouldReturnOk()
    {
        var email = $"resend_confirmed_{Guid.NewGuid():N}@mail.com";
        var password = "Senha@1234";

        var register = await RegisterAndConfirmAsync(email, password);
        Assert.NotNull(register);

        var response = await _client.PostAsJsonAsync("/api/auth/resend-confirmation", new ForgotPasswordRequestDto
        {
            Email = email
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmation_WithPendingEmail_ShouldSendToken()
    {
        var email = $"resend_pending_{Guid.NewGuid():N}@mail.com";
        var before = factory.EmailSender.Emails.Count;

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "Usuario Pendente",
            Email = email,
            Password = "Senha@1234"
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var response = await _client.PostAsJsonAsync("/api/auth/resend-confirmation", new ForgotPasswordRequestDto
        {
            Email = email
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(factory.EmailSender.Emails.Count > before);
    }

    [Fact]
    public async Task Me_WithoutToken_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithToken_ShouldReturnProfile()
    {
        var email = $"me_{Guid.NewGuid():N}@mail.com";
        var (_, token) = await RegisterConfirmAndLoginAsync(email, "Senha@1234");

        using var authorizedClient = factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await authorizedClient.GetAsync("/api/users/me");
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, profile.GetProperty("email").GetString());
    }

    [Fact]
    public async Task UpdateProfile_WithToken_ShouldReturnOk()
    {
        var email = $"update_{Guid.NewGuid():N}@mail.com";
        var (_, token) = await RegisterConfirmAndLoginAsync(email, "Senha@1234");

        using var authorizedClient = factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await authorizedClient.PutAsJsonAsync("/api/users/me", new UpdateProfileRequestDto
        {
            FullName = "Nome Atualizado",
            Email = email
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WithDuplicateEmail_ShouldReturnConflict()
    {
        var firstEmail = $"upd_first_{Guid.NewGuid():N}@mail.com";
        var secondEmail = $"upd_second_{Guid.NewGuid():N}@mail.com";

        var _ = await RegisterConfirmAndLoginAsync(firstEmail, "Senha@1234");
        var (_, secondToken) = await RegisterConfirmAndLoginAsync(secondEmail, "Senha@1234");

        using var authorizedClient = factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);

        var response = await authorizedClient.PutAsJsonAsync("/api/users/me", new UpdateProfileRequestDto
        {
            FullName = "Usuario Dois",
            Email = firstEmail
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ShouldReturnBadRequest()
    {
        var email = $"pwd_wrong_{Guid.NewGuid():N}@mail.com";
        var (_, token) = await RegisterConfirmAndLoginAsync(email, "Senha@1234");

        using var authorizedClient = factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await authorizedClient.PutAsJsonAsync("/api/users/change-password", new ChangePasswordRequestDto
        {
            CurrentPassword = "SenhaErrada@1234",
            NewPassword = "SenhaNova@1234"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithValidCurrentPassword_ShouldReturnOk()
    {
        var email = $"pwd_ok_{Guid.NewGuid():N}@mail.com";
        const string currentPassword = "Senha@1234";
        const string newPassword = "SenhaNova@1234";
        var (_, token) = await RegisterConfirmAndLoginAsync(email, currentPassword);

        using var authorizedClient = factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var changeResponse = await authorizedClient.PutAsJsonAsync("/api/users/change-password", new ChangePasswordRequestDto
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        });

        var oldLogin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = currentPassword
        });

        var newLogin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = newPassword
        });

        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task AdminDashboard_WithUserToken_ShouldReturnForbidden()
    {
        var email = $"userrole_{Guid.NewGuid():N}@mail.com";
        var (_, token) = await RegisterConfirmAndLoginAsync(email, "Senha@1234");

        using var authorizedClient = factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await authorizedClient.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithMultipleWrongPasswords_ShouldLockUser()
    {
        var email = $"lock_{Guid.NewGuid():N}@mail.com";
        const string password = "Senha@1234";

        await RegisterAndConfirmAsync(email, password);

        for (var i = 0; i < 5; i++)
        {
            var wrongAttempt = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
            {
                Email = email,
                Password = "SenhaErrada@1234"
            });

            Assert.Equal(HttpStatusCode.Unauthorized, wrongAttempt.StatusCode);
        }

        var lockedAttempt = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });

        var lockedJson = await lockedAttempt.Content.ReadFromJsonAsync<JsonElement>();
        var message = lockedJson.GetProperty("message").GetString() ?? string.Empty;

        Assert.Equal(HttpStatusCode.Unauthorized, lockedAttempt.StatusCode);
        Assert.Contains("bloqueado", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_ShouldReturnUnauthorized()
    {
        var email = $"revoked_{Guid.NewGuid():N}@mail.com";
        const string password = "Senha@1234";

        await RegisterAndConfirmAsync(email, password);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });

        var loginJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginJson.GetProperty("refreshToken").GetString()!;

        var rotateResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto
        {
            RefreshToken = refreshToken
        });

        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);

        var oldTokenReuseResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto
        {
            RefreshToken = refreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenReuseResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ChangingEmail_ShouldRequireNewConfirmation()
    {
        var initialEmail = $"change_email_{Guid.NewGuid():N}@mail.com";
        var newEmail = $"change_email_new_{Guid.NewGuid():N}@mail.com";
        const string password = "Senha@1234";

        var (registerJson, token) = await RegisterConfirmAndLoginAsync(initialEmail, password);

        using var authorizedClient = factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var updateResponse = await authorizedClient.PutAsJsonAsync("/api/users/me", new UpdateProfileRequestDto
        {
            FullName = "Usuario Com Novo Email",
            Email = newEmail
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var loginWithoutNewConfirmation = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = newEmail,
            Password = password
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginWithoutNewConfirmation.StatusCode);

        var resendResponse = await _client.PostAsJsonAsync("/api/auth/resend-confirmation", new ForgotPasswordRequestDto
        {
            Email = newEmail
        });

        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);

        var confirmResponse = await _client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequestDto
        {
            UserId = registerJson.GetProperty("userId").GetString()!,
            Token = ExtractTokenFromLatestEmail(factory, "Reenvio de confirmacao")
        });

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        var loginAfterConfirmation = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = newEmail,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginAfterConfirmation.StatusCode);
    }

    [Fact]
    public async Task AdminDashboard_WithAdminToken_ShouldReturnOk()
    {
        var email = $"admin_{Guid.NewGuid():N}@mail.com";
        const string password = "Senha@1234";

        var _ = await RegisterAndConfirmAsync(email, password);
        await AddRoleToUserAsync(email, "Admin");

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });

        var loginJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var adminToken = loginJson.GetProperty("accessToken").GetString();

        using var authorizedClient = factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var dashboardResponse = await authorizedClient.GetAsync("/api/admin/dashboard");
        var dashboardJson = await dashboardResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.True(dashboardJson.GetProperty("totalUsers").GetInt32() >= 1);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidTokenForExistingUser_ShouldReturnBadRequest()
    {
        var email = $"reset_invalid_{Guid.NewGuid():N}@mail.com";
        const string password = "Senha@1234";

        await RegisterAndConfirmAsync(email, password);

        var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequestDto
        {
            Email = email,
            Token = "token-invalido",
            NewPassword = "NovaSenha@1234"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resetResponse.StatusCode);
    }

    private async Task<JsonElement> RegisterAndConfirmAsync(string email, string password)
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "Usuario Teste",
            Email = email,
            Password = password
        });

        registerResponse.EnsureSuccessStatusCode();
        var registerJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();

        var confirmResponse = await _client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequestDto
        {
            UserId = registerJson.GetProperty("userId").GetString()!,
            Token = ExtractTokenFromLatestEmail(factory, "Confirme seu email")
        });

        confirmResponse.EnsureSuccessStatusCode();
        return registerJson;
    }

    private async Task<(JsonElement Register, string AccessToken)> RegisterConfirmAndLoginAsync(string email, string password)
    {
        var registerJson = await RegisterAndConfirmAsync(email, password);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });

        loginResponse.EnsureSuccessStatusCode();
        var loginJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return (registerJson, loginJson.GetProperty("accessToken").GetString()!);
    }

    private async Task AddRoleToUserAsync(string email, string role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("Usuario nao encontrado para atribuicao de role.");

        var result = await userManager.AddToRoleAsync(user, role);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Falha ao atribuir role ao usuario de teste.");
        }
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