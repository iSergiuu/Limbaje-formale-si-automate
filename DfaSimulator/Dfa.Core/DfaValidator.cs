namespace Dfa.Core;

public enum ValidationSeverity { Info, Warning, Error }

public record ValidationIssue(ValidationSeverity Severity, string Message);

public static class DfaValidator
{
    public static List<ValidationIssue> Validate(DfaModel dfa)
    {
        var issues = new List<ValidationIssue>();

        if (dfa.States.Count == 0)
            issues.Add(new(ValidationSeverity.Error, "Automatul nu conține nicio stare."));

        if (string.IsNullOrWhiteSpace(dfa.InitialState))
            issues.Add(new(ValidationSeverity.Error, "Nu a fost setată starea inițială (q₀)."));
        else if (!dfa.States.Contains(dfa.InitialState))
            issues.Add(new(ValidationSeverity.Error, $"Starea inițială «{dfa.InitialState}» nu se află în lista de stări."));

        if (dfa.FinalStates.Count == 0)
            issues.Add(new(ValidationSeverity.Warning, "Automatul nu are nicio stare finală (nu va accepta niciun cuvânt)."));

        var seen = new HashSet<(string, string)>();
        foreach (var t in dfa.Transitions)
        {
            if (!dfa.Alphabet.Contains(t.Symbol))
                issues.Add(new(ValidationSeverity.Error, $"Simbolul «{t.Symbol}» din tranziția {t.FromState}→{t.ToState} nu este în alfabet."));

            if (!seen.Add((t.FromState, t.Symbol)))
                issues.Add(new(ValidationSeverity.Error, $"Nedeterminism: din {t.FromState} pleacă mai multe tranziții pe «{t.Symbol}»."));
        }

        if (!string.IsNullOrWhiteSpace(dfa.InitialState) && dfa.States.Contains(dfa.InitialState))
        {
            var visited = new HashSet<string> { dfa.InitialState };
            var queue = new Queue<string>();
            queue.Enqueue(dfa.InitialState);

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                foreach (var t in dfa.Transitions.Where(x => x.FromState == curr))
                {
                    if (visited.Add(t.ToState))
                        queue.Enqueue(t.ToState);
                }
            }

            foreach (var s in dfa.States.Except(visited))
                issues.Add(new(ValidationSeverity.Warning, $"Starea «{s}» este inaccesibilă din starea inițială."));
        }

        return issues;
    }
}