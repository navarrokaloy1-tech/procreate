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
    /// Decodes HL7 escape sequences back into the characters they stand for,
    /// undoing what Hl7Builder.EscapeField does on the way out.
    ///
    /// Only ever applied to a value, never to a field before it is split into
    /// components: a decoded "\S\" is an ordinary caret in the text, and
    /// splitting on it afterwards would tear the value apart at a separator
    /// that was escaped precisely because it is not one.
    /// </summary>
    public static string Unescape(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains('\\')) return value;

        return value
            .Replace("\\F\\", "|")
            .Replace("\\S\\", "^")
            .Replace("\\R\\", "~")
            .Replace("\\T\\", "&")
            // Line break within a segment, which report bodies rely on.
            .Replace("\\.br\\", "\n")
            // Last: it yields the escape character itself, so running it
            // earlier would let its output be re-read as a new sequence.
            .Replace("\\E\\", "\\");
    }

    /// <summary>
    /// Extracts all OBX observations from an ORU^R01. Returns a list of parsed observations.
    /// OBX fields: [2]=value type, [3]=observation id, [5]=value, [6]=units,
    /// [7]=ref range, [8]=abnormal flag
    /// </summary>
    public List<ObxObservation> GetObservations()
    {
        var result = new List<ObxObservation>();
        int count = SegmentCount("OBX");
        for (int i = 0; i < count; i++)
        {
            // OBX-2 says whether this is a measurement or report prose, which
            // decides where the value belongs on our side.
            var valueType = GetField("OBX", i, 2);
            // OBX-3: observation identifier — format "code^name^coding_system"
            var code = GetComponent("OBX", i, 3, 0);
            var name = Unescape(GetComponent("OBX", i, 3, 1));
            var value = Unescape(GetField("OBX", i, 5));
            var units = Unescape(GetField("OBX", i, 6));
            var refRange = Unescape(GetField("OBX", i, 7));
            var flag = GetField("OBX", i, 8); // N, H, L, A...
            result.Add(new ObxObservation(valueType, code, name, value, units, refRange, flag));
        }
        return result;
    }

    /// <summary>
    /// Who read the study, from OBR-32 (principal result interpreter). The
    /// field is an XCN — id^family^given — so the name is rebuilt as it would
    /// be signed. Falls back to the raw id when no name was sent.
    /// </summary>
    public string GetResultInterpreter()
    {
        var family = Unescape(GetComponent("OBR", 0, 32, 1));
        var given = Unescape(GetComponent("OBR", 0, 32, 2));
        var name = $"{given} {family}".Trim();
        return name.Length > 0 ? name : Unescape(GetComponent("OBR", 0, 32, 0));
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

public record ObxObservation(
    string ValueType,
    string Code,
    string Name,
    string Value,
    string Units,
    string RefRange,
    string AbnormalFlag)
{
    /// <summary>
    /// True when the value is report prose rather than a measurement. TX is
    /// free text and FT is formatted text; both are how a radiologist or
    /// cardiologist write-up arrives, usually split over several OBX segments.
    /// </summary>
    public bool IsNarrative =>
        ValueType.Equals("TX", StringComparison.OrdinalIgnoreCase) ||
        ValueType.Equals("FT", StringComparison.OrdinalIgnoreCase);
}
