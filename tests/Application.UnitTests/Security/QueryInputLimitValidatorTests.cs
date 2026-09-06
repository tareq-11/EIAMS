using Application.Abstractions.Pagination;
using Application.AuditLogs.GetAuditLogs;
using Application.ExternalParties.GetList;
using Application.Users.GetList;

namespace Application.UnitTests.Security;

public sealed class QueryInputLimitValidatorTests
{
    [Fact]
    public async Task UserSearch_Should_RejectOversizedSearchAndOverflowPronePage()
    {
        var validator = new GetUsersQueryValidator();
        var query = new GetUsersQuery(
            new string('x', 201),
            Page: PaginationDefaults.MaximumPage + 1);

        FluentValidation.Results.ValidationResult result = await validator.ValidateAsync(query);

        result.Errors.ShouldContain(error => error.PropertyName == nameof(query.Search));
        result.Errors.ShouldContain(error => error.PropertyName == nameof(query.Page));
    }

    [Fact]
    public async Task ExternalPartySearch_Should_RejectOversizedSearchAndPageSize()
    {
        var validator = new GetExternalPartiesQueryValidator();
        var query = new GetExternalPartiesQuery(
            new string('x', 201),
            null,
            PaginationDefaults.DefaultPage,
            PaginationDefaults.MaximumPageSize + 1);

        FluentValidation.Results.ValidationResult result = await validator.ValidateAsync(query);

        result.Errors.ShouldContain(error => error.PropertyName == nameof(query.Search));
        result.Errors.ShouldContain(error => error.PropertyName == nameof(query.PageSize));
    }

    [Fact]
    public async Task AuditSearch_Should_RejectOversizedSearchAndOverflowPronePage()
    {
        var validator = new GetAuditLogsQueryValidator();
        var query = new GetAuditLogsQuery(
            Search: new string('x', 201),
            Page: PaginationDefaults.MaximumPage + 1);

        FluentValidation.Results.ValidationResult result = await validator.ValidateAsync(query);

        result.Errors.ShouldContain(error => error.PropertyName == nameof(query.Search));
        result.Errors.ShouldContain(error => error.PropertyName == nameof(query.Page));
    }
}
