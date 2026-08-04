using System.Diagnostics;
using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Abstractions.Behaviors;

internal static class PerformanceDecorator
{
    private const int DefaultThresholdMilliseconds = 500;

    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ILogger<CommandHandler<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            Result<TResponse> result = await innerHandler.Handle(command, cancellationToken);

            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > DefaultThresholdMilliseconds)
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
        ILogger<CommandBaseHandler<TCommand>> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            Result result = await innerHandler.Handle(command, cancellationToken);

            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > DefaultThresholdMilliseconds)
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
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            Result<TResponse> result = await innerHandler.Handle(query, cancellationToken);

            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > DefaultThresholdMilliseconds)
            {
                logger.LogWarning(
                    "Long running query {Query} completed in {ElapsedMilliseconds}ms",
                    typeof(TQuery).Name,
                    stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
    }
}
