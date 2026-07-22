using Xunit;

namespace CspAnalyzer.BackendInterop.Tests;

public class SpectrumResultTests
{
    [Fact]
    public void ParseArray_deserializes_plain_bool_isActive_without_a_regex_workaround()
    {
        // Matches backend/io.py:json_constructor's real output shape - the
        // S1 tuple-formatting bug (`[true]` instead of `true`) that used to
        // need Form1.cs:1522-1523's regex rewrite is fixed on the python
        // side, so plain deserialization here is the proof.
        const string json = """
            [
                {"EXP_NUMBER": 1, "isActive": true, "activePseudoprobability": 0.87},
                {"EXP_NUMBER": 2, "isActive": false, "activePseudoprobability": 0.12}
            ]
            """;

        var results = SpectrumResult.ParseArray(json);

        Assert.Equal(2, results.Length);
        Assert.Equal(1, results[0].ExpNumber);
        Assert.True(results[0].IsActive);
        Assert.Equal(0.87, results[0].ActivePseudoprobability, precision: 10);
        Assert.Equal(2, results[1].ExpNumber);
        Assert.False(results[1].IsActive);
    }
}
