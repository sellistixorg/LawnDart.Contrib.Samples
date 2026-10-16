# WebApp

Blazor storefront with interactive server rendering. Layout, CSS, and shared components in this project are the screen source of truth.

Aspire HTTP clients `catalog`, `basket`, and `ordering` point at `https+http://catalog-api`, `https+http://basket-api`, and `https+http://ordering-api`. When `IdentityUrl` is an absolute URL, the `identity` client uses that address. Health checks stay on `/health` and `/alive` in Development.

Sign-in posts `/api/identity/sign-in-shopper`. A successful body has the account id, the display name, an access token, and the token expiry. Those values go into the authentication cookie. The cookie is HttpOnly. The token is not written into the page. Basket, Ordering, and Identity calls send `Authorization: Bearer` with that token. An expired token drops the cookie. Cart, checkout, and orders then redirect to `/user/login`. Sign-out posts `/api/identity/sign-out-shopper` and clears the cookie. A return path must be local. An absolute or off-site URL goes to `/`. The catalog, search, and item pages stay anonymous. The header reads the display name from the cookie. Checkout reads the address from `GET /views/shopper-account-profile` on the `identity` client, using that account id. No profile data is kept app-wide.

The catalog page calls `GET /views/browse-catalog` and `GET /views/catalog-brands-and-types` on the `catalog` client. Rows, paging, and facet counts come from Browse Catalog. Brand and type names come from Catalog Brands And Types. Brand and type ids are Guids. `BrowseCatalogClient` keeps no page. Each visit reads `page`, `brand`, and `type` from that request. A refused connection, bad JSON, a timeout, or an unsupported address shows that the catalog is temporarily unavailable. Cancellation of the component token still throws. Product links use `/item/{guid}`.

The item page calls `GET /views/view-catalog-item/{itemId}` on the `catalog` client. The item id is a Guid. `ViewCatalogItemClient` keeps no item data. A refused connection, bad JSON, a timeout, or an unsupported address shows that the catalog is temporarily unavailable. An unknown item stays on the not-found state. Cancellation of the component token still throws.

The search page calls `GET /views/search-catalog` on the `catalog` client. `SearchCatalogClient` keeps no search result. Each visit reads `text` and `page` from that request. A refused connection, bad JSON, a timeout, or an unsupported address shows that the catalog is temporarily unavailable. Cancellation of the component token still throws. Result links use `/item/{guid}`.

The cart page and the header bag call `GET /views/basket-contents?buyerId=` on the `basket` client. Buyer id is the account id from the cookie. `BasketContentsClient` keeps no basket lines. A signed-out visit to the cart redirects to sign-in. The header bag stays empty until someone is signed in. Add posts one unit to `/api/basket/add-item-to-basket`. A quantity above 0 posts to `/api/basket/change-basket-item-quantity`. A quantity of 0 posts to `/api/basket/remove-item-from-basket`. A bag write reloads the header bag for that shopper. The reload runs on the header component's renderer. A listener that throws is logged and does not fail the write. A line total is unit price times quantity. The bag total is the sum of those lines. A refused connection, bad JSON, a timeout, or an unsupported address shows that the basket is temporarily unavailable. A rejected command shows a short message and reloads the bag. A concurrency conflict says the bag changed and asks the shopper to try again. It does not show the conflict body. Cancellation of the component token still throws.

Checkout posts `/api/ordering/place-order` on the `ordering` client. Buyer id is the account id from the cookie. A signed-out visit redirects to sign-in. Order lines are the basket lines, with the price captured in the basket. The shipping address is the form, prefilled from Shopper Account Profile. The request id is set when the page loads and posted as `Info.RequestId`. The buyer user name is the account `UserName`, read from Shopper Account Profile at submit. On success the page goes to `/user/orders?placed={orderId}`. The page does not clear the basket. The card payload is still the stand-in card. A refused connection, bad JSON, a timeout, or an unsupported address on the order post shows that ordering is temporarily unavailable. A basket read that fails during submit shows that the basket is temporarily unavailable. A rejected command shows a short message. A concurrency conflict says the order could not be saved and asks the shopper to try again. It does not show the conflict body. Cancellation of the component token still throws. `CheckoutClient` keeps no shopper data.

The orders page calls `GET /views/orders-by-shopper?buyerId=` on the `ordering` client. Buyer id is the account id from the cookie. `OrdersByShopperClient` keeps no rows. A signed-out visit redirects to sign-in. A shopper with no orders sees the empty orders state. Rows are newest first. Each row shows the order id, the date, the status, and the total. A refused connection, bad JSON, a timeout, or an unsupported address shows that ordering is temporarily unavailable. Cancellation of the component token still throws.

Checkout goes to `/user/orders?placed={orderId}`. On that first load, when the order is missing, the page reads the list again. It tries 10 times, 250 milliseconds apart. A later status message reads the list once.

`OrdersRefreshOnStatusChange` subscribes for the signed-in shopper. The callback runs on the renderer that subscribed. A listener that throws is logged. The subscription is disposed with the circuit.

Seven messages refresh the list. Order Started, Order Awaiting Validation, Order Status Stock Confirmed, and Order Paid carry the buyer id. A message for another shopper leaves this page alone. Order Shipped, Order Cancelled, and Order Cancelled For Stock carry the order id. The page keeps the order ids from its own latest read. A shipped or cancelled message refreshes the page only when that read contains the order id. An order id the page did not load leaves the page alone.

The storefront listens on the message transport. When `eventbus` is set, that transport is `LawnDart.Messaging.RabbitMQ`. The exchange is `eshop.integration`. Each order status event has its own queue, named `{event-type}.webapp`. Every WebApp instance uses the subscription name `webapp`, so each message reaches one instance. When `eventbus` is missing, the transport is in-memory. WebApp has no SQL database, so it does not call `AddEShopLawnDart`.

A catalog picture with no URL uses `images/example-product.svg`. Order detail stays on `GET /views/order-summary/{orderNumber}` until a page is added. Screen rules: `.cursor/rules/eshop-webapp.mdc`.

## Upstream files

Presentation files under `Components/Layout`, `Components/Pages`, `Components/Catalog`, `wwwroot/css`, `wwwroot/fonts`, `wwwroot/icons`, `wwwroot/images`, and `wwwroot/js` come from [dotnet/eShop](https://github.com/dotnet/eShop) commit `dc7ea499cd356924fb6689b3702964a5869dbae9` (`src/WebApp` and `src/WebAppComponents`). The file list and MIT license text are in `THIRD-PARTY-NOTICES.md`.

`Components/Chatbot/`, `wwwroot/js/chat-dock.js`, `wwwroot/js/chat-eyes.js`, `wwwroot/js/chat-fab.js`, `wwwroot/chat.png`, and `wwwroot/chatbot.png` are copied and unused. The layout does not render them. The page does not load their scripts or styles.

## Placeholder map

| Page | Placeholder | View slice |
|---|---|---|
| `/` catalog list and filters | catalog client `BrowseCatalogClient` | BrowseCatalog. Names from Catalog Brands And Types |
| `/catalog/search` | catalog client `SearchCatalogClient` | SearchCatalog |
| `/item/{itemId}` | catalog client `ViewCatalogItemClient` | ViewCatalogItem |
| `/cart` and the header bag | basket client `BasketContentsClient` | BasketContents. Buyer id is the cookie account id |
| `/checkout` | ordering client `CheckoutClient` | ShopperAccountProfile for the address (`identity` client, cookie account id). Place Order for the submit. BasketContents for the bag (`basket` client) |
| `/user/orders` | ordering client `OrdersByShopperClient` | OrdersByShopper. Buyer id is the cookie account id. OrderSummary is `GET /views/order-summary/{orderNumber}` when a detail page is added |
| Header account menu, `/user/login`, `/user/logout` | cookie authentication | Sign In Shopper and Sign Out Shopper. The display name is a cookie claim |
