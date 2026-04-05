using Microsoft.EntityFrameworkCore;
using Tenant.Api.Data;

namespace Tenant.Api.Services;

/// <summary>
/// Background worker that drains <see cref="IReceiptJobQueue"/> and asks
/// <see cref="IReceiptService"/> to materialise the PDF for each record.
/// Failures are logged and swallowed — warm-up is best-effort and must not
/// take the host down.
/// </summary>
public sealed class ReceiptWarmingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReceiptJobQueue _queue;
    private readonly ILogger<ReceiptWarmingWorker> _logger;

    public ReceiptWarmingWorker(
        IServiceScopeFactory scopeFactory,
        IReceiptJobQueue queue,
        ILogger<ReceiptWarmingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Receipt warming worker started.");

        await foreach (var recordPublicId in _queue.DequeueAllAsync(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested) break;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var receiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();

                // Warming bypasses authorization (the worker runs with no user
                // context): we call the internal warming method which only
                // needs the record id.
                await receiptService.WarmReceiptAsync(recordPublicId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Receipt warm-up failed for record {RecordId}", recordPublicId);
            }
        }

        _logger.LogInformation("Receipt warming worker stopped.");
    }
}
