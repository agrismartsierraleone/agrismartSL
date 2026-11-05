using AgrismartAPI.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Tests.Users;

public class SendGridEmailSenderTests
{
    private static SendGridEmailSender SenderThatThrows(Exception exception) => new(
        new HttpClient(new ThrowingHandler(exception)),
        Options.Create(new SendGridSettings { ApiKey = "test-key", FromEmail = "from@test.local" }),
        NullLogger<SendGridEmailSender>.Instance);

    [Fact]
    public async Task An_unreachable_server_does_not_fail_the_caller()
    {
        var sender = SenderThatThrows(new HttpRequestException("connection refused"));

        await sender.SendAsync("to@test.local", "To", "Subject", "<p>Body</p>");
    }

    [Fact]
    public async Task A_timeout_does_not_fail_the_caller()
    {
        var sender = SenderThatThrows(new TaskCanceledException("timed out"));

        await sender.SendAsync("to@test.local", "To", "Subject", "<p>Body</p>");
    }

    private class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }
}
