# Update Shopper Profile

Status: Ready

A shopper updates the name, phone, and default shipping address on an existing account.

`ShopperAccount` and `ShopperAccountState` live in `Aggregates/ShopperAccount/`. This slice adds `Handle` and `Apply` on that aggregate.

`UpdateShopperProfileCommand` appends `ShopperProfileUpdated` on stream `ShopperAccount:{accountId}`.

Email and user name stay as they were when the account was registered.

POST `/api/identity/update-shopper-profile`.

Update rejects an account id that is not on the stream.
