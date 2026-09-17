namespace AgrismartAPI.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlContent, CancellationToken cancellationToken = default);
}
