# Register Shopper Account

Status: Ready

A shopper registers an account.

`ShopperAccount` and `ShopperAccountState` live in `Aggregates/ShopperAccount/`.

`RegisterShopperAccountCommand` appends `ShopperAccountRegistered` on stream `ShopperAccount:{accountId}`.

The event stores the account id, user name, email, and name. The password is not on the event.

The login row is an ASP.NET Core Identity user in `identitylogindb`. The user id is the account id. The row stores the user name, the email, and the password hash. `PasswordHasher` writes the hash. The user name is the sign-in name.

The handler writes the event store first, then the login row.

1. It reserves the trimmed email on stream `ShopperEmail:{normalizedEmail}`. The key is the email in lower case.
2. It appends `ShopperAccountRegistered`.
3. It inserts the Identity user.

If the Identity insert fails, the events from steps 1 and 2 stay. The password hash is not stored. Sign in cannot succeed, because the login row is missing. The reserved email stays, so that email cannot be registered again.

A second register for that email, including a different account id or different letter case, is rejected with `EMAIL_ALREADY_REGISTERED` before either write.

A second user name is rejected with `USER_NAME_ALREADY_REGISTERED` before either write. ASP.NET Identity compares the name with its default normalizer, so the check is case-insensitive. That unique field is `NormalizedUserName`. Email uniqueness stays on the `ShopperEmail` stream.

A user name that the Identity user validator rejects is rejected with `USER_NAME_INVALID` before either write.

Two registrations with the same user name at the same moment can both pass the check. The second insert then fails with USER_NAME_ALREADY_REGISTERED after its events are written, and its email stays reserved.

A password that fails the default ASP.NET Identity password rules is rejected with `PASSWORD_REQUIREMENTS_NOT_MET` before either write.

POST `/api/identity/register-shopper-account`.

`RegisterShopperAccountCommand.ToString` redacts the password. The handler does not log the command or the password. LawnDart HTTP commands do not log the request body. The login context disables EF sensitive data logging.
