using AgrismartAPI.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AgrismartAPI.Infrastructure.Email;

public class SendGridEmailSender(IOptions<SendGridSettings> options, ILogger<SendGridEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlContent, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogWarning("SendGrid API key is not configured; skipping email to {Email} with subject '{Subject}'.", toEmail, subject);
            return;
        }

        var client = new SendGridClient(settings.ApiKey);
        var from = new EmailAddress(settings.FromEmail, settings.FromName);
        var to = new EmailAddress(toEmail, toName);
        var message = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: string.Empty, htmlContent);

        var response = await client.SendEmailAsync(message, cancellationToken);
        if ((int)response.StatusCode >= 400)
        {
            var body = await response.Body.ReadAsStringAsync(cancellationToken);
            logger.LogError("SendGrid failed to send email to {Email}: {StatusCode} {Body}", toEmail, response.StatusCode, body);
        }
    }
}
