using System.Reflection;
using System.Text.RegularExpressions;
using LawnDart;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Patterns.EventProcessing;
using LawnDart.Patterns.Reaction;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Registration;
using LawnDart.Samples.EShop.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class EShopSliceDiscoveryTests
{
    private static readonly List<string> ModuleLog = [];

    private static readonly string[] ServicesWithReactors =
    [
        "LawnDart.Samples.EShop.Basket.API",
        "LawnDart.Samples.EShop.Catalog.API",
        "LawnDart.Samples.EShop.Ordering.API",
        "LawnDart.Samples.EShop.OrderProcessor",
        "LawnDart.Samples.EShop.PaymentProcessor",
    ];

    private static readonly string[] ServicesWithProjections =
    [
        "LawnDart.Samples.EShop.Basket.API",
        "LawnDart.Samples.EShop.Catalog.API",
        "LawnDart.Samples.EShop.Identity.API",
        "LawnDart.Samples.EShop.Ordering.API",
        "LawnDart.Samples.EShop.PaymentProcessor",
    ];

    private static readonly string[] ServicesWithViewRoutes =
    [
        "LawnDart.Samples.EShop.Basket.API",
        "LawnDart.Samples.EShop.Catalog.API",
        "LawnDart.Samples.EShop.Identity.API",
        "LawnDart.Samples.EShop.Ordering.API",
        "LawnDart.Samples.EShop.PaymentProcessor",
    ];

    [Fact]
    public void Scan_finds_reactors_and_skips_abstract_and_open_generic_types()
    {
        var found = EShopSlices.FindReactors(typeof(EShopSliceDiscoveryTests).Assembly);

        Assert.Contains(found, pair => pair.Reactor == typeof(SampleReactor) && pair.Event == typeof(SampleApplied));
        Assert.Contains(found, pair => pair.Reactor == typeof(DualReactor) && pair.Event == typeof(SampleApplied));
        Assert.Contains(found, pair => pair.Reactor == typeof(DualReactor) && pair.Event == typeof(SampleUnknown));
        Assert.DoesNotContain(found, pair => pair.Reactor == typeof(AbstractSampleReactor));
        Assert.DoesNotContain(found, pair => pair.Reactor == typeof(OpenSampleReactor<>));
        Assert.DoesNotContain(found, pair => pair.Reactor.IsAbstract || pair.Reactor.IsGenericTypeDefinition);
    }

    [Fact]
    public void Dual_reactor_gets_two_hosted_services_and_a_second_scan_keeps_one_each()
    {
        var services = new ServiceCollection();
        services.AddEShopReactors(typeof(EShopSliceDiscoveryTests).Assembly);
        services.AddEShopReactors(typeof(EShopSliceDiscoveryTests).Assembly);

        Assert.Single(services, HostedService<SampleReactor, SampleApplied>());
        Assert.Single(services, HostedService<DualReactor, SampleApplied>());
        Assert.Single(services, HostedService<DualReactor, SampleUnknown>());
    }

    [Fact]
    public void Event_processor_is_registered_once()
    {
        var services = new ServiceCollection();
        services.AddEShopReactors(typeof(EShopSliceDiscoveryTests).Assembly);
        services.AddEShopReactors(typeof(EShopSliceDiscoveryTests).Assembly);

        var processors = EShopSlices.FindEventProcessors(typeof(EShopSliceDiscoveryTests).Assembly);
        Assert.Contains(
            processors,
            pair => pair.Processor == typeof(SampleProcessor) && pair.Event == typeof(SampleApplied));
        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(EShopEventProcessorHost<SampleProcessor, SampleApplied>));
    }

    [Fact]
    public void Modules_run_once_each_in_name_order()
    {
        ModuleLog.Clear();
        var builder = Host.CreateApplicationBuilder();
        builder.AddEShopSlices(typeof(EShopSliceDiscoveryTests).Assembly);

        Assert.Equal(
            new[] { typeof(AardvarkModule).FullName!, typeof(ZuluModule).FullName! },
            ModuleLog);
    }

    [Fact]
    public void Routing_keys_for_scanned_events_equal_the_tokens()
    {
        var found = EShopSlices.FindReactors(typeof(EShopSliceDiscoveryTests).Assembly)
            .Where(pair => pair.Reactor.DeclaringType == typeof(EShopSliceDiscoveryTests)
                || pair.Reactor == typeof(SampleReactor)
                || pair.Reactor == typeof(DualReactor))
            .ToArray();

        Assert.NotEmpty(found);
        foreach (var pair in found)
        {
            var token = EventTypeCatalog.TryGetDeclaredName(pair.Event);
            Assert.False(string.IsNullOrWhiteSpace(token));
            Assert.Equal(token, MessageTypeNames.Require(pair.Event));
        }
    }

    [Fact]
    public void Required_service_assemblies_are_loaded()
    {
        var loaded = EShopServiceAssemblies.All()
            .Select(assembly => assembly.GetName().Name)
            .ToArray();
        foreach (var name in EShopServiceAssemblies.Required)
            Assert.Contains(name, loaded);
    }

    [Fact]
    public void Each_service_reactor_is_registered_once()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var assembly in EShopServiceAssemblies.All())
        {
            var reactors = ClosedHandlers(assembly, typeof(IReactor<>));
            counts[assembly.GetName().Name!] = reactors.Count;
            AssertSamePairs(reactors, EShopSlices.FindReactors(assembly));

            var processors = ClosedHandlers(assembly, typeof(IEventProcessor<>));
            AssertSamePairs(
                processors,
                EShopSlices.FindEventProcessors(assembly)
                    .Select(pair => (Reactor: pair.Processor, pair.Event))
                    .ToArray());

            var services = new ServiceCollection();
            services.AddEShopReactors(assembly);
            services.AddEShopReactors(assembly);
            foreach (var pair in reactors)
                AssertSingleHosted(services, typeof(EShopReactorHost<,>), pair.Implementation, pair.Event);
            foreach (var pair in processors)
                AssertSingleHosted(services, typeof(EShopEventProcessorHost<,>), pair.Implementation, pair.Event);
        }

        AssertEachNamedServiceHasItems(counts, ServicesWithReactors, "reactors");
    }

    [Fact]
    public void Each_service_projection_is_registered_once()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var assembly in EShopServiceAssemblies.All())
        {
            var expected = ProjectionHandlers(assembly);
            counts[assembly.GetName().Name!] = expected.Count;
            if (expected.Count == 0)
                continue;

            var services = new ServiceCollection();
            services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            AddServiceContext(context, assembly);

            using var provider = services.BuildServiceProvider();
            var registrations = provider.GetRequiredKeyedService<IReadOnlyList<ProjectionRegistration>>(
                EShopLawnDartHost.ContextName);
            var registered = registrations.Select(item => item.HandlerType).ToArray();
            Assert.Equal(
                expected.Select(type => type.FullName).Order(StringComparer.Ordinal).ToArray(),
                registered.Select(type => type.FullName).Order(StringComparer.Ordinal).ToArray());
            foreach (var handler in expected)
            {
                var registration = Assert.Single(registrations, item => item.HandlerType == handler);
                var storageKey = handler.GetField("StorageKey", BindingFlags.Public | BindingFlags.Static);
                if (storageKey?.FieldType == typeof(string))
                    Assert.Equal(storageKey.GetValue(null), registration.StorageKey);
            }
        }

        AssertEachNamedServiceHasItems(counts, ServicesWithProjections, "projections");
    }

    [Fact]
    public void Each_declared_view_route_is_mapped()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var expected = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var assembly in EShopServiceAssemblies.All())
        {
            var declared = DeclaredViewRoutes(assembly);
            counts[assembly.GetName().Name!] = declared.Count;
            expected.AddRange(declared);
            app.MapEShopSlices(assembly);
        }

        AssertEachNamedServiceHasItems(counts, ServicesWithViewRoutes, "view routes");

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.Order(StringComparer.Ordinal).ToArray(), routes);
    }

    [Fact]
    public void Service_programs_do_not_register_slices_by_hand()
    {
        var root = FindSolutionRoot();
        var programs = Directory.EnumerateFiles(root, "Program.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .ToArray();
        var helpers = EShopServiceAssemblies.SliceHelperNames();

        Assert.NotEmpty(programs);
        Assert.Contains(helpers, name => name == "MapBrowseCatalog");
        foreach (var program in programs)
        {
            var violations = ProgramSliceViolations(File.ReadAllText(program), helpers);
            Assert.True(
                violations.Count == 0,
                program + ": " + string.Join("; ", violations));
        }
    }

    [Fact]
    public void Program_guard_flags_add_reactor_slice_using_and_slice_helper_calls()
    {
        var helpers = new[] { "MapBrowseCatalog", "AddConfirmGracePeriod" };

        Assert.Contains(
            ProgramSliceViolations("builder.Services.AddReactor<A, B>();", helpers),
            item => item.Contains("AddReactor<", StringComparison.Ordinal));
        Assert.Contains(
            ProgramSliceViolations("using LawnDart.Samples.EShop.Catalog.API.Slices.BrowseCatalog;", helpers),
            item => item.Contains(".Slices.", StringComparison.Ordinal));
        Assert.Contains(
            ProgramSliceViolations("app.MapBrowseCatalog();", helpers),
            item => item.Contains("MapBrowseCatalog", StringComparison.Ordinal));
        Assert.Contains(
            ProgramSliceViolations(
                "endpoints.LawnDart.Samples.EShop.Catalog.API.Slices.BrowseCatalog.BrowseCatalogEndpoint.MapBrowseCatalog();",
                helpers),
            item => item.Contains(".Slices.", StringComparison.Ordinal));
        Assert.Empty(ProgramSliceViolations(
            """
            using LawnDart.Samples.EShop.Infrastructure;
            builder.AddEShopSlices(assembly);
            app.MapEShopSlices(assembly);
            app.MapLawnDartCommands();
            """,
            helpers));
    }

    [Fact]
    public void Reactor_host_takes_a_scope_factory()
    {
        var ctor = typeof(EShopReactorHost<SampleReactor, SampleApplied>).GetConstructors().Single();
        Assert.Equal(typeof(IServiceScopeFactory), ctor.GetParameters()[0].ParameterType);
        Assert.DoesNotContain(
            ctor.GetParameters(),
            parameter => parameter.ParameterType == typeof(SampleReactor));
    }

    private static List<(Type Implementation, Type Event)> ClosedHandlers(Assembly assembly, Type openInterface)
    {
        var found = new List<(Type Implementation, Type Event)>();
        foreach (var type in LoadTypes(assembly))
        {
            if (type is not { IsClass: true, IsAbstract: false } || type.IsGenericTypeDefinition)
                continue;

            foreach (var implemented in type.GetInterfaces())
            {
                if (!implemented.IsGenericType || implemented.ContainsGenericParameters)
                    continue;

                if (implemented.GetGenericTypeDefinition() != openInterface)
                    continue;

                found.Add((type, implemented.GetGenericArguments()[0]));
            }
        }

        return found;
    }

    private static void AssertSamePairs(
        IReadOnlyList<(Type Implementation, Type Event)> expected,
        IReadOnlyList<(Type Reactor, Type Event)> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var pair in expected)
        {
            Assert.Single(
                actual,
                item => item.Reactor == pair.Implementation && item.Event == pair.Event);
        }
    }

    private static void AssertSingleHosted(
        IServiceCollection services,
        Type openHost,
        Type implementation,
        Type eventType)
    {
        var hosted = openHost.MakeGenericType(implementation, eventType);
        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == hosted);
    }

    private static List<Type> ProjectionHandlers(Assembly assembly)
    {
        var found = new List<Type>();
        foreach (var type in LoadTypes(assembly))
        {
            if (type is not { IsClass: true, IsAbstract: false } || type.IsGenericTypeDefinition)
                continue;

            if (IsProjectionHandler(type))
                found.Add(type);
        }

        return found;
    }

    private static bool IsProjectionHandler(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition().Name == "ProjectionBase`1")
                return true;
        }

        return false;
    }

    private static void AddServiceContext(BoundedContextBuilder context, Assembly serviceAssembly)
    {
        var token = ServiceToken(serviceAssembly);
        var shared = typeof(EShopVersion5Id).Assembly;
        var matches = typeof(EShopLawnDartHost).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(method => (Method: method, Score: ContextScore(method, token)))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Method.GetParameters().Length)
            .ToArray();
        if (matches.Length == 0)
        {
            throw new InvalidOperationException(
                "No context hook scans " + serviceAssembly.GetName().Name + ".");
        }

        var method = matches[0].Method;
        var count = method.GetParameters().Length;
        object?[] args = count switch
        {
            2 => [context, serviceAssembly],
            3 => [context, serviceAssembly, shared],
            _ => throw new InvalidOperationException(method.DeclaringType?.FullName + "." + method.Name)
        };
        method.Invoke(null, args);
    }

    private static int ContextScore(MethodInfo method, string token)
    {
        if (!method.Name.StartsWith("Add", StringComparison.Ordinal)
            || !method.Name.EndsWith("Context", StringComparison.Ordinal))
            return 0;

        var parameters = method.GetParameters();
        if (parameters.Length is < 2 or > 3)
            return 0;

        if (parameters[0].ParameterType != typeof(BoundedContextBuilder))
            return 0;

        if (parameters.Skip(1).Any(parameter => parameter.ParameterType != typeof(Assembly)))
            return 0;

        var middle = method.Name["Add".Length..^"Context".Length];
        if (middle.Equals(token, StringComparison.Ordinal))
            return 1000 + middle.Length;

        if (token.StartsWith(middle, StringComparison.Ordinal) && middle.Length >= 6)
            return middle.Length;

        return 0;
    }

    private static string ServiceToken(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? "";
        const string prefix = "LawnDart.Samples.EShop.";
        var rest = name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name;
        if (rest.EndsWith(".API", StringComparison.Ordinal))
            rest = rest[..^4];

        return rest;
    }

    private static List<string> DeclaredViewRoutes(Assembly assembly)
    {
        var routes = new List<string>();
        foreach (var type in LoadTypes(assembly))
        {
            var field = type.GetField("Route", BindingFlags.Public | BindingFlags.Static);
            if (field is not { IsLiteral: true } || field.FieldType != typeof(string))
                continue;

            if (field.GetRawConstantValue() is string value && value.StartsWith("/views/", StringComparison.Ordinal))
                routes.Add(value);
        }

        return routes;
    }

    private static void AssertEachNamedServiceHasItems(
        IReadOnlyDictionary<string, int> counts,
        IEnumerable<string> serviceNames,
        string kind)
    {
        foreach (var name in serviceNames)
        {
            var found = counts.TryGetValue(name, out var count);
            Assert.True(found, "Missing service assembly " + name + ".");
            Assert.True(count > 0, name + " has no " + kind + ".");
        }
    }

    private static IEnumerable<Type> LoadTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var details = string.Join(
                "; ",
                ex.LoaderExceptions.Select(item =>
                    item is null ? "(null)" : item.GetType().Name + ": " + item.Message));
            throw new InvalidOperationException(
                assembly.GetName().Name + " failed to load types: " + details,
                ex);
        }
    }

    internal static List<string> ProgramSliceViolations(string text, IEnumerable<string> helperNames)
    {
        var violations = new List<string>();
        var helpers = helperNames.Distinct(StringComparer.Ordinal).ToArray();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = StripLineComment(rawLine);
            if (line.Contains("AddReactor<", StringComparison.Ordinal))
                violations.Add("AddReactor<");

            if (SliceUsing.IsMatch(line))
                violations.Add("using a .Slices. namespace: " + line.Trim());

            if (!SliceUsing.IsMatch(line) && line.Contains(".Slices.", StringComparison.Ordinal))
                violations.Add("slice reference: " + line.Trim());

            foreach (var name in helpers)
            {
                if (SliceHelperCall(name).IsMatch(line))
                    violations.Add("slice helper call " + name);
            }
        }

        return violations;
    }

    private static string StripLineComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index < 0 ? line : line[..index];
    }

    private static readonly Regex SliceUsing = new(
        @"^\s*using\s+(static\s+)?[\w.]+\.Slices(\.[\w]+)*\s*;",
        RegexOptions.CultureInvariant);

    private static Regex SliceHelperCall(string name) =>
        new(@"\b" + Regex.Escape(name) + @"\s*\(", RegexOptions.CultureInvariant);

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LawnDart.Samples.EShop.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find LawnDart.Samples.EShop.sln.");
    }

    private static Predicate<ServiceDescriptor> HostedService<TReactor, TEvent>()
        where TReactor : class, IReactor<TEvent>
        where TEvent : class, IEvent =>
        descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(EShopReactorHost<TReactor, TEvent>);

    private sealed class AardvarkModule : IEShopSliceModule
    {
        public void AddServices(IServiceCollection services, IConfiguration configuration) =>
            ModuleLog.Add(typeof(AardvarkModule).FullName!);
    }

    private sealed class ZuluModule : IEShopSliceModule
    {
        public void AddServices(IServiceCollection services, IConfiguration configuration) =>
            ModuleLog.Add(typeof(ZuluModule).FullName!);
    }
}

[EventTypeName("slice-scan-applied")]
public sealed record SampleApplied(Guid Id, DateTime Timestamp, int Value) : IEvent;

[EventTypeName("slice-scan-unknown")]
public sealed record SampleUnknown(Guid Id, DateTime Timestamp) : IEvent;

public sealed class SampleReactor : IReactor<SampleApplied>
{
    public Task<IEnumerable<ICommand>> ReactAsync(
        SampleApplied @event,
        MessageContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<ICommand>>([]);
}

public sealed class DualReactor : IReactor<SampleApplied>, IReactor<SampleUnknown>
{
    public Task<IEnumerable<ICommand>> ReactAsync(
        SampleApplied @event,
        MessageContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<ICommand>>([]);

    public Task<IEnumerable<ICommand>> ReactAsync(
        SampleUnknown @event,
        MessageContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<ICommand>>([]);
}

public abstract class AbstractSampleReactor : IReactor<SampleApplied>
{
    public abstract Task<IEnumerable<ICommand>> ReactAsync(
        SampleApplied @event,
        MessageContext context,
        CancellationToken cancellationToken = default);
}

public class OpenSampleReactor<TEvent> : IReactor<TEvent>
    where TEvent : class, IEvent
{
    public Task<IEnumerable<ICommand>> ReactAsync(
        TEvent @event,
        MessageContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<ICommand>>([]);
}

public sealed class SampleProcessor : IEventProcessor<SampleApplied>
{
    public Task<IEnumerable<IEvent>> ProcessAsync(
        SampleApplied @event,
        MessageContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<IEvent>>([]);
}
