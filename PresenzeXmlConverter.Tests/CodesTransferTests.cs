using System.Windows;
using System.Windows.Controls;
using PresenzeXmlConverter.Wpf;

namespace PresenzeXmlConverter.Tests;

public sealed class CodesTransferTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vp-transfer-" + Guid.NewGuid().ToString("N"));

    public CodesTransferTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
        }
    }

    private LocalDatabase NewDatabase(string name) => new(Path.Combine(_folder, name + ".db"));

    [Fact]
    public void ExportedCodesAndCompaniesCanBeImportedOnAnotherPc()
    {
        var source = NewDatabase("a");
        source.SaveEmployeeMapping("Rossi Mario", "0001234");
        source.SavePolicyMapping("Ferie", "FE");
        source.SaveCompany("AZ001", "Rossi Srl", "cliente storico");
        var file = Path.Combine(_folder, "codici.json");

        var described = CodesTransfer.Export(source, file, DateTime.UtcNow);

        Assert.NotNull(described);
        Assert.True(File.Exists(file));
        var target = NewDatabase("b");
        Assert.True(CodesTransfer.TryRead(file, out var backup, out var problem), problem);
        var report = CodesTransfer.Import(target, backup);
        Assert.Empty(report.Skipped);
        Assert.Equal("0001234", target.GetEmployeeMappings()["Rossi Mario"]);
        Assert.Equal("FE", target.GetPolicyMappings()["Ferie"]);
        Assert.Single(target.GetCompanies());
    }

    [Fact]
    public void ExportingAnEmptyDatabaseWritesNothing()
    {
        var file = Path.Combine(_folder, "vuoto.json");

        Assert.Null(CodesTransfer.Export(NewDatabase("vuoto"), file, DateTime.UtcNow));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void AFileThatIsNotACodesExportIsRefusedWithAReason()
    {
        var file = Path.Combine(_folder, "altro.json");
        File.WriteAllText(file, "{ \"non\": \"codici\" }");

        Assert.False(CodesTransfer.TryRead(file, out _, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }
}

[Collection("Wpf")]
public class WpfCodesTransferTests
{
    private static IEnumerable<Button> Buttons(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is Button button)
            {
                yield return button;
            }
            foreach (var inner in Buttons(child))
            {
                yield return inner;
            }
        }
    }

    [Fact]
    public void TheTransferMenuOffersExportImportAndTheAutomaticCopies()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var folder = Directory.CreateTempSubdirectory().FullName;
            var service = new ConversionService(new LocalDatabase(Path.Combine(folder, "app.db")));
            var transfer = new TransferButton(service, () => null, _ => { }, () => { });

            var names = transfer.Entries().Where(entry => !entry.IsSeparator).Select(entry => entry.Text).ToArray();

            Assert.Equal(["Esporta codici e aziende…", "Importa codici e aziende…", "Apri la cartella delle copie automatiche"], names);
        });
    }

    [Fact]
    public void CodesAndCompaniesPagesBothHaveTheTransferButton()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var folder = Directory.CreateTempSubdirectory().FullName;
            var service = new ConversionService(new LocalDatabase(Path.Combine(folder, "app.db")));
            foreach (var page in new DependencyObject[] { new CodesView(service, () => null), new CompaniesView(service, () => null) })
            {
                Assert.Contains(Buttons(page), button => button.Content is string text && text.StartsWith("Trasferisci"));
            }
        });
    }
}
