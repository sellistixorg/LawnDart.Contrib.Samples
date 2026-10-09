# Place Order

Status: Ready

Checkout: shopper submits basket as an order (Submitted). Emits Order Placed and Order Started (basket-clear signal for Basket automation).

`PlaceOrderCommand` appends `OrderPlaced` and `OrderStarted` on stream `Order:{orderId}`. The model writes the stream key as `order:{orderId}`. This repo uses the aggregate type name, matching `Basket:{buyerId}`.

`Order` lives in `Aggregates/Order/`. Later Ordering slices add `Handle` and `Apply` on that aggregate. `OrderPlaced` stays in this slice. `OrderStarted` lives in `Shared/Events/` so Basket can subscribe without referencing Ordering.API.

The order id is the client request id. `CommandId` and `OrderId` must match. That id is the eShop `x-requestid` (`IdentifiedCommand`). A second Place Order with the same id and different content is ignored. The first order stays. The caller still gets 202. Order Started is not published again.

Buyer id is the Identity account id.

Order lines are the basket lines at submit. The price is the price captured in the basket.

The shipping address is the checkout form, prefilled from Shopper Account Profile. Street, city, country, and postal code are required. State is optional.

Events keep the card type, the cardholder name, the expiry, and the last four digits. The command has no security code. A number that is not four digits is rejected with `ORDER_CARD_NUMBER_REJECTED`. The checkout page sets card type 1. The client sends the name TESTUSER, an expiry one year ahead, and the last four `4444`.

The new order status is `Submitted`, eShop's initial status.

POST `/api/ordering/place-order`.

`OrderStarted` uses token `order-started`. It carries the order id and the buyer id. The SQL outbox copies every appended event, including this one, and the RabbitMQ transport publishes it. Clear Basket When Order Started subscribes to this signal. The WebApp does not clear the basket.

The checkout page posts this command on the `ordering` client. Buyer id is the `accountId` query on that page. The request id is set when the page loads and posted as `Info.RequestId`. The buyer user name is the account `UserName` from Shopper Account Profile, read at submit. On success the page goes to `/user/orders` and keeps `accountId`. A refused connection, bad JSON, a timeout, or an unsupported address on the order post shows "Ordering is temporarily unavailable." A basket read that fails during submit shows "Basket is temporarily unavailable." A 409 shows "The order could not be saved. Please try again." Known rejection codes map to short text. The page does not show raw server text.
