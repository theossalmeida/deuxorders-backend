using DeuxERP.Application.Notifications;
using DeuxERP.Infrastructure.Data;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DeuxERP.Infrastructure.Notifications;

public sealed class DailyOrderReminderService : BackgroundService
{
    private static readonly TimeOnly DueTodayTime = new(8, 0);
    private static readonly TimeOnly DueTomorrowTime = new(15, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyOrderReminderService> _logger;
    private readonly TimeZoneInfo _timeZone;

    public DailyOrderReminderService(
        IServiceScopeFactory scopeFactory,
        ILogger<DailyOrderReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeZone = ResolveSaoPauloTimeZone();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = GetNextRun(DateTimeOffset.UtcNow);
            var delay = nextRun.UtcDateTime - DateTime.UtcNow;

            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                break;

            await RunReminderAsync(nextRun.Kind, nextRun.LocalDate, stoppingToken);
        }
    }

    private async Task RunReminderAsync(ReminderKind kind, DateOnly localDate, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            var reminder = await TryClaimReminderAsync(db, ToDailyReminderKind(kind), localDate, ct);
            if (reminder == null)
            {
                _logger.LogDebug(
                    "Daily order reminder {ReminderKind} for {LocalDate} skipped because it was already claimed.",
                    kind,
                    localDate);
                return;
            }

            var targetDate = kind == ReminderKind.DueToday
                ? localDate
                : localDate.AddDays(1);

            var orders = await orderRepository.GetDueOnDateAsync(targetDate, ct);
            if (orders.Count == 0)
                return;

            var notificationType = kind == ReminderKind.DueToday
                ? NotificationType.DailyDueToday
                : NotificationType.DailyDueTomorrow;

            await push.SendToAllAsync(
                notificationType,
                kind == ReminderKind.DueToday ? "Pedidos para hoje" : "Pedidos para amanha",
                BuildBody(orders.Count, targetDate),
                "/orders",
                ct);

            reminder.MarkSent();
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Daily order reminder failed for {ReminderKind} on {LocalDate}.", kind, localDate);
        }
    }

    private static async Task<DailyReminderLog?> TryClaimReminderAsync(
        ApplicationDbContext db,
        DailyReminderKind kind,
        DateOnly localDate,
        CancellationToken ct)
    {
        var reminder = DailyReminderLog.Claim(localDate, kind);
        db.DailyReminderLogs.Add(reminder);

        try
        {
            await db.SaveChangesAsync(ct);
            return reminder;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            db.Entry(reminder).State = EntityState.Detached;
            return null;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static DailyReminderKind ToDailyReminderKind(ReminderKind kind) =>
        kind == ReminderKind.DueToday
            ? DailyReminderKind.DueToday
            : DailyReminderKind.DueTomorrow;

    private ReminderRun GetNextRun(DateTimeOffset utcNow)
    {
        var localNow = TimeZoneInfo.ConvertTime(utcNow, _timeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);

        var dueToday = BuildRun(today, DueTodayTime, ReminderKind.DueToday);
        var dueTomorrow = BuildRun(today, DueTomorrowTime, ReminderKind.DueTomorrow);

        if (dueToday.UtcDateTime <= utcNow.UtcDateTime)
            dueToday = BuildRun(today.AddDays(1), DueTodayTime, ReminderKind.DueToday);

        if (dueTomorrow.UtcDateTime <= utcNow.UtcDateTime)
            dueTomorrow = BuildRun(today.AddDays(1), DueTomorrowTime, ReminderKind.DueTomorrow);

        return dueToday.UtcDateTime <= dueTomorrow.UtcDateTime ? dueToday : dueTomorrow;
    }

    private ReminderRun BuildRun(DateOnly localDate, TimeOnly localTime, ReminderKind kind)
    {
        var localDateTime = localDate.ToDateTime(localTime);
        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, _timeZone);
        return new ReminderRun(kind, localDate, utcDateTime);
    }

    private static string BuildBody(int count, DateOnly targetDate)
    {
        var noun = count == 1 ? "pedido pendente" : "pedidos pendentes";
        return $"{count} {noun} para {targetDate:dd/MM/yyyy}.";
    }

    private static TimeZoneInfo ResolveSaoPauloTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }

    private enum ReminderKind
    {
        DueToday,
        DueTomorrow
    }

    private sealed record ReminderRun(ReminderKind Kind, DateOnly LocalDate, DateTime UtcDateTime);
}
