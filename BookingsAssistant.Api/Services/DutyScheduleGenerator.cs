using BookingsAssistant.Api.Data.Entities;

namespace BookingsAssistant.Api.Services;

public static class DutyScheduleGenerator
{
    private static readonly (string Name, DateTime Start, DateTime End)[] EssexHolidays =
    {
        ("Autumn half term",       new(2025, 10, 27), new(2025, 10, 31)),
        ("Christmas holiday",      new(2025, 12, 22), new(2026,  1,  2)),
        ("Spring half term",       new(2026,  2, 16), new(2026,  2, 20)),
        ("Easter holiday",         new(2026,  3, 30), new(2026,  4, 10)),
        ("Early May bank holiday", new(2026,  5,  4), new(2026,  5,  4)),
        ("Summer half term",       new(2026,  5, 25), new(2026,  5, 29)),
        ("Summer holiday",         new(2026,  7, 21), new(2026,  8, 31)),
    };

    /// <summary>
    /// Generates duty entries for a date range.
    /// Default pattern: every Friday 19:00 to Sunday 13:00.
    /// During school holidays: also Monday-Thursday (assumed 10:00-16:00).
    /// </summary>
    public static List<SiteDuty> Generate(DateTime from, DateTime to)
    {
        var duties = new List<SiteDuty>();
        var current = from.Date;

        while (current <= to.Date)
        {
            if (current.DayOfWeek == DayOfWeek.Friday)
            {
                duties.Add(new SiteDuty
                {
                    StartDate = current.AddHours(19),
                    EndDate = current.AddDays(2).AddHours(13), // Sunday 13:00
                });
                current = current.AddDays(1);
                continue;
            }

            if (IsSchoolHoliday(current) && IsWeekday(current))
            {
                var holidayName = GetHolidayName(current);
                duties.Add(new SiteDuty
                {
                    StartDate = current.AddHours(10),
                    EndDate = current.AddHours(16),
                    Notes = holidayName,
                });
            }

            current = current.AddDays(1);
        }

        return duties;
    }

    private static bool IsWeekday(DateTime date) =>
        date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Thursday;

    private static bool IsSchoolHoliday(DateTime date) =>
        EssexHolidays.Any(h => date.Date >= h.Start.Date && date.Date <= h.End.Date);

    private static string? GetHolidayName(DateTime date) =>
        EssexHolidays.FirstOrDefault(h => date.Date >= h.Start.Date && date.Date <= h.End.Date).Name;
}
