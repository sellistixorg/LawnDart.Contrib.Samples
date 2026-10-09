using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.WhenOrderStatusStockConfirmed;
using LawnDart.Samples.EShop.Shared.Events;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests;

public class PaymentProcessorSliceScanTests
{
    [Fact]
    public void Scan_finds_the_stock_confirmed_reactor()
    {
        var reactor = Assert.Single(EShopSlices.FindReactors(typeof(IPaymentGateway).Assembly));
        Assert.Equal(typeof(WhenOrderStatusStockConfirmedReactor), reactor.Reactor);
        Assert.Equal(typeof(OrderStatusStockConfirmed), reactor.Event);
        Assert.Empty(EShopSlices.FindEventProcessors(typeof(IPaymentGateway).Assembly));
    }
}
