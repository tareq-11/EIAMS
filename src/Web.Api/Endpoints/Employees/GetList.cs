using Application.Abstractions.Messaging;
using Application.Employees.GetList;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Employees;

internal sealed class GetList : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("employees", async (
            Guid? orgUnitId,
            Status? status,
            IQueryHandler<GetEmployeesQuery, List<EmployeeResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetEmployeesQuery(orgUnitId, status);

            Result<List<EmployeeResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Employees);
    }
}
