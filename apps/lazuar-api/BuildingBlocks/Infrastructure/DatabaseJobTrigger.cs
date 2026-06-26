using System;
using System.Threading;
using System.Threading.Tasks;

namespace BuildingBlocks.Infrastructure;

public class DatabaseJobTrigger
{
    private volatile TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Trigger()
    {
        var currentTcs = Interlocked.Exchange(ref _tcs, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        currentTcs.TrySetResult();
    }

    public async ValueTask WaitAsync(CancellationToken ct)
    {
        try
        {
            await _tcs.Task.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
