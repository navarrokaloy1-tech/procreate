namespace ProCreateApi.Services.Lis;

public class LisSettings
{
    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 2575;
    public string SendingFacility { get; set; } = "PROCREATE";
    public string ReceivingFacility { get; set; } = "LIS";
    public int MllpListenPort { get; set; } = 2576;
    public int TimeoutSeconds { get; set; } = 30;
}
