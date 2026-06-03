using System.Collections.Concurrent;

namespace BuildingBlocks.Infrastructure;

public static class TypeResolver
{
    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new();

    public static Type? Resolve(string typeName)
    {
        return TypeCache.GetOrAdd(typeName, name =>
        {
            // Attempt standard type resolution (works for AssemblyQualifiedName)
            var resolvedType = Type.GetType(name);
            if (resolvedType != null)
            {
                return resolvedType;
            }

            // Fallback: search loaded assemblies for full name or short name match
            var cleanName = name.Split(',')[0].Trim(); // Strip assembly details
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolvedType = assembly.GetType(cleanName);
                if (resolvedType != null)
                {
                    return resolvedType;
                }
            }

            return null;
        });
    }
}
