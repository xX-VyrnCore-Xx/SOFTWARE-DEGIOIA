namespace PresenzeXmlConverter.Tests;

public class CompaniesAndCodesServiceTests
{
    private static (LocalDatabase Database, string Folder) NewDatabase()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        return (new LocalDatabase(Path.Combine(folder, "app.db")), folder);
    }

    [Fact]
    public void CompaniesAreSavedRulesAppliedAndDeletedWithTheActivityLog()
    {
        var (database, folder) = NewDatabase();
        try
        {
            var service = new CompaniesService(database);
            Assert.Throws<InvalidDataException>(() => service.Save("TROPPOLUNGO", "x", ""));
            Assert.Throws<InvalidDataException>(() => service.Save("  ", "x", ""));

            service.Save("AZ001", "Rossi Srl", "note");
            var company = Assert.Single(service.Load());
            Assert.Equal("Rossi Srl", company.Name);
            Assert.Equal("generali", CompaniesService.DescribeRules(company));

            service.SaveRules(company, new CompanyRules(6m, true, false, ""));
            var withRules = Assert.Single(service.Load());
            Assert.NotNull(withRules.Rules);
            Assert.NotEqual("generali", CompaniesService.DescribeRules(withRules));
            service.SaveRules(withRules, null);
            Assert.Null(Assert.Single(service.Load()).Rules);

            service.UseForConversion(company);
            Assert.Equal("AZ001", database.GetSetting("company-code"));

            Assert.True(service.Delete(company));
            Assert.Empty(service.Load());
            Assert.Contains(database.GetActivities(), activity => activity.Kind == ActivityKind.Company);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void CodesLoadSaveInCompleteAndDeleteLikeTheClassicPage()
    {
        var (database, folder) = NewDatabase();
        try
        {
            database.SaveEmployeeMapping("Rossi Mario", "E001");
            database.SavePolicyMapping("Ferie", "FE");
            var service = new CodesService(database);
            service.Load(["Rossi Mario", "Bianchi Anna"], ["Ferie", "Permesso"]);

            Assert.Equal(2, service.Employees.Count);
            Assert.Equal(2, service.Policies.Count);
            Assert.Equal(2, service.CountMissing());
            Assert.Equal("Bianchi Anna *", service.Employees[0].DisplayName);

            // Senza «salva a metà» i codici mancanti bloccano e non si scrive nulla.
            Assert.Throws<InvalidDataException>(() => service.Save(allowIncomplete: false));

            service.Employees[0].Code = "E002";
            service.Employees[0].Hours = "6,5";
            service.Employees[0].Start = "01/03/2026";
            service.Policies[1].Code = "PERM123456";
            var partial = service.Save(allowIncomplete: true);
            Assert.NotNull(partial.Note);
            Assert.Contains("troppo lungo", partial.Note);
            Assert.Equal("E002", database.GetEmployeeMappings()["Bianchi Anna"]);
            Assert.Equal(6.5m, database.GetEmployeeProfiles()["Bianchi Anna"].DailyHours);
            Assert.DoesNotContain("Permesso", database.GetPolicyMappings().Keys);

            service.Policies[1].Code = "PE";
            Assert.Null(service.Save(allowIncomplete: false).Note);
            Assert.Equal("PE", database.GetPolicyMappings()["Permesso"]);

            // Ore, date e rapporti scritti male si segnalano.
            service.Employees[0].Hours = "99";
            Assert.Throws<InvalidDataException>(() => service.Save(allowIncomplete: false));
            service.Employees[0].Hours = string.Empty;
            service.Employees[0].End = "01/01/2026";
            Assert.Throws<InvalidDataException>(() => service.Save(allowIncomplete: false));
            service.Employees[0].End = string.Empty;

            // Lo stesso codice a due persone diverse è un errore (o un avviso se si salva a metà).
            service.Employees[1].Code = "E002";
            Assert.Throws<InvalidDataException>(() => service.Save(allowIncomplete: false));
            Assert.NotNull(service.Save(allowIncomplete: true).Warnings);
            service.Employees[1].Code = "E001";

            service.Delete(service.Employees[0], isPolicy: false);
            Assert.DoesNotContain("Bianchi Anna", database.GetEmployeeMappings().Keys);
            Assert.DoesNotContain("Bianchi Anna", database.GetEmployeeProfiles().Keys);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
