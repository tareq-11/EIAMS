using Domain.Common;
using Domain.DocumentAttachments;
using SharedKernel;

namespace Application.UnitTests.DocumentAttachments;

public sealed class DocumentAttachmentArchivalTests
{
    [Fact]
    public void ArchiveAsReplacedBy_Should_PreserveReplacementMetadata_WhenAttachmentIsActiveSignedOriginal()
    {
        // Arrange
        var attachmentId = Guid.NewGuid();
        var replacementId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        DateTime archivedAtUtc = DateTime.UtcNow;
        DocumentAttachment attachment = CreateAttachment(attachmentId, AttachmentType.SignedOriginal);

        // Act
        Result result = attachment.ArchiveAsReplacedBy(replacementId, userId, archivedAtUtc);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        attachment.IsActive.ShouldBeFalse();
        attachment.ArchivedAtUtc.ShouldBe(archivedAtUtc);
        attachment.ArchivedBy.ShouldBe(userId);
        attachment.ReplacesAttachmentId.ShouldBeNull();
        attachment.DomainEvents.ShouldContain(item => item is DocumentAttachmentArchivedDomainEvent);
    }

    [Fact]
    public void ArchiveAsReplacedBy_Should_ReturnExactError_WhenAttachmentIsAlreadyArchived()
    {
        // Arrange
        DocumentAttachment attachment = CreateAttachment(Guid.NewGuid(), AttachmentType.SignedOriginal);
        attachment.ArchiveAsReplacedBy(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow).IsSuccess.ShouldBeTrue();

        // Act
        Result result = attachment.ArchiveAsReplacedBy(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DocumentAttachmentErrors.AlreadyArchived(attachment.Id));
    }

    [Fact]
    public void ArchiveAsReplacedBy_Should_ReturnExactError_WhenAttachmentIsSupporting()
    {
        // Arrange
        DocumentAttachment attachment = CreateAttachment(Guid.NewGuid(), AttachmentType.Supporting);

        // Act
        Result result = attachment.ArchiveAsReplacedBy(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DocumentAttachmentErrors.OnlySignedOriginalCanBeArchived(attachment.Id));
    }

    [Fact]
    public void ArchiveAsReplacedBy_Should_ReturnExactError_WhenReplacementIsSameAttachment()
    {
        // Arrange
        DocumentAttachment attachment = CreateAttachment(Guid.NewGuid(), AttachmentType.SignedOriginal);

        // Act
        Result result = attachment.ArchiveAsReplacedBy(attachment.Id, Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DocumentAttachmentErrors.InvalidReplacement(attachment.Id));
    }

    private static DocumentAttachment CreateAttachment(Guid id, AttachmentType attachmentType) =>
        DocumentAttachment.Create(
            id,
            Guid.NewGuid(),
            attachmentType,
            Guid.NewGuid().ToString("N"),
            "document.pdf",
            "application/pdf",
            10,
            "checksum",
            Guid.NewGuid(),
            DateTime.UtcNow);
}
