# Clear Basket

Status: Ready

Empties the shopper basket. It is the explicit clear and the checkout handoff target. It maps to gRPC DeleteBasket.

`ClearBasketCommand` appends `BasketCleared` on stream `Basket:{buyerId}`.

`Basket` lives in `Aggregates/Basket/`. This slice adds `Handle` and `Apply` for this command on that aggregate. The command, the handler, and `BasketCleared` stay in this slice.

Clearing removes every line. A cleared basket can be used again by Add Item To Basket. Add starts fresh lines.

Clearing a basket that is already empty writes no event. That matches the unchanged-quantity rule.

An unknown basket is rejected with `BASKET_NOT_FOUND`. An empty buyer id is rejected.

Reason may be `UserCleared`, `OrderStarted`, or `Other`.

POST `/api/basket/clear-basket`.

The WebApp has no Clear button, as in eShop. Clear Basket When Order Started is a later automation. It will reuse this command or event.
