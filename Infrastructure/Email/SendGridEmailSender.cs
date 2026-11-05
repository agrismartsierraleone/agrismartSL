using System.Net.Http.Headers;
using System.Text;
using AgrismartAPI.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid.Helpers.Mail;

namespace AgrismartAPI.Infrastructure.Email;

/// <summary>Posts directly to SendGrid's v3 Mail Send API (<see cref="SendGridSettings.BaseApiUrl"/>) rather than routing through the SendGrid SDK's own HTTP client.</summary>
public class SendGridEmailSender(HttpClient httpClient, IOptions<SendGridSettings> options, ILogger<SendGridEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlContent, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogWarning("SendGrid API key is not configured; skipping email to {Email} with subject '{Subject}'.", toEmail, subject);
            return;
        }

        var from = new EmailAddress(settings.FromEmail, settings.FromName);
        var to = new EmailAddress(toEmail, toName);
        var message = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: string.Empty, htmlContent);

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.BaseApiUrl)
        {
            Content = new StringContent(message.Serialize(), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        // Emails go out after the change is saved, so a failure here must not turn a saved change into an API error.
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("SendGrid failed to send email to {Email}: {StatusCode} {Body}", toEmail, response.StatusCode, body);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(exception, "Could not reach SendGrid to send email to {Email} with subject '{Subject}'.", toEmail, subject);
        }
    }
}
