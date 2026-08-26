using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentLines;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using IntegrationTests.Regression;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Authorization;

[Collection(nameof(IntegrationTestCollection))]
public sealed class SignedCopyGateTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public SignedCopyGateTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData(DocumentType.Receiving)]
    [InlineData(DocumentType.Opening)]
    [InlineData(DocumentType.Issue)]
    [InlineData(DocumentType.Transfer)]
    [InlineData(DocumentType.Return)]
    [InlineData(DocumentType.Adjustment)]
    public async Task Post_Should_Fail_When_DocumentLacksSignedOriginalAttachment(DocumentType documentType)
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        // 2. Create Draft document WITHOUT signed copy
        var doc = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            seed.WarehouseId,
            documentType,
            $"SYS-RECV-{Guid.NewGuid():N}"[..15]);

        doc.UpdatePaperReference("PAPER-NO-SIGN", DateTime.UtcNow.Year).IsSuccess.ShouldBeTrue();
        doc.Submit().IsSuccess.ShouldBeTrue();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.WarehouseDocuments.Add(doc);

            Result<DocumentLine> docLineResult = DocumentLine.Create(
                Guid.NewGuid(),
                doc.Id,
                seed.NormalMaterialId,
                DocumentLineType.Normal,
                5m,
                null,
                5m,
                null,
                null,
                null);

            context.DocumentLines.Add(docLineResult.Value);

            await context.SaveChangesAsync();
        }

        // 3. Post should fail because SignedOriginal is missing
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            Result<PostingOutcome> postResult = await coordinator.PostAsync(doc.Id, doc.RowVersion, seed.ManagerUserId, CancellationToken.None);

            postResult.IsFailure.ShouldBeTrue();
            postResult.Error.Code.ShouldBe("WarehouseDocuments.SignedCopyRequired");
        }
    }
}
