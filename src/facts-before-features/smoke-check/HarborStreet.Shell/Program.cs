using System.Reflection;
using LawnDart;

// Smoke check: LawnDart assemblies resolve and core contracts are visible.
var lawnDart = typeof(IEvent).Assembly;
var version = lawnDart.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? lawnDart.GetName().Version?.ToString();
Console.WriteLine("Harbor Street sample shell");
Console.WriteLine($"LawnDart OK: {lawnDart.GetName().Name} {version}");
Console.WriteLine("dotnet build succeeded against LawnDart from nuget.org (version pinned in Directory.Packages.props).");
return 0;
