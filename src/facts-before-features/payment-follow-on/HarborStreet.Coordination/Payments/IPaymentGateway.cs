namespace HarborStreet.Coordination.Payments;

public sealed record GatewayCaptureResult(bool Succeeded, string ReferenceOrReason);

/// <summary>Port for the payment provider — outcomes become domain facts via commands.</summary>
public interface IPaymentGateway
{
    Task<GatewayCaptureResult> CaptureAsync(Guid orderId, decimal amount, CancellationToken cancellationToken = default);
}

/// <summary>Test double — configure success or decline.</summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    public bool NextCaptureSucceeds { get; set; } = true;

    public Task<GatewayCaptureResult> CaptureAsync(Guid orderId, decimal amount, CancellationToken cancellationToken = default)
        => Task.FromResult(NextCaptureSucceeds
            ? new GatewayCaptureResult(true, $"pay-{orderId:N}"[..20])
            : new GatewayCaptureResult(false, "Card declined"));
}
