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
        var ssl = configuration.GetValue("Email:EnableSsl", false);
        var isLocal = host is "localhost" or "127.0.0.1" or "::1" or "mailpit";
        if (!isLocal && !ssl)
            throw new InvalidOperationException("El correo fuera del entorno local requiere TLS.");
        var port = configuration.GetValue("Email:Port", isLocal ? 1025 : 587);
        var timeoutSeconds = configuration.GetValue("Email:TimeoutSeconds", 30);
        if (port is < 1 or > 65535 || timeoutSeconds is < 1 or > 120)
            throw new InvalidOperationException("Puerto o tiempo de espera de correo no válido.");

        using var message = new MailMessage(from, recipient, subject, body);
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = ssl,
            UseDefaultCredentials = false
        };
        var user = configuration["Email:Username"];
        if (!string.IsNullOrWhiteSpace(user))
            client.Credentials = new NetworkCredential(user, configuration["Email:Password"]);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try { await client.SendMailAsync(message, deadline.Token); }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("El proveedor de correo no respondió dentro del tiempo configurado.", exception);
        }
    }
}
