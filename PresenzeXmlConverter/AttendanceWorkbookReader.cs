using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace PresenzeXmlConverter;

/// <summary>
/// Esito della lettura delle richieste: le righe valide e l'elenco dei problemi
/// trovati. I problemi non interrompono l'importazione (l'utente li vede e
/// decide), ma nessuna riga viene mai persa in silenzio.
/// </summary>
internal sealed record WorkbookReadResult(
    IReadOnlyList<AttendanceRequest> Requests,
    IReadOnlyList<string> Problems)
{
    public int SkippedRows => Problems.Count;
}

internal sealed class WorkbookInspection
{
    private readonly IReadOnlyList<IReadOnlyList<WorkbookCell>> _rows;
    private readonly int _headerRow;
    private readonly int? _employeeCodeColumn;
    private readonly int _headerRowNumber;
    private readonly int _lastRowNumber;

    internal WorkbookInspection(
        string sheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<WorkbookCell>> rows,
        int headerRow,
        int? employeeCodeColumn,
        ColumnSelection? suggestedColumns,
        DateTime? periodStart,
        DateTime? periodEnd,
        int headerRowNumber = 0,
        int lastRowNumber = 0,
        IReadOnlyList<string>? sheetNames = null)
    {
        SheetNames = sheetNames ?? Array.Empty<string>();
        SheetName = sheetName;
        Headers = headers;
        _rows = rows;
        _headerRow = headerRow;
        _employeeCodeColumn = employeeCodeColumn;
        SuggestedColumns = suggestedColumns;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        _headerRowNumber = headerRowNumber;
        _lastRowNumber = lastRowNumber == 0 ? rows.Count + headerRow : lastRowNumber;
    }

    public string SheetName { get; }

    /// <summary>
    /// Fogli visibili del file, letti mentre il file era già aperto: l'interfaccia non
    /// deve riaprirlo (e rileggerlo da capo) solo per conoscere i nomi dei fogli.
    /// </summary>
    public IReadOnlyList<string> SheetNames { get; }

    public IReadOnlyList<string> Headers { get; }
    public ColumnSelection? SuggestedColumns { get; }
    public DateTime? PeriodStart { get; }
    public DateTime? PeriodEnd { get; }

    /// <summary>Ore di una giornata lavorativa standard, usate quando la durata è espressa in giorni.</summary>
    public const decimal DefaultHoursPerDay = 8m;

    /// <summary>Prima riga di dati del foglio (dopo l'intestazione).</summary>
    public int FirstDataRowNumber => _headerRowNumber + 1;

    /// <summary>Ultima riga usata del foglio.</summary>
    public int LastRowNumber => _lastRowNumber;

    /// <summary>
    /// Legge le richieste valide. Le righe incomplete o incoerenti vengono
    /// raccolte in <see cref="WorkbookReadResult.Problems"/> invece di far
    /// fallire l'intera importazione sulla prima riga sbagliata.
    /// </summary>
    public WorkbookReadResult Read(ColumnSelection columns, decimal hoursPerDay = DefaultHoursPerDay)
    {
        ValidateColumns(columns);
        if (hoursPerDay <= 0m || hoursPerDay > 24m)
        {
            hoursPerDay = DefaultHoursPerDay;
        }

        var requests = new List<AttendanceRequest>();
        var problems = new List<string>();
        // Alcuni export esprimono la durata in giorni ("Giorni", "Days", "gg"):
        // in quel caso va convertita in ore, altrimenti 2 giorni di ferie
        // diventerebbe 2 ore.
        var headerIsDayBased = columns.Duration >= 0 && columns.Duration < Headers.Count &&
                               IsDayBasedDurationHeader(Headers[columns.Duration]);
        foreach (var row in _rows.Where(row => row.Count > 0 && row[0].RowNumber > _headerRow))
        {
            var employee = GetCell(row, columns.Employee);
            var policy = GetCell(row, columns.Policy);
            var start = GetCell(row, columns.StartDate);
            var end = GetCell(row, columns.EndDate);
            var duration = GetCell(row, columns.Duration);
            var rowNumber = row[0].RowNumber;

            // Righe senza alcun contenuto utile (separatori, note di coda): ignorate.
            if (!new[] { employee, policy, start, end, duration }.Any(cell => cell.HasValue))
            {
                continue;
            }

            var employeeCode = _employeeCodeColumn.HasValue
                ? GetCell(row, _employeeCodeColumn.Value).Text.Trim()
                : string.Empty;
            var employeeName = NormalizeName(employee.Text);
            var policyName = NormalizeName(policy.Text);

            if (string.IsNullOrWhiteSpace(employeeName) || string.IsNullOrWhiteSpace(policyName))
            {
                problems.Add($"Riga {rowNumber}: manca il nominativo o il giustificativo.");
                continue;
            }
            if (employeeName.Length > 200 || policyName.Length > 200)
            {
                problems.Add($"Riga {rowNumber}: nominativo o giustificativo troppo lungo.");
                continue;
            }
            if (!start.TryGetDate(out var startDate) || !end.TryGetDate(out var endDate))
            {
                problems.Add($"Riga {rowNumber}: data di inizio o fine non riconosciuta.");
                continue;
            }
            if (!duration.TryGetDuration(out var rawDuration, out var cellIsDayBased))
            {
                problems.Add($"Riga {rowNumber}: durata non riconosciuta.");
                continue;
            }

            var hours = headerIsDayBased || cellIsDayBased ? rawDuration * hoursPerDay : rawDuration;
            if (startDate.Date > endDate.Date)
            {
                problems.Add($"Riga {rowNumber}: la data iniziale è successiva a quella finale.");
                continue;
            }
            if (hours < 0)
            {
                problems.Add($"Riga {rowNumber}: la durata è negativa.");
                continue;
            }
            if (hours > 24 * 100)
            {
                problems.Add($"Riga {rowNumber}: la durata supera il limite ragionevole (100 giornate).");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(employeeCode) && employeeCode.Trim().Length > 7)
            {
                problems.Add($"Riga {rowNumber}: il codice PAGHE supera i 7 caratteri.");
                continue;
            }

            requests.Add(new AttendanceRequest(
                employeeName,
                policyName,
                startDate.Date,
                endDate.Date,
                hours,
                rowNumber,
                string.IsNullOrWhiteSpace(employeeCode) ? null : employeeCode.Trim()));
        }

        if (requests.Count == 0)
        {
            var detail = problems.Count == 0
                ? string.Empty
                : $" Primo problema: {problems[0]}";
            throw new InvalidDataException(
                "Nel foglio non sono state trovate richieste di assenza valide." + detail);
        }
        return new WorkbookReadResult(requests, problems);
    }

    /// <summary>
    /// Richieste valide. Le righe problematiche vengono scartate: usare
    /// <see cref="Read"/> per conoscerle.
    /// </summary>
    public IReadOnlyList<AttendanceRequest> ReadRequests(ColumnSelection columns, decimal hoursPerDay = DefaultHoursPerDay) =>
        Read(columns, hoursPerDay).Requests;

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
        if (selected.Any(index => index < 0 || index >= Headers.Count))
        {
            throw new InvalidDataException("Seleziona una colonna valida per ogni campo richiesto.");
        }
        // Data iniziale e finale possono coincidere (fogli con una sola data);
        // tutti gli altri campi devono restare distinti.
        if (columns.Employee == columns.Policy ||
            columns.Employee == columns.StartDate ||
            columns.Employee == columns.EndDate ||
            columns.Employee == columns.Duration ||
            columns.Policy == columns.StartDate ||
            columns.Policy == columns.EndDate ||
            columns.Policy == columns.Duration ||
            columns.StartDate == columns.Duration ||
            columns.EndDate == columns.Duration)
        {
            throw new InvalidDataException("Seleziona una colonna distinta e valida per ogni campo richiesto.");
        }
    }

    /// <summary>Spazi multipli e spazi ai bordi non devono creare due dipendenti diversi.</summary>
    internal static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        return string.Join(
            " ",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Riconosce le intestazioni che esprimono la durata in giorni anziché in ore.
    /// </summary>
    internal static bool IsDayBasedDurationHeader(string header)
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

internal static class AttendanceWorkbookReader
{
    private const long MaximumWorkbookBytes = 50 * 1024 * 1024;
    private const long MaximumExpandedWorkbookBytes = 150 * 1024 * 1024;
    private const long MaximumArchiveEntryBytes = 100 * 1024 * 1024;
    private const int MaximumArchiveEntries = 1000;
    private const int MaximumWorksheetRows = 100_000;
    private const int MaximumWorksheetColumns = 256;
    private const long MaximumWorksheetCells = 2_000_000;

    /// <summary>
    /// Intestazioni riconosciute per campo, dalla più specifica alla più
    /// generica: l'ordine è anche il criterio di spareggio fra colonne ambigue.
    /// </summary>
    private static readonly string[][] FieldAliases =
    [
        [
            "employee display name", "employee full name", "employee name", "display name", "resource name",
            "nome e cognome", "nome completo", "nome dipendente", "nome del dipendente", "nominativo dipendente",
            "cognome e nome", "nome collaboratore", "nominativo", "collaboratore", "dipendente", "risorsa",
            "employee", "worker", "person", "persona"
        ],
        [
            "policy name", "leave policy", "absence policy", "policy",
            "leave type", "absence type",
            "tipo assenza", "tipo di assenza", "tipo richiesta", "tipo di richiesta", "motivo assenza",
            "causale assenza", "descrizione assenza", "giustificativo", "causale", "tipologia", "voce",
            "assenza", "reason",
            // «Policy type» è la categoria generica («Vacation (hours)»), mentre «Policy» è la
            // policy vera («IT vacation (hours)»), quella a cui si abbina il giustificativo:
            // quando il foglio ha entrambe vince «Policy», quindi «policy type» sta in fondo.
            "policy type"
        ],
        [
            "data di inizio", "data iniziale", "data inizio", "inizio assenza", "data dalla", "data da",
            "from date", "date from", "start date", "data richiesta", "data presenza", "data assenza",
            "inizio", "dal", "start"
        ],
        [
            "data di fine", "data finale", "data fine", "fine assenza", "data alla", "data al",
            "to date", "date to", "end date", "data conclusione", "fine", "al", "end"
        ],
        [
            "duration in range", "duration in period", "durata nel periodo", "durata nel range",
            "ore nel periodo", "ore totali", "totale ore", "total duration", "durata totale",
            "quantita ore", "numero ore", "n ore", "ore assenza", "ore richieste", "durata ore",
            "duration", "durata", "hours", "ore", "h"
        ]
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
        "codice dipendente",
        "cod dipendente",
        "matricola dipendente",
        "codice",
        "matricola"
    ];

    // Etichette che, in una riga di intestazione fuzzy, indicano inequivocabilmente un codice:
    // non devono mai essere associate al nome dipendente, alle date o alla durata.
    private static readonly string[] CodeMarkers = ["codice", "code", "cod", "matricola", "id"];

    // Nomi di foglio che suggeriscono il contenuto giusto (o decisamente sbagliato):
    // servono a scegliere il foglio da analizzare quando ce ne sono più di uno.
    private static readonly string[] PreferredSheetMarkers =
        ["presen", "assenze", "timbrature", "ferie", "permessi", "richieste", "dipendenti", "attendance", "leave"];

    private static readonly string[] DiscouragedSheetMarkers =
        ["istruzion", "legenda", "readme", "note", "config", "parametr", "help", "template"];

    private static readonly Regex PeriodRange = new(
        @"(?<start>\d{1,2}[/.-]\d{1,2}[/.-]\d{2,4}|\d{4}-\d{2}-\d{2})\s*(?:-|>|a|to|fino a)\s*(?<end>\d{1,2}[/.-]\d{1,2}[/.-]\d{2,4}|\d{4}-\d{2}-\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Nomi dei fogli visibili, nell'ordine del file: serve al programma per
    /// proporre un foglio diverso quando quello riconosciuto automaticamente non
    /// è quello giusto.
    /// </summary>
    public static IReadOnlyList<string> ListSheetNames(string path)
    {
        ValidateWorkbookArchive(path);
        using var workbook = new XLWorkbook(path);
        return workbook.Worksheets
            .Where(sheet => sheet.Visibility != XLWorksheetVisibility.VeryHidden)
            .Select(sheet => sheet.Name)
            .ToArray();
    }

    /// <summary>
    /// Legge il foglio migliore del file. Con <paramref name="sheetName"/> il
    /// foglio viene usato così com'è, senza riconoscimento automatico.
    /// </summary>
    public static WorkbookInspection Inspect(string path) => Inspect(path, null);

    public static WorkbookInspection Inspect(string path, string? sheetName)
    {
        ValidateWorkbookArchive(path);
        using var workbook = new XLWorkbook(path);
        if (workbook.Worksheets.Count == 0)
        {
            throw new InvalidDataException("Il file Excel non contiene fogli.");
        }

        // Scegli il foglio con la riconoscibilità migliore: intestazioni complete
        // vincono subito, il nome del foglio fa da spareggio.
        IXLWorksheet? bestSheet = null;
        ColumnSelection? bestSelection = null;
        int bestScore = int.MinValue;
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
            if (lastRow - firstRow + 1 < 2)
            {
                continue;
            }
            var headerRow = FindHeaderRow(sheet, firstRow, lastRow, firstColumn, lastColumn, out var selection, out var score);
            if (!string.IsNullOrWhiteSpace(sheetName) &&
                string.Equals(sheet.Name, sheetName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                // Foglio scelto a mano: si usa questo, ma si legge comunque
                // l'intestazione per riproporre l'abbinamento delle colonne.
                bestSheet = sheet;
                bestSelection = selection;
                bestHeaderRow = headerRow;
                break;
            }
            var total = score * 10 + SheetNameBonus(sheet.Name);
            if (total > bestScore)
            {
                bestScore = total;
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

        var (periodStart, periodEnd) = ReadPeriod(
            worksheet,
            firstRowNumber,
            Math.Min(headerRowNumber - 1, firstRowNumber + 10),
            firstColumnNumber,
            lastColumnNumber);
        return new WorkbookInspection(
            worksheet.Name,
            headers,
            rows,
            headerRowNumber,
            employeeCodeColumn >= 0 ? employeeCodeColumn : null,
            bestSelection,
            periodStart,
            periodEnd,
            headerRowNumber,
            lastRowNumber,
            workbook.Worksheets
                .Where(sheet => sheet.Visibility != XLWorksheetVisibility.VeryHidden)
                .Select(sheet => sheet.Name)
                .ToArray());
    }

    /// <summary>
    /// Mappa le intestazioni sui campi richiesti. Se il foglio ha una sola
    /// colonna data, questa viene usata sia come inizio sia come fine (molti
    /// export indicano un singolo giorno per richiesta).
    /// </summary>
    public static ColumnSelection? SuggestColumns(IReadOnlyList<string> headers)
    {
        var indexes = MapFields(headers);
        if (!IsUsableSelection(indexes))
        {
            return null;
        }
        return new ColumnSelection(indexes[0], indexes[1], indexes[2], indexes[3], indexes[4]);
    }

    private static int[] MapFields(IReadOnlyList<string> headers)
    {
        var indexes = new int[FieldAliases.Length];
        for (var field = 0; field < FieldAliases.Length; field++)
        {
            indexes[field] = FindColumn(headers, FieldAliases[field], rejectCodeHeaders: field is 0 or 4);
        }
        // Una sola colonna data vale per inizio e fine.
        if (indexes[3] < 0 && indexes[2] >= 0)
        {
            indexes[3] = indexes[2];
        }
        if (indexes[2] < 0 && indexes[3] >= 0)
        {
            indexes[2] = indexes[3];
        }
        return indexes;
    }

    /// <summary>
    /// Tutti i campi sono presenti e distinti; l'unica ripetizione ammessa è
    /// quella tra data iniziale e finale (foglio con una sola data).
    /// </summary>
    private static bool IsUsableSelection(IReadOnlyList<int> indexes)
    {
        if (indexes.Any(index => index < 0))
        {
            return false;
        }
        for (var first = 0; first < indexes.Count; first++)
        {
            for (var second = first + 1; second < indexes.Count; second++)
            {
                if (indexes[first] != indexes[second])
                {
                    continue;
                }
                if (first == 2 && second == 3)
                {
                    continue;
                }
                return false;
            }
        }
        return true;
    }

    public static ColumnSelection CreateSelection(IReadOnlyDictionary<string, int> selection) =>
        new(selection["employee"], selection["policy"], selection["startDate"], selection["endDate"], selection["duration"]);

    private static int SheetNameBonus(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        if (DiscouragedSheetMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal)))
        {
            return -8;
        }
        return PreferredSheetMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal)) ? 4 : 0;
    }

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
            var indexes = MapFields(headers);
            var rowScore = indexes.Count(index => index >= 0);
            if (rowScore > bestScore)
            {
                bestScore = rowScore;
                bestRow = row;
                if (IsUsableSelection(indexes))
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
            for (var aliasIndex = 0; aliasIndex < normalizedAliases.Length; aliasIndex++)
            {
                var alias = normalizedAliases[aliasIndex];
                var aliasScore = ScoreHeader(header, alias);
                if (aliasScore > 0)
                {
                    // A parità di corrispondenza vince l'alias più specifico,
                    // cioè quello elencato prima nella lista del campo.
                    aliasScore += Math.Max(0, 40 - aliasIndex);
                }
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
        // Intestazioni abbreviate dal foglio (es. "ore ass." per l'alias "ore assenza").
        if (headerTokens.All(token => token.Length >= 3 &&
                                      aliasTokens.Any(aliasToken => aliasToken.StartsWith(token, StringComparison.Ordinal))))
        {
            return 430 + headerTokens.Length;
        }
        if (headerTokens.All(aliasTokens.Contains))
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
                .Replace('.', ' ')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void ValidateWorkbookArchive(string path)
    {
        if (!WorkbookDrop.HasSupportedExtension(path))
        {
            throw new InvalidDataException("Seleziona un file Excel in formato .xlsx o .xlsm.");
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

        // Celle formattate come durata ([h]:mm): ClosedXML le espone come TimeSpan.
        if (IsTimeSpanCell(cell))
        {
            var hours = Math.Round(Convert.ToDecimal(cell.GetTimeSpan().TotalHours), 4, MidpointRounding.AwayFromZero);
            return new WorkbookCell(rowNumber, hours.ToString(CultureInfo.InvariantCulture), null, hours);
        }

        var text = cell.GetString().Trim();
        if (!WorkbookCell.IsMeaningfulText(text))
        {
            return WorkbookCell.Empty(rowNumber);
        }
        return new WorkbookCell(rowNumber, text, null, null);
    }

    private static bool IsTimeSpanCell(IXLCell cell) =>
        string.Equals(cell.DataType.ToString(), "TimeSpan", StringComparison.Ordinal);

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
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                var rawLabel = worksheet.Cell(row, column).GetString();
                var label = Normalize(rawLabel);
                var value = ReadCell(worksheet.Cell(row, column + 1), row);
                if (label is "from date" or "data da" or "data dalla" or "dal" or "periodo dal" or "inizio periodo")
                {
                    if (value.TryGetDate(out var date))
                    {
                        start = date.Date;
                    }
                }
                else if (label is "to date" or "data al" or "data alla" or "al" or "periodo al" or "fine periodo")
                {
                    if (value.TryGetDate(out var date))
                    {
                        end = date.Date;
                    }
                }

                // Intervallo scritto in una sola cella: "01/08/2026 - 31/08/2026".
                if (!start.HasValue || !end.HasValue)
                {
                    var range = PeriodRange.Match(rawLabel ?? string.Empty);
                    if (range.Success)
                    {
                        if (!start.HasValue &&
                            new WorkbookCell(row, range.Groups["start"].Value, null, null).TryGetDate(out var rangeStart))
                        {
                            start = rangeStart.Date;
                        }
                        if (!end.HasValue &&
                            new WorkbookCell(row, range.Groups["end"].Value, null, null).TryGetDate(out var rangeEnd))
                        {
                            end = rangeEnd.Date;
                        }
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
