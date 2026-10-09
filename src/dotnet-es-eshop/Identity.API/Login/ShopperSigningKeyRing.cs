using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// Loads shopper signing keys from the login database, or creates the single shared key.
/// </summary>
public sealed class ShopperSigningKeyRing
{
    /// <summary>
    /// Primary key of the one shared signing key. A second insert with this id fails and reloads the first row.
    /// </summary>
    public const string SlotId = "shopper-access";

    private readonly IServiceScopeFactory _scopes;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<RsaSecurityKey> _keys = [];

    /// <summary>
    /// Creates the ring.
    /// </summary>
    /// <param name="scopes">Scopes used to open the login database.</param>
    public ShopperSigningKeyRing(IServiceScopeFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        _scopes = scopes;
    }

    /// <summary>
    /// Returns every stored public key and keeps the shared signing key in memory.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The stored keys. The shared slot is first when it exists.</returns>
    /// <remarks>
    /// The private keys stay in the login database. Two hosts that start together both try to insert
    /// <see cref="SlotId"/>. The second insert fails and both hosts sign with the row that won.
    /// Do not log the keys.
    /// </remarks>
    public async Task<IReadOnlyList<RsaSecurityKey>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_keys.Count > 0)
            return _keys;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_keys.Count > 0)
                return _keys;

            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (await TryLoadAsync(db, cancellationToken).ConfigureAwait(false))
                return _keys;

            var created = ShopperAccessTokens.CreateKey();
            created.KeyId = SlotId;
            db.ShopperSigningKeys.Add(new ShopperSigningKey
            {
                Id = SlotId,
                Pkcs8PrivateKey = ShopperAccessTokens.ExportPkcs8(created),
                CreatedAtUtc = DateTime.UtcNow
            });
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
            }

            if (!await TryLoadAsync(db, cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("Shopper signing key was not stored.");

            return _keys;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Copies the loaded public keys into <paramref name="validation"/> for bearer checks.
    /// </summary>
    /// <param name="validation">In-memory key list used by Identity.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>A task that completes when the list is replaced.</returns>
    public async Task PublishAsync(ShopperValidationKeys validation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validation);
        var keys = await GetAsync(cancellationToken).ConfigureAwait(false);
        validation.Replace(keys);
    }

    private async Task<bool> TryLoadAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var rows = await db.ShopperSigningKeys
            .OrderBy(key => key.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count == 0)
            return false;

        var slot = rows.FirstOrDefault(row => row.Id == SlotId);
        var ordered = slot is null
            ? rows
            : new[] { slot }.Concat(rows.Where(row => row.Id != SlotId));
        _keys = ordered.Select(row => ShopperAccessTokens.ImportPkcs8(row.Pkcs8PrivateKey, row.Id)).ToList();
        return true;
    }
}
