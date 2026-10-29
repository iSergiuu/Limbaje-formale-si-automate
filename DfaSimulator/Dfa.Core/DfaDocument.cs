using System.Text.Json;

namespace Dfa.Core;

public record StateVisualInfo(double X, double Y, string Description = "");
public record TestWordCase(string Word, bool ExpectedAccepted);

public class DfaDocument
{
    public string Title { get; set; } = "Automat nou";
    public string Description { get; set; } = string.Empty;
    public DfaModel Dfa { get; set; } = new();
    public Dictionary<string, StateVisualInfo> StateLayout { get; set; } = new();
    public List<TestWordCase> TestSuite { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static DfaDocument FromJson(string json) =>
        JsonSerializer.Deserialize<DfaDocument>(json, JsonOptions)
        ?? throw new InvalidDataException("Fișierul JSON este gol sau invalid.");
}