using System.Diagnostics;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Guardie di velocità sulle parti pure del motore: un foglio grande (migliaia di righe) non
/// deve mai far aspettare l'utente. I limiti sono larghi (macchine lente della CI) ma
/// bastano a scoprire un ciclo dentro un ciclo, come quello dei controlli sui dipendenti
/// (centinaia di nomi per migliaia di movimenti: 700 ms prima della correzione).
/// </summary>
public class PerformanceGuardTests
{
    private static (DailyMovement[] Movements, string[] Employees, Dictionary<string, string> Policies) LargeSample()
    {
        var movements = Enumerable.Range(0, 9000)
            .Select(index => new DailyMovement(
                $"Dipendente {index % 900:D3}",
                (2000000 + (index % 900)).ToString(),
                "Ferie",
                "01",
                new DateTime(2026, 8, 3).AddDays(index / 900),
                8,
                0,
                index + 5))
            .ToArray();
        var employees = Enumerable.Range(0, 900).Select(index => $"Dipendente {index:D3}").ToArray();
        return (movements, employees, new Dictionary<string, string> { ["Ferie"] = "01" });
    }

    [Fact]
    public void ReviewingManyMovementsStaysFast()
    {
        var (movements, employees, policies) = LargeSample();

        var stopwatch = Stopwatch.StartNew();
        MovementChecks.Review(movements, policies, employees, new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), 8m, true);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 1500, $"Review troppo lenta: {stopwatch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public void ValidatingAndWritingALargeXmlStaysFast()
    {
        var (movements, _, _) = LargeSample();

        var stopwatch = Stopwatch.StartNew();
        var xml = TrriPwXmlWriter.Create("AZ001", movements);
        var issues = TrriPwXmlValidator.Validate(System.Text.Encoding.UTF8.GetBytes(xml));
        stopwatch.Stop();

        Assert.Empty(issues);
        Assert.True(stopwatch.ElapsedMilliseconds < 4000, $"Scrittura e controllo troppo lenti: {stopwatch.ElapsedMilliseconds} ms.");
    }
}
