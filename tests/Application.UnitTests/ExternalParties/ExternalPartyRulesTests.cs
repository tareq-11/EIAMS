using Domain.Common;
using Domain.Custodies;
using Domain.ExternalParties;
using Shouldly;

namespace Application.UnitTests.ExternalParties;

public sealed class ExternalPartyRulesTests
{
    [Fact]
    public void Create_Should_Normalize_Identity_And_Start_Active()
    {
        var party = ExternalParty.Create(
            Guid.NewGuid(),
            "  شركة التوريد  ",
            " ext-001 ",
            " 0790000000 ",
            " ملاحظة ");

        party.NameAr.ShouldBe("شركة التوريد");
        party.NormalizedNameAr.ShouldBe("شركة التوريد");
        party.Code.ShouldBe("ext-001");
        party.NormalizedCode.ShouldBe("EXT-001");
        party.ContactInfo.ShouldBe("0790000000");
        party.Notes.ShouldBe("ملاحظة");
        party.Status.ShouldBe(Status.Active);
        party.RowVersion.ShouldBe(1);
    }

    [Fact]
    public void Update_And_Status_Change_Should_Advance_RowVersion()
    {
        var party = ExternalParty.Create(
            Guid.NewGuid(), "شركة أولى", null, null, null);

        party.UpdateDetails("شركة ثانية", "EXT-2", null, null);
        party.SetStatus(Status.Inactive);

        party.RowVersion.ShouldBe(3);
        party.Status.ShouldBe(Status.Inactive);
        party.NameAr.ShouldBe("شركة ثانية");
    }

    [Fact]
    public void Operational_Custody_Should_Reject_Employee_Holder()
    {
        SharedKernel.Result<Custody> result = Custody.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PartyType.Employee,
            Guid.NewGuid(),
            CustodyKind.Operational,
            Guid.NewGuid(),
            DateTime.UtcNow);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CustodyErrors.OperationalRequiresNonEmployee);
    }

    [Theory]
    [InlineData(PartyType.OrganizationalUnit)]
    [InlineData(PartyType.Site)]
    [InlineData(PartyType.External)]
    public void Operational_Custody_Should_Accept_NonEmployee_Holders(PartyType holderType)
    {
        SharedKernel.Result<Custody> result = Custody.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            holderType,
            Guid.NewGuid(),
            CustodyKind.Operational,
            Guid.NewGuid(),
            DateTime.UtcNow);

        result.IsSuccess.ShouldBeTrue();
    }
}
