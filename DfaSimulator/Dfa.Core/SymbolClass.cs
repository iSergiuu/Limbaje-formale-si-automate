namespace Dfa.Core;

public class SymbolClass
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Characters { get; set; } = string.Empty;

    public bool Matches(char c) => Characters.Contains(c);
}