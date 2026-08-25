using System.Net.Sockets;
using System.Text;

namespace ProCreateApi.Services.Lis;

/// <summary>
/// Sends a single HL7 v2 message over MLLP (Minimal Lower Layer Protocol) to the LIS
/// and reads the ACK response.
/// MLLP frame: 0x0B + message bytes + 0x1C + 0x0D
/// </summary>
public class MllpClient
{
    private const byte StartBlock = 0x0B;
    private const byte EndBlock = 0x1C;
    private const byte CarriageReturn = 0x0D;

    private readonly LisSettings _settings;

    public MllpClient(LisSettings settings) => _settings = settings;

    public async Task<string> SendAsync(string hl7Message, CancellationToken ct = default)
    {
        using var tcp = new TcpClient();
        tcp.SendTimeout = _settings.TimeoutSeconds * 1000;
        tcp.ReceiveTimeout = _settings.TimeoutSeconds * 1000;

        await tcp.ConnectAsync(_settings.Host, _settings.Port, ct);
        var stream = tcp.GetStream();

        // Encode and frame the outbound message
        var messageBytes = Encoding.ASCII.GetBytes(hl7Message);
        var frame = new byte[messageBytes.Length + 3];
        frame[0] = StartBlock;
        messageBytes.CopyTo(frame, 1);
        frame[^2] = EndBlock;
        frame[^1] = CarriageReturn;

        await stream.WriteAsync(frame, ct);
        await stream.FlushAsync(ct);

        // Read ACK — first byte must be StartBlock
        return await ReadMllpMessageAsync(stream, ct);
    }

    internal static async Task<string> ReadMllpMessageAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new List<byte>(1024);
        var tmp = new byte[1];

        // Wait for StartBlock
        while (true)
        {
            int read = await stream.ReadAsync(tmp, ct);
            if (read == 0) throw new EndOfStreamException("LIS closed connection before sending MLLP start block.");
            if (tmp[0] == StartBlock) break;
        }

        // Read until EndBlock + CR
        while (true)
        {
            int read = await stream.ReadAsync(tmp, ct);
            if (read == 0) throw new EndOfStreamException("LIS closed connection mid-message.");
            if (tmp[0] == EndBlock)
            {
                // consume the trailing CR; single-byte read is intentional here
#pragma warning disable CA2022
                await stream.ReadAsync(tmp, ct);
#pragma warning restore CA2022
                break;
            }
            buffer.Add(tmp[0]);
        }

        return Encoding.ASCII.GetString(buffer.ToArray());
    }
}
