using Infrastructure.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Application.UnitTests.Storage;

public sealed class LocalFileStorageOptionsValidatorTests
{
    [Fact]
    public void Validate_Should_CreateDedicatedWritableDirectory()
    {
        string contentRoot = CreateTemporaryDirectory();
        string storageRoot = Path.Combine(contentRoot, "attachments");

        try
        {
            var validator = new LocalFileStorageOptionsValidator(CreateEnvironment(contentRoot));

            ValidateOptionsResult result = validator.Validate(
                name: null,
                new LocalFileStorageOptions { RootPath = storageRoot });

            result.Succeeded.ShouldBeTrue();
            Directory.Exists(storageRoot).ShouldBeTrue();
            Directory.EnumerateFileSystemEntries(storageRoot).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void Validate_Should_RejectContentRoot()
    {
        string contentRoot = CreateTemporaryDirectory();

        try
        {
            var validator = new LocalFileStorageOptionsValidator(CreateEnvironment(contentRoot));

            ValidateOptionsResult result = validator.Validate(
                name: null,
                new LocalFileStorageOptions { RootPath = "." });

            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldContain("dedicated directory");
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void Validate_Should_RejectFilesystemRoot()
    {
        string contentRoot = CreateTemporaryDirectory();

        try
        {
            var validator = new LocalFileStorageOptionsValidator(CreateEnvironment(contentRoot));

            ValidateOptionsResult result = validator.Validate(
                name: null,
                new LocalFileStorageOptions { RootPath = Path.GetPathRoot(contentRoot)! });

            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldContain("dedicated directory");
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void Validate_Should_RejectInvalidPath()
    {
        string contentRoot = CreateTemporaryDirectory();

        try
        {
            var validator = new LocalFileStorageOptionsValidator(CreateEnvironment(contentRoot));

            ValidateOptionsResult result = validator.Validate(
                name: null,
                new LocalFileStorageOptions { RootPath = "invalid\0path" });

            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldContain("invalid");
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static IHostEnvironment CreateEnvironment(string contentRoot)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(contentRoot);
        return environment;
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"eiams-storage-validator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
