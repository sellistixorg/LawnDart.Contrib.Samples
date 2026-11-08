using System.Runtime.CompilerServices;
using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.Database.Tests;

/// <summary>
/// Reads <c>ESHOP_DATABASE</c> and <c>ESHOP_REQUIRE_DATABASE</c> for the database tests.
/// </summary>
internal static class DatabaseSession
{
    public const string DatabaseVariable = "ESHOP_DATABASE";
    public const string RequireVariable = "ESHOP_REQUIRE_DATABASE";

    public static bool Require { get; } =
        string.Equals(Environment.GetEnvironmentVariable(RequireVariable), "1", StringComparison.Ordinal);

    public static bool IsSelected { get; } = TryRead(out _);

    public static string SkipReason =>
        "Set " + DatabaseVariable + " to SqlServer or Postgres to run database tests.";

    public static EShopDatabase Database
    {
        get
        {
            if (!TryRead(out var database))
            {
                throw new InvalidOperationException(
                    Require
                        ? RequireVariable + "=1 requires " + DatabaseVariable + " set to SqlServer or Postgres."
                        : SkipReason);
            }

            return database;
        }
    }

    private static bool TryRead(out EShopDatabase database)
    {
        var raw = Environment.GetEnvironmentVariable(DatabaseVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            database = default;
            return false;
        }

        if (!EShopDatabaseSetting.TryParse(raw.Trim(), out database))
        {
            throw new InvalidOperationException(
                DatabaseVariable + " '" + raw.Trim() + "' is not supported. Use SqlServer or Postgres.");
        }

        return true;
    }
}

internal static class TestcontainersStartup
{
    [ModuleInitializer]
    internal static void DisableReaper()
    {
        Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
    }
}

/// <summary>
/// Skips the test unless <c>ESHOP_DATABASE</c> is set. <c>ESHOP_REQUIRE_DATABASE=1</c> makes a missing value a failure.
/// </summary>
public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (!DatabaseSession.IsSelected && !DatabaseSession.Require)
            Skip = DatabaseSession.SkipReason;
    }
}

/// <summary>
/// Same gate as <see cref="DatabaseFactAttribute"/> for a theory.
/// </summary>
public sealed class DatabaseTheoryAttribute : TheoryAttribute
{
    public DatabaseTheoryAttribute()
    {
        if (!DatabaseSession.IsSelected && !DatabaseSession.Require)
            Skip = DatabaseSession.SkipReason;
    }
}
