using Microsoft.Extensions.Configuration;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// The one-shot command that creates eShop database tables.
/// Normal startup does not create tables. It only checks that this command has already run.
/// </summary>
public static class EShopSchemaSetup
{
    /// <summary>Command-line argument that runs schema setup and then exits.</summary>
    public const string Argument = "--setup-schema";

    /// <summary>
    /// Configuration flag set for the setup process. Seeders see it and do no work.
    /// </summary>
    public const string ConfigurationKey = "EShop:SchemaSetup";

    /// <summary>
    /// True when <paramref name="args"/> contains <see cref="Argument"/>.
    /// </summary>
    /// <param name="args">The process arguments.</param>
    public static bool IsRequested(IEnumerable<string>? args)
    {
        if (args is null)
            return false;

        foreach (var arg in args)
        {
            if (string.Equals(arg.Trim(), Argument, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when this process was marked as the setup command.
    /// </summary>
    /// <param name="configuration">The host configuration.</param>
    public static bool IsConfigured(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return string.Equals(configuration[ConfigurationKey], "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Marks the host so seeders do not run, even if they are registered.
    /// </summary>
    /// <param name="configuration">The host configuration builder.</param>
    /// <returns>The same builder.</returns>
    public static IConfigurationBuilder Mark(this IConfigurationBuilder configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ConfigurationKey] = "true"
        });
        return configuration;
    }
}
