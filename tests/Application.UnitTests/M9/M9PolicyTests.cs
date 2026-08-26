using Application.UnitTests.Abstractions;
using Domain.Sites;
using Domain.TransferInfos;
using Domain.Warehouses;
using Infrastructure.Policies;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.UnitTests.M9;

public sealed class M9PolicyTests : BaseHandlerTest
{
    [Fact]
    public void Site_Should_NormalizeGovernorateCode()
    {
        // Arrange
        var siteId = Guid.NewGuid();

        // Act
        Result<Site> result = Site.Create(
            siteId,
            Guid.NewGuid(),
            "Main site",
            "SITE-1",
            null,
            "  amman-1  ");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.GovernorateCode.ShouldBe("AMMAN-1");
    }

    [Theory]
    [InlineData("invalid value")]
    [InlineData("*")]
    [InlineData("123456789012345678901")]
    public void Site_Should_RejectInvalidGovernorateCode(string governorateCode)
    {
        // Arrange
        var siteId = Guid.NewGuid();

        // Act
        Result<Site> result = Site.Create(
            siteId,
            Guid.NewGuid(),
            "Main site",
            "SITE-1",
            null,
            governorateCode);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SiteErrors.GovernorateCodeInvalid);
    }

    [Fact]
    public async Task TransferPolicy_Should_BlockCrossGovernorateTransfer_WhenEnabled()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (Warehouse source, Warehouse destination) = await SeedWarehousesAsync(context, "AMMAN", "IRBID");
        var policy = new TransferPolicyService(
            context,
            Options.Create(new TransferPolicyOptions { BlockCrossGovernorate = true }));

        // Act
        Result result = await policy.EnsureTransferAllowedAsync(
            source.Id,
            destination.Id,
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TransferPolicies.CrossGovernorateBlocked");
    }

    [Fact]
    public async Task TransferPolicy_Should_AllowSameGovernorateTransfer_WhenEnabled()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (Warehouse source, Warehouse destination) = await SeedWarehousesAsync(context, "AMMAN", "AMMAN");
        var policy = new TransferPolicyService(
            context,
            Options.Create(new TransferPolicyOptions { BlockCrossGovernorate = true }));

        // Act
        Result result = await policy.EnsureTransferAllowedAsync(
            source.Id,
            destination.Id,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task TransferPolicy_Should_AllowTransfer_WhenPolicyIsDisabled()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (Warehouse source, Warehouse destination) = await SeedWarehousesAsync(context, "AMMAN", "IRBID");
        var policy = new TransferPolicyService(
            context,
            Options.Create(new TransferPolicyOptions { BlockCrossGovernorate = false }));

        // Act
        Result result = await policy.EnsureTransferAllowedAsync(
            source.Id,
            destination.Id,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    private static async Task<(Warehouse Source, Warehouse Destination)> SeedWarehousesAsync(
        TestDbContext context,
        string sourceGovernorate,
        string destinationGovernorate)
    {
        Result<Site> sourceSite = Site.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Source site",
            $"S-{Guid.NewGuid():N}",
            null,
            sourceGovernorate);
        Result<Site> destinationSite = Site.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Destination site",
            $"D-{Guid.NewGuid():N}",
            null,
            destinationGovernorate);
        sourceSite.IsSuccess.ShouldBeTrue();
        destinationSite.IsSuccess.ShouldBeTrue();

        var source = Warehouse.Create(
            Guid.NewGuid(), sourceSite.Value.Id, "Source", $"SW-{Guid.NewGuid():N}", "Main", true);
        var destination = Warehouse.Create(
            Guid.NewGuid(), destinationSite.Value.Id, "Destination", $"DW-{Guid.NewGuid():N}", "Main", true);
        context.AddRange(sourceSite.Value, destinationSite.Value, source, destination);
        await context.SaveChangesAsync();
        return (source, destination);
    }
}
