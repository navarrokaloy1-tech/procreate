using ProCreateApi.Models;

namespace ProCreateApi.Services.Lis;

public interface ILisService
{
    Task SendOrderAsync(LabOrder order, CancellationToken ct = default);
    Task RetryOrderAsync(int labOrderId, CancellationToken ct = default);
    Task SendPatientAsync(Patient patient, bool isUpdate = false, CancellationToken ct = default);
    Task ProcessInboundResultAsync(Hl7Parser message, CancellationToken ct = default);
    LisConnectionInfo GetConnectionInfo();
}

public record LisConnectionInfo(bool Enabled, string Host, int Port, int ListenPort);
