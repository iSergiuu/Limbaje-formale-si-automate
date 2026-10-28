namespace Dfa.Core;

public record Transition(string FromState, string Symbol, string ToState);

public class DfaModel
{
    public HashSet<string> States { get; set; } = new();
    public HashSet<string> Alphabet { get; set; } = new();
    public string? InitialState { get; set; }
    public HashSet<string> FinalStates { get; set; } = new();
    public List<Transition> Transitions { get; set; } = new();
    public List<SymbolClass> SymbolClasses { get; set; } = new();

    public (bool Success, string ErrorMessage) AddTransition(string fromState, string symbol, string toState)
    {
        if (!States.Contains(fromState))
            return (false, $"Starea sursă «{fromState}» nu există.");
        if (!States.Contains(toState))
            return (false, $"Starea destinație «{toState}» nu există.");
        if (!Alphabet.Contains(symbol))
            return (false, $"Simbolul «{symbol}» nu aparține alfabetului Σ.");

        var existing = Transitions.FirstOrDefault(t => t.FromState == fromState && t.Symbol == symbol);
        if (existing != null)
        {
            return (false, $"Din {fromState} există deja o tranziție pe «{symbol}» către {existing.ToState}.");
        }

        Transitions.Add(new Transition(fromState, symbol, toState));
        return (true, string.Empty);
    }

    public string? GetNextState(string state, string symbol)
    {
        return Transitions.FirstOrDefault(t => t.FromState == state && t.Symbol == symbol)?.ToState;
    }

    public string? ResolveSymbol(char inputChar)
    {
        string s = inputChar.ToString();
        if (Alphabet.Contains(s))
            return s;

        var matchedClass = SymbolClasses.FirstOrDefault(sc => sc.Matches(inputChar));
        if (matchedClass != null && Alphabet.Contains(matchedClass.Name))
            return matchedClass.Name;

        return null;
    }

    public void CompleteWithTrapState(string trapStateName = "q_capcana")
    {
        bool needed = false;
        foreach (var state in States.ToList())
        {
            foreach (var symbol in Alphabet)
            {
                if (GetNextState(state, symbol) == null)
                {
                    needed = true;
                    break;
                }
            }
        }

        if (!needed) return;

        States.Add(trapStateName);
        foreach (var state in States)
        {
            foreach (var symbol in Alphabet)
            {
                if (GetNextState(state, symbol) == null)
                {
                    Transitions.Add(new Transition(state, symbol, trapStateName));
                }
            }
        }
    }
}