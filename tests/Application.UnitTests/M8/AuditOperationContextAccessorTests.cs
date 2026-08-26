using Application.Abstractions.Audit;
using Infrastructure.AuditLogs;

namespace Application.UnitTests.M8;

public sealed class AuditOperationContextAccessorTests
{
    [Fact]
    public void Current_Should_BeNull_WhenNoScopeWasOpened()
    {
        // Arrange
        var accessor = new AuditOperationContextAccessor();

        // Act

        // Assert
        accessor.Current.ShouldBeNull();
    }

    [Fact]
    public void BeginScope_Should_MakeDescriptorCurrent_WhenScopeIsOpen()
    {
        // Arrange
        var accessor = new AuditOperationContextAccessor();
        AuditOperationDescriptor descriptor = CreateDescriptor();

        // Act
        using IDisposable scope = accessor.BeginScope(descriptor);

        // Assert
        accessor.Current.ShouldBe(descriptor);
    }

    [Fact]
    public void BeginScope_Should_RestorePreviousFrame_WhenScopesAreNestedAndInnerScopeIsDisposed()
    {
        // Arrange
        var accessor = new AuditOperationContextAccessor();
        AuditOperationDescriptor outerDescriptor = CreateDescriptor();
        AuditOperationDescriptor innerDescriptor = CreateDescriptor();

        using IDisposable outerScope = accessor.BeginScope(outerDescriptor);

        // Act
        using (accessor.BeginScope(innerDescriptor))
        {
            accessor.Current.ShouldBe(innerDescriptor);
        }

        // Assert
        accessor.Current.ShouldBe(outerDescriptor);
    }

    [Fact]
    public async Task Scope_Should_RemainVisibleAcrossAwaits_WhenHandlersAwaitInternally()
    {
        // Arrange
        var accessor = new AuditOperationContextAccessor();
        AuditOperationDescriptor descriptor = CreateDescriptor();

        using IDisposable scope = accessor.BeginScope(descriptor);

        // Act
        await Task.Yield();

        // Assert
        accessor.Current.ShouldBe(descriptor);
    }

    [Fact]
    public async Task ConcurrentTasks_Should_OwnIndependentScopes_WhenScopesAreOpenedInParallel()
    {
        // Arrange
        var accessor = new AuditOperationContextAccessor();
        const int TaskCount = 32;
        AuditOperationDescriptor[] descriptors =
            [.. Enumerable.Range(0, TaskCount).Select(_ => CreateDescriptor())];

        // Act
        await Task.WhenAll(descriptors.Select(descriptor => Task.Run(async () =>
        {
            using IDisposable scope = accessor.BeginScope(descriptor);

            await Task.Yield();

            accessor.Current.ShouldNotBeNull();
            accessor.Current.ShouldBe(descriptor);

            await Task.Delay(1);

            accessor.Current.OperationId.ShouldBe(descriptor.OperationId);
        })));

        // Assert
        accessor.Current.ShouldBeNull();
    }

    [Fact]
    public void RecordSynthetic_Should_BeNoOp_WhenNoScopeIsOpen()
    {
        // Arrange
        var accessor = new AuditOperationContextAccessor();
        var subject = new AuditSyntheticSubject(
            Guid.NewGuid(),
            "User",
            Guid.NewGuid(),
            "Authenticated",
            "LoginUserCommand",
            null);

        // Act
        accessor.RecordSynthetic(subject);

        // Assert
        accessor.Current.ShouldBeNull();
    }

    [Fact]
    public void RecordSynthetic_Should_EnqueueOntoCurrentScopeOnly_WhenNestedScopeIsOpen()
    {
        // Arrange
        var accessor = new AuditOperationContextAccessor();
        AuditOperationDescriptor outerDescriptor = CreateDescriptor();
        AuditOperationDescriptor innerDescriptor = CreateDescriptor();
        var innerSubject = new AuditSyntheticSubject(
            Guid.NewGuid(),
            "User",
            Guid.NewGuid(),
            "Authenticated",
            "LoginUserCommand",
            null);

        using IDisposable outerScope = accessor.BeginScope(outerDescriptor);

        using (IDisposable innerScope = accessor.BeginScope(innerDescriptor))
        {
            // Act
            accessor.RecordSynthetic(innerSubject);
        }

        // Assert
        innerDescriptor.SyntheticSubjects.ShouldHaveSingleItem().ShouldBe(innerSubject);
        outerDescriptor.SyntheticSubjects.ShouldBeEmpty();
    }

    [Fact]
    public void Dispose_Should_NotClobberCurrentFrame_WhenDisposedTwice()
    {
        // Arrange
        var accessor = new AuditOperationContextAccessor();
        AuditOperationDescriptor outerDescriptor = CreateDescriptor();
        AuditOperationDescriptor innerDescriptor = CreateDescriptor();

        using IDisposable outerScope = accessor.BeginScope(outerDescriptor);
        IDisposable innerScope = accessor.BeginScope(innerDescriptor);

        innerScope.Dispose();

        // Act
        innerScope.Dispose();

        // Assert
        accessor.Current.ShouldBe(outerDescriptor);
    }

    [Fact]
    public void Dispose_Should_ClearCurrentFrame_WhenRootScopeIsDisposedTwice()
    {
        // Arrange
        var accessor = new AuditOperationContextAccessor();
        IDisposable scope = accessor.BeginScope(CreateDescriptor());

        scope.Dispose();

        // Act
        scope.Dispose();

        // Assert
        accessor.Current.ShouldBeNull();
    }

    private static AuditOperationDescriptor CreateDescriptor() =>
        new(
            Guid.NewGuid(),
            "request-1",
            Guid.NewGuid(),
            "127.0.0.1",
            nameof(AuditOperationContextAccessorTests),
            AuditOperationKind.Http);
}
