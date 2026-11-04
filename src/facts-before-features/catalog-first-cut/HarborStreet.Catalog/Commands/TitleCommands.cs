using LawnDart;

namespace HarborStreet.Catalog.Commands;

/// <summary>Add Title to Catalog</summary>
public record AddTitleToCatalogCommand(
    Guid Id,
    Guid TitleId,
    string Name,
    decimal Price,
    bool IsScarce = false) : ICommand;

/// <summary>Update Title Price</summary>
public record UpdateTitlePriceCommand(
    Guid Id,
    Guid TitleId,
    decimal NewPrice) : ICommand;

/// <summary>Restock Title</summary>
public record RestockTitleCommand(
    Guid Id,
    Guid TitleId,
    int Quantity) : ICommand;

/// <summary>Mark Title Scarce</summary>
public record MarkTitleScarceCommand(
    Guid Id,
    Guid TitleId) : ICommand;
