# Clear Basket When Order Started

Status: Ready

Checkout handoff: when Ordering emits Order Started, issue Clear Basket. Maps to OrderStartedIntegrationEventHandler.

`ClearBasketWhenOrderStartedReactor` subscribes to `OrderStarted`. It issues the existing `ClearBasketCommand` for that buyer with reason `OrderStarted`. Clear Basket does not check the reason.

`OrderStarted.BuyerId` is the basket stream id. The stream is `Basket:{buyerId}`. Place Order writes that same buyer id on `OrderStarted`. Both are the Identity account id.

Clear Basket stays the command, the handler, and the `Handle` on `Aggregates/Basket/`. This slice does not copy them.

`AddEShopSlices` scans the Basket assembly and registers the reactor. `AddEShopLawnDart` already registers the sample RabbitMQ transport on the `basket` queue and the SQL inbox. The reactor subscribes to `OrderStarted` on that transport. The routing key is `order-started`. The inbox drops a second delivery of the same message id.

An empty buyer id is logged and skipped. A buyer with no basket is skipped, because Clear Basket rejects an unknown basket and this delivery must not be retried. A basket that is already empty is issued to Clear Basket, which writes no event. A redelivered Order Started does not fail.

Ordering emits `order-started` from Place Order. Place Order stays in Ordering.

The command endpoint stays Clear Basket. This slice adds the reactor only.
