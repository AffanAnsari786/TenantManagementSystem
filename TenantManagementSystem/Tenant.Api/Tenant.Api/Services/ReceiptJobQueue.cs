using System.Threading.Channels;

namespace Tenant.Api.Services;

public interface IReceiptJobQueue
{
    /// <summary>
    /// Non-blocking enqueue of a record id for asynchronous receipt
    /// pre-generation. Callers never await actual PDF work — this is strictly
    /// best-effort warm-up.
    /// </summary>
    void Enqueue(Guid recordPublicId);

    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
}

public sealed class ReceiptJobQueue : IReceiptJobQueue
{
    // Bounded channel with drop-oldest policy: under a write storm we keep
    // the most recent requests instead of letting the queue grow unbounded.
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(capacity: 1024)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public void Enqueue(Guid recordPublicId)
    {
        // TryWrite never blocks; with DropOldest it always succeeds.
        _channel.Writer.TryWrite(recordPublicId);
    }

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
