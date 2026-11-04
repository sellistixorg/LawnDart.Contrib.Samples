using LawnDart;

namespace HarborStreet.Coordination.Commands;

public record AddTitleToCatalogCommand(Guid Id, Guid TitleId, string Name, decimal Price, bool IsScarce = false) : ICommand;
public record RestockTitleCommand(Guid Id, Guid TitleId, int Quantity) : ICommand;
public record ReserveStockCommand(Guid Id, Guid TitleId, Guid OrderId, int Quantity) : ICommand;
public record ReleaseStockCommand(Guid Id, Guid TitleId, Guid OrderId, int Quantity) : ICommand;

public record PlaceOrderCommand(Guid Id, Guid OrderId, Guid CustomerId, Guid TitleId, int Quantity) : ICommand;
public record CapturePaymentCommand(Guid Id, Guid OrderId, string PaymentReference) : ICommand;
public record RecordPaymentFailureCommand(Guid Id, Guid OrderId, string Reason) : ICommand;
public record CancelOrderForPaymentFailureCommand(Guid Id, Guid OrderId, string Reason) : ICommand;
public record StartFulfillmentCommand(Guid Id, Guid OrderId) : ICommand;
