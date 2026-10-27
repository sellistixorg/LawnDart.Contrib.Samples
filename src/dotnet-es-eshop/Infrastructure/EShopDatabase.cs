using Microsoft.Extensions.Configuration;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// The database a service host stores its events, outbox, views, checkpoints, and inbox in.
/// </summary>
public enum EShopDatabase
{
    /// <summary>SQL Server. The default.</summary>
    SqlServer,

    /// <summary>Postgres.</summary>
    Postgres
}

/// <summary>
/// Reads the <c>EShop:Database</c> setting and the <c>--database</c> launch option.
/// </summary>
/// <remarks>
/// <para>
/// Allowed values are <c>SqlServer</c> and <c>Postgres</c>, with case ignored.
/// When the setting is missing or empty, the host uses SQL Server.
/// Any other value stops startup with a message that lists the allowed values.
/// </para>
/// <para>
/// The setting is ordinary .NET configuration, so it can come from <c>appsettings.json</c>,
/// the environment variable <c>EShop__Database</c>, or the command line (<c>--EShop:Database Postgres</c>).
/// The shorter launch option <c>--database postgres</c> sets the same value and wins over the others.
/// </para>
/// </remarks>
public static class EShopDatabaseSetting
{
    /// <summary>Configuration key for the database choice.</summary>
    public const string ConfigurationKey = "EShop:Database";

    /// <summary>Launch option that sets <see cref="ConfigurationKey"/>.</summary>
    public const string LaunchOption = "--database";

    /// <summary>The database used when nothing is set.</summary>
    public const EShopDatabase Default = EShopDatabase.SqlServer;

    private static readonly string AllowedValues =
        $"'{nameof(EShopDatabase.SqlServer)}' or '{nameof(EShopDatabase.Postgres)}'";

    /// <summary>
    /// Reads <see cref="ConfigurationKey"/>. Missing or empty gives <see cref="Default"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value is not an allowed database name.</exception>
    public static EShopDatabase ReadEShopDatabase(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(value))
            return Default;

        if (TryParse(value, out var database))
            return database;

        throw new InvalidOperationException(
            $"Setting '{ConfigurationKey}' is '{value}'. Use {AllowedValues}, or leave it unset for SQL Server.");
    }

    /// <summary>
    /// Parses a database name. <c>SqlServer</c> and <c>Postgres</c> match with case ignored.
    /// Numbers and other names do not match.
    /// </summary>
    public static bool TryParse(string? value, out EShopDatabase database)
    {
        var name = value?.Trim();
        if (string.Equals(name, nameof(EShopDatabase.SqlServer), StringComparison.OrdinalIgnoreCase))
        {
            database = EShopDatabase.SqlServer;
            return true;
        }

        if (string.Equals(name, nameof(EShopDatabase.Postgres), StringComparison.OrdinalIgnoreCase))
        {
            database = EShopDatabase.Postgres;
            return true;
        }

        database = Default;
        return false;
    }

    /// <summary>
    /// Maps <c>--database &lt;name&gt;</c> or <c>--database=&lt;name&gt;</c> in <paramref name="args"/>
    /// to <see cref="ConfigurationKey"/>. Call it right after the host builder is created,
    /// so the launch option wins over settings files and environment variables.
    /// When the option is not given, the configuration is left as it is.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The option has no value, is given more than once, or names a database that is not allowed.
    /// </exception>
    public static IConfigurationBuilder AddEShopDatabaseLaunchOption(
        this IConfigurationBuilder configuration,
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(args);

        var value = FindLaunchOption(args);
        if (value is null)
            return configuration;

        if (!TryParse(value, out var database))
        {
            throw new InvalidOperationException(
                $"Launch option '{LaunchOption}' is '{value}'. Use {AllowedValues}.");
        }

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ConfigurationKey] = database.ToString()
        });
        return configuration;
    }

    private static string? FindLaunchOption(string[] args)
    {
        string? found = null;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string? value;
            if (string.Equals(arg, LaunchOption, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
                {
                    throw new InvalidOperationException(
                        $"Launch option '{LaunchOption}' needs a value: {AllowedValues}.");
                }

                value = args[++i];
            }
            else if (arg.StartsWith(LaunchOption + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = arg[(LaunchOption.Length + 1)..];
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException(
                        $"Launch option '{LaunchOption}' needs a value: {AllowedValues}.");
                }
            }
            else
            {
                continue;
            }

            if (found is not null)
            {
                throw new InvalidOperationException(
                    $"Launch option '{LaunchOption}' is given more than once. Give it once.");
            }

            found = value;
        }

        return found;
    }
}
