using DeuxERP.Domain.Common;

namespace DeuxERP.Domain.Notifications;

public class DailyReminderLog : Entity
{
    public Guid Id { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public DailyReminderKind Kind { get; private set; }
    public DateTime ClaimedAt { get; private set; }
    public DateTime? SentAt { get; private set; }

    public static DailyReminderLog Claim(DateOnly localDate, DailyReminderKind kind)
    {
        return new DailyReminderLog
        {
            Id = Guid.CreateVersion7(),
            LocalDate = localDate,
            Kind = kind,
            ClaimedAt = DateTime.UtcNow
        };
    }

    public void MarkSent() => SentAt = DateTime.UtcNow;

    private DailyReminderLog() { }
}

public enum DailyReminderKind
{
    DueToday = 1,
    DueTomorrow = 2
}
