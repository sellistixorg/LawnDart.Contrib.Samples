# Harbor Street Books event model v1

**Workspace export:** [harbor-street-v1.workspace.json](./harbor-street-v1.workspace.json). Eventhesis is a complementary modeling tool.
**Names:** [COMMANDS_AND_EVENTS.md](./COMMANDS_AND_EVENTS.md)
**Last-copy scenario:** [LAST_COPY_GWT.md](./LAST_COPY_GWT.md)

## Slices on the board (left to right)

| # | Slice | Primary command(s) | Key outcomes |
|---|---|---|---|
| 1 | Catalog | Add Title to Catalog, Restock Title, Mark Title Scarce | Title and stock facts; product catalog view |
| 2 | Cart | Open Cart, Add Title to Cart | Pre-commitment |
| 3 | Place Order | Place Order | Order Placed and Stock Reserved; error Title Unavailable; last-copy scenario |
| 4 | Capture Payment | Capture Payment; Cancel Order for Payment Failure | Follow-on; Stock Released on cancel |
| 5 | Ship | Start Fulfillment; Mark Order Shipped | Fulfillment after payment |

Not every name from `COMMANDS_AND_EVENTS.md` is a widget on the board (for example Abandon Cart, Update Title Price). The board covers the teaching path. The name list stays the inventory for later samples.

## Importing into Eventhesis

Eventhesis is a complementary modeling tool.

1. Create or open a workspace.
2. Import `harbor-street-v1.workspace.json`.
3. Keep this JSON as the source for the board. Re-export a figure from the tool when you want a picture of the board.

## Last-copy scenario on the board

Widget `gwt-last-copy-checkout` sits on the Place Order slice.

- When: Place Order
- Then (success): Order Placed, Stock Reserved
- Then (failure): Title Unavailable

The narrative is in `LAST_COPY_GWT.md`.
