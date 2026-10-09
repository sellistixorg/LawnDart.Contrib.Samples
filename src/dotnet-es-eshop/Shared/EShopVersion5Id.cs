using System.Security.Cryptography;
using System.Text;

namespace LawnDart.Samples.EShop.Shared;

/// <summary>
/// RFC 4122 version 5 ids for eShop commands and messages.
/// </summary>
/// <remarks>
/// The namespace is the DNS UUID. The name is UTF-8. A prefixed name is
/// <c>{prefix}:{name}</c>.
/// </remarks>
public static class EShopVersion5Id
{
    /// <summary>
    /// RFC 4122 DNS namespace.
    /// </summary>
    public static readonly Guid DnsNamespace = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

    /// <summary>
    /// Version 5 id for <paramref name="name"/> in <see cref="DnsNamespace"/>.
    /// </summary>
    /// <param name="name">Name encoded as UTF-8.</param>
    /// <returns>The version 5 UUID.</returns>
    public static Guid ForName(string name) => Create(DnsNamespace, name);

    /// <summary>
    /// Version 5 id whose name is <paramref name="prefix"/>, a colon, and <paramref name="name"/>.
    /// </summary>
    /// <param name="prefix">Text before the colon.</param>
    /// <param name="name">Text after the colon.</param>
    /// <returns>The version 5 UUID.</returns>
    public static Guid ForPrefixedName(string prefix, string name)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(name);
        return ForName(prefix + ":" + name);
    }

    /// <summary>
    /// RFC 4122 name-based UUID (version 5). SHA-1 of the namespace id and <paramref name="name"/>.
    /// </summary>
    /// <param name="namespaceId">Namespace UUID.</param>
    /// <param name="name">Name encoded as UTF-8.</param>
    /// <returns>The version 5 UUID.</returns>
    public static Guid Create(Guid namespaceId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Span<byte> namespaceBytes = stackalloc byte[16];
        namespaceId.TryWriteBytes(namespaceBytes);
        SwapGuidFields(namespaceBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var buffer = new byte[16 + nameBytes.Length];
        namespaceBytes.CopyTo(buffer);
        nameBytes.CopyTo(buffer.AsSpan(16));

        Span<byte> hash = stackalloc byte[20];
        SHA1.HashData(buffer, hash);

        Span<byte> guidBytes = stackalloc byte[16];
        hash[..16].CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        SwapGuidFields(guidBytes);
        return new Guid(guidBytes);
    }

    private static void SwapGuidFields(Span<byte> bytes)
    {
        (bytes[0], bytes[3]) = (bytes[3], bytes[0]);
        (bytes[1], bytes[2]) = (bytes[2], bytes[1]);
        (bytes[4], bytes[5]) = (bytes[5], bytes[4]);
        (bytes[6], bytes[7]) = (bytes[7], bytes[6]);
    }
}
