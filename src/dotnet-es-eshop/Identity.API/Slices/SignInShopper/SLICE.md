# Sign In Shopper

Status: Ready

A shopper signs in with a user name and a password.

`SignInShopperCommand` checks the password against the Identity login row, then appends `ShopperSignedIn` on stream `ShopperAccount:{accountId}`. The account id is the login row id.

`ShopperAccount` lives in `Aggregates/ShopperAccount/`. This slice adds `Handle` and `Apply` on that aggregate.

The login row stays in `identitylogindb`. This slice adds no credential model. The password and its hash stay off the event.

Sign-in finds the user by name. An email address is not accepted. It then calls `CheckPasswordSignInAsync` with lockout on failure. eShop `AccountController.Login` calls `PasswordSignInAsync(userName, password, rememberLogin, lockoutOnFailure: true)`. The check here is that password check and that lockout. Identity keeps its default lockout: 5 failures, then 5 minutes.

A wrong user name and a wrong password both fail with `INVALID_CREDENTIALS`. The message is `User name or password is incorrect, or account is locked/disabled.` A locked account returns that same error. The response does not say which check failed.

POST `/api/identity/sign-in-shopper`. A successful call is still 202. The body is the account id, the display name, the access token, and the token expiry. The display name is the given name and the family name. When those are blank, it is the login name. The access token is a signed JWT. `sub` is the account id. `name` is the display name. The token lasts one hour. A rejected call stays 422 and the body stays the problem details. The handler does not put the password in that body or in the token.

`SignInShopperCommand.ToString` redacts the password. The handler does not log the command or the password.
