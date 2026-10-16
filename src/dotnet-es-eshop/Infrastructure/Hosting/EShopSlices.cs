using System.Reflection;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Patterns.EventProcessing;
using LawnDart.Patterns.Reaction;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Optional per-slice hook for services and endpoints that an assembly scan cannot infer.
/// </summary>
public interface IEShopSliceModule
{
    /// <summary>
    /// Adds services this slice needs beyond its reactors.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The host configuration.</param>
    void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    /// <summary>
    /// Maps this slice's view route.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

/// <summary>
/// Finds reactors, event processors, and slice modules in one service assembly.
/// </summary>
public static class EShopSlices
{
    /// <summary>
    /// Registers every reactor and event processor in <paramref name="serviceAssembly"/>,
    /// then calls <see cref="IEShopSliceModule.AddServices"/> on every module in that assembly.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder.</typeparam>
    /// <param name="builder">The service host.</param>
    /// <param name="serviceAssembly">The service assembly to scan.</param>
    /// <returns>The same <paramref name="builder"/>.</returns>
    /// <remarks>
    /// Modules are created with a parameterless constructor and called in full-name order.
    /// A reactor is registered with <see cref="EShopReactorHost{TReactor,TEvent}"/>.
    /// An event processor is registered with <see cref="EShopEventProcessorHost{TProcessor,TEvent}"/>.
    /// Each message opens a scope and resolves the reactor or processor in that scope.
    /// </remarks>
    public static TBuilder AddEShopSlices<TBuilder>(this TBuilder builder, Assembly serviceAssembly)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceAssembly);

        builder.Services.AddEShopReactors(serviceAssembly);
        foreach (var module in CreateModules(serviceAssembly))
            module.AddServices(builder.Services, builder.Configuration);

        return builder;
    }

    /// <summary>
    /// Registers every reactor and event processor in <paramref name="serviceAssembly"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceAssembly">The service assembly to scan.</param>
    /// <returns>The same <paramref name="services"/>.</returns>
    /// <remarks>
    /// Each closed <c>IReactor&lt;TEvent&gt;</c> is registered as transient and hosted by
    /// <see cref="EShopReactorHost{TReactor,TEvent}"/>.
    /// Each closed <c>IEventProcessor&lt;TEvent&gt;</c> is registered as transient and hosted by
    /// <see cref="EShopEventProcessorHost{TProcessor,TEvent}"/>.
    /// A second call leaves one hosted service for each pair.
    /// </remarks>
    public static IServiceCollection AddEShopReactors(this IServiceCollection services, Assembly serviceAssembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceAssembly);

        foreach (var (reactor, eventType) in FindReactors(serviceAssembly))
            AddClosedHost(services, typeof(EShopReactorHost<,>), reactor, eventType);

        foreach (var (processor, eventType) in FindEventProcessors(serviceAssembly))
            AddClosedHost(services, typeof(EShopEventProcessorHost<,>), processor, eventType);

        return services;
    }

    /// <summary>
    /// Calls <see cref="IEShopSliceModule.MapEndpoints"/> on every module in <paramref name="serviceAssembly"/>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="serviceAssembly">The service assembly to scan.</param>
    /// <returns>The same <paramref name="endpoints"/>.</returns>
    /// <remarks>
    /// Modules are created with a parameterless constructor and called in full-name order.
    /// </remarks>
    public static IEndpointRouteBuilder MapEShopSlices(this IEndpointRouteBuilder endpoints, Assembly serviceAssembly)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(serviceAssembly);

        foreach (var module in CreateModules(serviceAssembly))
            module.MapEndpoints(endpoints);

        return endpoints;
    }

    /// <summary>
    /// Reactor and event pairs in <paramref name="serviceAssembly"/>, sorted by reactor full name, then event token.
    /// </summary>
    /// <param name="serviceAssembly">The service assembly to scan.</param>
    /// <returns>The pairs. Abstract types and open generic types are left out.</returns>
    public static IReadOnlyList<(Type Reactor, Type Event)> FindReactors(Assembly serviceAssembly)
    {
        ArgumentNullException.ThrowIfNull(serviceAssembly);
        return FindClosed(serviceAssembly, typeof(IReactor<>));
    }

    /// <summary>
    /// Event processor and event pairs in <paramref name="serviceAssembly"/>, sorted by processor full name, then event token.
    /// </summary>
    /// <param name="serviceAssembly">The service assembly to scan.</param>
    /// <returns>The pairs. Abstract types and open generic types are left out.</returns>
    public static IReadOnlyList<(Type Processor, Type Event)> FindEventProcessors(Assembly serviceAssembly)
    {
        ArgumentNullException.ThrowIfNull(serviceAssembly);
        return FindClosed(serviceAssembly, typeof(IEventProcessor<>));
    }

    private static IReadOnlyList<(Type Implementation, Type Event)> FindClosed(Assembly assembly, Type openInterface)
    {
        var found = new List<(Type Implementation, Type Event)>();
        foreach (var type in ConcreteClasses(assembly))
        {
            foreach (var implemented in type.GetInterfaces())
            {
                if (!implemented.IsGenericType || implemented.ContainsGenericParameters)
                    continue;

                if (implemented.GetGenericTypeDefinition() != openInterface)
                    continue;

                found.Add((type, implemented.GetGenericArguments()[0]));
            }
        }

        found.Sort(static (left, right) =>
        {
            var byName = string.Compare(left.Implementation.FullName, right.Implementation.FullName, StringComparison.Ordinal);
            if (byName != 0)
                return byName;

            return string.Compare(
                EventToken(left.Event),
                EventToken(right.Event),
                StringComparison.Ordinal);
        });
        return found;
    }

    private static IEnumerable<IEShopSliceModule> CreateModules(Assembly assembly)
    {
        foreach (var type in ConcreteClasses(assembly))
        {
            if (!typeof(IEShopSliceModule).IsAssignableFrom(type))
                continue;

            if (Activator.CreateInstance(type, nonPublic: true) is not IEShopSliceModule module)
            {
                throw new InvalidOperationException(
                    $"{type.FullName} could not be created. Give it a parameterless constructor.");
            }

            yield return module;
        }
    }

    private static IEnumerable<Type> ConcreteClasses(Assembly assembly)
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

        var concrete = new List<Type>();
        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false } && !type.IsGenericTypeDefinition)
                concrete.Add(type);
        }

        concrete.Sort(static (left, right) =>
            string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));
        return concrete;
    }

    private static void AddClosedHost(
        IServiceCollection services,
        Type openHost,
        Type implementation,
        Type eventType)
    {
        services.TryAddTransient(implementation);
        services.AddEShopSubscription(eventType);
        var host = openHost.MakeGenericType(implementation, eventType);
        services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IHostedService), host));
    }

    private static string EventToken(Type eventType) =>
        EventTypeCatalog.TryGetDeclaredName(eventType) ?? eventType.FullName ?? eventType.Name;
}
