using System.Threading.Channels;

namespace BuildingBlocks.Infrastructure;

public class DatabaseJobTrigger
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Trigger() => _channel.Writer.TryWrite(true);

    public async ValueTask WaitAsync(CancellationToken ct) => await _channel.Reader.WaitToReadAsync(ct);
}
