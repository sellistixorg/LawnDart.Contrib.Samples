using LawnDart;

namespace HarborStreet.Iteration.Commands;

public record AddTitleToCatalogCommand(Guid Id, Guid TitleId, string Name, decimal Price, bool IsScarce = false) : ICommand;
public record OpenWarehouseCommand(Guid Id, Guid WarehouseId, string Name) : ICommand;
public record RestockTitleAtWarehouseCommand(Guid Id, Guid WarehouseId, Guid TitleId, int Quantity) : ICommand;
public record PlaceOrderCommand(Guid Id, Guid OrderId, Guid CustomerId, Guid WarehouseId, Guid TitleId, int Quantity) : ICommand;
