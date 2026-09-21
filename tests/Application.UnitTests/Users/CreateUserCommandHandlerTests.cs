using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.UnitTests.Abstractions;
using Application.Users.Create;
using Domain.Common;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class CreateUserCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_RejectDuplicateNormalizedUsername_WithoutAddingUser()
    {
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(User.Create(Guid.NewGuid(), "existing@example.com", "test-user", "Existing", "User", "hash"));
        await context.SaveChangesAsync();
        var actorId = Guid.NewGuid();
        IScopeAuthorizationService scopes = Substitute.For<IScopeAuthorizationService>();
        scopes.HasPermissionAsync(actorId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        scopes.GetUserAssignmentAsync(actorId, Arg.Any<CancellationToken>())
            .Returns(new UserAuthorizationAssignment(Guid.NewGuid(), actorId, Guid.NewGuid(), ScopeType.Enterprise, null));
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(actorId);
        var handler = new CreateUserCommandHandler(context, userContext, scopes, Substitute.For<IPasswordHasher>());

        Result<Guid> result = await handler.Handle(new CreateUserCommand("new@example.com", "  TEST-USER  ", "New", "User", "Password123!"), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.UsernameNotUnique);
        (await context.Users.CountAsync()).ShouldBe(1);
    }
}
