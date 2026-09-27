using Microsoft.EntityFrameworkCore.Migrations;

namespace BoardOil.Ef.Migrations;

public static class ScheduledJobMigrationExtensions
{
    /// <summary>
    /// Requests a Once pass after startup migrations finish. Repeated requests coalesce;
    /// the scheduler, not the migration, owns enqueueing and clearing the request.
    /// </summary>
    public static void RequestOnceSchedule(this MigrationBuilder migrationBuilder, string schedulerStateName)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(schedulerStateName);
        if (schedulerStateName.Length > 120
            || !schedulerStateName.All(character => char.IsAsciiLetterOrDigit(character)
                || character is '.' or '_' or '-'))
        {
            throw new ArgumentException(
                "Checkpoint names must use 1-120 ASCII letters, digits, dots, underscores or hyphens.",
                nameof(schedulerStateName));
        }

        // Names are restricted to the scheduler's safe key alphabet above. Keep this SQL
        // independent of runtime services so future migrations only record durable intent.
        migrationBuilder.Sql($"""
            INSERT INTO "ScheduledJobSchedulerStates"
                ("Name", "LastRunTimeUtc", "LastEvaluatedAtUtc", "PendingDueAtUtc",
                 "RunRequested", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES
                ('{schedulerStateName}', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'), NULL, NULL,
                 1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'), strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT ("Name") DO UPDATE SET
                "RunRequested" = 1,
                "PendingDueAtUtc" = NULL,
                "UpdatedAtUtc" = excluded."UpdatedAtUtc";
            """);
    }
}
