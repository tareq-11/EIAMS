namespace Application.DocumentAttachments.GetContent;

public sealed class DocumentAttachmentContentResponse
{
    public Stream Content { get; init; }

    public string MimeType { get; init; }

    public string OriginalFilename { get; init; }
}
