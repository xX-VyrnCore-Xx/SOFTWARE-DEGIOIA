using System.Globalization;
using System.Text;

namespace PresenzeXmlConverter;

/// <summary>
/// Lo stesso dipendente scritto in modi diversi («Vincenzo Antenori» e «Antenori Vincenzo», maiuscole o
/// accenti diversi) è una persona sola: ha lo stesso codice PAGHE e non va trattato come un conflitto né
/// dividere i suoi giorni in due righe. Due persone diverse con lo stesso codice restano invece un problema.
/// </summary>
internal static class EmployeeNames
{
    /// <summary>Chiave del nome: parole in minuscolo, senza accenti né punteggiatura, in ordine alfabetico.</summary>
    public static string Key(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }
        var tokens = new List<string>();
        var current = new StringBuilder();
        foreach (var character in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(character))
            {
                current.Append(char.ToLowerInvariant(character));
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }
        tokens.Sort(StringComparer.Ordinal);
        return string.Join(' ', tokens);
    }

    public static bool SamePerson(string? first, string? second)
    {
        var key = Key(first);
        return key.Length > 0 && key == Key(second);
    }

    /// <summary>Valore legato a un nome: esatto, altrimenti quello di una variante della stessa persona.</summary>
    public static bool TryFind<T>(IReadOnlyDictionary<string, T> values, string name, out T value)
    {
        if (values.TryGetValue(name, out value!))
        {
            return true;
        }
        var key = Key(name);
        if (key.Length > 0)
        {
            foreach (var pair in values)
            {
                if (Key(pair.Key) == key)
                {
                    value = pair.Value;
                    return true;
                }
            }
        }
        value = default!;
        return false;
    }

    /// <summary>
    /// Unifica le grafie dello stesso dipendente nelle richieste (vale la prima incontrata), salvo che i loro
    /// codici PAGHE siano diversi: allora sono persone distinte e restano come sono.
    /// </summary>
    public static IReadOnlyList<AttendanceRequest> Canonicalize(
        IReadOnlyList<AttendanceRequest> requests,
        IReadOnlyDictionary<string, string>? employeeCodes)
    {
        string? CodeOf(AttendanceRequest request)
        {
            if (employeeCodes is not null && TryFind(employeeCodes, request.EmployeeName, out var mapped) &&
                !string.IsNullOrWhiteSpace(mapped))
            {
                return mapped.Trim();
            }
            return string.IsNullOrWhiteSpace(request.EmployeeCode) ? null : request.EmployeeCode.Trim();
        }

        var groups = requests
            .GroupBy(request => Key(request.EmployeeName))
            .Where(group => group.Key.Length > 0 && group.Select(request => request.EmployeeName).Distinct(StringComparer.Ordinal).Count() > 1)
            .ToArray();
        if (groups.Length == 0)
        {
            return requests;
        }

        var rename = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var codes = group.Select(CodeOf).Where(code => code is not null).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (codes > 1)
            {
                continue;
            }
            var canonical = group.First().EmployeeName;
            foreach (var name in group.Select(request => request.EmployeeName).Distinct(StringComparer.Ordinal))
            {
                rename[name] = canonical;
            }
        }
        return rename.Count == 0
            ? requests
            : requests
                .Select(request => rename.TryGetValue(request.EmployeeName, out var canonical) && canonical != request.EmployeeName
                    ? request with { EmployeeName = canonical }
                    : request)
                .ToArray();
    }
}
