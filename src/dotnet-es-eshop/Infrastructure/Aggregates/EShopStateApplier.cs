using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using LawnDart;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Applies one event to aggregate state by calling <c>Apply(TEvent)</c> on that state.
/// </summary>
public static class EShopStateApplier
{
    private static readonly ConcurrentDictionary<(Type State, Type Event), Action<object, IEvent>> Applicators = new();

    /// <summary>
    /// Calls the <c>Apply</c> method on <paramref name="state"/> whose parameter type is the runtime type of <paramref name="event"/>.
    /// </summary>
    /// <typeparam name="TState">The aggregate state type.</typeparam>
    /// <param name="state">The state to update.</param>
    /// <param name="event">The event to apply.</param>
    /// <remarks>
    /// The method may be public or non-public. It must take exactly that event type and return void.
    /// The compiled call is cached for the state type and the event type.
    /// A missing method throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    public static void Apply<TState>(TState state, IEvent @event)
        where TState : class, IState
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(@event);

        var stateType = state.GetType();
        var eventType = @event.GetType();
        var apply = Applicators.GetOrAdd((stateType, eventType), static key => Compile(key.State, key.Event));
        apply(state, @event);
    }

    private static Action<object, IEvent> Compile(Type stateType, Type eventType)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var method = stateType.GetMethod("Apply", flags, binder: null, [eventType], modifiers: null);
        if (method is null || method.ReturnType != typeof(void))
        {
            throw new InvalidOperationException(
                $"{stateType.Name} has no Apply({eventType.Name}). Add it in the slice that appends {eventType.Name}.");
        }

        var dynamic = new DynamicMethod(
            "EShopApply",
            typeof(void),
            [typeof(object), typeof(IEvent)],
            stateType,
            skipVisibility: true);
        var il = dynamic.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, stateType);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Castclass, eventType);
        il.Emit(OpCodes.Callvirt, method);
        il.Emit(OpCodes.Ret);
        return dynamic.CreateDelegate<Action<object, IEvent>>();
    }
}
