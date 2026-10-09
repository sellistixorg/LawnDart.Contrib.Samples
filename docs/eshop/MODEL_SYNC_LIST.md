# Model sync list

Merged slice code differs from the model exports in `docs/models/eshop/` in these places. Update the model to match the code.

- Add Item To Basket and Change Basket Item Quantity reject a bad quantity with `BASKET_QUANTITY_INVALID`. The model names that error `INVALID_BASKET_QUANTITY`.
- Change Basket Item Quantity and Remove Item From Basket reject an unknown basket with `BASKET_NOT_FOUND` and a missing line with `BASKET_ITEM_NOT_FOUND`. The Basket model defines neither error.
- Basket Contents stores the unit price from the add. It does not store OldUnitPrice.
- Basket Contents is the storefront page `/cart`. The model screen route is `/basket`.
- Browse Catalog is the storefront page `/`. The model screen route is `/catalog`.
- View Catalog Item is the storefront page `/item/{itemId}`. The model screen route is `/catalog/items/:id`.
- Catalog Item Detail (admin) folds Catalog Item Removed and returns `Removed`. The model view lists neither that event nor that field.
- Search Catalog folds Catalog Item Restocked and Catalog Stock Deducted. The model event list for that view does not include them.
- Search Catalog returns item id, name, description, price, picture URL, available stock, and `IsAvailable` on each row. The model lists no item summary fields.
- Cancel Order For Insufficient Stock uses `ORDER_NOT_FOUND`, `ORDER_ID_REQUIRED`, and `ORDER_REQUEST_ID_REQUIRED`. The model lists no error codes on that slice.
- Mark Order Awaiting Validation uses `ORDER_NOT_FOUND`, `ORDER_ID_REQUIRED`, and `ORDER_REQUEST_ID_REQUIRED`. The model lists no error codes on that slice.
- Mark Order Stock Confirmed uses `ORDER_NOT_FOUND`, `ORDER_ID_REQUIRED`, and `ORDER_REQUEST_ID_REQUIRED`. The model lists no error codes on that slice.
- Mark Order Stock Confirmed carries the buyer name on `OrderStatusStockConfirmed`. The model event has no buyer name field.
- Place Order uses subject-first codes such as `ORDER_ITEMS_EMPTY` and `ORDER_ADDRESS_REQUIRED`. The model lists no error codes on that slice.
- Place Order stores UserName, Street, State, ZipCode, card type, cardholder name, last four, expiry, and PaymentMethodId on `OrderPlaced`, and the address and card fields on order state (the user name is stored as Name). The model Order Placed event lists city and country, and not those fields.
- Charge Order Payment carries the currency code on `PaymentChargeRequested`. The model event has no currency field.
- Charge Order Payment uses `PAYMENT_COMMAND_ID_REQUIRED`, `PAYMENT_ATTEMPT_ID_REQUIRED`, `PAYMENT_ORDER_ID_REQUIRED`, and `PAYMENT_AMOUNT_INVALID`. The model lists no error codes on that slice.
