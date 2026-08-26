using Application.Abstractions.Posting;
using Domain.Assets;
using Domain.Common;
using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.DocumentLines;
using Domain.IssueTos;
using Domain.ReceivingInfos;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Regression;

[Collection(nameof(IntegrationTestCollection))]
public sealed class CustodyAssignmentRegressionTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public CustodyAssignmentRegressionTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task CustodyFlow_OperationalToPersonal_Should_TransitionStatesAndPreserveHistory()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        DateTime now = DateTime.UtcNow;

        // 2. Receive 1 Asset
        WarehouseDocument receiving = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            [(seed.AssetMaterialId, DocumentLineType.Asset, 1m)],
            (document, context) =>
            {
                ReceivingInfo info = ReceivingInfo.Create(document.Id, "Vendor", "INV-1", ReceivingType.Supplier).Value;
                context.ReceivingInfos.Add(info);
            });

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            (await coordinator.PostAsync(receiving.Id, receiving.RowVersion, seed.ManagerUserId, CancellationToken.None)).IsSuccess.ShouldBeTrue();
        }

        // 3. Get Created Asset ID
        Guid assetId;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Asset asset = await context.Assets.SingleAsync(a => a.MaterialId == seed.AssetMaterialId);
            assetId = asset.Id;
        }

        // 4. Open Operational Custody (e.g. from Issue to OrgUnit)
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Custody opCustody = Custody.Open(
                Guid.NewGuid(),
                assetId,
                PartyType.OrganizationalUnit,
                seed.OrgUnitId,
                CustodyKind.Operational,
                receiving.Id,
                now).Value;

            context.Custodies.Add(opCustody);
            await context.SaveChangesAsync();

            // 5. Reassign to Personal Custody: Close Operational, Open Personal
            opCustody.Close(null, now.AddHours(2));

            Custody personalCustody = Custody.Open(
                Guid.NewGuid(),
                assetId,
                PartyType.Employee,
                seed.EmployeeId,
                CustodyKind.Personal,
                receiving.Id,
                now.AddHours(2)).Value;

            CustodyHistory history = CustodyHistory.Create(
                Guid.NewGuid(),
                personalCustody.Id,
                CustodyStatus.Active,
                CustodyStatus.Closed,
                seed.KeeperUserId,
                now.AddHours(2),
                "Assigned to employee").Value;

            context.Custodies.Add(personalCustody);
            context.CustodyHistories.Add(history);
            await context.SaveChangesAsync();

            // 6. Assertions
            List<Custody> allCustodies = await context.Custodies.Where(c => c.AssetId == assetId).OrderBy(c => c.FromUtc).ToListAsync();
            allCustodies.Count.ShouldBe(2);

            Custody closedOp = allCustodies[0];
            closedOp.Status.ShouldBe(CustodyStatus.Closed);
            closedOp.CustodyKind.ShouldBe(CustodyKind.Operational);

            Custody activePers = allCustodies[1];
            activePers.Status.ShouldBe(CustodyStatus.Active);
            activePers.CustodyKind.ShouldBe(CustodyKind.Personal);
            activePers.HolderId.ShouldBe(seed.EmployeeId);

            CustodyHistory recordedHistory = await context.CustodyHistories.SingleAsync(h => h.CustodyId == personalCustody.Id);
            recordedHistory.FromStatus.ShouldBe(CustodyStatus.Active);
            recordedHistory.ToStatus.ShouldBe(CustodyStatus.Closed);
        }
    }
}
