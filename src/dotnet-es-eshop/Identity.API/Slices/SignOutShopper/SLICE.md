# Sign Out Shopper

Status: Ready

A shopper ends the authenticated session.

`ShopperAccount` lives in `Aggregates/ShopperAccount/`. This slice adds `Handle` and `Apply` on that aggregate.

`SignOutShopperCommand` appends `ShopperSignedOut` on stream `ShopperAccount:{accountId}`.

The account must already exist and be active. A missing or inactive account is rejected with `SHOPPER_ACCOUNT_NOT_FOUND`. The message is `Shopper account does not exist.` The model defines no error code on this slice.

POST `/api/identity/sign-out-shopper`.

`LogoutId` is accepted on the command and is not applied. It is not written on the event.
