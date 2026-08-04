using Application.Abstractions.Messaging;
using Application.Employees.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Employees;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("employees/{employeeId:guid}", async (
            Guid employeeId,
            IQueryHandler<GetEmployeeByIdQuery, EmployeeResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetEmployeeByIdQuery(employeeId);

            Result<EmployeeResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Employees);
    }
}
