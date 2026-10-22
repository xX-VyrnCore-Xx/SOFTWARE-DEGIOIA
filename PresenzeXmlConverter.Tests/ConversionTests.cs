using System.Text;
using System.Xml.Linq;
using System.IO.Compression;
using ClosedXML.Excel;

namespace PresenzeXmlConverter.Tests;

public class ConversionTests
{
    [Fact]
    public void SampleWorkbookContainsExpectedAttendanceRequests()
    {
        var path = SampleWorkbook.Path;
        var inspection = AttendanceWorkbookReader.Inspect(path);

        Assert.Equal(SampleWorkbook.SheetName, inspection.SheetName);
        Assert.Equal(new DateTime(2026, 8, 1), inspection.PeriodStart);
        Assert.Equal(new DateTime(2026, 8, 31), inspection.PeriodEnd);
        Assert.NotNull(inspection.SuggestedColumns);

        var requests = inspection.ReadRequests(inspection.SuggestedColumns!);
        Assert.Equal(SampleWorkbook.RequestCount, requests.Count);
        Assert.Equal(SampleWorkbook.EmployeeCount, requests.Select(request => request.EmployeeName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(SampleWorkbook.PolicyCount, requests.Select(request => request.Policy).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void MultiDayDurationIsDistributedAcrossWeekdaysAndSkipsNationalHolidays()
    {
        var request = new AttendanceRequest("Mario Rossi", "IT vacation (hours)", new DateTime(2026, 8, 14), new DateTime(2026, 8, 17), 16m, 4);
        var result = AttendanceMovementGenerator.Generate(
            [request],
            new Dictionary<string, string> { ["Mario Rossi"] = "1234567" },
            new Dictionary<string, string> { ["IT vacation (hours)"] = "01" },
            null,
            null);

        Assert.Equal(2, result.Movements.Count);
        Assert.Equal(new DateTime(2026, 8, 14), result.Movements[0].Date);
        Assert.Equal(new DateTime(2026, 8, 17), result.Movements[1].Date);
        Assert.All(result.Movements, movement =>
        {
            Assert.Equal(8, movement.Hours);
            Assert.Equal(0, movement.Minutes);
        });
        Assert.True(ItalianHolidays.Contains(new DateTime(2026, 8, 15)));
        Assert.True(ItalianHolidays.Contains(new DateTime(2026, 4, 6)));
    }

    [Fact]
    public void SampleWorkbookCanBeExpandedIntoDailyMovementsWithoutLosingRequestedMinutes()
    {
        var path = SampleWorkbook.Path;
        var inspection = AttendanceWorkbookReader.Inspect(path);
        var requests = inspection.ReadRequests(inspection.SuggestedColumns!);
        var employees = requests.Select(request => request.EmployeeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((name, index) => (name, code: (index + 1).ToString("D7")))
            .ToDictionary(item => item.name, item => item.code, StringComparer.OrdinalIgnoreCase);
        var policies = requests.Select(request => request.Policy)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(policy => policy, _ => "01", StringComparer.OrdinalIgnoreCase);

        var result = AttendanceMovementGenerator.Generate(
            requests,
            employees,
            policies,
            inspection.PeriodStart,
            inspection.PeriodEnd,
            // Questo test verifica la distribuzione sui soli feriali: il sabato
            // automatico ha i suoi test in WorkCalendarTests.
            new GenerationOptions(AutoDetect: false));
        var expectedMinutes = requests
            .Where(request => Enumerable.Range(0, (request.EndDate.Date - request.StartDate.Date).Days + 1)
                .Select(offset => request.StartDate.Date.AddDays(offset))
                .Any(day => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !ItalianHolidays.Contains(day)))
            .Sum(request => decimal.ToInt32(decimal.Round(request.Hours * 60m, 0, MidpointRounding.AwayFromZero)));

        Assert.NotEmpty(result.Movements);
        Assert.Equal(expectedMinutes, result.Movements.Sum(movement => movement.Hours * 60 + movement.Minutes));
        Assert.All(result.Movements, movement =>
        {
            Assert.DoesNotContain(movement.Date.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
            Assert.False(ItalianHolidays.Contains(movement.Date));
        });
    }

    [Fact]
    public void OptionalOfficialEmployeeCodeColumnIsReadWhenPresent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"presenze-test-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Richieste");
                sheet.Cell(1, 1).Value = "Persona";
                sheet.Cell(1, 2).Value = "Causale";
                sheet.Cell(1, 3).Value = "Inizio";
                sheet.Cell(1, 4).Value = "Fine";
                sheet.Cell(1, 5).Value = "Ore";
                sheet.Cell(1, 6).Value = "Codice PAGHE";
                sheet.Cell(2, 1).Value = "Mario Rossi";
                sheet.Cell(2, 2).Value = "Ferie";
                sheet.Cell(2, 3).Value = new DateTime(2026, 8, 17);
                sheet.Cell(2, 4).Value = new DateTime(2026, 8, 17);
                sheet.Cell(2, 5).Value = 8;
                sheet.Cell(2, 6).Value = "0012345";
                workbook.SaveAs(path);
            }

            var inspection = AttendanceWorkbookReader.Inspect(path);
            var request = Assert.Single(inspection.ReadRequests(new ColumnSelection(0, 1, 2, 3, 4)));

            Assert.Equal("0012345", request.EmployeeCode);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData("1,5", 1.5)]
    [InlineData("1.5", 1.5)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("8h30", 8.5)]
    [InlineData("8h", 8)]
    [InlineData("8,5h", 8.5)]
    [InlineData("8:30", 8.5)]
    [InlineData("0:45", 0.75)]
    public void DurationTextUsesTheLastSeparatorAsTheDecimalSeparator(string text, decimal expected)
    {
        var cell = new WorkbookCell(1, text, null, null);

        Assert.True(cell.TryGetNumber(out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("non è un numero", false)]
    public void InvalidDurationTextIsRejected(string text, bool expected)
    {
        var cell = new WorkbookCell(1, text, null, null);

        Assert.Equal(expected, cell.TryGetNumber(out _));
    }

    [Fact]
    public void FuzzyHeadersAreRecognizedWithoutManualMapping()
    {
        // Intestazioni con casing, punteggiatura e sinonimi diversi dal solito.
        var headers = new[]
        {
            "NOME DIPENDENTE",
            "Causale assenza",
            "Data Inizio (dal)",
            "Data Fine (al)",
            "Ore totali"
        };

        var selection = AttendanceWorkbookReader.SuggestColumns(headers);

        Assert.NotNull(selection);
        Assert.Equal(0, selection!.Employee);
        Assert.Equal(1, selection.Policy);
        Assert.Equal(2, selection.StartDate);
        Assert.Equal(3, selection.EndDate);
        Assert.Equal(4, selection.Duration);
    }

    [Fact]
    public void PayrollCodeHeaderIsNeverMappedToTheEmployeeNameField()
    {
        // La colonna dei codici compare prima del nominativo: il motore deve
        // comunque assegnare "Dipendente" al campo nome, non al codice.
        var headers = new[] { "Codice", "Dipendente", "Causale", "Dal", "Al", "Ore" };

        var selection = AttendanceWorkbookReader.SuggestColumns(headers);

        Assert.NotNull(selection);
        Assert.Equal(1, selection!.Employee);
        Assert.Equal(2, selection.Policy);
        Assert.Equal(3, selection.StartDate);
        Assert.Equal(4, selection.EndDate);
        Assert.Equal(5, selection.Duration);
    }

    [Fact]
    public void ExcelTimeFormattedDurationCellsAreConvertedToHours()
    {
        var path = Path.Combine(Path.GetTempPath(), $"presenze-time-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Richieste");
                sheet.Cell(1, 1).Value = "Nome dipendente";
                sheet.Cell(1, 2).Value = "Causale";
                sheet.Cell(1, 3).Value = "Data inizio";
                sheet.Cell(1, 4).Value = "Data fine";
                sheet.Cell(1, 5).Value = "Ore";
                sheet.Cell(2, 1).Value = "Mario Rossi";
                sheet.Cell(2, 2).Value = "Ferie";
                sheet.Cell(2, 3).Value = new DateTime(2026, 8, 17);
                sheet.Cell(2, 4).Value = new DateTime(2026, 8, 17);
                // Durata come cella oraria Excel: 08:30 = frazione di giorno.
                sheet.Cell(2, 5).Value = 8.5d / 24d;
                sheet.Cell(2, 5).Style.NumberFormat.Format = "hh:mm";
                workbook.SaveAs(path);
            }

            var inspection = AttendanceWorkbookReader.Inspect(path);
            var request = Assert.Single(inspection.ReadRequests(inspection.SuggestedColumns!));

            Assert.Equal(8.5m, request.Hours);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void DuplicateMovementsForTheSameDayAndReasonAreMergedWithoutLosingHours()
    {
        var requests = new[]
        {
            new AttendanceRequest("Mario Rossi", "Ferie", new DateTime(2026, 8, 17), new DateTime(2026, 8, 17), 4m, 10),
            new AttendanceRequest("Mario Rossi", "Ferie", new DateTime(2026, 8, 17), new DateTime(2026, 8, 17), 2.5m, 20)
        };
        var result = AttendanceMovementGenerator.Generate(
            requests,
            new Dictionary<string, string> { ["Mario Rossi"] = "1234567" },
            new Dictionary<string, string> { ["Ferie"] = "01" },
            null,
            null);

        var movement = Assert.Single(result.Movements);
        Assert.Equal(6, movement.Hours);
        Assert.Equal(30, movement.Minutes);
        Assert.Equal(10, movement.SourceRow);
    }

    [Fact]
    public void MergedMovementsBeyondTwentyFourHoursProduceAWarning()
    {
        var requests = new[]
        {
            new AttendanceRequest("Mario Rossi", "Ferie", new DateTime(2026, 8, 17), new DateTime(2026, 8, 17), 20m, 10),
            new AttendanceRequest("Mario Rossi", "Ferie", new DateTime(2026, 8, 17), new DateTime(2026, 8, 17), 8m, 20)
        };
        var result = AttendanceMovementGenerator.Generate(
            requests,
            new Dictionary<string, string> { ["Mario Rossi"] = "1234567" },
            new Dictionary<string, string> { ["Ferie"] = "01" },
            null,
            null);

        Assert.Single(result.Movements);
        Assert.Contains(result.Warnings, warning => warning.Contains("oltre 24 ore", StringComparison.Ordinal));
    }

    [Fact]
    public void StreamingSaveProducesTheSameFileAsTheStringBuilderPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"presenzeflow-stream-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var movements = new[]
            {
                new DailyMovement("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 8, 17), 7, 30),
                new DailyMovement("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 8, 18), 8, 0),
                new DailyMovement("Luigi Bianchi", "7654321", "Permesso", "02", new DateTime(2026, 8, 17), 2, 15)
            };
            var stringPath = Path.Combine(directory, "string.xml");
            var streamingPath = Path.Combine(directory, "streaming.xml");

            TrriPwXmlWriter.SaveFile(stringPath, TrriPwXmlWriter.Create("AZ001", movements));
            TrriPwXmlWriter.SaveFile(streamingPath, "AZ001", movements);

            Assert.Equal(File.ReadAllBytes(stringPath), File.ReadAllBytes(streamingPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ColumnMappingRejectsReusingTheSameExcelColumn()
    {
        var path = SampleWorkbook.Path;
        var inspection = AttendanceWorkbookReader.Inspect(path);
        var valid = inspection.SuggestedColumns!;

        Assert.Throws<InvalidDataException>(() =>
            inspection.ReadRequests(valid with { Policy = valid.Employee }));
    }

    [Fact]
    public void WorkbookReaderRejectsHighlyCompressedArchiveEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"presenze-bomb-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("xl/worksheets/sheet1.xml");
                using var writer = new StreamWriter(entry.Open());
                writer.Write(new string('A', 16 * 1024 * 1024));
            }

            Assert.Throws<InvalidDataException>(() => AttendanceWorkbookReader.Inspect(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void OverlappingRequestsAreFlaggedWithoutSilentlyDroppingHours()
    {
        var requests = new[]
        {
            new AttendanceRequest("Mario Rossi", "Ferie", new DateTime(2026, 8, 17), new DateTime(2026, 8, 17), 4m, 12),
            new AttendanceRequest("Mario Rossi", "Permesso", new DateTime(2026, 8, 17), new DateTime(2026, 8, 17), 2m, 18)
        };
        var result = AttendanceMovementGenerator.Generate(
            requests,
            new Dictionary<string, string> { ["Mario Rossi"] = "1234567" },
            new Dictionary<string, string> { ["Ferie"] = "01", ["Permesso"] = "02" },
            null,
            null);

        Assert.Equal(2, result.Movements.Count);
        Assert.Contains(result.Warnings, warning =>
            warning.Contains("righe 12, 18", StringComparison.Ordinal));
    }

    [Fact]
    public void XmlUsesDailyOfficialFieldsAndIsUtf8WithoutBom()
    {
        var movements = new[]
        {
            new DailyMovement("Mario Rossi", "1234567", "IT vacation (hours)", "01", new DateTime(2026, 8, 17), 7, 30)
        };

        var xml = TrriPwXmlWriter.Create("AZ001", movements);
        var bytes = Encoding.UTF8.GetBytes(xml);
        var document = XDocument.Parse(xml);
        var employee = Assert.Single(document.Root!.Elements("Dipendente"));
        var movement = Assert.Single(employee.Element("Movimenti")!.Elements("Movimento"));

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        // L'attributo è del nodo Movimenti, come nel tracciato Zucchetti.
        Assert.Equal("N", (string?)employee.Element("Movimenti")!.Attribute("GenerazioneAutomaticaDaTeorico"));
        Assert.Null(employee.Attribute("GenerazioneAutomaticaDaTeorico"));
        // Struttura identica al file che Zucchetti importa: codici come attributi del nodo.
        Assert.Equal("Fornitura", document.Root!.Name.LocalName);
        Assert.Equal("AZ001", (string?)employee.Attribute("CodAziendaUfficiale"));
        Assert.Equal("1234567", (string?)employee.Attribute("CodDipendenteUfficiale"));
        Assert.Equal("01", (string?)movement.Element("CodGiustificativoRilPres"));
        Assert.Equal("01", (string?)movement.Element("CodGiustificativoUfficiale"));
        Assert.Equal("2026-08-17", (string?)movement.Element("Data"));
        Assert.Equal("07", (string?)movement.Element("NumOre"));
        Assert.Equal("30", (string?)movement.Element("NumMinuti"));
    }

    [Fact]
    public void SameEmployeeDayAndReasonAreMergedIntoOneMovement()
    {
        var day = new DateTime(2026, 8, 17);
        var movements = new[]
        {
            new DailyMovement("Mario Rossi", "1234567", "Ferie", "01", day, 4, 0, 5),
            new DailyMovement("Mario Rossi", "1234567", "Ferie", "01", day, 3, 45, 9),
            new DailyMovement("Mario Rossi", "1234567", "Permesso", "02", day, 1, 0, 11)
        };

        var document = XDocument.Parse(TrriPwXmlWriter.Create("AZ001", movements));
        var list = document.Root!.Element("Dipendente")!.Element("Movimenti")!.Elements("Movimento").ToArray();

        Assert.Equal(2, list.Length);
        var holiday = Assert.Single(list, item => (string?)item.Element("CodGiustificativoUfficiale") == "01");
        Assert.Equal("07", (string?)holiday.Element("NumOre"));
        Assert.Equal("45", (string?)holiday.Element("NumMinuti"));
    }

    /// <summary>
    /// I limiti del tracciato stanno in un unico posto e vengono applicati al
    /// valore, non a occhio: qui si controlla il confine esatto di ogni campo
    /// documentato (codice azienda 6, dipendente 7, giustificativo 3, ore 99,
    /// minuti 59). Un limite sbagliato qui fa rifiutare un file valido da PAGHE.
    /// </summary>
    [Fact]
    public void XmlWriterEnforcesTheDocumentedTracciatoLimits()
    {
        Assert.Equal("Fornitura", TrriPwXmlWriter.RootElement);
        var company = new string('A', TrriPwXmlWriter.MaxCompanyCodeLength);
        var movement = new DailyMovement(
            "Rossi Mario",
            new string('1', TrriPwXmlWriter.MaxEmployeeCodeLength),
            "Ferie",
            new string('9', TrriPwXmlWriter.MaxReasonCodeLength),
            new DateTime(2026, 5, 12),
            TrriPwXmlWriter.MaxHoursPerMovement,
            TrriPwXmlWriter.MaxMinutesPerMovement);

        // Al limite: il documento viene prodotto.
        Assert.Contains("<Fornitura>", TrriPwXmlWriter.Create(company, [movement]));

        // Un carattere oltre il limite: rifiutato.
        Assert.Throws<InvalidDataException>(() =>
            TrriPwXmlWriter.Create(company + "A", [movement]));
        Assert.Throws<InvalidDataException>(() =>
            TrriPwXmlWriter.Create(company, [movement with { EmployeeCode = movement.EmployeeCode + "1" }]));
        Assert.Throws<InvalidDataException>(() =>
            TrriPwXmlWriter.Create(company, [movement with { ReasonCode = movement.ReasonCode + "9" }]));
        Assert.Throws<InvalidDataException>(() =>
            TrriPwXmlWriter.Create(company, [movement with { Hours = TrriPwXmlWriter.MaxHoursPerMovement + 1 }]));
        Assert.Throws<InvalidDataException>(() =>
            TrriPwXmlWriter.Create(company, [movement with { Minutes = TrriPwXmlWriter.MaxMinutesPerMovement + 1 }]));
    }

    /// <summary>
    /// L'errore di validazione deve dire QUALE dipendente correggere: la griglia
    /// mostra i nominativi, non le posizioni, e un messaggio generico costringe a
    /// cercare a mano la riga sbagliata.
    /// </summary>
    [Fact]
    public void XmlValidationErrorsNameTheEmployeeToFix()
    {
        var exception = Assert.Throws<InvalidDataException>(() => TrriPwXmlWriter.Create(
            "AZ001",
            [new DailyMovement("Rossi Mario", "12345678", "Ferie", "01", new DateTime(2026, 5, 12), 8, 0)]));

        Assert.Contains("Rossi Mario", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void XmlWriterRejectsInvalidCodesAndEmptyMovementSets()
    {
        Assert.Throws<InvalidDataException>(() => TrriPwXmlWriter.Create("AZ001", []));
        Assert.Throws<InvalidDataException>(() => TrriPwXmlWriter.Create(
            "AZ001",
            [new DailyMovement("Mario Rossi", "12345678", "Ferie", "01", new DateTime(2026, 8, 17), 8, 0)]));
    }

    [Fact]
    public void XmlWriterRejectsEmployeeCodeCollisions()
    {
        var movements = new[]
        {
            new DailyMovement("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 8, 17), 8, 0),
            new DailyMovement("Luigi Bianchi", "1234567", "Ferie", "01", new DateTime(2026, 8, 17), 8, 0)
        };

        Assert.Throws<InvalidDataException>(() => TrriPwXmlWriter.Create("AZ001", movements));
    }

    [Fact]
    public void XmlWriterOrdersRecordsDeterministically()
    {
        var first = new DailyMovement("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 8, 18), 4, 0);
        var second = first with { Date = new DateTime(2026, 8, 17) };

        Assert.Equal(
            TrriPwXmlWriter.Create("AZ001", [first, second]),
            TrriPwXmlWriter.Create("AZ001", [second, first]));
    }

    [Fact]
    public void XmlExportWritesUtf8WithoutBomAndReplacesTheDestinationAtomically()
    {
        var path = Path.Combine(Path.GetTempPath(), $"presenzeflow-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(path, "old content");
            var xml = TrriPwXmlWriter.Create(
                "AZ001",
                [new DailyMovement("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 8, 17), 8, 0)]);

            TrriPwXmlWriter.SaveFile(path, xml);

            var bytes = File.ReadAllBytes(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            Assert.Equal(xml, Encoding.UTF8.GetString(bytes));
            Assert.Equal("Fornitura", XDocument.Load(path).Root!.Name.LocalName);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LocalDatabasePersistsTrimmedMappingsAndChecksItsSchema()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"presenzeflow-db-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.db");
        try
        {
            var database = new LocalDatabase(path);
            database.SaveEmployeeMapping("  Mario Rossi  ", " 0012345 ");
            database.SavePolicyMapping(" Ferie ", " 01 ");

            Assert.Equal("0012345", Assert.Single(database.GetEmployeeMappings()).Value);
            Assert.Equal("01", Assert.Single(database.GetPolicyMappings()).Value);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string CreateWorkbookWithDuration(double value, string durationHeader, string policy = "Ferie", string? text = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"presenze-dur-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Richieste");
        sheet.Cell(1, 1).Value = "Nome dipendente";
        sheet.Cell(1, 2).Value = "Causale";
        sheet.Cell(1, 3).Value = "Data inizio";
        sheet.Cell(1, 4).Value = "Data fine";
        sheet.Cell(1, 5).Value = durationHeader;
        sheet.Cell(2, 1).Value = "Mario Rossi";
        sheet.Cell(2, 2).Value = policy;
        sheet.Cell(2, 3).Value = new DateTime(2026, 8, 17);
        sheet.Cell(2, 4).Value = new DateTime(2026, 8, 17);
        if (text is null)
        {
            sheet.Cell(2, 5).Value = value;
        }
        else
        {
            sheet.Cell(2, 5).Value = text;
        }
        workbook.SaveAs(path);
        return path;
    }

    [Theory]
    [InlineData("Giorni", 2, 16)]
    [InlineData("Durata (giorni)", 3, 24)]
    [InlineData("Days", 1, 8)]
    public void DayBasedDurationHeadersAreConvertedToHours(string header, double value, decimal expectedHours)
    {
        var path = CreateWorkbookWithDuration(value, header);
        try
        {
            var inspection = AttendanceWorkbookReader.Inspect(path);
            var request = Assert.Single(inspection.ReadRequests(new ColumnSelection(0, 1, 2, 3, 4)));

            Assert.Equal(expectedHours, request.Hours);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData("Ore totali", 2, 2)]
    [InlineData("Duration in Range", 7.5, 7.5)]
    public void HourBasedDurationHeadersAreNotRescaled(string header, double value, decimal expectedHours)
    {
        var path = CreateWorkbookWithDuration(value, header);
        try
        {
            var inspection = AttendanceWorkbookReader.Inspect(path);
            var request = Assert.Single(inspection.ReadRequests(new ColumnSelection(0, 1, 2, 3, 4)));

            Assert.Equal(expectedHours, request.Hours);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData("Malattia", 1, 8)]
    [InlineData("Malattia", 2, 16)]
    [InlineData("Malattia", 0.5, 4)]
    [InlineData("Malattia INPS", 3, 24)]
    [InlineData("Sick leave", 1, 8)]
    [InlineData("Illness", 1, 8)]
    [InlineData("Ferie", 1, 1)]
    [InlineData("Permesso", 2, 2)]
    public void IllnessIsReadInDaysEvenInAnHoursColumn(string policy, double value, decimal expectedHours)
    {
        var path = CreateWorkbookWithDuration(value, "Durata", policy);
        try
        {
            var inspection = AttendanceWorkbookReader.Inspect(path);
            var request = Assert.Single(inspection.ReadRequests(new ColumnSelection(0, 1, 2, 3, 4)));

            Assert.Equal(expectedHours, request.Hours);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IllnessDaysFollowTheStandardDayOfTheCompany()
    {
        var path = CreateWorkbookWithDuration(2, "Durata", "Malattia");
        try
        {
            var inspection = AttendanceWorkbookReader.Inspect(path);
            var request = Assert.Single(inspection.ReadRequests(new ColumnSelection(0, 1, 2, 3, 4), hoursPerDay: 7.5m));

            Assert.Equal(15m, request.Hours);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("16h", 16)]
    [InlineData("8 h", 8)]
    [InlineData("2 giorni", 16)]
    [InlineData("2", 16)]
    public void IllnessWrittenWithAnExplicitUnitKeepsThatUnit(string text, decimal expectedHours)
    {
        var path = CreateWorkbookWithDuration(0, "Durata", "Malattia", text);
        try
        {
            var inspection = AttendanceWorkbookReader.Inspect(path);
            var request = Assert.Single(inspection.ReadRequests(new ColumnSelection(0, 1, 2, 3, 4)));

            Assert.Equal(expectedHours, request.Hours);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Malattia", true)]
    [InlineData("  malattia figlio ", true)]
    [InlineData("Sick Leave", true)]
    [InlineData("Illness", true)]
    [InlineData("Ferie", false)]
    [InlineData("Permesso", false)]
    [InlineData("", false)]
    public void OnlyIllnessPoliciesAreRecognised(string policy, bool expected) =>
        Assert.Equal(expected, WorkbookInspection.IsIllnessPolicy(policy));

    [Theory]
    [InlineData("16h", true)]
    [InlineData("8 ore", true)]
    [InlineData("7.5 hours", true)]
    [InlineData("8h30", true)]
    [InlineData("2", false)]
    [InlineData("2 giorni", false)]
    [InlineData("", false)]
    public void AnExplicitHoursUnitIsRecognised(string text, bool expected) =>
        Assert.Equal(expected, WorkbookInspection.HasHoursMarker(text));

    [Theory]
    [InlineData(@"C:\x\presenze.xlsx", true)]
    [InlineData(@"C:\x\PRESENZE.XLSM", true)]
    [InlineData(@"C:\x\presenze.xls", false)]
    [InlineData(@"C:\x\presenze.csv", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void BothExcelFormatsAreAcceptedAndNothingElse(string? path, bool expected)
    {
        Assert.Equal(expected, WorkbookDrop.HasSupportedExtension(path));
        Assert.Equal(expected, WorkbookDrop.LooksLikeWorkbook(path));
    }

    [Fact]
    public void AMacroEnabledWorkbookIsReadLikeAnyOther()
    {
        var path = Path.Combine(Path.GetTempPath(), $"presenze-macro-{Guid.NewGuid():N}.xlsm");
        try
        {
            File.Copy(SampleWorkbook.Path, path);

            var inspection = AttendanceWorkbookReader.Inspect(path);

            Assert.Equal(SampleWorkbook.RequestCount, inspection.ReadRequests(inspection.SuggestedColumns!).Count);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
