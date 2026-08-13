using System.Security.Cryptography;
using Application.Abstractions.Storage;
using Domain.DocumentAttachments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Storage;

/// <summary>
/// Development-only local-filesystem <see cref="IFileStorage"/>. Files are addressed by a
/// server-generated <see cref="Guid"/>-based key - the client's filename is never used as a path.
/// </summary>
internal sealed class LocalFileStorage(
    IOptions<LocalFileStorageOptions> options,
    IOptions<AttachmentStorageOptions> attachmentOptions,
    IHostEnvironment hostEnvironment,
    ILogger<LocalFileStorage> logger) : IFileStorage
{
    public async Task<Result<StoredFile>> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        string rootPath = ResolveRootPath();
        string storageKey = Guid.NewGuid().ToString("N");
        string filePath = Path.Combine(rootPath, storageKey);
        bool storedSuccessfully = false;

        try
        {
            Directory.CreateDirectory(rootPath);

            await using var output = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            byte[] buffer = new byte[81_920];
            long totalBytes = 0;

            while (true)
            {
                int bytesRead = await content.ReadAsync(buffer, cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;

                if (totalBytes > attachmentOptions.Value.MaxFileSizeInBytes)
                {
                    return Result.Failure<StoredFile>(
                        DocumentAttachmentErrors.FileTooLarge(attachmentOptions.Value.MaxFileSizeInBytes));
                }

                hash.AppendData(buffer, 0, bytesRead);
                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            if (totalBytes == 0)
            {
                return Result.Failure<StoredFile>(DocumentAttachmentErrors.FileEmpty);
            }

            await output.FlushAsync(cancellationToken);

            string checksum = Convert.ToHexStringLower(hash.GetHashAndReset());
            storedSuccessfully = true;

            return new StoredFile(storageKey, totalBytes, checksum);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Failed to store an attachment file");

            return Result.Failure<StoredFile>(DocumentAttachmentErrors.StorageFailure);
        }
        finally
        {
            if (!storedSuccessfully)
            {
                TryDeletePartialFile(filePath);
            }
        }
    }

    public Task<Result<Stream>> OpenAsync(string storageKey, CancellationToken cancellationToken)
    {
        string filePath = Path.Combine(ResolveRootPath(), storageKey);

        if (!File.Exists(filePath))
        {
            return Task.FromResult(Result.Failure<Stream>(DocumentAttachmentErrors.ContentNotFound(storageKey)));
        }

#pragma warning disable CA2000 // Ownership transfers to the caller, who is responsible for disposing the stream.
        Stream stream = File.OpenRead(filePath);
#pragma warning restore CA2000

        return Task.FromResult(Result.Success(stream));
    }

    public Task<Result> DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        string filePath = Path.Combine(ResolveRootPath(), storageKey);

        try
        {
            File.Delete(filePath);

            return Task.FromResult(Result.Success());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Failed to delete attachment file with storage key {StorageKey}", storageKey);

            return Task.FromResult(Result.Failure(DocumentAttachmentErrors.StorageFailure));
        }
    }

    private string ResolveRootPath() =>
        Path.IsPathRooted(options.Value.RootPath)
            ? options.Value.RootPath
            : Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, options.Value.RootPath));

    private void TryDeletePartialFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Failed to delete a partial attachment file");
        }
    }
}
