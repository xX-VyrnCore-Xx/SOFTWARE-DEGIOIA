namespace PresenzeXmlConverter;

internal static class AttendanceMovementGenerator
{
    public static GenerationResult Generate(
        IReadOnlyList<AttendanceRequest> requests,
        IReadOnlyDictionary<string, string> employeeCodes,
        IReadOnlyDictionary<string, string> policyCodes,
        DateTime? periodStart,
        DateTime? periodEnd)
    {
        var movements = new List<DailyMovement>();
        var warnings = new List<string>();

        foreach (var request in requests)
        {
            var start = request.StartDate.Date;
            var end = request.EndDate.Date;
            if (periodStart.HasValue && start < periodStart.Value.Date)
            {
                start = periodStart.Value.Date;
            }
            if (periodEnd.HasValue && end > periodEnd.Value.Date)
            {
                end = periodEnd.Value.Date;
            }

            if (start > end)
            {
                warnings.Add($"Riga {request.SourceRow}: richiesta fuori dal periodo Excel, esclusa.");
                continue;
            }

            var workDays = EnumerateWorkDays(start, end).ToArray();
            if (workDays.Length == 0)
            {
                warnings.Add($"Riga {request.SourceRow}: nessun giorno lavorativo nel periodo della richiesta.");
                continue;
            }

            var totalMinutes = decimal.ToInt32(decimal.Round(
                request.Hours * 60m,
                0,
                MidpointRounding.AwayFromZero));
            if (totalMinutes <= 0)
            {
                warnings.Add($"Riga {request.SourceRow}: durata non positiva, richiesta esclusa.");
                continue;
            }

            var baseMinutes = totalMinutes / workDays.Length;
            var extraMinutes = totalMinutes % workDays.Length;
            if (baseMinutes + (extraMinutes > 0 ? 1 : 0) > 99 * 60 + 59)
            {
                throw new InvalidDataException(
                    $"La durata della riga {request.SourceRow} supera la capienza del tracciato TRRIPW.");
            }

            if (!employeeCodes.TryGetValue(request.EmployeeName, out var mappedEmployeeCode) ||
                string.IsNullOrWhiteSpace(mappedEmployeeCode))
            {
                throw new InvalidDataException($"Manca il codice PAGHE per “{request.EmployeeName}”.");
            }
            if (!policyCodes.TryGetValue(request.Policy, out var mappedReasonCode) ||
                string.IsNullOrWhiteSpace(mappedReasonCode))
            {
                throw new InvalidDataException($"Manca il codice giustificativo per “{request.Policy}”.");
            }

            var employeeCode = mappedEmployeeCode.Trim();
            var reasonCode = mappedReasonCode.Trim();
            if (employeeCode.Length > 7 || reasonCode.Length > 3)
            {
                throw new InvalidDataException(
                    $"Il codice PAGHE o il giustificativo supera il limite del tracciato (riga {request.SourceRow}).");
            }
            for (var index = 0; index < workDays.Length; index++)
            {
                var minutes = baseMinutes + (index < extraMinutes ? 1 : 0);
                if (minutes == 0)
                {
                    continue;
                }
                movements.Add(new DailyMovement(
                    request.EmployeeName,
                    employeeCode,
                    request.Policy,
                    reasonCode,
                    workDays[index],
                    minutes / 60,
                    minutes % 60,
                    request.SourceRow));
            }
        }

        foreach (var dateGroup in movements.GroupBy(movement => (movement.EmployeeCode, movement.Date)))
        {
            var sourceRows = dateGroup.Select(movement => movement.SourceRow)
                .Where(row => row > 0)
                .Distinct()
                .Order()
                .ToArray();
            if (sourceRows.Length > 1)
            {
                warnings.Add(
                    $"Dipendente {dateGroup.Key.EmployeeCode}, {dateGroup.Key.Date:dd/MM/yyyy}: più richieste si sovrappongono (righe {string.Join(", ", sourceRows)}). Verifica che le ore non siano duplicate.");
            }
        }

        var merged = MergeSameReasonMovements(movements, warnings);

        return new GenerationResult(
            merged.OrderBy(movement => movement.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(movement => movement.Date)
                .ThenBy(movement => movement.ReasonCode, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            warnings);
    }

    /// <summary>
    /// Unisce i movimenti duplicati (stesso dipendente, data e giustificativo) sommando le ore:
    /// nel tracciato TRRIPW non devono comparire movimenti identici ripetuti, e sommare
    /// è l'unico modo di non perdere ore quando più righe Excel descrivono lo stesso assenza.
    /// </summary>
    private static List<DailyMovement> MergeSameReasonMovements(
        List<DailyMovement> movements,
        List<string> warnings)
    {
        if (movements.Count == 0)
        {
            return movements;
        }

        var merged = new List<DailyMovement>(movements.Count);
        foreach (var group in movements.GroupBy(movement =>
                     (movement.EmployeeCode, movement.Date, movement.ReasonCode),
                     new MovementKeyComparer()).OrderBy(group => group.Key.Date))
        {
            var groupMovements = group.ToArray();
            if (groupMovements.Length == 1)
            {
                merged.Add(groupMovements[0]);
                continue;
            }

            var totalMinutes = groupMovements.Sum(movement => movement.Hours * 60 + movement.Minutes);
            if (totalMinutes > 24 * 60)
            {
                warnings.Add(
                    $"Dipendente {group.Key.EmployeeCode}, {group.Key.Date:dd/MM/yyyy}: le richieste duplicate sommano a {totalMinutes / 60}h {totalMinutes % 60:D2} min (oltre 24 ore). Verifica i dati sorgente.");
            }
            var representative = groupMovements[0];
            merged.Add(representative with
            {
                Hours = totalMinutes / 60,
                Minutes = totalMinutes % 60,
                SourceRow = groupMovements.Min(movement => movement.SourceRow)
            });
        }
        return merged;
    }

    private sealed class MovementKeyComparer : IEqualityComparer<(string EmployeeCode, DateTime Date, string ReasonCode)>
    {
        public bool Equals(
            (string EmployeeCode, DateTime Date, string ReasonCode) x,
            (string EmployeeCode, DateTime Date, string ReasonCode) y) =>
            x.Date == y.Date &&
            string.Equals(x.EmployeeCode, y.EmployeeCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ReasonCode, y.ReasonCode, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string EmployeeCode, DateTime Date, string ReasonCode) obj) =>
            HashCode.Combine(
                obj.Date,
                obj.EmployeeCode.ToUpperInvariant(),
                obj.ReasonCode.ToUpperInvariant());
    }

    private static IEnumerable<DateTime> EnumerateWorkDays(DateTime start, DateTime end)
    {
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || ItalianHolidays.Contains(day))
            {
                continue;
            }
            yield return day;
        }
    }
}

internal static class ItalianHolidays
{
    public static bool Contains(DateTime date)
    {
        var day = date.Date;
        return (day.Month, day.Day) is
            (1, 1) or (1, 6) or (4, 25) or (5, 1) or (6, 2) or
            (8, 15) or (11, 1) or (12, 8) or (12, 25) or (12, 26) ||
            day == EasterMonday(day.Year);
    }

    private static DateTime EasterMonday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateTime(year, month, day).AddDays(1);
    }
}
