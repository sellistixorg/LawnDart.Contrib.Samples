namespace HarborStreet.EndToEnd.Payments;

public sealed record GatewayCaptureResult(bool Succeeded, string ReferenceOrReason);

public interface IPaymentGateway
{
    Task<GatewayCaptureResult> CaptureAsync(Guid orderId, decimal amount, CancellationToken cancellationToken = default);
}

public sealed class FakePaymentGateway : IPaymentGateway
{
    public bool NextCaptureSucceeds { get; set; } = true;

    public Task<GatewayCaptureResult> CaptureAsync(Guid orderId, decimal amount, CancellationToken cancellationToken = default)
        => Task.FromResult(NextCaptureSucceeds
            ? new GatewayCaptureResult(true, $"pay-{orderId:N}"[..20])
            : new GatewayCaptureResult(false, "Card declined"));
}
