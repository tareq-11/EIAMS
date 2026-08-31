using Application.Abstractions.Messaging;

namespace Application.Reports.Dashboard;

public sealed record GetDashboardReportQuery(Guid? WarehouseId) : IQuery<DashboardReportResponse>;
