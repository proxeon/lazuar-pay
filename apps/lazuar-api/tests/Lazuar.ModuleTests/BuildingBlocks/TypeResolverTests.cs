using BuildingBlocks.Infrastructure;
using NUnit.Framework;

namespace Lazuar.ModuleTests.BuildingBlocks;

[TestFixture]
public class TypeResolverTests
{
    [Test]
    public void Known_Assembly_Qualified_Name_Resolves_And_Is_Cached()
    {
        var type = typeof(TypeResolver);
        var name = type.AssemblyQualifiedName!;

        Assert.That(TypeResolver.Resolve(name), Is.EqualTo(type));
        Assert.That(TypeResolver.IsCached(name), Is.True);
    }

    [Test]
    public void Failed_Resolve_Is_Not_Cached()
    {
        const string missing = "Lazuar.DoesNotExist.LatePluginEvent, Lazuar.DoesNotExist";

        Assert.That(TypeResolver.Resolve(missing), Is.Null);
        Assert.That(TypeResolver.IsCached(missing), Is.False);
        Assert.That(TypeResolver.Resolve(missing), Is.Null);
    }

    [Test]
    public void Full_Name_Fallback_Finds_Already_Loaded_Type()
    {
        var type = typeof(TypeResolverTests);
        Assert.That(TypeResolver.Resolve(type.FullName!), Is.EqualTo(type));
    }
}
