namespace PresenzeXmlConverter.Tests;

public class ConversionServiceTests
{
    private sealed class FakePrompts : IConversionPrompts
    {
        public int MappingRequests { get; private set; }

        public ColumnSelection? AskColumns(IReadOnlyList<string> headers) => null;

        public bool ConfirmCodeReplace(string employee, string fileCode, string savedCode) => true;

        public bool CompleteMappings(IReadOnlyList<string> missingEmployees, IReadOnlyList<string> missingPolicies, LocalDatabase database)
        {
            MappingRequests++;
            var index = 0;
            foreach (var employee in missingEmployees)
            {
                database.SaveEmployeeMapping(employee, $"E{++index:D3}");
            }
            foreach (var policy in missingPolicies)
            {
                database.SavePolicyMapping(policy, "FE");
            }
            return true;
        }

        public bool ConfirmAlreadyDelivered(ExportRecord previous) => true;

        public void ShowProblem(string title, string message)
        {
        }
    }

    [Fact]
    public async Task TheWholeFlowReadsGeneratesChecksExportsAndRemembersTheDelivery()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var database = new LocalDatabase(Path.Combine(folder, "app.db"));
            var service = new ConversionService(database) { CompanyCode = "AZ001" };
            var prompts = new FakePrompts();

            Assert.True(await service.LoadAsync(SampleWorkbook.Path, prompts));
            Assert.Equal(SampleWorkbook.RequestCount, service.Requests.Count);
            Assert.False(service.HasPreview);

            Assert.Equal(PreviewOutcome.Ready, await service.GenerateAsync(prompts));
            Assert.Equal(1, prompts.MappingRequests);
            Assert.True(service.HasPreview);
            Assert.NotEmpty(service.Movements);
            Assert.Equal("AZ001", database.GetSetting("company-code"));

            Assert.Null(service.FindPreviousDelivery());
            var output = Path.Combine(folder, "out.xml");
            service.Export(output);
            Assert.Empty(TrriPwXmlValidator.ValidateFile(output));
            Assert.NotNull(service.FindPreviousDelivery());
            Assert.Single(database.GetExports());
            Assert.Contains(database.GetActivities(), activity => activity.Kind == ActivityKind.Export);

            // Un secondo giro con i codici già noti non chiede più nulla.
            Assert.Equal(PreviewOutcome.Ready, await service.GenerateAsync(prompts));
            Assert.Equal(1, prompts.MappingRequests);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task TheMonthlySummaryReportAndCsvComeFromTheSameMovements()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var service = new ConversionService(new LocalDatabase(Path.Combine(folder, "app.db"))) { CompanyCode = "AZ001" };
            var prompts = new FakePrompts();
            Assert.Empty(service.MonthlySummary());
            Assert.Equal("Report ore.pdf", service.SuggestedReportFileName("pdf"));

            await service.LoadAsync(SampleWorkbook.Path, prompts);
            Assert.Equal(PreviewOutcome.Ready, await service.GenerateAsync(prompts));

            var rows = service.MonthlySummary();
            Assert.NotEmpty(rows);
            Assert.Equal(
                service.Movements.Sum(movement => (movement.Hours * 60) + movement.Minutes),
                rows.Sum(row => row.TotalMinutes));

            var csv = service.BuildMonthlySummaryCsv();
            Assert.StartsWith("Dipendente;Codice;Mese;", csv);
            Assert.Equal(rows.Count + 1, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);

            var pdf = service.BuildMonthlyReportPdf(new DateTime(2026, 4, 1, 9, 30, 0));
            Assert.True(pdf.Length > 500);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

            var first = service.Movements.Min(movement => movement.Date);
            Assert.Equal($"Report ore {first.Year}-{first.Month:00}.pdf", service.SuggestedReportFileName("pdf"));
            Assert.Equal($"Riepilogo ore {first.Year}-{first.Month:00}.csv", service.SuggestedReportFileName("csv"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task ARefusedMappingStopsTheGenerationWithoutAPreview()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var service = new ConversionService(new LocalDatabase(Path.Combine(folder, "app.db"))) { CompanyCode = "AZ001" };
            var prompts = new RefusingPrompts();
            Assert.True(await service.LoadAsync(SampleWorkbook.Path, prompts));
            Assert.Equal(PreviewOutcome.Cancelled, await service.GenerateAsync(prompts));
            Assert.False(service.HasPreview);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task AnInvalidCompanyCodeIsRejectedBeforeAnythingIsWritten()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var service = new ConversionService(new LocalDatabase(Path.Combine(folder, "app.db"))) { CompanyCode = "TROPPOLUNGO" };
            await service.LoadAsync(SampleWorkbook.Path, new FakePrompts());
            await Assert.ThrowsAsync<InvalidDataException>(() => service.GenerateAsync(new FakePrompts()));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private sealed class RefusingPrompts : IConversionPrompts
    {
        public ColumnSelection? AskColumns(IReadOnlyList<string> headers) => null;

        public bool ConfirmCodeReplace(string employee, string fileCode, string savedCode) => false;

        public bool CompleteMappings(IReadOnlyList<string> missingEmployees, IReadOnlyList<string> missingPolicies, LocalDatabase database) => false;

        public bool ConfirmAlreadyDelivered(ExportRecord previous) => false;

        public void ShowProblem(string title, string message)
        {
        }
    }
}
