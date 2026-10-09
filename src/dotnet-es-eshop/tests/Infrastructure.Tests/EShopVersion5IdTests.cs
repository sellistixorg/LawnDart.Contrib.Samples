using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class EShopVersion5IdTests
{
    [Fact]
    public void Known_names_keep_their_version_5_ids()
    {
        var orderId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        Assert.Equal(
            new Guid("2ed6657d-e927-568b-95e1-2665a8aea6a2"),
            EShopVersion5Id.Create(EShopVersion5Id.DnsNamespace, "www.example.com"));
        Assert.Equal(
            new Guid("4125ff5f-8d50-502b-9496-f8f5719513c0"),
            EShopVersion5Id.ForPrefixedName("grace-period-confirmed", orderId.ToString("D")));
        Assert.Equal(
            new Guid("3d6379f2-d875-5f66-998c-b0b1889c482e"),
            EShopVersion5Id.ForPrefixedName("payment-succeeded", "msg-1"));
        Assert.Equal(
            new Guid("5dbfb7b2-e452-52f7-8a24-f111bc319fc1"),
            EShopVersion5Id.ForPrefixedName("payment-attempt", "msg-1"));
        Assert.Equal(
            new Guid("3520dfe3-9ea3-5bc4-8e3d-31467366de2b"),
            EShopVersion5Id.ForPrefixedName("order-paid", "msg-1"));
        Assert.Equal(
            GracePeriodConfirmed.IdFor(orderId),
            EShopVersion5Id.ForPrefixedName("grace-period-confirmed", orderId.ToString("D")));
    }
}
