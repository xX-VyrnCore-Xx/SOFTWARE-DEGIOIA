using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

/// <summary>Lo stesso dipendente scritto in modi diversi è una persona sola, non un conflitto di codice.</summary>
public class EmployeeNamesTests
{
    [Theory]
    [InlineData("Vincenzo Antenori", "Antenori Vincenzo")]
    [InlineData("Vincenzo Antenori", "ANTENORI  vincenzo")]
    [InlineData("Niccolò Zanè", "Zane Niccolo")]
    [InlineData("Rossi, Mario", "Mario Rossi")]
    public void SpellingVariantsOfTheSameNameAreTheSamePerson(string first, string second)
    {
        Assert.True(EmployeeNames.SamePerson(first, second));
    }

    [Fact]
    public void DifferentPeopleAreNotMerged()
    {
        Assert.False(EmployeeNames.SamePerson("Mario Rossi", "Mario Bianchi"));
        Assert.False(EmployeeNames.SamePerson("", ""));
    }

    private static AttendanceRequest Ferie(string name, int day) =>
        new(name, "Ferie", new DateTime(2026, 9, day), new DateTime(2026, 9, day), 8m, day);

    [Fact]
    public void TwoSpellingsWithTheSameCodeBecomeOneEmployeeInTheMovements()
    {
        var codes = new Dictionary<string, string>
        {
            ["Vincenzo Antenori"] = "0000001",
            ["Antenori Vincenzo"] = "0000001"
        };

        var result = AttendanceMovementGenerator.Generate(
            [Ferie("Vincenzo Antenori", 7), Ferie("Antenori Vincenzo", 8)],
            codes,
            new Dictionary<string, string> { ["Ferie"] = "FE" },
            null,
            null,
            new GenerationOptions(FillPresence: true));

        Assert.Single(result.Movements.Select(movement => movement.EmployeeName).Distinct());
        // Una sola presenza per giorno: niente doppioni per le due grafie.
        Assert.Equal(
            result.Movements.Count(movement => AttendanceMovementGenerator.IsPresence(movement)),
            result.Movements.Where(movement => AttendanceMovementGenerator.IsPresence(movement)).Select(movement => movement.Date).Distinct().Count());
        // E l'XML si può scrivere: lo stesso codice non è «associato a più nominativi».
        var xml = TrriPwXmlWriter.Create("AZ001", result.Movements);
        Assert.Contains("0000001", xml);
    }

    [Fact]
    public void TheNameOfAVariantWithoutItsOwnCodeUsesThePersonsCode()
    {
        var result = AttendanceMovementGenerator.Generate(
            [Ferie("Vincenzo Antenori", 7), Ferie("Antenori Vincenzo", 8)],
            new Dictionary<string, string> { ["Vincenzo Antenori"] = "0000001" },
            new Dictionary<string, string> { ["Ferie"] = "FE" },
            null,
            null);

        Assert.All(result.Movements, movement => Assert.Equal("0000001", movement.EmployeeCode));
    }

    [Fact]
    public void VariantsWithDifferentCodesStayTwoPeople()
    {
        var codes = new Dictionary<string, string>
        {
            ["Vincenzo Antenori"] = "0000001",
            ["Antenori Vincenzo"] = "0000002"
        };

        var result = AttendanceMovementGenerator.Generate(
            [Ferie("Vincenzo Antenori", 7), Ferie("Antenori Vincenzo", 8)],
            codes,
            new Dictionary<string, string> { ["Ferie"] = "FE" },
            null,
            null);

        Assert.Equal(2, result.Movements.Select(movement => movement.EmployeeName).Distinct().Count());
    }

    [Fact]
    public void TwoDifferentPeopleSharingACodeStillCannotBeExported()
    {
        var movements = new[]
        {
            new DailyMovement("Mario Rossi", "0000001", "Ferie", "FE", new DateTime(2026, 9, 7), 8, 0),
            new DailyMovement("Luca Bianchi", "0000001", "Ferie", "FE", new DateTime(2026, 9, 8), 8, 0)
        };

        Assert.Throws<InvalidDataException>(() => TrriPwXmlWriter.Create("AZ001", movements));
    }
}
