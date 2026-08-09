using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Messaging.Infrastructure.Messaging;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Messaging;

[TestFixture]
public class ConsoleMessagingServiceTests
{
    [Test]
    public async Task SendMessageAsync_CompletesWithoutError()
    {
        var sut = new ConsoleMessagingService(NullLogger<ConsoleMessagingService>.Instance);
        var act = async () => await sut.SendMessageAsync("+60123456789", "hello");
        await act.Should().NotThrowAsync();
    }
}
