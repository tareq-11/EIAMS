using Application.Abstractions.Data;
using Application.Abstractions.Policies;
using Domain.TransferInfos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Policies;

internal sealed class TransferPolicyService(
    IApplicationDbContext context,
    IOptions<TransferPolicyOptions> options) : ITransferPolicyService
{
    public async Task<Result> EnsureTransferAllowedAsync(
        Guid sourceWarehouseId,
        Guid destinationWarehouseId,
        CancellationToken cancellationToken)
    {
        TransferPolicyOptions policy = options.Value;

        if (!policy.BlockCrossGovernorate)
        {
            return Result.Success();
        }

        var locations = await (
            from warehouse in context.Warehouses.AsNoTracking()
            join site in context.Sites.AsNoTracking() on warehouse.SiteId equals site.Id
            where warehouse.Id == sourceWarehouseId || warehouse.Id == destinationWarehouseId
            select new { warehouse.Id, site.GovernorateCode })
            .ToListAsync(cancellationToken);

        string? sourceCode = locations
            .SingleOrDefault(item => item.Id == sourceWarehouseId)?.GovernorateCode;
        string? destinationCode = locations
            .SingleOrDefault(item => item.Id == destinationWarehouseId)?.GovernorateCode;

        if (string.IsNullOrWhiteSpace(sourceCode) || string.IsNullOrWhiteSpace(destinationCode))
        {
            return policy.RequireGovernorateCodeWhenEnabled
                ? Result.Failure(TransferPolicyErrors.GovernorateRequired(
                    sourceWarehouseId, destinationWarehouseId))
                : Result.Success();
        }

        return string.Equals(sourceCode, destinationCode, StringComparison.Ordinal)
            ? Result.Success()
            : Result.Failure(TransferPolicyErrors.CrossGovernorateBlocked(
                sourceWarehouseId,
                destinationWarehouseId,
                sourceCode,
                destinationCode));
    }
}
