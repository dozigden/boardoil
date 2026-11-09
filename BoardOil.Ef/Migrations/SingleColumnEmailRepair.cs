using System.Globalization;
using System.Text.Json;
using BoardOil.Data.Abstractions.Users;
using Microsoft.Data.Sqlite;

namespace BoardOil.Ef.Migrations;

// Versioned migration functions: retain these for databases upgrading from old schemas.
internal static class SingleColumnEmailRepair
{
    internal static void Register(SqliteConnection connection)
    {
        connection.CreateFunction<string, string>("bo_email_repair_plan_v1", BuildPlan, isDeterministic: true);
    }

    private sealed record SourceUser(int Id, string Email);
    private sealed record PlannedUser(int Id, string NewEmail);

    private static string BuildPlan(string usersJson)
    {
        var users = JsonSerializer.Deserialize<SourceUser[]>(usersJson)!
            .OrderBy(x => x.Id).ToArray();
        var reserved = users.Select(x => EmailComparison.Normalise(x.Email)).ToHashSet(StringComparer.Ordinal);
        var owners = new HashSet<string?>(StringComparer.Ordinal);
        var plan = new List<PlannedUser>();
        foreach (var user in users)
        {
            if (owners.Add(EmailComparison.Normalise(user.Email)))
            {
                plan.Add(new PlannedUser(user.Id, user.Email));
                continue;
            }

            var email = user.Email.Trim();
            var separator = email.LastIndexOf('@');
            var localPart = email;
            var domain = string.Empty;
            if (separator >= 0)
            {
                localPart = email[..separator];
                domain = email[separator..];
            }
            for (var suffix = 1; ; suffix++)
            {
                var candidate = localPart + "+" + suffix.ToString(CultureInfo.InvariantCulture) + domain;
                if (reserved.Add(EmailComparison.Normalise(candidate)))
                {
                    plan.Add(new PlannedUser(user.Id, candidate));
                    break;
                }
            }
        }
        return JsonSerializer.Serialize(plan);
    }

}
