using AuthApi.Services;

namespace AuthApi.Tests.Infrastructure;

public record SentEmail(string ToEmail, string Subject, string BodyHtml);

public class InMemoryEmailSender : IEmailSender
{
    private readonly List<SentEmail> _emails = new();

    public IReadOnlyList<SentEmail> Emails => _emails;

    public Task SendAsync(string toEmail, string subject, string bodyHtml)
    {
        _emails.Add(new SentEmail(toEmail, subject, bodyHtml));
        return Task.CompletedTask;
    }
}