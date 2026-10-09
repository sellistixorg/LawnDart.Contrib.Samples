using LawnDart.Messaging;
using LawnDart.Samples.EShop.Payment.Commands;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.TranslateGatewaySucceeded;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.TranslateGatewaySucceeded;

public class TranslateGatewaySucceededTests
{
    [Fact]
    public async Task Gateway_success_issues_record_payment_succeeded()
    {
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var messageId = PaymentGatewayChargeSucceeded.MessageIdFor(attemptId);
        var callback = Callback(attemptId, orderId, 42.50m, "EUR", "  gw-1  ");

        var command = Assert.IsType<RecordPaymentSucceededCommand>(
            Assert.Single(await ReactAsync(callback, messageId)));

        Assert.Equal(attemptId, command.AttemptId);
        Assert.Equal(orderId, command.OrderId);
        Assert.Equal("  gw-1  ", command.GatewayReference);
        Assert.Equal(42.50m, command.Amount);
        Assert.Equal(TranslateGatewaySucceededMessageHost.CommandIdFor(messageId), command.Id);
        Assert.Equal('5', command.Id.ToString("D")[14]);
        Assert.NotEqual(Guid.Empty, command.Id);
    }

    [Fact]
    public async Task Same_message_id_is_the_same_command()
    {
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var messageId = PaymentGatewayChargeSucceeded.MessageIdFor(attemptId);
        var callback = Callback(attemptId, orderId, 10m, null, null);
        var context = new MessageContext { MessageId = messageId };

        var first = Assert.IsType<RecordPaymentSucceededCommand>(Assert.Single(await ReactAsync(callback, context)));
        var second = Assert.IsType<RecordPaymentSucceededCommand>(Assert.Single(await ReactAsync(callback, context)));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(attemptId, first.AttemptId);
        Assert.Equal(orderId, first.OrderId);
        Assert.Null(first.GatewayReference);
        Assert.Equal(10m, first.Amount);
    }

    [Fact]
    public async Task Different_message_ids_are_different_commands()
    {
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var callback = Callback(attemptId, orderId, 8m, "USD", "gw");

        var first = Assert.IsType<RecordPaymentSucceededCommand>(
            Assert.Single(await ReactAsync(callback, Guid.NewGuid().ToString("D"))));
        var second = Assert.IsType<RecordPaymentSucceededCommand>(
            Assert.Single(await ReactAsync(callback, Guid.NewGuid().ToString("D"))));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(attemptId, first.AttemptId);
        Assert.Equal(orderId, second.OrderId);
    }

    [Fact]
    public async Task Missing_message_id_is_rethrown()
    {
        var attemptId = Guid.NewGuid();
        var callback = Callback(attemptId, Guid.NewGuid(), 4m, null, null);
        var reactor = new TranslateGatewaySucceededMessageHost();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reactor.ReactAsync(callback, new MessageContext { MessageId = " " }));

        Assert.Contains(attemptId.ToString("D"), error.Message, StringComparison.Ordinal);
        Assert.Contains("no message id", error.Message, StringComparison.Ordinal);
    }

    private static async Task<IReadOnlyList<LawnDart.ICommand>> ReactAsync(
        PaymentGatewayChargeSucceeded callback,
        string messageId) =>
        await ReactAsync(callback, new MessageContext { MessageId = messageId });

    private static async Task<IReadOnlyList<LawnDart.ICommand>> ReactAsync(
        PaymentGatewayChargeSucceeded callback,
        MessageContext context)
    {
        var reactor = new TranslateGatewaySucceededMessageHost();
        var commands = await reactor.ReactAsync(callback, context);
        return commands.ToList();
    }

    private static PaymentGatewayChargeSucceeded Callback(
        Guid attemptId,
        Guid orderId,
        decimal amount,
        string? currency,
        string? gatewayReference) =>
        new(attemptId, orderId, amount, currency, gatewayReference);
}
