using System.Reflection;
using System.Reflection.Emit;
using LawnDart;
using LawnDart.Aggregates;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class EShopApplyCoverageTests
{
    private static readonly Dictionary<string, string[]> RequiredEvents = new(StringComparer.Ordinal)
    {
        ["Order"] =
        [
            "OrderPlaced",
            "OrderStarted",
            "OrderAwaitingValidation",
            "OrderStatusStockConfirmed",
            "OrderCancelledForStock",
        ],
        ["CatalogItem"] =
        [
            "CatalogItemAdded",
            "CatalogItemUpdated",
            "CatalogItemPriceChanged",
            "CatalogItemRemoved",
            "CatalogItemRestocked",
            "CatalogStockDeducted",
        ],
        ["Basket"] =
        [
            "BasketItemAdded",
            "BasketItemQuantityChanged",
            "BasketItemRemoved",
            "BasketCleared",
        ],
        ["ShopperAccount"] =
        [
            "ShopperAccountRegistered",
            "ShopperProfileUpdated",
            "ShopperSignedIn",
            "ShopperSignedOut",
        ],
    };

    [Fact]
    public void Every_emitted_event_has_an_apply_method()
    {
        var strict = EShopServiceAssemblies.All()
            .SelectMany(EShopApplyCoverage.Aggregates)
            .Where(EShopApplyCoverage.UsesStateApplier)
            .ToArray();
        var gaps = new List<string>();

        foreach (var name in RequiredEvents.Keys)
        {
            if (!strict.Any(aggregate => aggregate.Name == name))
                gaps.Add(name + " does not dispatch through EShopStateApplier.");
        }

        foreach (var aggregate in strict)
        {
            var state = EShopApplyCoverage.StateType(aggregate);
            var emitted = EShopApplyCoverage.EmittedEvents(aggregate);
            if (RequiredEvents.TryGetValue(aggregate.Name, out var required))
            {
                foreach (var eventName in required)
                {
                    if (!emitted.Any(type => type.Name == eventName))
                        gaps.Add(aggregate.Name + " emit set is missing " + eventName + ".");
                }
            }

            foreach (var eventType in emitted)
            {
                if (!EShopApplyCoverage.HasApply(state, eventType))
                    gaps.Add(state.Name + " has no Apply(" + eventType.Name + ").");
            }
        }

        Assert.True(gaps.Count == 0, string.Join(Environment.NewLine, gaps));
    }

    [Fact]
    public void Coverage_check_reports_a_missing_apply_before_a_command_runs()
    {
        var gaps = EShopApplyCoverage.MissingApplies(typeof(MissingApplyAggregate));
        Assert.Contains("MissingApplyState has no Apply(SampleUnknown).", gaps);

        Assert.Empty(EShopApplyCoverage.MissingApplies(typeof(CoveredApplyAggregate)));
    }
}

internal static class EShopServiceAssemblies
{
    // Add a name when a new service is added. A new slice does not change this list.
    internal static readonly string[] Required =
    [
        "LawnDart.Samples.EShop.Basket.API",
        "LawnDart.Samples.EShop.Catalog.API",
        "LawnDart.Samples.EShop.Identity.API",
        "LawnDart.Samples.EShop.Ordering.API",
        "LawnDart.Samples.EShop.OrderProcessor",
        "LawnDart.Samples.EShop.PaymentProcessor",
    ];

    public static IReadOnlyList<Assembly> All()
    {
        var loaded = new SortedDictionary<string, Assembly>(StringComparer.Ordinal);
        foreach (var name in typeof(EShopServiceAssemblies).Assembly.GetReferencedAssemblies())
        {
            if (name.Name is not null && IsServiceAssembly(name.Name))
                loaded[name.Name] = Assembly.Load(name);
        }

        // The compiler drops a project reference when this test project names no type from it.
        // The service DLL is still copied next to the tests. Load it from there.
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "LawnDart.Samples.EShop.*.dll"))
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!IsServiceAssembly(fileName) || loaded.ContainsKey(fileName))
                continue;

            loaded[fileName] = Assembly.Load(AssemblyName.GetAssemblyName(path));
        }

        return loaded.Values.ToArray();
    }

    private static bool IsServiceAssembly(string name) =>
        name.StartsWith("LawnDart.Samples.EShop.", StringComparison.Ordinal)
        && name != "LawnDart.Samples.EShop.Infrastructure"
        && name != "LawnDart.Samples.EShop.Infrastructure.Tests"
        && name != "LawnDart.Samples.EShop.Shared"
        && name != "LawnDart.Samples.EShop.ServiceDefaults";

    public static IReadOnlyList<string> SliceHelperNames()
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var assembly in All())
        {
            foreach (var type in ConcreteTypes(assembly))
            {
                if (type.Namespace?.Contains(".Slices.", StringComparison.Ordinal) != true)
                    continue;

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (method.IsSpecialName)
                        continue;

                    var extension = method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), inherit: false);
                    if (extension || method.Name.StartsWith("Add", StringComparison.Ordinal) || method.Name.StartsWith("Map", StringComparison.Ordinal))
                        names.Add(method.Name);
                }
            }
        }

        return names.ToArray();
    }

    private static IEnumerable<Type> ConcreteTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }
}

internal static class EShopApplyCoverage
{
    private static readonly int[] OneByteOperand = OperandSizes(twoByte: false);
    private static readonly int[] TwoByteOperand = OperandSizes(twoByte: true);

    public static IEnumerable<Type> Aggregates(Assembly assembly)
    {
        IEnumerable<Type> types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(type => type is not null)!;
        }

        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false } && StateTypeOrNull(type) is not null)
                yield return type;
        }
    }

    public static bool UsesStateApplier(Type aggregate)
    {
        var method = aggregate.GetMethod(
            "ApplyEventToState",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (method is null)
            return false;

        return Calls(method).Any(called =>
            called.Name == "Apply"
            && called.DeclaringType == typeof(EShopStateApplier));
    }

    public static Type StateType(Type aggregate) =>
        StateTypeOrNull(aggregate)
        ?? throw new InvalidOperationException(aggregate.FullName + " is not an aggregate.");

    public static IReadOnlyList<Type> EmittedEvents(Type aggregate)
    {
        var found = new List<Type>();
        foreach (var method in Methods(aggregate))
        {
            foreach (var called in Calls(method))
            {
                if (!IsAggregateApply(called))
                    continue;

                found.Add(called.GetGenericArguments()[0]);
            }
        }

        return found
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool HasApply(Type state, Type eventType)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var method = state.GetMethod("Apply", flags, binder: null, [eventType], modifiers: null);
        return method is not null && method.ReturnType == typeof(void);
    }

    public static IReadOnlyList<string> MissingApplies(Type aggregate)
    {
        if (!UsesStateApplier(aggregate))
            return [];

        var state = StateType(aggregate);
        return EmittedEvents(aggregate)
            .Where(eventType => !HasApply(state, eventType))
            .Select(eventType => state.Name + " has no Apply(" + eventType.Name + ").")
            .ToArray();
    }

    private static Type? StateTypeOrNull(Type type)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
                return current.GetGenericArguments()[0];

            current = current.BaseType;
        }

        return null;
    }

    private static bool IsAggregateApply(MethodBase method)
    {
        if (method.Name != "Apply" || !method.IsGenericMethod)
            return false;

        var declaring = method.DeclaringType;
        return declaring is not null
            && declaring.IsGenericType
            && declaring.GetGenericTypeDefinition() == typeof(AggregateRoot<>);
    }

    private static IEnumerable<MethodInfo> Methods(Type aggregate)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (var method in aggregate.GetMethods(flags))
            yield return method;

        foreach (var nested in aggregate.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var method in nested.GetMethods(flags))
                yield return method;
        }
    }

    private static IEnumerable<MethodBase> Calls(MethodInfo method)
    {
        var body = method.GetMethodBody();
        var il = body?.GetILAsByteArray();
        if (il is null || il.Length == 0)
            yield break;

        var index = 0;
        while (index < il.Length)
        {
            var op = il[index++];
            int[] table;
            var isCall = false;
            if (op == 0xFE)
            {
                if (index >= il.Length)
                    yield break;

                op = il[index++];
                table = TwoByteOperand;
            }
            else
            {
                table = OneByteOperand;
                isCall = op is 0x28 or 0x6F;
            }

            var size = table[op];
            if (size == int.MinValue)
                throw new InvalidOperationException($"Unknown opcode 0x{op:X2} in {method.DeclaringType?.Name}.{method.Name}.");

            if (size < 0)
            {
                var count = BitConverter.ToInt32(il, index);
                index += 4 + (4 * count);
                continue;
            }

            if (isCall && size == 4 && index + 4 <= il.Length)
            {
                var token = BitConverter.ToInt32(il, index);
                MethodBase? resolved = null;
                try
                {
                    resolved = method.Module.ResolveMethod(token, TypeArguments(method.DeclaringType), TypeArguments(method));
                }
                catch (ArgumentException)
                {
                }

                if (resolved is not null)
                    yield return resolved;
            }

            index += size;
        }
    }

    private static Type[]? TypeArguments(Type? type)
    {
        if (type is null || !type.IsGenericType)
            return null;

        return type.GetGenericArguments();
    }

    private static Type[]? TypeArguments(MethodInfo method) =>
        method.IsGenericMethod ? method.GetGenericArguments() : null;

    private static int[] OperandSizes(bool twoByte)
    {
        var sizes = new int[256];
        Array.Fill(sizes, int.MinValue);
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode code)
                continue;

            var raw = (ushort)code.Value;
            var isTwo = (raw & 0xFF00) == 0xFE00;
            if (isTwo != twoByte)
                continue;

            sizes[raw & 0xFF] = OperandBytes(code.OperandType);
        }

        return sizes;
    }

    private static int OperandBytes(OperandType type) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI
            or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString
            or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => -1,
        _ => throw new InvalidOperationException("Unsupported operand " + type + "."),
    };
}

internal sealed class MissingApplyAggregate : AggregateRoot<MissingApplyState>
{
    public void Handle(SampleUnknown missed) => Apply(missed);

    protected override void ApplyEventToState(IEvent @event) => EShopStateApplier.Apply(State, @event);
}

internal sealed class MissingApplyState : IState;

internal sealed class CoveredApplyAggregate : AggregateRoot<CoveredApplyState>
{
    public void Handle(SampleApplied applied) => Apply(applied);

    protected override void ApplyEventToState(IEvent @event) => EShopStateApplier.Apply(State, @event);
}

internal sealed class CoveredApplyState : IState
{
    internal void Apply(SampleApplied applied)
    {
        _ = applied;
    }
}
