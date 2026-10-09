# Change Basket Item Quantity

Status: Ready

Shopper sets an explicit quantity on an existing basket line (part of eShop UpdateBasket dump).

`ChangeBasketItemQuantityCommand` appends `BasketItemQuantityChanged` on stream `Basket:{buyerId}`.

`Basket` lives in `Aggregates/Basket/`. This slice adds `Handle` and `Apply` for this command on that aggregate. The command, the handler, and `BasketItemQuantityChanged` stay in this slice.

The new quantity replaces the line quantity. It is not added to the current quantity. Name, price, and picture stay as they were. When the new quantity equals the current quantity, the command writes no event.

Quantity below 1 is rejected with `BASKET_QUANTITY_INVALID`. The message is `Quantity must be at least 1 (eShop BasketItem validation).` A quantity of 0 is rejected. The line stays. Remove Item From Basket removes a line. The WebApp will later send quantity 0 to Remove Item From Basket, as eShop's WebApp does when a cart line is set to 0.

An unknown basket is rejected with `BASKET_NOT_FOUND`. A product that is not on the basket is rejected with `BASKET_ITEM_NOT_FOUND`.

POST `/api/basket/change-basket-item-quantity`.

The cart page posts a quantity above 0 here through Basket Contents.
