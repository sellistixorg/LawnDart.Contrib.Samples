# Basket Contents

Status: Ready

Shopper read model of current basket lines and totals (gRPC GetBasket / WebApp basket UI).

`BasketContentsProjection` folds `BasketItemAdded`, `BasketItemQuantityChanged`, `BasketItemRemoved`, and `BasketCleared` into one row per stream `Basket:{buyerId}`.

Those event types stay where they already are. `Basket` stays in `Aggregates/Basket/`.

GET `/views/basket-contents?buyerId=`.

A missing or empty buyer id returns 400. An unknown buyer returns an empty basket. A basket with no lines returns an empty basket.

Prices stay at the values captured on add. This view does not read Catalog Item Price Changed. A quantity change keeps that unit price and picture. Item count is the total units. Subtotal is the sum of unit price times quantity. A line total is that line's unit price times its quantity.

The cart page and the header bag call this route through the `basket` HTTP client. Buyer id is the `accountId` query on the page. Add posts one unit to `/api/basket/add-item-to-basket`. A quantity above 0 posts to `/api/basket/change-basket-item-quantity`. A quantity of 0 posts to `/api/basket/remove-item-from-basket`. The page has no Clear button.

A signed-out shopper sees an empty bag. The cart page links to sign-in. A signed-in shopper with no account id sees that the account is missing, and the item page links to sign-in.

A refused connection, bad JSON, a timeout, or an unsupported address shows that the basket is temporarily unavailable. A rejected command shows a short message and reloads the bag. A concurrency conflict says the bag changed and asks the shopper to try again. It does not show the conflict body. Cancellation of the component token still throws. The client keeps no basket lines.
