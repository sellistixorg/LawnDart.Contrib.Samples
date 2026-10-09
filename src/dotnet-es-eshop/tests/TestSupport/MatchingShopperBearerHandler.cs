using System.Net.Http.Headers;
using System.Text.Json;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Adds a bearer token for tests. The subject matches the buyer or account id on the outgoing request.
/// </summary>
public sealed class MatchingShopperBearerHandler : DelegatingHandler
{
    private static readonly string[] Names = ["buyerId", "accountId"];

    /// <summary>
    /// Creates the handler and a new signing key.
    /// </summary>
    public MatchingShopperBearerHandler()
        : this(ShopperAccessTokens.CreateKey())
    {
    }

    /// <summary>
    /// Creates the handler for <paramref name="signingKey"/>.
    /// </summary>
    /// <param name="signingKey">Key the test host also passes to bearer validation.</param>
    public MatchingShopperBearerHandler(RsaSecurityKey signingKey)
        : base(new HttpClientHandler())
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        SigningKey = signingKey;
    }

    /// <summary>
    /// Key this handler uses to sign tokens.
    /// </summary>
    public RsaSecurityKey SigningKey { get; }

    /// <summary>
    /// Registers shopper bearer validation with <see cref="SigningKey"/>.
    /// </summary>
    /// <param name="services">The test host services.</param>
    /// <returns>This handler.</returns>
    public MatchingShopperBearerHandler Protect(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddShopperBearerAccess(SigningKey);
        return this;
    }

    /// <summary>
    /// Client that sends the matching bearer token.
    /// </summary>
    /// <param name="address">Base address of the test host.</param>
    /// <returns>The client. Disposing it does not dispose this handler.</returns>
    public HttpClient CreateClient(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return new HttpClient(this, disposeHandler: false)
        {
            BaseAddress = new Uri(address)
        };
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Headers.Authorization is null)
        {
            var accountId = await FirstAccountIdAsync(request, cancellationToken).ConfigureAwait(false);
            if (accountId is Guid id)
            {
                var token = ShopperAccessTokens.Issue(SigningKey, id, "Shopper");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Guid?> FirstAccountIdAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is Uri uri && !string.IsNullOrEmpty(uri.Query))
        {
            var query = QueryHelpers.ParseQuery(uri.Query);
            foreach (var name in Names)
            {
                if (query.TryGetValue(name, out var values)
                    && Guid.TryParse(values.ToString(), out var id)
                    && id != Guid.Empty)
                {
                    return id;
                }
            }
        }

        if (request.Content is null)
            return null;

        var json = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        foreach (var id in IdsInJson(json))
        {
            if (id != Guid.Empty)
                return id;
        }

        return null;
    }

    private static IEnumerable<Guid> IdsInJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            yield break;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                yield break;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!Names.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                if (property.Value.ValueKind == JsonValueKind.String
                    && Guid.TryParse(property.Value.GetString(), out var id))
                {
                    yield return id;
                }
            }
        }
    }
}
