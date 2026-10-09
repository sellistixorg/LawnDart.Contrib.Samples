namespace LawnDart.Samples.EShop.Shared;

/// <summary>
/// Version 5 ids for seeded commands and aggregates.
/// The name prefix is <c>seed</c>.
/// </summary>
public static class EShopSeedIds
{
    /// <summary>
    /// Prefix passed to <see cref="EShopVersion5Id.ForPrefixedName"/>.
    /// </summary>
    public const string Prefix = "seed";

    /// <summary>
    /// Version 5 id whose name is <c>seed:{name}</c>.
    /// </summary>
    /// <param name="name">Text after the prefix.</param>
    /// <returns>The version 5 UUID.</returns>
    public static Guid Create(string name) => EShopVersion5Id.ForPrefixedName(Prefix, name);
}
