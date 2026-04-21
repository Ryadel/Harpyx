using System.Net;
using System.Net.Mail;
using Harpyx.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Harpyx.Infrastructure.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(EmailOptions options, ILogger<SmtpEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.From))
        {
            _logger.LogWarning("SMTP is not configured. Invitation email to {Email} was not sent.", to);
            return;
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        using var message = new MailMessage(_options.From, to, subject, body);
        await client.SendMailAsync(message, cancellationToken);
    }
}
