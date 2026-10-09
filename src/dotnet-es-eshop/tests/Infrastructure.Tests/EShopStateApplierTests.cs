using System.Collections;
using System.Reflection;
using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class EShopStateApplierTests
{
    [Fact]
    public void Apply_calls_a_non_public_method_and_caches_the_pair()
    {
        var state = new SampleState();
        var applied = new SampleApplied(Guid.NewGuid(), DateTime.UtcNow, 2);
        var before = CacheCount();

        EShopStateApplier.Apply(state, applied);
        var afterFirst = CacheCount();
        EShopStateApplier.Apply(state, applied);
        var afterSecond = CacheCount();

        Assert.Equal(4, state.Total);
        Assert.Equal(afterFirst, afterSecond);
        Assert.True(afterFirst == before || afterFirst == before + 1);
    }

    [Fact]
    public void Apply_throws_when_the_state_has_no_method_for_the_event()
    {
        var state = new SampleState();
        var unknown = new SampleUnknown(Guid.NewGuid(), DateTime.UtcNow);

        var ex = Assert.Throws<InvalidOperationException>(() => EShopStateApplier.Apply(state, unknown));

        Assert.Equal(
            "SampleState has no Apply(SampleUnknown). Add it in the slice that appends SampleUnknown.",
            ex.Message);
    }

    private static int CacheCount()
    {
        var field = typeof(EShopStateApplier).GetField(
            "Applicators",
            BindingFlags.NonPublic | BindingFlags.Static);
        var cache = field!.GetValue(null);
        return ((ICollection)cache!).Count;
    }

    private sealed class SampleState : IState
    {
        public int Total { get; private set; }

        private void Apply(SampleApplied applied) => Total += applied.Value;
    }
}
