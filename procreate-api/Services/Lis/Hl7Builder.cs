using ProCreateApi.Models;
using System.Text;

namespace ProCreateApi.Services.Lis;

public class Hl7Builder
{
    private readonly LisSettings _settings;

    public Hl7Builder(LisSettings settings) => _settings = settings;

    // Segment terminator in HL7 v2 is CR (\r)
    private const char CR = '\r';

    public string BuildOrmO01(LabOrder order)
    {
        var patient = order.Visit.Patient;
        var test = order.LabTest;
        var now = DateTime.Now.ToString("yyyyMMddHHmmss");
        var msgId = $"ORM{order.Id:D8}";
        var dob = patient.DateOfBirth.ToString("yyyyMMdd");
        var gender = MapGender(patient.Gender);

        var sb = new StringBuilder();
        sb.Append($"MSH|^~\\&|PROCREATE|{_settings.SendingFacility}|LIS|{_settings.ReceivingFacility}|{now}||ORM^O01|{msgId}|P|2.5{CR}");
        sb.Append($"PID|1||{patient.PatientCode}^^^{_settings.SendingFacility}||{patient.LastName}^{patient.FirstName}^{patient.MiddleName}||{dob}|{gender}|||{EscapeField(patient.Address)}||{patient.ContactNumber}{CR}");
        sb.Append($"ORC|NW|{order.SpecimenBarcode}||||||||||{CR}");
        sb.Append($"OBR|1|{order.SpecimenBarcode}||{test.Code}^{EscapeField(test.Name)}|||{now}||||||||||{EscapeField(order.Visit.ReferringPhysician)}{CR}");
        return sb.ToString();
    }

    public string BuildAdtA01(Patient patient)
        => BuildAdt(patient, "A01");

    public string BuildAdtA08(Patient patient)
        => BuildAdt(patient, "A08");

    private string BuildAdt(Patient patient, string eventCode)
    {
        var now = DateTime.Now.ToString("yyyyMMddHHmmss");
        var msgId = $"ADT{patient.Id:D8}";
        var dob = patient.DateOfBirth.ToString("yyyyMMdd");
        var gender = MapGender(patient.Gender);

        var sb = new StringBuilder();
        sb.Append($"MSH|^~\\&|PROCREATE|{_settings.SendingFacility}|LIS|{_settings.ReceivingFacility}|{now}||ADT^{eventCode}|{msgId}|P|2.5{CR}");
        sb.Append($"EVN|{eventCode}|{now}{CR}");
        sb.Append($"PID|1||{patient.PatientCode}^^^{_settings.SendingFacility}||{patient.LastName}^{patient.FirstName}^{patient.MiddleName}||{dob}|{gender}|||{EscapeField(patient.Address)}||{patient.ContactNumber}||||||{CR}");
        return sb.ToString();
    }

    public string BuildAck(string originalMsgId, string ackCode = "AA")
    {
        var now = DateTime.Now.ToString("yyyyMMddHHmmss");
        var msgId = $"ACK{now}";

        var sb = new StringBuilder();
        sb.Append($"MSH|^~\\&|PROCREATE|{_settings.SendingFacility}|LIS|{_settings.ReceivingFacility}|{now}||ACK|{msgId}|P|2.5{CR}");
        sb.Append($"MSA|{ackCode}|{originalMsgId}{CR}");
        return sb.ToString();
    }

    private static string MapGender(string gender) => gender.ToLower() switch
    {
        "male" or "m" => "M",
        "female" or "f" => "F",
        _ => "U"
    };

    // Escape pipe characters in field values so they don't break HL7 parsing
    private static string EscapeField(string value)
        => value.Replace("|", "\\F\\").Replace("^", "\\S\\").Replace("~", "\\R\\");
}
