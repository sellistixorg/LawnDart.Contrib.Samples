# Shopper Account Profile

Status: Ready

Read model of a shopper account for manage-account and checkout defaults. Subscribes to Registered and Profile Updated.

`ShopperAccountProfileProjection` folds `ShopperAccountRegistered` and `ShopperProfileUpdated` into `ShopperAccountProfileView`. One row per stream `ShopperAccount:{accountId}`.

Those event types stay in `Slices/RegisterShopperAccount/` and `Slices/UpdateShopperProfile/`. `ShopperAccount` stays in `Aggregates/ShopperAccount/`.

The password and its hash stay in `identitylogindb`. They stay off these events and off the view. The view keeps the account and address fields. It has no card fields.

GET `/views/shopper-account-profile?accountId={accountId}`.

An unknown account returns 404.

The header display name and the checkout address call this route through the `identity` HTTP client. The client base address is `EShopStorefrontLinks.IdentityUrl`. Each page reads `accountId` from its own query. `UserMenu` holds the display name for that request. Checkout passes that same query value into `GetShippingAddressAsync`. No profile data is kept app-wide. When the query has no account id, the header name is blank and the checkout address fields are blank.
