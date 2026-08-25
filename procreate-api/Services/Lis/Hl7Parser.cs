namespace ProCreateApi.Services.Lis;

/// <summary>
/// Lightweight HL7 v2 parser. Handles the segments we need for LIS integration.
/// </summary>
public class Hl7Parser
{
    private readonly Dictionary<string, List<string[]>> _segments = new();
    private string _messageType = string.Empty;
    private string _messageId = string.Empty;

    public string MessageType => _messageType;
    public string MessageId => _messageId;

    public static Hl7Parser Parse(string rawMessage)
    {
        var parser = new Hl7Parser();
        // Segments are separated by CR; trim trailing whitespace
        var lines = rawMessage.Split('\r', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Length < 3) continue;
            var segName = line[..3];
            // Fields are pipe-separated; for MSH field 1 IS the separator itself
            var fields = segName == "MSH"
                ? new[] { "MSH" }.Concat(line[3..].Split('|')).ToArray()
                : line.Split('|');

            if (!parser._segments.ContainsKey(segName))
                parser._segments[segName] = new List<string[]>();
            parser._segments[segName].Add(fields);
        }

        // MSH-9 = message type (e.g. "ORU^R01")
        parser._messageType = parser.GetField("MSH", 0, 9);
        // MSH-10 = message control ID
        parser._messageId = parser.GetField("MSH", 0, 10);
        return parser;
    }

    /// <summary>Returns a field value. segIndex = which occurrence of the segment (0-based).</summary>
    public string GetField(string segment, int segIndex, int fieldIndex)
    {
        if (!_segments.TryGetValue(segment, out var list)) return string.Empty;
        if (segIndex >= list.Count) return string.Empty;
        var fields = list[segIndex];
        return fieldIndex < fields.Length ? fields[fieldIndex] : string.Empty;
    }

    /// <summary>Returns a specific component within a field (1-based component index, 0-based field).</summary>
    public string GetComponent(string segment, int segIndex, int fieldIndex, int componentIndex)
    {
        var field = GetField(segment, segIndex, fieldIndex);
        var parts = field.Split('^');
        return componentIndex < parts.Length ? parts[componentIndex] : string.Empty;
    }

    public int SegmentCount(string segment)
        => _segments.TryGetValue(segment, out var list) ? list.Count : 0;

    /// <summary>
    /// Extracts all OBX observations from an ORU^R01. Returns a list of parsed observations.
    /// OBX fields: [3]=observation id, [5]=value, [6]=units, [7]=ref range, [8]=abnormal flag
    /// </summary>
    public List<ObxObservation> GetObservations()
    {
        var result = new List<ObxObservation>();
        int count = SegmentCount("OBX");
        for (int i = 0; i < count; i++)
        {
            // OBX-3: observation identifier — format "code^name^coding_system"
            var code = GetComponent("OBX", i, 3, 0);
            var name = GetComponent("OBX", i, 3, 1);
            var value = GetField("OBX", i, 5);
            var units = GetField("OBX", i, 6);
            var refRange = GetField("OBX", i, 7);
            var flag = GetField("OBX", i, 8); // N, H, L, A...
            result.Add(new ObxObservation(code, name, value, units, refRange, flag));
        }
        return result;
    }

    /// <summary>
    /// Returns the placer order number from the first OBR segment (OBR-2),
    /// which should match our SpecimenBarcode or order code.
    /// </summary>
    public string GetPlacerOrderId()
        => GetField("OBR", 0, 2);

    public string GetPatientId()
        => GetComponent("PID", 0, 3, 0);
}

public record ObxObservation(string Code, string Name, string Value, string Units, string RefRange, string AbnormalFlag);
