using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Storage;

internal sealed class LocalFileStorageOptionsValidator(IHostEnvironment hostEnvironment)
    : IValidateOptions<LocalFileStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, LocalFileStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            return ValidateOptionsResult.Fail("AttachmentStorage:Local:RootPath is required.");
        }

        string fullPath;
        try
        {
            fullPath = LocalFileStoragePath.Resolve(options.RootPath, hostEnvironment.ContentRootPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ValidateOptionsResult.Fail($"Attachment storage root is invalid: {exception.Message}");
        }

        string normalizedPath = Path.TrimEndingDirectorySeparator(fullPath);
        string filesystemRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(fullPath)!);
        string contentRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(hostEnvironment.ContentRootPath));
        if (string.Equals(normalizedPath, filesystemRoot, StringComparison.Ordinal) ||
            string.Equals(normalizedPath, contentRoot, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "Attachment storage root must be a dedicated directory, not the filesystem or content root.");
        }

        string probePath = Path.Combine(fullPath, $".write-probe-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(fullPath);
            using FileStream probe = new(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
            probe.WriteByte(0);
            return ValidateOptionsResult.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ValidateOptionsResult.Fail(
                $"Attachment storage root is not writable: {exception.Message}");
        }
        finally
        {
            try
            {
                File.Delete(probePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Validation has already established whether the directory is writable. A probe
                // opened with DeleteOnClose should not remain, but failure to repeat deletion here
                // must not hide the original validation result.
            }
        }
    }
}

internal static class LocalFileStoragePath
{
    internal static string Resolve(string configuredPath, string contentRootPath) =>
        Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, configuredPath));
}
