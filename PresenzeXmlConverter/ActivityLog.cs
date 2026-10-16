using System.Globalization;
using System.Text;

namespace PresenzeXmlConverter;

/// <summary>Che cosa è successo: serve a raggruppare e filtrare le voci del registro.</summary>
internal enum ActivityKind
{
    Import,
    Preview,
    Export,
    Codes,
    Company,
    Backup,
    Update,
    Settings,
    Check
}

/// <summary>Una voce del registro delle attività.</summary>
internal sealed record ActivityEntry(long Id, DateTime AtUtc, ActivityKind Kind, string Title, string Detail)
{
    public DateTime LocalTime => AtUtc.ToLocalTime();
}

/// <summary>
/// Registro delle attività: testi, filtro ed esportazione in CSV. Solo calcolo, senza controlli né database, così si
/// verifica con i test.
/// </summary>
internal static class ActivityLog
{
    /// <summary>Quante voci si conservano: le più vecchie si eliminano da sole.</summary>
    public const int MaximumEntries = 5000;

    private const int MaximumTitle = 160;
    private const int MaximumDetail = 400;

    public static string Label(ActivityKind kind) => kind switch
    {
        ActivityKind.Import => "Importazione",
        ActivityKind.Preview => "Anteprima",
        ActivityKind.Export => "Esportazione",
        ActivityKind.Codes => "Codici",
        ActivityKind.Company => "Aziende",
        ActivityKind.Backup => "Backup",
        ActivityKind.Update => "Aggiornamento",
        ActivityKind.Settings => "Impostazioni",
        _ => "Controllo"
    };

    /// <summary>Tipo letto dal database; un valore sconosciuto (versione più nuova) diventa «Controllo» invece di far fallire la lettura.</summary>
    public static ActivityKind ParseKind(string? text) =>
        Enum.TryParse<ActivityKind>(text, ignoreCase: true, out var kind) && Enum.IsDefined(kind) ? kind : ActivityKind.Check;

    /// <summary>Testi puliti e di lunghezza limitata: una riga sola, niente a capo.</summary>
    public static string Clean(string? text, int maximum)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }
        var single = string.Join(' ', text.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return single.Length <= maximum ? single : single[..(maximum - 1)].TrimEnd() + "…";
    }

    public static string CleanTitle(string? text) => Clean(text, MaximumTitle);

    public static string CleanDetail(string? text) => Clean(text, MaximumDetail);

    /// <summary>«oggi 10:24», «ieri 18:02», «12/10/2026 09:15».</summary>
    public static string DescribeWhen(DateTime localTime, DateTime now) => ExportHistory.DescribeDay(localTime, now);

    /// <summary>Voci che contengono tutte le parole cercate (in qualsiasi ordine, senza accenti né maiuscole) e, se indicato, di un solo tipo.</summary>
    public static IReadOnlyList<ActivityEntry> Filter(IEnumerable<ActivityEntry> entries, string? query, ActivityKind? kind = null)
    {
        var terms = MovementSearch.SplitTerms(query);
        return entries
            .Where(entry => kind is null || entry.Kind == kind)
            .Where(entry => terms.Count == 0 ||
                            MovementSearch.Matches($"{Label(entry.Kind)} {entry.Title} {entry.Detail}", terms))
            .ToList();
    }

    /// <summary>«128 operazioni · l'ultima oggi 10:24», oppure un invito se è vuoto.</summary>
    public static string Summarize(IReadOnlyList<ActivityEntry> entries, DateTime now)
    {
        if (entries.Count == 0)
        {
            return "Nessuna attività registrata.";
        }
        var last = entries.MaxBy(entry => entry.AtUtc)!;
        var noun = entries.Count == 1 ? "operazione" : "operazioni";
        return $"{entries.Count:N0} {noun} · l'ultima {DescribeWhen(last.LocalTime, now)}";
    }

    /// <summary>
    /// CSV per Excel italiano (separatore «;», con BOM UTF-8). Le celle che iniziano con = + - @ si prefissano con un
    /// apice: aperte in Excel non vengono interpretate come formule.
    /// </summary>
    public static string ToCsv(IEnumerable<ActivityEntry> entries)
    {
        var text = new StringBuilder();
        text.Append('﻿');
        text.Append("Quando;Tipo;Azione;Dettaglio\r\n");
        foreach (var entry in entries.OrderBy(entry => entry.AtUtc).ThenBy(entry => entry.Id))
        {
            text.Append(Cell(entry.LocalTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(';')
                .Append(Cell(Label(entry.Kind))).Append(';')
                .Append(Cell(entry.Title)).Append(';')
                .Append(Cell(entry.Detail)).Append("\r\n");
        }
        return text.ToString();
    }

    public static string Cell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
        {
            text = "'" + text;
        }
        return text.IndexOfAny([';', '"', '\r', '\n']) >= 0
            ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : text;
    }
}
