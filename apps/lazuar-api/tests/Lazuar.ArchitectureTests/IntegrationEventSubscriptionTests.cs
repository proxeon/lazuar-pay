using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using NUnit.Framework;

namespace Lazuar.ArchitectureTests;

[TestFixture]
public class IntegrationEventSubscriptionTests
{
    /// <summary>
    /// Defined events with no in-process consumer yet. Publishing one still throws
    /// (InMemoryEventBus) so a forgotten Use*Subscriptions cannot look successful.
    /// </summary>
    private static readonly HashSet<string> EventsWithoutInProcessHandlers = new(StringComparer.Ordinal)
    {
        "Modules.Billing.Contracts.Events.ManualPaymentRecordedIntegrationEvent",
        "Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent",
    };

    [Test]
    public void Every_Integration_Event_Handler_Is_Subscribed()
    {
        var subscribeSource = LoadSubscribeSource();
        var missing = new List<string>();

        foreach (var (handler, eventType) in FindHandlers())
        {
            var eventName = eventType.Name;
            var handlerName = handler.Name;
            var subscribe = new Regex(
                $@"Subscribe<\s*[\w.]*{Regex.Escape(eventName)}\s*,\s*[\w.]*{Regex.Escape(handlerName)}\s*>",
                RegexOptions.CultureInvariant);
            if (!subscribe.IsMatch(subscribeSource))
            {
                missing.Add($"{handler.FullName} -> {eventType.FullName}");
            }
        }

        Assert.That(missing, Is.Empty,
            "IIntegrationEventHandler<T> must have a matching Subscribe<T, Handler> in Use*Subscriptions. Missing:\n"
            + string.Join("\n", missing));
    }

    [Test]
    public void Every_Integration_Event_Has_A_Subscribe_Or_Is_Explicitly_Unused()
    {
        var subscribeSource = LoadSubscribeSource();
        var missing = new List<string>();

        foreach (var eventType in FindIntegrationEvents())
        {
            var fullName = eventType.FullName ?? eventType.Name;
            if (EventsWithoutInProcessHandlers.Contains(fullName))
            {
                continue;
            }

            if (!subscribeSource.Contains($"Subscribe<{eventType.Name},")
                && !subscribeSource.Contains($"Subscribe<{fullName},"))
            {
                missing.Add(fullName);
            }
        }

        Assert.That(missing, Is.Empty,
            "Every IIntegrationEvent must have Subscribe<T, …> or be listed as unused. Missing:\n"
            + string.Join("\n", missing));
    }

    private static IEnumerable<(Type Handler, Type Event)> FindHandlers()
    {
        foreach (var type in LoadAssemblies().SelectMany(SafeGetTypes))
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType
                    && iface.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
                {
                    yield return (type, iface.GetGenericArguments()[0]);
                }
            }
        }
    }

    private static IEnumerable<Type> FindIntegrationEvents()
    {
        return LoadAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && typeof(IIntegrationEvent).IsAssignableFrom(t)
                        && t != typeof(IIntegrationEvent));
    }

    private static IEnumerable<Assembly> LoadAssemblies()
    {
        // Touch anchors so NetArch-style project refs are actually loaded.
        _ = new Assembly[]
        {
            typeof(BuildingBlocks.Application.IIntegrationEvent).Assembly,
            typeof(Lazuar.Api.EventHandlers.ApiKeyRevokedIntegrationEventHandler).Assembly,
            typeof(Modules.One.Infrastructure.DependencyInjection).Assembly,
            typeof(Modules.Messaging.Infrastructure.DependencyInjection).Assembly,
            typeof(Modules.CRM.Infrastructure.DependencyInjection).Assembly,
            typeof(Modules.Payments.Infrastructure.DependencyInjection).Assembly,
            typeof(Modules.Ops.Infrastructure.DependencyInjection).Assembly,
            typeof(Modules.Billing.Infrastructure.DependencyInjection).Assembly,
            typeof(Modules.Lhdn.Infrastructure.DependencyInjection).Assembly,
            typeof(Modules.Commerce.Infrastructure.DependencyInjection).Assembly,
            typeof(Modules.Communications.Infrastructure.DependencyInjection).Assembly,
            typeof(Modules.Billing.Contracts.Events.ManualPaymentRecordedIntegrationEvent).Assembly,
            typeof(Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent).Assembly,
            typeof(Modules.Payments.Contracts.Events.ApiCreditPurchasedIntegrationEvent).Assembly,
        };

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a =>
            {
                var name = a.GetName().Name ?? "";
                return name.StartsWith("Modules.", StringComparison.Ordinal)
                       || name.StartsWith("Lazuar.", StringComparison.Ordinal)
                       || name.StartsWith("BuildingBlocks.", StringComparison.Ordinal);
            });
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    private static string LoadSubscribeSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var apiRoot = Path.Combine(dir.FullName, "apps", "lazuar-api");
            if (!Directory.Exists(apiRoot))
            {
                apiRoot = dir.FullName;
            }

            var files = Directory.Exists(apiRoot)
                ? Directory.GetFiles(apiRoot, "*.cs", SearchOption.AllDirectories)
                : Array.Empty<string>();

            var subscribeFiles = files
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                            && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                            && !p.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(p => File.ReadAllText(p).Contains("Subscribe<", StringComparison.Ordinal))
                .ToList();

            if (subscribeFiles.Count > 0)
            {
                return string.Join("\n", subscribeFiles.Select(File.ReadAllText));
            }

            dir = dir.Parent;
        }

        Assert.Fail("Could not locate Subscribe< registrations.");
        return "";
    }
}
