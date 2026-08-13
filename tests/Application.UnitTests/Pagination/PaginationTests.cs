using Application.Abstractions.Pagination;
using Application.UnitTests.Abstractions;
using Domain.Organizations;

namespace Application.UnitTests.Pagination;

public sealed class PaginationTests : BaseHandlerTest
{
    [Fact]
    public async Task ToPagedResultAsync_Should_ReturnRequestedPageAndMetadata_WhenPageExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Organizations.AddRange(Enumerable.Range(1, 5)
            .Select(index => Organization.Create(
                Guid.NewGuid(),
                $"Organization {index}",
                $"ORG-{index}")));
        await context.SaveChangesAsync();

        // Act
        PagedResult<string> result = await context.Organizations
            .Select(organization => organization.Code)
            .OrderBy(code => code)
            .ToPagedResultAsync(2, 2, CancellationToken.None);

        // Assert
        result.Items.ShouldBe(["ORG-3", "ORG-4"]);
        result.Page.ShouldBe(2);
        result.PageSize.ShouldBe(2);
        result.TotalItems.ShouldBe(5);
        result.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task ToPagedResultAsync_Should_ReturnEmptyItemsAndPreserveTotal_WhenPageIsPastEnd()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Organizations.Add(Organization.Create(Guid.NewGuid(), "Organization", "ORG"));
        await context.SaveChangesAsync();

        // Act
        PagedResult<Guid> result = await context.Organizations
            .Select(organization => organization.Id)
            .OrderBy(id => id)
            .ToPagedResultAsync(2, 10, CancellationToken.None);

        // Assert
        result.Items.ShouldBeEmpty();
        result.TotalItems.ShouldBe(1);
        result.TotalPages.ShouldBe(1);
    }

    [Fact]
    public void PagedResult_Should_ReturnZeroTotalPages_WhenThereAreNoItems()
    {
        // Arrange
        var result = new PagedResult<Guid>([], 1, 20, 0);

        // Act
        int totalPages = result.TotalPages;

        // Assert
        totalPages.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, PaginationDefaults.MaximumPageSize + 1)]
    [InlineData(PaginationDefaults.MaximumPage + 1, 1)]
    public async Task ToPagedResultAsync_Should_Throw_WhenPaginationValuesAreOutsideContract(
        int page,
        int pageSize)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IOrderedQueryable<Guid> query = context.Organizations
            .Select(organization => organization.Id)
            .OrderBy(id => id);

        // Act
        Task Act() => query.ToPagedResultAsync(page, pageSize, CancellationToken.None);

        // Assert
        await Should.ThrowAsync<ArgumentOutOfRangeException>(Act);
    }
}
