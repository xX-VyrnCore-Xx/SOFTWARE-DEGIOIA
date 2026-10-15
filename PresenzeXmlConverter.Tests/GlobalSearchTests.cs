using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public class GlobalSearchTests
{
    private static readonly CompanyRecord[] Companies =
    [
        new("AZ001", "Rossi Srl", string.Empty),
        new("AZ002", "Bianchi Spa", "cliente storico")
    ];

    private static readonly Dictionary<string, string> Employees = new()
    {
        ["Rossi Mario"] = "0012",
        ["Verdi Anna"] = string.Empty
    };

    private static readonly Dictionary<string, string> Policies = new() { ["Ferie"] = "F01", ["Malattia"] = "M01" };

    private static readonly ExportRecord[] Exports =
    [
        new(new DateTime(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc), "presenze-ottobre.xml", @"C:\x\presenze-ottobre.xml", "AZ001", 12, 240, 5000, 7)
    ];

    private static IReadOnlyList<SearchHit> Find(string? query, int movements = 0) =>
        GlobalSearch.Find(query, movements, Companies, Employees, Policies, Exports);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("r")]
    public void ShortOrEmptyQueryFindsNothing(string? query) => Assert.Empty(Find(query, 10));

    [Fact]
    public void FindsEachKindIgnoringCaseAndAccents()
    {
        Assert.Equal(SearchKind.Company, Assert.Single(Find("BIANCHI")).Kind);
        Assert.Equal(SearchKind.Policy, Assert.Single(Find("ferie")).Kind);
        Assert.Equal(SearchKind.Export, Assert.Single(Find("ottobre")).Kind);
        Assert.Equal("Verdi Anna", Assert.Single(Find("verdi")).Title);
    }

    [Fact]
    public void WordsInAnyOrderAndCodesMatch()
    {
        Assert.Equal("Rossi Mario", Find("mario rossi").First(h => h.Kind == SearchKind.Employee).Title);
        Assert.Contains(Find("0012"), h => h.Kind == SearchKind.Employee);
        Assert.Contains(Find("az002"), h => h.Kind == SearchKind.Company && h.Key == "AZ002");
    }

    [Fact]
    public void SharedWordFindsSeveralKinds()
    {
        var hits = Find("rossi");
        Assert.Contains(hits, h => h.Kind == SearchKind.Company);
        Assert.Contains(hits, h => h.Kind == SearchKind.Employee);
    }

    [Fact]
    public void MovementsEntryComesFirstOnlyWhenThereAreMovements()
    {
        Assert.Equal(SearchKind.Movements, Find("rossi", 240)[0].Kind);
        Assert.DoesNotContain(Find("rossi", 0), h => h.Kind == SearchKind.Movements);
    }

    [Fact]
    public void DetailsDescribeMissingCodes()
    {
        Assert.Contains("codice da inserire", Find("verdi")[0].Detail);
        Assert.Contains("codice 0012", Find("mario")[0].Detail);
    }

    [Fact]
    public void ResultsAreCapped()
    {
        var many = Enumerable.Range(0, 20).ToDictionary(i => $"Rossi Persona {i:00}", _ => "1");
        var hits = GlobalSearch.Find("rossi", 5, Companies, many, many, Exports);
        Assert.True(hits.Count <= GlobalSearch.MaximumHits);
        Assert.Equal(3, hits.Count(h => h.Kind == SearchKind.Employee));
    }

    [Fact]
    public void PrefixMatchesRankFirst()
    {
        var people = new Dictionary<string, string> { ["Del Rossi Luca"] = "1", ["Rossi Zeno"] = "2" };
        var hits = GlobalSearch.Find("rossi", 0, [], people, new Dictionary<string, string>(), []);
        Assert.Equal("Rossi Zeno", hits[0].Title);
    }
}
