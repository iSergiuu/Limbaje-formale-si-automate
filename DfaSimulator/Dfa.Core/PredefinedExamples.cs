namespace Dfa.Core;

public static class PredefinedExamples
{
    public static DfaDocument CreateEvenA()
    {
        var doc = new DfaDocument
        {
            Title = "1. Număr par de 'a'",
            Description = "Acceptă cuvintele peste {a, b} care conțin un număr par de simboluri 'a'."
        };

        doc.Dfa.States.UnionWith(["q0", "q1"]);
        doc.Dfa.Alphabet.UnionWith(["a", "b"]);
        doc.Dfa.InitialState = "q0";
        doc.Dfa.FinalStates.Add("q0");

        doc.Dfa.AddTransition("q0", "a", "q1");
        doc.Dfa.AddTransition("q0", "b", "q0");
        doc.Dfa.AddTransition("q1", "a", "q0");
        doc.Dfa.AddTransition("q1", "b", "q1");

        doc.StateLayout["q0"] = new(200, 150, "Număr PAR de 'a' citite până acum");
        doc.StateLayout["q1"] = new(450, 150, "Număr IMPAR de 'a' citite până acum");

        doc.TestSuite.AddRange([
            new("", true), new("bb", true), new("abba", true),
            new("a", false), new("bab", false)
        ]);

        return doc;
    }

    public static DfaDocument CreateBinaryDivisibleBy3()
    {
        var doc = new DfaDocument
        {
            Title = "5. Numere binare divizibile cu 3",
            Description = "Fiecare stare reține restul împărțirii la 3 a numărului binar citit până acum."
        };

        doc.Dfa.States.UnionWith(["q0", "q1", "q2"]);
        doc.Dfa.Alphabet.UnionWith(["0", "1"]);
        doc.Dfa.InitialState = "q0";
        doc.Dfa.FinalStates.Add("q0");

        doc.Dfa.AddTransition("q0", "0", "q0");
        doc.Dfa.AddTransition("q0", "1", "q1");
        doc.Dfa.AddTransition("q1", "0", "q2");
        doc.Dfa.AddTransition("q1", "1", "q0");
        doc.Dfa.AddTransition("q2", "0", "q1");
        doc.Dfa.AddTransition("q2", "1", "q2");

        doc.StateLayout["q0"] = new(180, 150, "Restul împărțirii la 3 este 0 (divizibil)");
        doc.StateLayout["q1"] = new(380, 150, "Restul împărțirii la 3 este 1");
        doc.StateLayout["q2"] = new(580, 150, "Restul împărțirii la 3 este 2");

        doc.TestSuite.AddRange([
            new("0", true), new("11", true), new("110", true), new("1001", true),
            new("1", false), new("10", false), new("100", false)
        ]);

        return doc;
    }

    public static DfaDocument CreateRomanianLicensePlate()
    {
        var doc = new DfaDocument
        {
            Title = "A1. Validator Număr de Înmatriculare",
            Description = "Validează numerele auto din România (ex: B 123 ABC, AG 12 XYZ)."
        };

        doc.Dfa.SymbolClasses.AddRange([
            new SymbolClass { Name = "B", Description = "Litera B (București)", Characters = "B" },
            new SymbolClass { Name = "L", Description = "Literă A-Z (fără B)", Characters = "ACDEFGHIJKLMNOPQRSTUVWXYZ" },
            new SymbolClass { Name = "C", Description = "Cifră 0-9", Characters = "0123456789" },
            new SymbolClass { Name = "_", Description = "Spațiu", Characters = " " }
        ]);

        doc.Dfa.Alphabet.UnionWith(["B", "L", "C", "_"]);
        doc.Dfa.States.UnionWith(["q0", "qB", "qJ1", "qJ2", "qB_sp1", "qJ_sp1", "qB_c1", "q_c2", "q_c3", "q_sp2", "q_l1", "q_l2", "q_ok"]);
        doc.Dfa.InitialState = "q0";
        doc.Dfa.FinalStates.Add("q_ok");

        doc.Dfa.AddTransition("q0", "B", "qB");
        doc.Dfa.AddTransition("q0", "L", "qJ1");

        doc.Dfa.AddTransition("qB", "_", "qB_sp1");
        doc.Dfa.AddTransition("qB", "B", "qJ2");
        doc.Dfa.AddTransition("qB", "L", "qJ2");

        doc.Dfa.AddTransition("qJ1", "B", "qJ2");
        doc.Dfa.AddTransition("qJ1", "L", "qJ2");

        doc.Dfa.AddTransition("qJ2", "_", "qJ_sp1");

        doc.Dfa.AddTransition("qB_sp1", "C", "qB_c1");
        doc.Dfa.AddTransition("qB_c1", "C", "q_c2");
        doc.Dfa.AddTransition("q_c2", "C", "q_c3");

        doc.Dfa.States.Add("qJ_c1");
        doc.Dfa.AddTransition("qJ_sp1", "C", "qJ_c1");
        doc.Dfa.AddTransition("qJ_c1", "C", "q_c3");

        doc.Dfa.AddTransition("q_c2", "_", "q_sp2");
        doc.Dfa.AddTransition("q_c3", "_", "q_sp2");

        doc.Dfa.AddTransition("q_sp2", "B", "q_l1");
        doc.Dfa.AddTransition("q_sp2", "L", "q_l1");

        doc.Dfa.AddTransition("q_l1", "B", "q_l2");
        doc.Dfa.AddTransition("q_l1", "L", "q_l2");

        doc.Dfa.AddTransition("q_l2", "B", "q_ok");
        doc.Dfa.AddTransition("q_l2", "L", "q_ok");

        doc.StateLayout["q0"] = new(60, 200, "Start: aștept codul de județ (1 sau 2 litere)");
        doc.StateLayout["qB"] = new(170, 110, "Am citit 'B': aștept spațiu (București) sau a doua literă de județ");
        doc.StateLayout["qJ1"] = new(170, 290, "Am citit prima literă a județului: aștept a doua literă");
        doc.StateLayout["qJ2"] = new(290, 290, "Cod județ complet: aștept spațiu");
        doc.StateLayout["qB_sp1"] = new(290, 110, "București: aștept 2 sau 3 cifre");
        doc.StateLayout["qJ_sp1"] = new(410, 290, "Județ: aștept exact 2 cifre");
        doc.StateLayout["qB_c1"] = new(410, 110, "București: am 1 cifră, mai aștept 1 sau 2 cifre");
        doc.StateLayout["qJ_c1"] = new(530, 290, "Județ: am 1 cifră, mai aștept exact 1 cifră");
        doc.StateLayout["q_c2"] = new(530, 110, "București (2 cifre): aștept a 3-a cifră sau spațiu");
        doc.StateLayout["q_c3"] = new(650, 200, "Cifre complete: aștept spațiu");
        doc.StateLayout["q_sp2"] = new(760, 200, "Format incomplet: aștept 3 litere");
        doc.StateLayout["q_l1"] = new(870, 200, "Format incomplet: mai aștept 2 litere");
        doc.StateLayout["q_l2"] = new(980, 200, "Format incomplet: mai aștept 1 literă");
        doc.StateLayout["q_ok"] = new(1090, 200, "Număr de înmatriculare VALID!");

        doc.TestSuite.AddRange([
            new("B 123 ABC", true), new("B 12 ABC", true), new("CJ 12 ABC", true),
            new("AG 07 XYZ", true), new("BV 99 BBB", true), new("IS 01 AAA", true),
            new("B 001 ZZZ", true), new("TM 45 DEF", true),
            new("CJ 123 ABC", false), new("B 1 ABC", false), new("B123ABC", false),
            new("CJ 12 AB", false), new("CJ 12 ABCD", false), new("12 CJ ABC", false),
            new("B 1234 ABC", false), new("", false)
        ]);

        return doc;
    }
}