using System.Net;
using System.Net.Mail;
using AuthApi.Models;

namespace AuthApi.Services;

public class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string bodyHtml)
    {
        var settings = configuration.GetSection(EmailSettings.SectionName).Get<EmailSettings>() ?? new EmailSettings();

        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromEmail))
        {
            logger.LogWarning("EmailSettings nao configurado. Email para {ToEmail} nao foi enviado.", toEmail);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName ?? settings.FromEmail),
            Subject = subject,
            Body = bodyHtml,
            IsBodyHtml = true
        };

        message.To.Add(toEmail);

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(settings.UserName)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(settings.UserName, settings.Password)
        };

        await client.SendMailAsync(message);
    }
}