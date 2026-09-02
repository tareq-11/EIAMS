using System.Diagnostics;
using System.Diagnostics.Metrics;
using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.Abstractions.Behaviors;

internal static class PerformanceDecorator
{
    internal const string MeterName = "CleanArchitecture.Application";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Histogram<double> HandlerDuration = Meter.CreateHistogram<double>(
        "application.handler.duration",
        "ms",
        "Application command and query handler duration.");

    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ILogger<CommandHandler<TCommand, TResponse>> logger,
        IOptions<PerformanceMonitoringOptions> options)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            Result<TResponse> result = await innerHandler.Handle(command, cancellationToken);

            stopwatch.Stop();

            RecordDuration("command", typeof(TCommand).Name, stopwatch.Elapsed.TotalMilliseconds, result.IsSuccess);

            if (stopwatch.ElapsedMilliseconds > options.Value.SlowHandlerThresholdMilliseconds)
            {
                logger.LogWarning(
                    "Long running command {Command} completed in {ElapsedMilliseconds}ms",
                    typeof(TCommand).Name,
                    stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        ILogger<CommandBaseHandler<TCommand>> logger,
        IOptions<PerformanceMonitoringOptions> options)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            Result result = await innerHandler.Handle(command, cancellationToken);

            stopwatch.Stop();

            RecordDuration("command", typeof(TCommand).Name, stopwatch.Elapsed.TotalMilliseconds, result.IsSuccess);

            if (stopwatch.ElapsedMilliseconds > options.Value.SlowHandlerThresholdMilliseconds)
            {
                logger.LogWarning(
                    "Long running command {Command} completed in {ElapsedMilliseconds}ms",
                    typeof(TCommand).Name,
                    stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILogger<QueryHandler<TQuery, TResponse>> logger,
        IOptions<PerformanceMonitoringOptions> options)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            Result<TResponse> result = await innerHandler.Handle(query, cancellationToken);

            stopwatch.Stop();

            RecordDuration("query", typeof(TQuery).Name, stopwatch.Elapsed.TotalMilliseconds, result.IsSuccess);

            if (stopwatch.ElapsedMilliseconds > options.Value.SlowHandlerThresholdMilliseconds)
            {
                logger.LogWarning(
                    "Long running query {Query} completed in {ElapsedMilliseconds}ms",
                    typeof(TQuery).Name,
                    stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
    }

    private static void RecordDuration(string handlerType, string handlerName, double duration, bool succeeded) =>
        HandlerDuration.Record(
            duration,
            new KeyValuePair<string, object?>("handler.type", handlerType),
            new KeyValuePair<string, object?>("handler.name", handlerName),
            new KeyValuePair<string, object?>("handler.succeeded", succeeded));
}

public sealed class PerformanceMonitoringOptions
{
    public const string SectionName = "PerformanceMonitoring";

    public int SlowHandlerThresholdMilliseconds { get; set; } = 500;
}
