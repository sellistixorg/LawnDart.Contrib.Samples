using HarborStreet.Iteration.Commands;
using HarborStreet.Iteration.Domain;
using LawnDart.Aggregates;
using LawnDart.Dcb;
using LawnDart.EventStore;

namespace HarborStreet.Iteration.Services;

public enum WarehouseCheckoutStatus
{
    Succeeded,
    RejectedUnavailable,
    ConflictLost
}

public sealed record WarehouseCheckoutOutcome(WarehouseCheckoutStatus Status, Guid OrderId, Guid WarehouseId);

/// <summary>v2 Place Order at a warehouse via DCB on title + warehouse tags.</summary>
public sealed class MultiWarehouseCheckout
{
    private readonly IAggregateRepository _aggregates;
    private readonly IDcbRepository _dcb;

    public MultiWarehouseCheckout(IAggregateRepository aggregates, IDcbRepository dcb)
    {
        _aggregates = aggregates;
        _dcb = dcb;
    }

    public async Task EnsureTitleAsync(Guid titleId, string name, decimal price, bool scarce = true,
        CancellationToken cancellationToken = default)
    {
        var title = await _aggregates.GetOrCreateAsync<Title>(titleId, cancellationToken);
        if (!title.State.Exists)
        {
            await _aggregates.HandleCommandAsync(
                title,
                new AddTitleToCatalogCommand(Guid.NewGuid(), titleId, name, price, scarce),
                cancellationToken: cancellationToken);
        }
    }

    public async Task OpenWarehouseAsync(Guid warehouseId, string name, CancellationToken cancellationToken = default)
    {
        var wh = await _aggregates.GetOrCreateAsync<Warehouse>(warehouseId, cancellationToken);
        if (!wh.State.Exists)
        {
            await _aggregates.HandleCommandAsync(
                wh,
                new OpenWarehouseCommand(Guid.NewGuid(), warehouseId, name),
                cancellationToken: cancellationToken);
        }
    }

    public async Task RestockAsync(Guid warehouseId, Guid titleId, int quantity,
        CancellationToken cancellationToken = default)
    {
        var tags = WarehouseInventoryDecision.BoundaryTags(warehouseId, titleId);
        var entity = await _dcb.GetOrCreateEntityAsync<WarehouseInventoryDecision>(tags, cancellationToken);
        await _dcb.HandleCommandAsync(
            entity,
            new RestockTitleAtWarehouseCommand(Guid.NewGuid(), warehouseId, titleId, quantity),
            cancellationToken: cancellationToken);
    }

    public async Task<WarehouseCheckoutOutcome> PlaceOrderAsync(
        Guid orderId,
        Guid customerId,
        Guid warehouseId,
        Guid titleId,
        int quantity = 1,
        WarehouseInventoryDecision? preloaded = null,
        CancellationToken cancellationToken = default)
    {
        var tags = WarehouseInventoryDecision.BoundaryTags(warehouseId, titleId);
        var entity = preloaded
            ?? await _dcb.GetOrCreateEntityAsync<WarehouseInventoryDecision>(tags, cancellationToken);

        try
        {
            await _dcb.HandleCommandAsync(
                entity,
                new PlaceOrderCommand(Guid.NewGuid(), orderId, customerId, warehouseId, titleId, quantity),
                cancellationToken: cancellationToken);

            return new WarehouseCheckoutOutcome(WarehouseCheckoutStatus.Succeeded, orderId, warehouseId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return new WarehouseCheckoutOutcome(WarehouseCheckoutStatus.RejectedUnavailable, orderId, warehouseId);
        }
        catch (ConcurrencyException)
        {
            return new WarehouseCheckoutOutcome(WarehouseCheckoutStatus.ConflictLost, orderId, warehouseId);
        }
    }
}
