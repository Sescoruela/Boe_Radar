using System.Net;
using System.Net.Mail;
using BoeRadar.Application;
using Microsoft.Extensions.Configuration;

namespace BoeRadar.Infrastructure.Messaging;

internal sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body,
        CancellationToken cancellationToken)
    {
        var host = configuration["Email:Host"];
        var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Configura Email:Host y Email:From antes de enviar alertas.");

        using var message = new MailMessage(from, recipient, subject, body);
        using var client = new SmtpClient(host, configuration.GetValue("Email:Port", 1025))
        {
            EnableSsl = configuration.GetValue("Email:EnableSsl", false)
        };
        var user = configuration["Email:Username"];
        if (!string.IsNullOrWhiteSpace(user))
            client.Credentials = new NetworkCredential(user, configuration["Email:Password"]);
        await client.SendMailAsync(message, cancellationToken);
    }
}
