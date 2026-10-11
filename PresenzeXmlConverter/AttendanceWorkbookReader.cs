using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace PresenzeXmlConverter;

internal sealed class WorkbookInspection
{
    private readonly IReadOnlyList<IReadOnlyList<WorkbookCell>> _rows;
    private readonly int _headerRow;
    private readonly int? _employeeCodeColumn;

    internal WorkbookInspection(
        string sheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<WorkbookCell>> rows,
        int headerRow,
        int? employeeCodeColumn,
        ColumnSelection? suggestedColumns,
        DateTime? periodStart,
        DateTime? periodEnd)
    {
        SheetName = sheetName;
        Headers = headers;
        _rows = rows;
        _headerRow = headerRow;
        _employeeCodeColumn = employeeCodeColumn;
        SuggestedColumns = suggestedColumns;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
    }

    public string SheetName { get; }
    public IReadOnlyList<string> Headers { get; }
    public ColumnSelection? SuggestedColumns { get; }
    public DateTime? PeriodStart { get; }
    public DateTime? PeriodEnd { get; }

    /// <summary>Ore di una giornata lavorativa standard, usate quando la durata è espressa in giorni.</summary>
    public const decimal DefaultHoursPerDay = 8m;

    public IReadOnlyList<AttendanceRequest> ReadRequests(ColumnSelection columns, decimal hoursPerDay = DefaultHoursPerDay)
    {
        ValidateColumns(columns);
        if (hoursPerDay <= 0m || hoursPerDay > 24m)
        {
            hoursPerDay = DefaultHoursPerDay;
        }
        // Alcuni export esprimono la durata in giorni ("Giorni", "Days", "gg"): in quel
        // caso va convertita in ore, altrimenti 2 giorni di ferie diventerebbe 2 ore.
        var durationInDays = IsDayBasedDurationHeader(
            columns.Duration >= 0 && columns.Duration < Headers.Count ? Headers[columns.Duration] : string.Empty);
        var requests = new List<AttendanceRequest>();
        foreach (var row in _rows.Where(row => row.Count > 0 && row[0].RowNumber > _headerRow))
        {
            var employee = GetCell(row, columns.Employee);
            var policy = GetCell(row, columns.Policy);
            var start = GetCell(row, columns.StartDate);
            var end = GetCell(row, columns.EndDate);
            var duration = GetCell(row, columns.Duration);
            var employeeCode = _employeeCodeColumn.HasValue
                ? GetCell(row, _employeeCodeColumn.Value).Text.Trim()
                : string.Empty;
            if (!new[] { employee, policy, start, end, duration }.Any(cell => cell.HasValue))
            {
                continue;
            }

            var rowNumber = row[0].RowNumber;
            var employeeName = employee.Text.Trim();
            var policyName = policy.Text.Trim();
            if (string.IsNullOrWhiteSpace(employeeName) ||
                string.IsNullOrWhiteSpace(policyName) ||
                employeeName.Length > 200 ||
                policyName.Length > 200 ||
                !start.TryGetDate(out var startDate) ||
                !end.TryGetDate(out var endDate) ||
                !duration.TryGetNumber(out var rawDuration))
            {
                throw new InvalidDataException(
                    $"La riga {rowNumber} contiene dati incompleti o non validi. Verifica nome, giustificativo, date e durata.");
            }
            var hours = durationInDays ? rawDuration * hoursPerDay : rawDuration;

            if (startDate.Date > endDate.Date)
            {
                throw new InvalidDataException($"La data iniziale è successiva alla data finale nella riga {rowNumber}.");
            }
            if (hours < 0)
            {
                throw new InvalidDataException($"La durata non può essere negativa (riga {rowNumber}).");
            }
            if (hours > 24 * 100)
            {
                throw new InvalidDataException($"La durata della riga {rowNumber} sembra irrealistica (oltre 100 giorni-ore).");
            }
            if (!string.IsNullOrWhiteSpace(employeeCode) && employeeCode.Trim().Length > 7)
            {
                throw new InvalidDataException(
                    $"Il codice PAGHE supera il limite di 7 caratteri (riga {rowNumber}).");
            }

            requests.Add(new AttendanceRequest(
                employeeName,
                policyName,
                startDate.Date,
                endDate.Date,
                hours,
                rowNumber,
                string.IsNullOrWhiteSpace(employeeCode) ? null : employeeCode));
        }

        if (requests.Count == 0)
        {
            throw new InvalidDataException("Nel foglio non sono state trovate richieste di assenza valide.");
        }
        return requests;
    }

    private void ValidateColumns(ColumnSelection columns)
    {
        var selected = new[]
        {
            columns.Employee,
            columns.Policy,
            columns.StartDate,
            columns.EndDate,
            columns.Duration
        };
        if (selected.Any(index => index < 0 || index >= Headers.Count) ||
            selected.Distinct().Count() != selected.Length)
        {
            throw new InvalidDataException("Seleziona una colonna distinta e valida per ogni campo richiesto.");
        }
    }

    /// <summary>
    /// Riconosce le intestazioni che esprimono la durata in giorni anziché in ore.
    /// </summary>
    private static bool IsDayBasedDurationHeader(string header)
    {
        var value = header.Trim().ToLowerInvariant();
        if (value.Length == 0)
        {
            return false;
        }
        var tokens = new string(value.Select(character => char.IsLetterOrDigit(character) ? character : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(token =>
            token.StartsWith("giorn", StringComparison.Ordinal) ||
            token.StartsWith("day", StringComparison.Ordinal) ||
            token is "gg" or "g");
    }

    private static WorkbookCell GetCell(IReadOnlyList<WorkbookCell> row, int column)
    {
        return column < row.Count ? row[column] : WorkbookCell.Empty(row[0].RowNumber);
    }
}

internal sealed record WorkbookCell(
    int RowNumber,
    string Text,
    DateTime? DateValue,
    decimal? NumberValue)
{
    private static readonly Regex HourMinuteText = new(@"^(\d{1,3})\s*[hH]\s*(\d{1,2})?$", RegexOptions.Compiled);
    private static readonly Regex ClockText = new(@"^(\d{1,3})\s*:\s*(\d{1,2})(?:\s*:\s*\d{1,2})?$", RegexOptions.Compiled);
    private static readonly Regex TrailingHoursText = new(@"^(\d{1,3}(?:[.,]\d{1,2})?)\s*[hH]$", RegexOptions.Compiled);

    public bool HasValue => !string.IsNullOrWhiteSpace(Text) || DateValue.HasValue || NumberValue.HasValue;

    public static WorkbookCell Empty(int rowNumber) => new(rowNumber, string.Empty, null, null);

    public bool TryGetDate(out DateTime date)
    {
        if (DateValue.HasValue)
        {
            date = DateValue.Value;
            return true;
        }
        return DateTime.TryParse(Text, CultureInfo.GetCultureInfo("it-IT"), DateTimeStyles.AllowWhiteSpaces, out date) ||
               DateTime.TryParse(Text, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.AllowWhiteSpaces, out date);
    }

    public bool TryGetNumber(out decimal number)
    {
        if (NumberValue.HasValue)
        {
            number = NumberValue.Value;
            return true;
        }

        var value = Text.Trim().Replace(" ", string.Empty).Replace("\u00a0", string.Empty);
        if (value.Length == 0)
        {
            number = 0m;
            return false;
        }

        // Forme testuali di durata: "8h30", "8h", "8,5h", "8:30".
        var hourMinute = HourMinuteText.Match(value);
        if (hourMinute.Success)
        {
            var hoursPart = decimal.Parse(hourMinute.Groups[1].Value, CultureInfo.InvariantCulture);
            var minutesPart = hourMinute.Groups[2].Success
                ? decimal.Parse(hourMinute.Groups[2].Value, CultureInfo.InvariantCulture)
                : 0m;
            number = hoursPart + minutesPart / 60m;
            return true;
        }
        var clock = ClockText.Match(value);
        if (clock.Success)
        {
            number = decimal.Parse(clock.Groups[1].Value, CultureInfo.InvariantCulture) +
                     decimal.Parse(clock.Groups[2].Value, CultureInfo.InvariantCulture) / 60m;
            return true;
        }
        var trailingHours = TrailingHoursText.Match(value);
        if (trailingHours.Success)
        {
            return TryParseDecimal(trailingHours.Groups[1].Value, out number);
        }

        return TryParseDecimal(value, out number);
    }

    private static bool TryParseDecimal(string value, out decimal number)
    {
        var comma = value.LastIndexOf(',');
        var dot = value.LastIndexOf('.');
        if (comma >= 0 && dot >= 0)
        {
            var decimalSeparator = comma > dot ? ',' : '.';
            var groupSeparator = decimalSeparator == ',' ? '.' : ',';
            value = value.Replace(groupSeparator.ToString(), string.Empty)
                .Replace(decimalSeparator, '.');
        }
        else
        {
            value = value.Replace(',', '.');
        }

        return decimal.TryParse(
            value,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out number);
    }
}

internal static class AttendanceWorkbookReader
{
    private const long MaximumWorkbookBytes = 50 * 1024 * 1024;
    private const long MaximumExpandedWorkbookBytes = 150 * 1024 * 1024;
    private const long MaximumArchiveEntryBytes = 100 * 1024 * 1024;
    private const int MaximumArchiveEntries = 1000;
    private const int MaximumWorksheetRows = 100_000;
    private const int MaximumWorksheetColumns = 256;
    private const long MaximumWorksheetCells = 2_000_000;

    private static readonly string[][] FieldAliases =
    [
        ["display name", "employee name", "employee display name", "employee full name", "full name", "resource name", "nome completo", "nome dipendente", "nome del dipendente", "nominativo", "collaboratore", "dipendente", "employee", "worker", "person", "persona"],
        ["policy", "policy name", "leave policy", "absence policy", "causale", "giustificativo", "tipo assenza", "motivo assenza", "leave type", "absence type", "reason", "policy type", "tipo richiesta"],
        ["start date", "start", "date start", "data inizio", "data di inizio", "from date", "date from", "inizio", "dal", "data richiesta"],
        ["end date", "end", "date end", "data fine", "data di fine", "to date", "date to", "fine", "al", "data conclusione"],
        ["duration in range", "duration in period", "durata nel periodo", "durata nel range", "ore nel periodo", "total duration", "durata totale", "duration", "durata", "hours", "ore", "ore totali", "totale ore"]
    ];
    private static readonly string[] EmployeeCodeAliases =
    [
        "employee code",
        "employee id",
        "payroll code",
        "payroll id",
        "codice dipendente ufficiale",
        "cod dipendente ufficiale",
        "coddipendenteufficiale",
        "codice paghe ufficiale",
        "codice paghe",
        "id paghe",
        "codice",
        "matricola"
    ];

    // Etichette che, in una riga di intestazione fuzzy, indicano inequivocabilmente un codice:
    // non devono mai essere associate al nome dipendente, alle date o alla durata.
    private static readonly string[] CodeMarkers = ["codice", "code", "cod", "matricola", "id"];

    public static WorkbookInspection Inspect(string path)
    {
        ValidateWorkbookArchive(path);
        using var workbook = new XLWorkbook(path);
        if (workbook.Worksheets.Count == 0)
        {
            throw new InvalidDataException("Il file Excel non contiene fogli.");
        }

        // Scegli il foglio con la riconoscibilità migliore: il primo con intestazioni
        // complete vince subito, altrimenti resta il punteggio più alto.
        IXLWorksheet? bestSheet = null;
        ColumnSelection? bestSelection = null;
        int bestScore = -1;
        int bestHeaderRow = -1;
        foreach (var sheet in workbook.Worksheets.Where(sheet =>
                     sheet.Visibility != XLWorksheetVisibility.VeryHidden))
        {
            var usedRange = sheet.RangeUsed();
            if (usedRange is null)
            {
                continue;
            }
            var firstRow = usedRange.RangeAddress.FirstAddress.RowNumber;
            var lastRow = usedRange.RangeAddress.LastAddress.RowNumber;
            var firstColumn = usedRange.RangeAddress.FirstAddress.ColumnNumber;
            var lastColumn = usedRange.RangeAddress.LastAddress.ColumnNumber;
            var headerRow = FindHeaderRow(sheet, firstRow, lastRow, firstColumn, lastColumn, out var selection, out var score);
            if (score > bestScore)
            {
                bestScore = score;
                bestSheet = sheet;
                bestSelection = selection;
                bestHeaderRow = headerRow;
                if (score >= FieldAliases.Length)
                {
                    break;
                }
            }
        }

        var worksheet = bestSheet
            ?? throw new InvalidDataException("Il file Excel non contiene fogli leggibili.");

        var used = worksheet.RangeUsed()
            ?? throw new InvalidDataException("Il foglio Excel è vuoto.");

        var firstRowNumber = used.RangeAddress.FirstAddress.RowNumber;
        var lastRowNumber = used.RangeAddress.LastAddress.RowNumber;
        var firstColumnNumber = used.RangeAddress.FirstAddress.ColumnNumber;
        var lastColumnNumber = used.RangeAddress.LastAddress.ColumnNumber;
        var rowCount = lastRowNumber - firstRowNumber + 1;
        var columnCount = lastColumnNumber - firstColumnNumber + 1;
        if (rowCount > MaximumWorksheetRows ||
            columnCount > MaximumWorksheetColumns ||
            (long)rowCount * columnCount > MaximumWorksheetCells)
        {
            throw new InvalidDataException(
                $"Il foglio supera i limiti supportati di {MaximumWorksheetRows:N0} righe, " +
                $"{MaximumWorksheetColumns} colonne o {MaximumWorksheetCells:N0} celle utilizzate.");
        }
        var headerRowNumber = bestHeaderRow >= firstRowNumber ? bestHeaderRow : firstRowNumber;
        var headers = Enumerable.Range(firstColumnNumber, columnCount)
            .Select(column => worksheet.Cell(headerRowNumber, column).GetString().Trim())
            .ToArray();
        if (headers.Any(header => header.Length > 256))
        {
            throw new InvalidDataException("Una o più intestazioni Excel superano il limite di 256 caratteri.");
        }
        var employeeCodeColumn = FindColumn(headers, EmployeeCodeAliases);

        var rows = new List<IReadOnlyList<WorkbookCell>>(Math.Max(0, lastRowNumber - headerRowNumber) + 1);
        for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRowNumber; rowNumber++)
        {
            var cells = new List<WorkbookCell>(columnCount);
            for (var column = firstColumnNumber; column <= lastColumnNumber; column++)
            {
                cells.Add(ReadCell(worksheet.Cell(rowNumber, column), rowNumber));
            }
            rows.Add(cells);
        }

        var (periodStart, periodEnd) = ReadPeriod(worksheet, firstRowNumber, Math.Min(headerRowNumber - 1, firstRowNumber + 10), firstColumnNumber, lastColumnNumber);
        return new WorkbookInspection(
            worksheet.Name,
            headers,
            rows,
            headerRowNumber,
            employeeCodeColumn >= 0 ? employeeCodeColumn : null,
            bestSelection,
            periodStart,
            periodEnd);
    }

    public static ColumnSelection? SuggestColumns(IReadOnlyList<string> headers)
    {
        var indexes = new int[FieldAliases.Length];
        for (var field = 0; field < FieldAliases.Length; field++)
        {
            indexes[field] = FindColumn(headers, FieldAliases[field], rejectCodeHeaders: field is 0 or 4);
            if (indexes[field] < 0)
            {
                return null;
            }
        }
        if (indexes.Distinct().Count() != indexes.Length)
        {
            return null;
        }

        return new ColumnSelection(indexes[0], indexes[1], indexes[2], indexes[3], indexes[4]);
    }

    public static ColumnSelection CreateSelection(IReadOnlyDictionary<string, int> selection) =>
        new(selection["employee"], selection["policy"], selection["startDate"], selection["endDate"], selection["duration"]);

    private static int FindHeaderRow(
        IXLWorksheet worksheet,
        int firstRow,
        int lastRow,
        int firstColumn,
        int lastColumn,
        out ColumnSelection? selection,
        out int score)
    {
        var bestRow = firstRow;
        var bestScore = -1;
        ColumnSelection? bestSelection = null;
        var fallbackRow = firstRow;
        var fallbackFilledCells = -1;
        var finalCandidate = Math.Min(lastRow, firstRow + 24);
        for (var row = firstRow; row <= finalCandidate; row++)
        {
            var headers = Enumerable.Range(firstColumn, lastColumn - firstColumn + 1)
                .Select(column => worksheet.Cell(row, column).GetString().Trim())
                .ToArray();
            var filledCells = headers.Count(header => !string.IsNullOrWhiteSpace(header));
            if (filledCells > fallbackFilledCells)
            {
                fallbackFilledCells = filledCells;
                fallbackRow = row;
            }
            var indexes = SuggestColumns(headers) is { } complete
                ? new[] { complete.Employee, complete.Policy, complete.StartDate, complete.EndDate, complete.Duration }
                : Enumerable.Range(0, FieldAliases.Length)
                    .Select(field => FindColumn(headers, FieldAliases[field], rejectCodeHeaders: field is 0 or 4))
                    .ToArray();
            var rowScore = indexes.Count(index => index >= 0);
            if (rowScore > bestScore)
            {
                bestScore = rowScore;
                bestRow = row;
                if (indexes.All(index => index >= 0) && indexes.Distinct().Count() == indexes.Length)
                {
                    bestSelection = new ColumnSelection(indexes[0], indexes[1], indexes[2], indexes[3], indexes[4]);
                }
                if (rowScore == FieldAliases.Length)
                {
                    break;
                }
            }
        }

        selection = bestSelection;
        score = bestScore < 0 ? 0 : bestScore;
        if (bestScore < 3)
        {
            bestRow = fallbackRow;
            selection = null;
        }
        return bestRow;
    }

    private static int FindColumn(IReadOnlyList<string> headers, IReadOnlyList<string> aliases, bool rejectCodeHeaders = false)
    {
        var normalizedHeaders = headers.Select(Normalize).ToArray();
        var normalizedAliases = aliases.Select(Normalize).ToArray();
        var bestIndex = -1;
        var bestScore = 0;
        for (var headerIndex = 0; headerIndex < normalizedHeaders.Length; headerIndex++)
        {
            var header = normalizedHeaders[headerIndex];
            if (string.IsNullOrEmpty(header))
            {
                continue;
            }
            var isCodeHeader = rejectCodeHeaders && ContainsAnyToken(header, CodeMarkers);
            foreach (var alias in normalizedAliases)
            {
                var aliasScore = ScoreHeader(header, alias);
                if (aliasScore > bestScore && (!isCodeHeader || aliasScore >= 1000))
                {
                    bestScore = aliasScore;
                    bestIndex = headerIndex;
                }
            }
        }
        return bestScore > 0 ? bestIndex : -1;
    }

    private static int ScoreHeader(string header, string alias)
    {
        if (header == alias)
        {
            // Corrispondenza esatta: sempre la più affidabile.
            return 1000 + alias.Length;
        }
        if (alias.Length < 3)
        {
            // Le alias molto corte ("al", "dal") matchano solo esattamente.
            return 0;
        }
        var headerTokens = Tokenize(header);
        var aliasTokens = Tokenize(alias);
        if (aliasTokens.Length == 0 || headerTokens.Length == 0)
        {
            return 0;
        }
        if (ContainsTokenSequence(headerTokens, aliasTokens))
        {
            return 600 + alias.Length;
        }
        if (aliasTokens.Length >= 2 && aliasTokens.All(headerTokens.Contains))
        {
            return 500 + alias.Length;
        }
        if (aliasTokens.Length == 1 && headerTokens.Length >= 1 &&
            headerTokens.Any(token => token.Length >= 4 && token.StartsWith(aliasTokens[0], StringComparison.Ordinal)))
        {
            return 450 + alias.Length;
        }
        if (headerTokens.Length >= 1 && headerTokens.All(aliasTokens.Contains))
        {
            return 400 + headerTokens.Length;
        }
        return 0;
    }

    private static bool ContainsAnyToken(string header, IReadOnlyList<string> markers) =>
        markers.Any(marker => Tokenize(header).Contains(marker));

    private static bool ContainsTokenSequence(string[] haystack, string[] needle)
    {
        for (var start = 0; start <= haystack.Length - needle.Length; start++)
        {
            var matches = true;
            for (var offset = 0; offset < needle.Length; offset++)
            {
                if (!string.Equals(haystack[start + offset], needle[offset], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }
            if (matches)
            {
                return true;
            }
        }
        return false;
    }

    private static string[] Tokenize(string value) =>
        value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static string Normalize(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var decomposed = normalized.Normalize(System.Text.NormalizationForm.FormD);
        var withoutDiacritics = string.Concat(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark));
        return string.Join(
            " ",
            withoutDiacritics
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Replace('/', ' ')
                .Replace('(', ' ')
                .Replace(')', ' ')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void ValidateWorkbookArchive(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Seleziona un file Excel in formato .xlsx.");
        }

        var file = new FileInfo(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("Il file Excel selezionato non esiste.", path);
        }
        if (file.Length is 0 or > MaximumWorkbookBytes)
        {
            throw new InvalidDataException("Il file Excel è vuoto o supera il limite di 50 MB.");
        }

        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count > MaximumArchiveEntries)
        {
            throw new InvalidDataException("Il file Excel contiene un numero eccessivo di componenti.");
        }

        long expandedSize = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > MaximumArchiveEntryBytes ||
                (entry.CompressedLength == 0 && entry.Length > 0) ||
                (entry.CompressedLength > 0 && entry.Length / (double)entry.CompressedLength > 1000))
            {
                throw new InvalidDataException("Il file Excel contiene un componente compresso oltre i limiti consentiti.");
            }

            expandedSize = checked(expandedSize + entry.Length);
            if (expandedSize > MaximumExpandedWorkbookBytes)
            {
                throw new InvalidDataException("Il contenuto espanso del file Excel supera il limite di 150 MB.");
            }
        }
    }

    private static WorkbookCell ReadCell(IXLCell cell, int rowNumber)
    {
        if (cell.DataType == XLDataType.Blank)
        {
            return WorkbookCell.Empty(rowNumber);
        }

        if (cell.DataType == XLDataType.DateTime)
        {
            var date = cell.GetDateTime();
            return new WorkbookCell(rowNumber, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), date, null);
        }
        if (cell.DataType == XLDataType.Number)
        {
            var raw = cell.GetDouble();
            var number = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
            // Celle con formato orario ("hh:mm"): il valore è una frazione di giorno.
            // Solo sotto 1 (meno di 24h) il significato "durata" è univoco; altrimenti
            // si interpreta come numero di ore già espresso.
            if (raw > 0 && raw < 1 && IsTimeLikeNumberFormat(cell.Style.NumberFormat.Format))
            {
                number = Math.Round(Convert.ToDecimal(raw * 24d), 4, MidpointRounding.AwayFromZero);
            }
            return new WorkbookCell(rowNumber, number.ToString(CultureInfo.InvariantCulture), null, number);
        }

        return new WorkbookCell(rowNumber, cell.GetString().Trim(), null, null);
    }

    private static bool IsTimeLikeNumberFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return false;
        }
        // I formati ora contengono "h" o ":"; i formati data contengono "y" o "/".
        return (format.Contains('h', StringComparison.OrdinalIgnoreCase) || format.Contains(':')) &&
               !format.Contains('y', StringComparison.OrdinalIgnoreCase);
    }

    private static (DateTime? Start, DateTime? End) ReadPeriod(
        IXLWorksheet worksheet,
        int firstRow,
        int lastRow,
        int firstColumn,
        int lastColumn)
    {
        DateTime? start = null;
        DateTime? end = null;
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = firstColumn; column < lastColumn; column++)
            {
                var label = Normalize(worksheet.Cell(row, column).GetString());
                var value = ReadCell(worksheet.Cell(row, column + 1), row);
                if (label is "from date" or "data da" or "dal")
                {
                    if (value.TryGetDate(out var date))
                    {
                        start = date.Date;
                    }
                }
                else if (label is "to date" or "data al" or "al")
                {
                    if (value.TryGetDate(out var date))
                    {
                        end = date.Date;
                    }
                }
            }
        }

        if (start.HasValue && end.HasValue && start.Value > end.Value)
        {
            throw new InvalidDataException("L’intervallo di date indicato nel foglio Excel non è valido.");
        }
        return (start, end);
    }
}
