using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public class CommandCatalogTests
{
    [Fact]
    public void EveryCommandIsCompleteAndUnique()
    {
        Assert.All(CommandCatalog.All, command =>
        {
            Assert.False(string.IsNullOrWhiteSpace(command.Id));
            Assert.False(string.IsNullOrWhiteSpace(command.Title));
            Assert.False(string.IsNullOrWhiteSpace(command.Hint));
        });
        Assert.Equal(CommandCatalog.All.Count, CommandCatalog.All.Select(command => command.Id).Distinct().Count());
        Assert.Equal(CommandCatalog.All.Count, CommandCatalog.All.Select(command => command.Title).Distinct().Count());
    }

    [Fact]
    public void ShortcutsListedInTheCatalogAreTheRealOnes()
    {
        var byId = CommandCatalog.All.ToDictionary(command => command.Id);
        Assert.Equal("Ctrl+O", byId["import"].Shortcut);
        Assert.Equal("Ctrl+G", byId["preview"].Shortcut);
        Assert.Equal("Ctrl+S", byId["export"].Shortcut);
        Assert.Equal("Ctrl+7", byId["go-activity"].Shortcut);
    }

    [Theory]
    [InlineData(">", true)]
    [InlineData("  > gen", true)]
    [InlineData("genera", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void PrefixMakesItACommandQuery(string? query, bool expected) =>
        Assert.Equal(expected, CommandCatalog.IsCommandQuery(query));

    [Theory]
    [InlineData("> genera ", "genera")]
    [InlineData(">", "")]
    [InlineData("  esporta", "esporta")]
    [InlineData(null, "")]
    public void PrefixIsStripped(string? query, string expected) =>
        Assert.Equal(expected, CommandCatalog.StripPrefix(query));

    [Fact]
    public void FindsByTitleKeywordsAndIgnoresAccentsAndOrder()
    {
        Assert.Equal("preview", CommandCatalog.Find("anteprima")[0].Id);
        Assert.Equal("export", CommandCatalog.Find("consegna")[0].Id);
        Assert.Contains(CommandCatalog.Find("attivita registro"), command => command.Id == "go-activity");
        Assert.Contains(CommandCatalog.Find("ATTIVITÀ"), command => command.Id == "go-activity");
        Assert.Empty(CommandCatalog.Find("inesistente"));
    }

    [Fact]
    public void TitleMatchesRankBeforeKeywordMatches()
    {
        // «tema» è nel titolo dei tre comandi del tema e solo nelle parole chiave di nessun altro: tutti titolo.
        var theme = CommandCatalog.Find("tema");
        Assert.Equal(["theme-dark", "theme-light", "theme-system"], theme.Select(command => command.Id).Take(3));
        // «salva» inizia il titolo di «Salva l'XML» e sta solo nelle parole chiave di «backup»: il titolo prima.
        var save = CommandCatalog.Find("salva").Select(command => command.Id).ToList();
        Assert.True(save.IndexOf("export") < save.IndexOf("backup"));
    }

    [Fact]
    public void EmptyQuerySuggestsTheFirstAvailableCommands()
    {
        var suggested = CommandCatalog.Find("");
        Assert.Equal(CommandCatalog.MaximumShown, suggested.Count);
        Assert.Equal("import", suggested[0].Id);

        var withoutImport = CommandCatalog.Find("", command => command.Id != "import");
        Assert.DoesNotContain(withoutImport, command => command.Id == "import");
        Assert.Equal(CommandCatalog.MaximumShown, withoutImport.Count);
    }

    [Fact]
    public void UnavailableCommandsAreNeverOffered()
    {
        var found = CommandCatalog.Find("esporta", command => command.Id != "export");
        Assert.DoesNotContain(found, command => command.Id == "export");
    }

    [Fact]
    public void LimitIsRespected() => Assert.Equal(2, CommandCatalog.Find("vai", limit: 2).Count);

    private static SearchHit Data(int index) => new(SearchKind.Employee, $"Persona {index}", "Dipendente", $"p{index}");

    [Fact]
    public void ComposeKeepsAllDataAndAFewCommandsWhenThereIsRoom()
    {
        var data = Enumerable.Range(1, 3).Select(Data).ToList();
        var commands = CommandCatalog.Find("vai");
        var hits = CommandCatalog.Compose(data, commands, commandsOnly: false);

        Assert.Equal(CommandCatalog.MaximumShown, hits.Count);
        Assert.Equal(data, hits.Take(3).ToList());
        Assert.All(hits.Skip(3), hit => Assert.Equal(SearchKind.Command, hit.Kind));
    }

    [Fact]
    public void ComposeNeverLetsCommandsCoverTheData()
    {
        var data = Enumerable.Range(1, 8).Select(Data).ToList();
        var hits = CommandCatalog.Compose(data, CommandCatalog.Find("vai"), commandsOnly: false);

        Assert.Equal(CommandCatalog.MaximumShown, hits.Count);
        Assert.Equal(CommandCatalog.MaximumAmongResults, hits.Count(hit => hit.Kind == SearchKind.Command));
        Assert.Equal(5, hits.Count(hit => hit.Kind == SearchKind.Employee));
    }

    [Fact]
    public void ComposeWithFewCommandsUsesTheRoomForData()
    {
        var data = Enumerable.Range(1, 8).Select(Data).ToList();
        var one = CommandCatalog.Find("rimuovi");
        var hits = CommandCatalog.Compose(data, one, commandsOnly: false);
        Assert.Equal(CommandCatalog.MaximumShown, hits.Count);
        Assert.Equal(1, hits.Count(hit => hit.Kind == SearchKind.Command));
        Assert.Equal(7, hits.Count(hit => hit.Kind == SearchKind.Employee));
    }

    [Fact]
    public void CommandsOnlyIgnoresData()
    {
        var hits = CommandCatalog.Compose([Data(1)], CommandCatalog.Find(">"), commandsOnly: true);
        Assert.All(hits, hit => Assert.Equal(SearchKind.Command, hit.Kind));
        Assert.Equal(CommandCatalog.MaximumShown, hits.Count);
    }

    [Fact]
    public void HitCarriesTheCommandIdAsKeyAndShortcutInTheDetail()
    {
        var hit = CommandCatalog.ToHit(CommandCatalog.All.First(command => command.Id == "preview"));
        Assert.Equal(SearchKind.Command, hit.Kind);
        Assert.Equal("preview", hit.Key);
        Assert.Contains("Ctrl+G", hit.Detail);
        Assert.StartsWith("Comando", hit.Detail);
        var noShortcut = CommandCatalog.ToHit(CommandCatalog.All.First(command => command.Id == "backup"));
        Assert.DoesNotContain("Ctrl", noShortcut.Detail);
    }
}
