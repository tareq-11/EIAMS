using Application.Abstractions.Audit;
using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Abstractions.Behaviors;

internal static class AuditCommandDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        IAuditOperationContextAccessor accessor,
        IRequestAuditContext requestAudit,
        IUserContext userContext,
        ICommandHandler<TCommand, TResponse> inner)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            using IDisposable scope = accessor.BeginScope(CreateDescriptor());

            return await inner.Handle(command, cancellationToken);
        }

        private AuditOperationDescriptor CreateDescriptor()
        {
            Guid? userId = userContext.UserIdOrDefault;
            string? requestId = requestAudit.GetRequestId();

            if (requestId is null)
            {
                return new AuditOperationDescriptor(
                    Guid.NewGuid(),
                    requestId,
                    userId,
                    requestAudit.GetClientIpAddress(),
                    typeof(TCommand).Name,
                    AuditOperationKind.Background);
            }

            return new AuditOperationDescriptor(
                Guid.NewGuid(),
                requestId,
                userId,
                requestAudit.GetClientIpAddress(),
                typeof(TCommand).Name,
                userId.HasValue ? AuditOperationKind.Http : AuditOperationKind.Anonymous);
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        IAuditOperationContextAccessor accessor,
        IRequestAuditContext requestAudit,
        IUserContext userContext,
        ICommandHandler<TCommand> inner)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            using IDisposable scope = accessor.BeginScope(CreateDescriptor());

            return await inner.Handle(command, cancellationToken);
        }

        private AuditOperationDescriptor CreateDescriptor()
        {
            Guid? userId = userContext.UserIdOrDefault;
            string? requestId = requestAudit.GetRequestId();

            if (requestId is null)
            {
                return new AuditOperationDescriptor(
                    Guid.NewGuid(),
                    requestId,
                    userId,
                    requestAudit.GetClientIpAddress(),
                    typeof(TCommand).Name,
                    AuditOperationKind.Background);
            }

            return new AuditOperationDescriptor(
                Guid.NewGuid(),
                requestId,
                userId,
                requestAudit.GetClientIpAddress(),
                typeof(TCommand).Name,
                userId.HasValue ? AuditOperationKind.Http : AuditOperationKind.Anonymous);
        }
    }
}
