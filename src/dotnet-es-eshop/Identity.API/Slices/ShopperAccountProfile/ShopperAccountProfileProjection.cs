using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Sdk;
using LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Slices.UpdateShopperProfile;

namespace LawnDart.Samples.EShop.Identity.API.Slices.ShopperAccountProfile;

/// <summary>
/// Read model of a shopper account for manage-account and checkout defaults. Subscribes to Registered and Profile Updated.
/// </summary>
/// <remarks>
/// The password and its hash stay in the login store. They are not on
/// <see cref="ShopperAccountRegistered"/> or <see cref="ShopperProfileUpdated"/>, and this projection does not write them.
/// </remarks>
[SingleStreamProjection("ShopperAccountProfile", streamType: "ShopperAccount", tenantScope: TenantScope.TenantScoped)]
public sealed class ShopperAccountProfileProjection : ProjectionBase<ShopperAccountProfileView>
{
    /// <summary>
    /// View-store key for one shopper account stream.
    /// </summary>
    public const string StorageKey = "ShopperAccountProfile:v1";

    /// <summary>
    /// View instance id for <paramref name="accountId"/>.
    /// </summary>
    /// <param name="accountId">Account id.</param>
    /// <returns>The stream id <c>ShopperAccount:{accountId}</c>.</returns>
    public static string InstanceId(Guid accountId) => $"ShopperAccount:{accountId}";

    /// <summary>
    /// A shopper account was created. Tagged: shopper-account:{accountId}.
    /// </summary>
    /// <param name="registered">Shopper Account Registered.</param>
    /// <remarks>
    /// The password and its hash are not on this event and are not written to the view.
    /// </remarks>
    public void Handle(ShopperAccountRegistered registered)
    {
        State.AccountId = registered.AccountId;
        State.UserName = registered.UserName;
        State.Email = registered.Email;
        State.Name = registered.Name;
        State.LastName = registered.LastName;
        State.IsActive = true;
    }

    /// <summary>
    /// Shopper profile fields changed.
    /// </summary>
    /// <param name="updated">Shopper Profile Updated.</param>
    /// <remarks>
    /// Email and user name stay as they were when the account was registered.
    /// The password and its hash are not on this event and are not written to the view.
    /// </remarks>
    public void Handle(ShopperProfileUpdated updated)
    {
        State.AccountId = updated.AccountId;
        State.Name = updated.Name;
        State.LastName = updated.LastName;
        State.PhoneNumber = updated.PhoneNumber;
        State.Street = updated.Street;
        State.City = updated.City;
        State.State = updated.State;
        State.Country = updated.Country;
        State.ZipCode = updated.ZipCode;
    }
}
