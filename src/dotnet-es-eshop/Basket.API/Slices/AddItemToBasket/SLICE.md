# Add Item To Basket

Status: Ready

Shopper adds a catalog item line (or increases qty if already present).

`AddItemToBasketCommand` appends `BasketItemAdded` on stream `Basket:{buyerId}`.

`Basket` lives in `Aggregates/Basket/`. This slice adds `Handle` and `Apply` on that aggregate. `BasketItemAdded` lives there too, so later basket slices share one event type.

The basket is keyed by buyer id. Buyer id is the Identity account id.

A product already in the basket increases that line's quantity. The name, price, and picture stay as they were on the first add. The event quantity is the quantity after the add. A second line is not added.

Quantity below 1 is rejected with `BASKET_QUANTITY_INVALID`. The message is `Quantity must be at least 1 (eShop BasketItem validation).` A summed quantity that overflows `int` is rejected with the same code.

A negative unit price is rejected.

POST `/api/basket/add-item-to-basket`.

The cart page posts one unit here through Basket Contents.
