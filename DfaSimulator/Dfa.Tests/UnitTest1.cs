using Dfa.Core;
using Xunit;

namespace Dfa.Tests;

public class DfaTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData("bb", true)]
    [InlineData("abba", true)]
    [InlineData("a", false)]
    [InlineData("bab", false)]
    public void EvenA_ShouldValidateWordsCorrectly(string word, bool expected)
    {
        var doc = PredefinedExamples.CreateEvenA();
        var run = new DfaRun(doc.Dfa, word);

        Assert.Equal(expected, run.IsAccepted);
    }

    [Fact]
    public void Determinism_ShouldRejectDuplicateTransitionOnSameSymbol()
    {
        var doc = PredefinedExamples.CreateEvenA();
        var result = doc.Dfa.AddTransition("q0", "a", "q0"); 

        Assert.False(result.Success);
        Assert.Contains("există deja o tranziție", result.ErrorMessage);
    }

    [Theory]
    [InlineData("B 123 ABC", true)]
    [InlineData("B 12 ABC", true)]
    [InlineData("CJ 12 ABC", true)]
    [InlineData("AG 99 ZZZ", true)]
    [InlineData("BV 10 BBA", true)]
    [InlineData("CJ 123 ABC", false)]
    [InlineData("B 1 ABC", false)]   
    [InlineData("B123ABC", false)] 
    [InlineData("CJ 12 AB", false)]  
    public void LicensePlate_ShouldValidateCorrectly(string plate, bool expected)
    {
        var doc = PredefinedExamples.CreateRomanianLicensePlate();
        var run = new DfaRun(doc.Dfa, plate);

        Assert.Equal(expected, run.IsAccepted);
    }
}