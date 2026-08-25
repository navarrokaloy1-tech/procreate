using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ProCreateApi.Services.Lis;

/// <summary>
/// Background TCP listener that accepts HL7 ORU^R01 messages from the LIS over MLLP,
/// delegates processing to ILisService, and sends an ACK back.
/// </summary>
public class MllpServer : BackgroundService
{
    private readonly LisSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MllpServer> _logger;

    public MllpServer(LisSettings settings, IServiceScopeFactory scopeFactory, ILogger<MllpServer> logger)
    {
        _settings = settings;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("LIS integration is disabled — MLLP server not started.");
            return;
        }

        var listener = new TcpListener(IPAddress.Any, _settings.MllpListenPort);
        listener.Start();
        _logger.LogInformation("MLLP server listening on port {Port}", _settings.MllpListenPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);
                // Fire-and-forget each connection so the listener stays available
                _ = HandleClientAsync(client, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MLLP listener error");
            }
        }

        listener.Stop();
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var remote = client.Client.RemoteEndPoint;
            _logger.LogDebug("MLLP connection from {Remote}", remote);
            try
            {
                var stream = client.GetStream();
                var rawMessage = await MllpClient.ReadMllpMessageAsync(stream, ct);

                var parser = Hl7Parser.Parse(rawMessage);
                var msgType = parser.MessageType;
                _logger.LogInformation("Received HL7 {Type} msgId={Id} from {Remote}", msgType, parser.MessageId, remote);

                using var scope = _scopeFactory.CreateScope();
                var lis = scope.ServiceProvider.GetRequiredService<ILisService>();
                var builder = scope.ServiceProvider.GetRequiredService<Hl7Builder>();

                string ackCode = "AA";
                try
                {
                    if (msgType.StartsWith("ORU^R01", StringComparison.OrdinalIgnoreCase))
                        await lis.ProcessInboundResultAsync(parser, ct);
                    else
                        _logger.LogWarning("Unhandled HL7 message type: {Type}", msgType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing inbound HL7 message");
                    ackCode = "AE"; // Application Error
                }

                var ack = builder.BuildAck(parser.MessageId, ackCode);
                await SendMllpResponseAsync(stream, ack, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MLLP client handler failed for {Remote}", remote);
            }
        }
    }

    private static async Task SendMllpResponseAsync(NetworkStream stream, string message, CancellationToken ct)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(message);
        var frame = new byte[bytes.Length + 3];
        frame[0] = 0x0B;
        bytes.CopyTo(frame, 1);
        frame[^2] = 0x1C;
        frame[^1] = 0x0D;
        await stream.WriteAsync(frame, ct);
        await stream.FlushAsync(ct);
    }
}
