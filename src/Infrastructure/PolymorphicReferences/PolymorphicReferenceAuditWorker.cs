using Application.Abstractions.PolymorphicReferences;
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

        await Task.Delay(settings.InitialDelay, stoppingToken);

        using var timer = new PeriodicTimer(settings.Interval);

        do
        {
            await RunAuditCycleAsync(settings.MaximumLoggedFindingsPerCycle, stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunAuditCycleAsync(int maximumFindings, CancellationToken cancellationToken)
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
                return;
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
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Polymorphic reference audit cycle failed");
        }
    }
}
