using System.Collections.Concurrent;

namespace BuildingBlocks.Infrastructure;

public static class TypeResolver
{
    private static readonly ConcurrentDictionary<string, Type> TypeCache = new();

    public static Type? Resolve(string typeName)
    {
        if (TypeCache.TryGetValue(typeName, out var cached))
        {
            return cached;
        }

        var resolvedType = Type.GetType(typeName);
        if (resolvedType == null)
        {
            var cleanName = typeName.Split(',')[0].Trim();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolvedType = assembly.GetType(cleanName);
                if (resolvedType != null)
                {
                    break;
                }
            }
        }

        if (resolvedType != null)
        {
            TypeCache.TryAdd(typeName, resolvedType);
        }

        return resolvedType;
    }

    internal static bool IsCached(string typeName) => TypeCache.ContainsKey(typeName);
}
