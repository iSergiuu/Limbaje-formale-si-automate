namespace Dfa.Core;

public record ExecutionStep(
    int StepIndex,
    string ReadSymbol,     
    string RemainingInput,  
    string? StateBefore,
    string? CurrentState,   
    string? MatchedSymbol   
);

public class DfaRun
{
    public DfaModel Dfa { get; }
    public string InputWord { get; }
    public List<ExecutionStep> Steps { get; } = new();
    public int CurrentStepIndex { get; private set; } = 0;

    public bool IsAccepted { get; private set; }
    public string VerdictMessage { get; private set; } = string.Empty;

    public ExecutionStep CurrentStep => Steps[CurrentStepIndex];
    public bool IsAtStart => CurrentStepIndex == 0;
    public bool IsAtEnd => CurrentStepIndex == Steps.Count - 1;

    public DfaRun(DfaModel dfa, string inputWord)
    {
        Dfa = dfa;
        InputWord = inputWord ?? string.Empty;
        PrecomputeSteps();
    }

    private void PrecomputeSteps()
    {
        Steps.Clear();

        if (string.IsNullOrWhiteSpace(Dfa.InitialState) || !Dfa.States.Contains(Dfa.InitialState))
        {
            IsAccepted = false;
            VerdictMessage = "EROARE: Automatul nu are o stare inițială validă.";
            return;
        }

        string currentState = Dfa.InitialState;
        string remainingAtStart = InputWord.Length == 0 ? "ε" : InputWord;

        Steps.Add(new ExecutionStep(0, "—", remainingAtStart, null, currentState, null));

        for (int i = 0; i < InputWord.Length; i++)
        {
            char c = InputWord[i];
            string rem = (i + 1 < InputWord.Length) ? InputWord[(i + 1)..] : "ε";
            string? resolvedSymbol = Dfa.ResolveSymbol(c);

            if (resolvedSymbol == null)
            {
                Steps.Add(new ExecutionStep(i + 1, c.ToString(), rem, currentState, null, null));
                IsAccepted = false;
                VerdictMessage = $"RESPINS: simbolul «{c}» nu aparține alfabetului (poziția {i + 1}).";
                return;
            }

            string? nextState = Dfa.GetNextState(currentState, resolvedSymbol);

            if (nextState == null)
            {
                Steps.Add(new ExecutionStep(i + 1, c.ToString(), rem, currentState, null, resolvedSymbol));
                IsAccepted = false;
                VerdictMessage = $"RESPINS: din {currentState} nu există tranziție pe «{c}» (poziția {i + 1}).";
                return;
            }

            Steps.Add(new ExecutionStep(i + 1, c.ToString(), rem, currentState, nextState, resolvedSymbol));
            currentState = nextState;
        }

        if (Dfa.FinalStates.Contains(currentState))
        {
            IsAccepted = true;
            VerdictMessage = $"ACCEPTAT: cuvântul s-a terminat în starea finală {currentState}.";
        }
        else
        {
            IsAccepted = false;
            VerdictMessage = $"RESPINS: cuvântul s-a terminat în {currentState}, care nu este finală.";
        }
    }

    public bool StepForward()
    {
        if (CurrentStepIndex < Steps.Count - 1)
        {
            CurrentStepIndex++;
            return true;
        }
        return false;
    }

    public bool StepBack()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
            return true;
        }
        return false;
    }

    public void Reset() => CurrentStepIndex = 0;
    public void RunToEnd() => CurrentStepIndex = Steps.Count - 1;
}