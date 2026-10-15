using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public sealed class CodesDeleteTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vp-del-" + Guid.NewGuid().ToString("N"));

    public CodesDeleteTests() => Directory.CreateDirectory(_folder);

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

    private LocalDatabase NewDatabase() => new(Path.Combine(_folder, "a.db"));

    [Fact]
    public void DeletingAnEmployeeRemovesTheMappingAndTheProfileButNotOthers()
    {
        var database = NewDatabase();
        database.SaveEmployeeMapping("Rossi Mario", "0001234");
        database.SaveEmployeeMapping("Bianchi Anna", "0005678");
        database.SaveEmployeeProfile("Rossi Mario", new EmployeeProfile(6m, new DateTime(2026, 3, 1), null));
        database.SaveEmployeeProfile("Bianchi Anna", new EmployeeProfile(8m, null, null));

        Assert.True(database.DeleteEmployeeMapping("Rossi Mario"));

        Assert.DoesNotContain("Rossi Mario", database.GetEmployeeMappings().Keys);
        Assert.DoesNotContain("Rossi Mario", database.GetEmployeeProfiles().Keys);
        Assert.Equal("0005678", database.GetEmployeeMappings()["Bianchi Anna"]);
        Assert.Contains("Bianchi Anna", database.GetEmployeeProfiles().Keys);
    }

    [Fact]
    public void NamesAreMatchedIgnoringCaseAndSurroundingSpaces()
    {
        var database = NewDatabase();
        database.SaveEmployeeMapping("Rossi Mario", "0001234");

        Assert.True(database.DeleteEmployeeMapping("  rossi mario "));
        Assert.Empty(database.GetEmployeeMappings());
    }

    [Fact]
    public void DeletingSomethingThatIsNotThereReturnsFalseAndChangesNothing()
    {
        var database = NewDatabase();
        database.SaveEmployeeMapping("Rossi Mario", "0001234");
        database.SavePolicyMapping("Ferie", "FE");

        Assert.False(database.DeleteEmployeeMapping("Nessuno"));
        Assert.False(database.DeletePolicyMapping("Nulla"));
        Assert.Single(database.GetEmployeeMappings());
        Assert.Single(database.GetPolicyMappings());
    }

    [Fact]
    public void DeletingAPolicyRemovesOnlyThatPolicy()
    {
        var database = NewDatabase();
        database.SavePolicyMapping("Ferie", "FE");
        database.SavePolicyMapping("Permesso", "PE");

        Assert.True(database.DeletePolicyMapping("ferie"));

        Assert.Equal(new[] { "Permesso" }, database.GetPolicyMappings().Keys.ToArray());
    }

    [Fact]
    public void DeletingACompanyRemovesItWithItsRules()
    {
        var database = NewDatabase();
        database.SaveCompany("AZ001", "Rossi Srl");
        database.SaveCompanyRules("AZ001", new CompanyRules(7.5m, true, false, "12-25"));
        database.SaveCompany("AZ002", "Verdi Spa");

        Assert.True(database.DeleteCompany("AZ001"));

        Assert.Equal(["AZ002"], database.GetCompanies().Select(company => company.Code));
        Assert.False(database.DeleteCompany("AZ001"));
    }

    [Fact]
    public void TablesCannotBeResizedAndRowSelectionTablesUseWholeRows()
    {
        var folder = SourceFolder();
        if (folder is null)
        {
            return;
        }
        var grid = File.ReadAllText(Path.Combine(folder, "UiControls.cs"));
        // Altezza di righe e intestazioni decisa dall'app: niente più righe trascinate a dismisura.
        Assert.Contains("AllowUserToResizeRows = false;", grid, StringComparison.Ordinal);
        Assert.Contains("public void UseRowSelection()", grid, StringComparison.Ordinal);

        // Le tabelle i cui pulsanti agiscono sulla riga scelta usano la selezione per riga (con la selezione di
        // cella SelectedRows è vuoto e «Elimina» non trova nulla).
        Assert.Contains("_companiesGrid.UseRowSelection();", File.ReadAllText(Path.Combine(folder, "MainForm.Companies.cs")), StringComparison.Ordinal);
        var shell = File.ReadAllText(Path.Combine(folder, "MainForm.Shell.cs"));
        Assert.Contains("_historyGrid.UseRowSelection();", shell, StringComparison.Ordinal);
        Assert.Contains("_dashExportsGrid.UseRowSelection();", shell, StringComparison.Ordinal);
    }

    private static string? SourceFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Installer.iss")))
            {
                return Path.Combine(directory.FullName, "PresenzeXmlConverter");
            }
            directory = directory.Parent;
        }
        return null;
    }
}
