# When Order Awaiting Validation Validate Stock

Status: Ready

Automation: Ordering Order Awaiting Validation → issues Validate Order Stock.

`WhenOrderAwaitingValidationValidateStockReactor` subscribes to `OrderAwaitingValidation`. It issues the existing `ValidateOrderStockCommand` with that order id and each line's product id and units.

The command id is a version 5 GUID from the order id (`validate-order-stock:{orderId}`). A redelivery of the same order is the same command. Validate Order Stock stays the command, the handler, and the check on `OrderStockValidation:{orderId}`. This slice does not copy them. The first result stays. A repeat does not fail.

`AddEShopSlices` scans the Catalog assembly and registers the reactor. `AddEShopLawnDart` already registers the sample RabbitMQ transport on the `catalog` queue (`eshop.catalog`) and the SQL inbox. The AppHost `catalog-api` project references `eventbus`. The reactor subscribes to `OrderAwaitingValidation` on that transport. The routing key is `order-awaiting-validation`, the token on the shared event. Ordering publishes that same token from Mark Order Awaiting Validation. The inbox drops a second delivery of the same message id.

An empty order id is logged and skipped. An empty line list is logged and skipped. Invalid stock input (an empty product id, or a quantity of zero or less) is logged and skipped before the command is issued. Store failures are not caught in the reactor. The reactor host rethrows them, and the transport retries.

This slice adds the reactor only. Validate Order Stock keeps `POST /api/catalog/validate-order-stock`. A skipped command, or a command that fails, writes no stock result. The order stays in AwaitingValidation.
