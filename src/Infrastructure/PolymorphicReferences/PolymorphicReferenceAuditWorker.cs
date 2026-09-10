using Application.Abstractions.PolymorphicReferences;
using Infrastructure.BackgroundWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.PolymorphicReferences;

internal sealed class PolymorphicReferenceAuditWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PolymorphicReferenceAuditOptions> options,
    ILogger<PolymorphicReferenceAuditWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PolymorphicReferenceAuditOptions settings = options.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Polymorphic reference audit worker is disabled");
            return;
        }

        try
        {
            await Task.Delay(settings.InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        using var timer = new PeriodicTimer(settings.Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            long startedTimestamp = BackgroundWorkerMetrics.Start();
            string outcome = await RunAuditCycleAsync(settings.MaximumLoggedFindingsPerCycle, stoppingToken);
            BackgroundWorkerMetrics.Record("polymorphic_reference_audit", outcome, startedTimestamp);
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>Runs one bounded audit cycle and returns a low-cardinality outcome for telemetry.</summary>
    internal async Task<string> RunAuditCycleAsync(int maximumFindings, CancellationToken cancellationToken = default)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IPolymorphicReferenceAuditor auditor =
                scope.ServiceProvider.GetRequiredService<IPolymorphicReferenceAuditor>();
            PolymorphicReferenceAuditResult result = await auditor.AuditAsync(maximumFindings, cancellationToken);

            if (result.SkippedBecauseLockUnavailable)
            {
                logger.LogDebug("Skipped polymorphic reference audit because another instance owns the lock");
                return "skipped_lock_unavailable";
            }

            foreach (PolymorphicReferenceFinding finding in result.Findings)
            {
                logger.LogWarning(
                    "Invalid polymorphic reference: {SourceType} {SourceId}, party {PartyType}/{PartyId}, status {LifecycleStatus}, reason {Reason}",
                    finding.SourceType,
                    finding.SourceId,
                    finding.PartyType,
                    finding.PartyId,
                    finding.LifecycleStatus,
                    finding.Reason);
            }

            logger.LogInformation(
                "Polymorphic reference audit completed with {TotalFindings} findings; logged {LoggedFindings}",
                result.TotalFindings,
                result.Findings.Count);
            return "succeeded";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal host shutdown.
            return "cancelled";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Polymorphic reference audit cycle failed");
            return "failed";
        }
    }
}
