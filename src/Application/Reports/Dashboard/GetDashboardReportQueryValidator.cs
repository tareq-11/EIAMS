using FluentValidation;

namespace Application.Reports.Dashboard;

public sealed class GetDashboardReportQueryValidator : AbstractValidator<GetDashboardReportQuery>
{
    public GetDashboardReportQueryValidator()
    {
        RuleFor(query => query.WarehouseId).NotEqual(Guid.Empty).When(query => query.WarehouseId.HasValue);
    }
}
