using System.Net;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Users;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Application.Users;

/// <summary>Sends account lifecycle emails (new account credentials, credential/role changes) via <see cref="IEmailSender"/>.</summary>
public class UserNotificationService(IEmailSender emailSender, IOptions<AppLinksSettings> appLinks)
{
    public Task SendAccountCreatedEmailAsync(User user, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        var subject = "Your AgrismartAPI account has been created";
        var html = $"""
            <p>Hello {Encode(user.Name)},</p>
            <p>An account has been created for you on AgrismartAPI as a <strong>{Encode(user.Role.ToString())}</strong>.</p>
            <p>Use the credentials below to log in:</p>
            <ul>
                <li><strong>Email:</strong> {Encode(user.Email)}</li>
                <li><strong>Temporary password:</strong> {Encode(temporaryPassword)}</li>
            </ul>
            {LoginLinkHtml()}
            <p>For your security, please change your password after logging in for the first time.</p>
            """;

        return emailSender.SendAsync(user.Email, user.Name, subject, html, cancellationToken);
    }

    public Task SendCredentialsUpdatedEmailAsync(User user, IReadOnlyList<string> changes, CancellationToken cancellationToken = default)
    {
        var subject = "Your AgrismartAPI account credentials were updated";
        var changeList = string.Concat(changes.Select(change => $"<li>{Encode(change)}</li>"));
        var html = $"""
            <p>Hello {Encode(user.Name)},</p>
            <p>The following changes were made to your AgrismartAPI account:</p>
            <ul>{changeList}</ul>
            <p>If you did not request this change, please contact an administrator immediately.</p>
            {LoginLinkHtml()}
            """;

        return emailSender.SendAsync(user.Email, user.Name, subject, html, cancellationToken);
    }

    public Task SendAccountDeletedEmailAsync(User user, CancellationToken cancellationToken = default)
    {
        var subject = "Your AgrismartAPI account has been deleted";
        var html = $"""
            <p>Hello {Encode(user.Name)},</p>
            <p>Your AgrismartAPI account ({Encode(user.Email)}) has been deleted by an administrator. You will no longer be able to log in with these credentials.</p>
            <p>If you believe this was done in error, please contact an administrator.</p>
            """;

        return emailSender.SendAsync(user.Email, user.Name, subject, html, cancellationToken);
    }

    private string LoginLinkHtml()
    {
        var loginUrl = appLinks.Value.LoginUrl;
        return string.IsNullOrWhiteSpace(loginUrl)
            ? string.Empty
            : $"""<p><a href="{Encode(loginUrl)}">Log in to AgrismartAPI</a></p>""";
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
