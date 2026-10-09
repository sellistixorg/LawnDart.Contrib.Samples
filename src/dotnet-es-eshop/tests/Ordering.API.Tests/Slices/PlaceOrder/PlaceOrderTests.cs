using System.Text.Json;
using LawnDart;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.PlaceOrder;

public class PlaceOrderTests
{
    [Fact]
    public async Task Place_order_emits_order_placed_and_order_started()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var capId = Guid.NewGuid();
        var expires = new DateTime(2028, 4, 1, 0, 0, 0, DateTimeKind.Utc);

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Valid(orderId, buyerId) with
            {
                UserName = "Ada Lovelace",
                Street = "1 Main",
                City = "Seattle",
                State = "WA",
                Country = "USA",
                ZipCode = "98101",
                CardTypeId = 1,
                CardHolderName = "Ada Lovelace",
                MaskedCardNumber = "4444",
                CardExpiration = expires,
                OrderItems =
                [
                    new OrderLine(bootId, "Alpine boot", 120m, 2),
                    new OrderLine(capId, "Wool cap", 25m, 1)
                ]
            })
            .ThenEmittedTypes(typeof(OrderPlaced), typeof(OrderStarted))
            .ThenEmittedEvent<OrderPlaced>(e =>
                e.OrderId == orderId
                && e.BuyerId == buyerId
                && e.UserName == "Ada Lovelace"
                && e.OrderStatus == OrderAggregate.SubmittedStatus
                && e.Street == "1 Main"
                && e.City == "Seattle"
                && e.State == "WA"
                && e.Country == "USA"
                && e.ZipCode == "98101"
                && e.CardTypeId == 1
                && e.CardHolderName == "Ada Lovelace"
                && e.MaskedCardNumber == "4444"
                && e.CardExpiration == expires
                && e.OrderItems.Count == 2
                && e.OrderItems[0].ProductId == bootId
                && e.OrderItems[0].UnitPrice == 120m
                && e.OrderItems[0].Units == 2
                && e.OrderItems[1].ProductId == capId)
            .ThenEmittedEvent<OrderStarted>(e => e.OrderId == orderId && e.BuyerId == buyerId)
            .ThenAppendedOnlyTo($"Order:{orderId}")
            .AndAssert(result =>
            {
                var state = result.Aggregate.State;
                Assert.Equal(orderId, state.OrderId);
                Assert.Equal(buyerId, state.BuyerId);
                Assert.Equal(buyerId, state.IdentityGuid);
                Assert.Equal("Ada Lovelace", state.Name);
                Assert.Equal(OrderAggregate.SubmittedStatus, state.OrderStatus);
                Assert.NotNull(state.OrderDate);
                Assert.Equal("1 Main", state.Street);
                Assert.Equal("Seattle", state.City);
                Assert.Equal("WA", state.State);
                Assert.Equal("USA", state.Country);
                Assert.Equal("98101", state.ZipCode);
                Assert.Equal(2, state.OrderItems.Count);
                Assert.Equal("4444", state.MaskedCardNumber);
                Assert.Equal(4, state.MaskedCardNumber.Length);
                Assert.NotEqual(Guid.Empty, state.PaymentMethodId);
                Assert.Equal(orderId, state.RequestId);
                Assert.True(state.Started);
                Assert.Null(state.Description);
                var json = JsonSerializer.Serialize(result.EmittedEvents);
                Assert.DoesNotContain("CardSecurity", json, StringComparison.Ordinal);
                Assert.DoesNotContain("1111222233334444", json, StringComparison.Ordinal);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_allows_a_blank_state()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Valid(orderId, buyerId) with { State = "  " })
            .ThenEmittedEvent<OrderPlaced>(e => e.State is null && e.City == "Seattle")
            .AndAssert(result => Assert.Null(result.Aggregate.State.State))
            .RunAsync();
    }

    [Fact]
    public async Task Second_place_order_for_the_same_request_writes_no_events()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var first = Valid(orderId, buyerId);

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .Given(Placed(first))
            .Given(Started(orderId, buyerId))
            .When(first with { Street = "Other street", MaskedCardNumber = "1111222233334444" })
            .AndAssert(result =>
            {
                Assert.Empty(result.EmittedEvents);
                Assert.Equal("1 Main", result.Aggregate.State.Street);
                Assert.Equal("4444", result.Aggregate.State.MaskedCardNumber);
                Assert.Equal(buyerId, result.Aggregate.State.BuyerId);
                Assert.True(result.Aggregate.State.Started);
            })
            .RunAsync();
    }

    [Theory]
    [InlineData(nameof(PlaceOrderCommand.CommandId), OrderAggregate.RequestIdRequiredTitle)]
    [InlineData(nameof(PlaceOrderCommand.OrderId), OrderAggregate.OrderIdRequiredTitle)]
    [InlineData(nameof(PlaceOrderCommand.BuyerId), OrderAggregate.BuyerIdRequiredTitle)]
    public async Task Place_order_rejects_an_empty_id(string field, string title)
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var command = Valid(orderId, buyerId);
        command = field switch
        {
            nameof(PlaceOrderCommand.CommandId) => command with { CommandId = Guid.Empty },
            nameof(PlaceOrderCommand.OrderId) => command with { OrderId = Guid.Empty },
            _ => command with { BuyerId = Guid.Empty }
        };

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(command)
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal(title, Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_rejects_an_order_id_that_does_not_match_the_request_id()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Valid(orderId, Guid.NewGuid()) with { CommandId = Guid.NewGuid() })
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal(
                    OrderAggregate.RequestIdMismatchTitle,
                    Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_rejects_a_blank_user_name()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Valid(orderId, Guid.NewGuid()) with { UserName = " " })
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal(
                    OrderAggregate.UserNameRequiredTitle,
                    Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    [Theory]
    [InlineData(nameof(PlaceOrderCommand.Street))]
    [InlineData(nameof(PlaceOrderCommand.City))]
    [InlineData(nameof(PlaceOrderCommand.Country))]
    [InlineData(nameof(PlaceOrderCommand.ZipCode))]
    public async Task Place_order_rejects_a_missing_address(string field)
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var command = Valid(orderId, Guid.NewGuid());
        command = field switch
        {
            nameof(PlaceOrderCommand.Street) => command with { Street = " " },
            nameof(PlaceOrderCommand.City) => command with { City = "" },
            nameof(PlaceOrderCommand.Country) => command with { Country = " " },
            _ => command with { ZipCode = "" }
        };

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(command)
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal(
                    OrderAggregate.AddressRequiredTitle,
                    Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_rejects_an_empty_basket()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Valid(orderId, Guid.NewGuid()) with { OrderItems = [] })
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(OrderAggregate.ItemsEmptyTitle, error.Title);
                Assert.Equal(OrderAggregate.ItemsEmptyMessage, error.Message);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_rejects_a_null_line_list()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Valid(orderId, Guid.NewGuid()) with { OrderItems = null! })
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal(
                    OrderAggregate.ItemsEmptyTitle,
                    Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_rejects_a_line_with_an_empty_product_id()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var command = Valid(orderId, Guid.NewGuid());
        command = command with
        {
            OrderItems = [command.OrderItems[0] with { ProductId = Guid.Empty }]
        };

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(command)
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal(
                    OrderAggregate.ItemProductRequiredTitle,
                    Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_rejects_a_line_with_a_blank_name()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var command = Valid(orderId, Guid.NewGuid());
        command = command with
        {
            OrderItems = [command.OrderItems[0] with { ProductName = " " }]
        };

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(command)
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal(
                    OrderAggregate.ItemNameRequiredTitle,
                    Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_rejects_a_negative_price()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var command = Valid(orderId, Guid.NewGuid());
        command = command with
        {
            OrderItems = [command.OrderItems[0] with { UnitPrice = -1m }]
        };

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(command)
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal(
                    OrderAggregate.ItemPriceInvalidTitle,
                    Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_rejects_zero_units()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();
        var command = Valid(orderId, Guid.NewGuid());
        command = command with
        {
            OrderItems = [command.OrderItems[0] with { Units = 0 }]
        };

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(command)
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal(
                    OrderAggregate.ItemUnitsInvalidTitle,
                    Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_rejects_a_missing_card_type()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Valid(orderId, Guid.NewGuid()) with { CardTypeId = 0 })
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal(
                    OrderAggregate.CardTypeRequiredTitle,
                    Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_rejects_a_blank_cardholder()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Valid(orderId, Guid.NewGuid()) with { CardHolderName = " " })
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal(
                    OrderAggregate.CardHolderRequiredTitle,
                    Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_rejects_a_blank_card_number()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Valid(orderId, Guid.NewGuid()) with { MaskedCardNumber = " " })
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal(
                    OrderAggregate.CardLastFourRequiredTitle,
                    Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    [Theory]
    [InlineData("1111222233334444")]
    [InlineData("111")]
    [InlineData("****4444")]
    public async Task Place_order_rejects_anything_other_than_the_last_four(string cardNumber)
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Valid(orderId, Guid.NewGuid()) with { MaskedCardNumber = cardNumber })
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var error = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(OrderAggregate.CardNumberRejectedTitle, error.Title);
                Assert.DoesNotContain(cardNumber, error.Message, StringComparison.Ordinal);
                Assert.Empty(result.EmittedEvents);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Place_order_rejects_a_missing_expiration()
    {
        await using var ctx = Context();
        var orderId = Guid.NewGuid();

        await AggregateSpec
            .For<OrderAggregate>(ctx, orderId)
            .When(Valid(orderId, Guid.NewGuid()) with { CardExpiration = default })
            .ThenThrows<DomainException>()
            .AndAssert(result =>
                Assert.Equal(
                    OrderAggregate.CardExpirationRequiredTitle,
                    Assert.IsType<DomainException>(result.Exception).Title))
            .RunAsync();
    }

    private static BddTestContext Context() =>
        BddTestContext.CreateInMemory(typeof(OrderPlaced), typeof(OrderStarted));

    private static PlaceOrderCommand Valid(Guid orderId, Guid buyerId) =>
        new(
            orderId,
            orderId,
            buyerId,
            "Ada Lovelace",
            "1 Main",
            "Seattle",
            "WA",
            "USA",
            "98101",
            1,
            "Ada Lovelace",
            "4444",
            new DateTime(2028, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            [new OrderLine(Guid.NewGuid(), "Alpine boot", 120m, 2)]);

    private static OrderPlaced Placed(PlaceOrderCommand command) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.OrderId,
            command.BuyerId,
            command.UserName,
            OrderAggregate.SubmittedStatus,
            command.OrderItems,
            command.Street,
            command.City,
            command.State,
            command.Country,
            command.ZipCode,
            command.CardTypeId,
            command.CardHolderName,
            command.MaskedCardNumber,
            command.CardExpiration,
            Guid.NewGuid());

    private static OrderStarted Started(Guid orderId, Guid buyerId) =>
        new(Guid.NewGuid(), DateTime.UtcNow, orderId, buyerId);
}
