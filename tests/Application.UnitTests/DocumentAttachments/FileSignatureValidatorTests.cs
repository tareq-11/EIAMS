using Application.DocumentAttachments.Upload;

namespace Application.UnitTests.DocumentAttachments;

public sealed class FileSignatureValidatorTests
{
    public static TheoryData<string, byte[]> SupportedSignatures => new()
    {
        { "application/pdf", "%PDF-1.7"u8.ToArray() },
        { "image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0] },
        { "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A] },
        { "image/tiff", [0x49, 0x49, 0x2A, 0x00] },
        { "image/tiff", [0x4D, 0x4D, 0x00, 0x2A] }
    };

    [Theory]
    [MemberData(nameof(SupportedSignatures))]
    public async Task MatchesMimeTypeAsync_Should_ReadUntilSignatureIsComplete(
        string mimeType,
        byte[] content)
    {
        await using var stream = new OneByteAtATimeStream(content);

        bool result = await FileSignatureValidator.MatchesMimeTypeAsync(
            stream,
            mimeType,
            CancellationToken.None);

        result.ShouldBeTrue();
        stream.Position.ShouldBe(0);
    }

    [Fact]
    public async Task MatchesMimeTypeAsync_Should_RejectTruncatedSignatureAndRestorePosition()
    {
        await using var stream = new MemoryStream("%PD"u8.ToArray());

        bool result = await FileSignatureValidator.MatchesMimeTypeAsync(
            stream,
            "application/pdf",
            CancellationToken.None);

        result.ShouldBeFalse();
        stream.Position.ShouldBe(0);
    }

    [Fact]
    public async Task MatchesMimeTypeAsync_Should_RejectUnsupportedMimeType()
    {
        await using var stream = new MemoryStream("plain text"u8.ToArray());

        bool result = await FileSignatureValidator.MatchesMimeTypeAsync(
            stream,
            "text/plain",
            CancellationToken.None);

        result.ShouldBeFalse();
    }

    private sealed class OneByteAtATimeStream(byte[] content) : MemoryStream(content)
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
