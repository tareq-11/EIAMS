namespace Application.DocumentAttachments.Upload;

internal static class FileSignatureValidator
{
    private const int MaximumSignatureLength = 8;

    internal static async Task<bool> MatchesMimeTypeAsync(
        Stream content,
        string mimeType,
        CancellationToken cancellationToken)
    {
        if (!content.CanRead || !content.CanSeek)
        {
            return false;
        }

        long originalPosition = content.Position;
        byte[] header = new byte[MaximumSignatureLength];
        int bytesRead = await content.ReadAsync(header, cancellationToken);
        content.Position = originalPosition;

        ReadOnlySpan<byte> actual = header.AsSpan(0, bytesRead);

        return mimeType.ToUpperInvariant() switch
        {
            "APPLICATION/PDF" => StartsWith(actual, "%PDF-"u8),
            "IMAGE/JPEG" => StartsWith(actual, [0xFF, 0xD8, 0xFF]),
            "IMAGE/PNG" => StartsWith(actual, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            "IMAGE/TIFF" => StartsWith(actual, [0x49, 0x49, 0x2A, 0x00]) ||
                            StartsWith(actual, [0x4D, 0x4D, 0x00, 0x2A]),
            _ => false
        };
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> signature) =>
        value.StartsWith(signature);
}
