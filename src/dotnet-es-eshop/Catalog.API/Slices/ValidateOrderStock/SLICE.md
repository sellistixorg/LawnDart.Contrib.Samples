# Validate Order Stock

Status: Ready

Catalog checks availability for order lines. One command with branching outcomes (Confirmed vs Rejected). Preferred over two separate commands. Maps to the AwaitingValidation handler.

`ValidateOrderStockCommand` reads each catalog item and appends one result on stream `OrderStockValidation:{orderId}`.

The model pins the command to Catalog Item (`catalog-item:{itemId}`). This repo stores the result on `OrderStockValidation`, keyed by order id. The check covers every line, and it does not change available stock, so the item stream gets no event. The stream id uses the aggregate type name.

POST `/api/catalog/validate-order-stock`.

Units for the same product are added, then compared with available stock. Available stock at least equal to that total counts as in stock. A removed item has Exists set to false. An unknown item has Exists set to false. Both count as no stock. Duplicate lines are not a command failure. The summed total decides Confirmed or Rejected.

When every line has stock, the command appends `OrderStockConfirmed` (`order-stock-confirmed`). Otherwise it appends `OrderStockRejected` (`order-stock-rejected`) with `ConfirmedOrderStockItems`: each product id and whether that line had stock. The SQL outbox copies that appended event in the same write. The When Catalog Stock Confirmed and When Catalog Stock Rejected slices consume these signals.

A checked order writes nothing on a repeat. The repeat does not fail. The first result stays, including when stock or the line list changes later. eShop checks stock again on every awaiting-validation message. This slice keeps the first result so a repeat does not publish a second signal. A concurrent repeat that loses the append reloads the result and returns.

An empty line list, an empty order id, an empty command id, an empty product id, or a quantity of zero or less is rejected before any write. The codes are `ORDER_STOCK_ITEMS_REQUIRED`, `ORDER_STOCK_ORDER_ID_REQUIRED`, `ORDER_STOCK_COMMAND_ID_REQUIRED`, `ORDER_STOCK_PRODUCT_ID_REQUIRED`, and `ORDER_STOCK_UNITS_INVALID`.

The When Order Awaiting Validation Validate Stock slice issues this command. This command does not subscribe to that signal. A command that fails writes no stock result. The order stays in AwaitingValidation.
