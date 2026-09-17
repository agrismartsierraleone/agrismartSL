using System.Net;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Users;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Application.Users;

/// <summary>Sends account lifecycle emails (new account credentials, credential/role changes) via <see cref="IEmailSender"/>.</summary>
public class UserNotificationService(IEmailSender emailSender, IOptions<AppLinksSettings> appLinks)
{
    /// <param name="isTemporaryPassword">
    /// True for an admin-issued password (via <c>POST /users</c> or a credentials reset) — the email
    /// says it's temporary and expires in <see cref="User.TemporaryPasswordLifetime"/>. False for a
    /// buyer's own self-chosen password from <c>POST /auth/register</c> — it's just a welcome/confirmation.
    /// </param>
    public Task SendAccountCreatedEmailAsync(User user, string password, bool isTemporaryPassword, CancellationToken cancellationToken = default)
    {
        var subject = "Your AgrismartAPI account has been created";
        var intro = isTemporaryPassword
            ? $"An account has been created for you on AgrismartAPI as a <strong>{Encode(user.Role.ToString())}</strong>."
            : "Welcome to AgrismartAPI! Your account has been created.";
        var expiryNote = isTemporaryPassword
            ? $"<p>This is a <strong>temporary password</strong> and expires {Hours(User.TemporaryPasswordLifetime)} hours after this email is sent. You'll be required to set your own password the first time you log in — if it expires first, ask an administrator to reset it.</p>"
            : "<p>Keep this password safe. You can change it any time from within the app.</p>";

        var html = $"""
            <p>Hello {Encode(user.Name)},</p>
            <p>{intro}</p>
            <p>Use the credentials below to log in:</p>
            <ul>
                <li><strong>Email:</strong> {Encode(user.Email)}</li>
                <li><strong>Password:</strong> {Encode(password)}</li>
            </ul>
            {AccessLinksHtml(user.Role)}
            {expiryNote}
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
            {AccessLinksHtml(user.Role)}
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

    /// <summary>Sellers/Admins use the web admin app (login link); Buyers use the mobile app (store links).</summary>
    private string AccessLinksHtml(UserRole role)
    {
        if (role != UserRole.Buyer)
        {
            var loginUrl = appLinks.Value.LoginUrl;
            return string.IsNullOrWhiteSpace(loginUrl)
                ? string.Empty
                : $"""<p><a href="{Encode(loginUrl)}">Log in to AgrismartAPI</a></p>""";
        }

        var links = new List<string>();
        if (!string.IsNullOrWhiteSpace(appLinks.Value.PlayStoreUrl))
        {
            links.Add($"""<a href="{Encode(appLinks.Value.PlayStoreUrl)}">Get it on Google Play (Android)</a>""");
        }

        if (!string.IsNullOrWhiteSpace(appLinks.Value.AppStoreUrl))
        {
            links.Add($"""<a href="{Encode(appLinks.Value.AppStoreUrl)}">Download on the App Store (iOS)</a>""");
        }

        return links.Count == 0
            ? string.Empty
            : $"<p>Download the AgrismartAPI app to log in: {string.Join(" &nbsp;|&nbsp; ", links)}</p>";
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string Hours(TimeSpan value) => value.TotalHours.ToString("0.#");
}
