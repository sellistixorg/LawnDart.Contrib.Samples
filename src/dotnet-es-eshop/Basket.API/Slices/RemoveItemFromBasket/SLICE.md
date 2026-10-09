# Remove Item From Basket

Status: Ready

Shopper removes a product line from the basket.

`RemoveItemFromBasketCommand` appends `BasketItemRemoved` on stream `Basket:{buyerId}`.

`Basket` lives in `Aggregates/Basket/`. This slice adds `Handle` and `Apply` for this command on that aggregate. The command, the handler, and `BasketItemRemoved` stay in this slice.

The line is removed entirely. Removing the last line leaves an empty basket. Add Item To Basket can use that basket again. Re-adding a removed product starts a fresh line with the new name, price, and picture.

An unknown basket is rejected with `BASKET_NOT_FOUND`. A product that is not on the basket is rejected with `BASKET_ITEM_NOT_FOUND`.

POST `/api/basket/remove-item-from-basket`.

The cart page sends quantity 0 here through Basket Contents, as eShop's WebApp does.
