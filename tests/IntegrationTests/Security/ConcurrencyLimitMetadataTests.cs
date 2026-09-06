using Microsoft.AspNetCore.RateLimiting;
using Web.Api.Controllers.DocumentAttachments;
using Web.Api.Controllers.InventoryAdjustments;
using Web.Api.Controllers.Reports;
using Web.Api.Controllers.WarehouseDocuments;
using Web.Api.Infrastructure;

namespace IntegrationTests.Security;

public sealed class ConcurrencyLimitMetadataTests
{
    public static TheoryData<Type, string> ExpensiveActions => new()
    {
        { typeof(UploadDocumentAttachmentController), RateLimitingPolicies.Upload },
        { typeof(PostDocumentController), RateLimitingPolicies.Posting },
        { typeof(PostInventoryAdjustmentController), RateLimitingPolicies.Posting },
        { typeof(ReverseInventoryAdjustmentController), RateLimitingPolicies.Posting },
        { typeof(GetDashboardReportController), RateLimitingPolicies.Reporting },
        { typeof(GetInventoryReportController), RateLimitingPolicies.Reporting },
        { typeof(GetDocumentsReportController), RateLimitingPolicies.Reporting },
        { typeof(GetAssetsReportController), RateLimitingPolicies.Reporting },
        { typeof(GetCountAdjustmentsReportController), RateLimitingPolicies.Reporting }
    };

    [Theory]
    [MemberData(nameof(ExpensiveActions))]
    public void ExpensiveActions_Should_HaveDedicatedConcurrencyPolicy(Type controllerType, string policyName)
    {
        EnableRateLimitingAttribute? attribute = controllerType
            .GetMethod("Handle")!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
            .Cast<EnableRateLimitingAttribute>()
            .SingleOrDefault();

        attribute.ShouldNotBeNull();
        attribute.PolicyName.ShouldBe(policyName);
    }
}
