namespace DeuxERP.Application.Common;

public static class BusinessDateRange
{
    private static readonly TimeZoneInfo BusinessZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    // Calendar dates include the entire last day in the business timezone.
    // Explicit UTC/offset timestamps retain their exact, exclusive boundary.
    public static (DateTime? From, DateTime? To) Normalize(DateTime? from, DateTime? to)
    {
        var start = Boundary(from, false);
        var end = Boundary(to, true);
        if (start.HasValue && end.HasValue && start >= end)
            throw new InvalidOperationException("O início do período deve ser anterior ao fim.");
        return (start, end);
    }

    private static DateTime? Boundary(DateTime? value, bool end)
    {
        if (!value.HasValue) return null;
        if (value.Value.Kind != DateTimeKind.Unspecified) return value.Value.ToUniversalTime();
        var local = value.Value;
        if (end && local.TimeOfDay == TimeSpan.Zero) local = local.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, BusinessZone);
    }
}
