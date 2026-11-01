using AwesomeAssertions;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Services;

namespace ShopPOS.Tests;

public class AuthServiceTests
{
    [Fact]
    public void CurrentUserProvider_ShouldReturnOwnerWithId1()
    {
        // Arrange
        var provider = new CurrentUserProvider();

        // Act & Assert
        provider.UserId.Should().Be(1);
        provider.UserName.Should().Be("owner");
        provider.FullName.Should().Be("Owner");

        var user = provider.GetCurrentUser();
        user.Should().NotBeNull();
        user.Id.Should().Be(1);
        user.Username.Should().Be("owner");
        user.FullName.Should().Be("Owner");
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AuthService_ShouldAlwaysBeAuthenticated_AndReturnOwner()
    {
        // Arrange
        ICurrentUserProvider provider = new CurrentUserProvider();
        IAuthService sut = new AuthService(provider);

        // Act & Assert
        sut.IsAuthenticated.Should().BeTrue();
        sut.CurrentUser.Should().NotBeNull();
        sut.CurrentUser!.Id.Should().Be(1);
        sut.CurrentUser!.FullName.Should().Be("Owner");
    }

    [Fact]
    public async Task AuthService_LogoutAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        ICurrentUserProvider provider = new CurrentUserProvider();
        IAuthService sut = new AuthService(provider);

        // Act
        var task = sut.LogoutAsync();
        await task;

        // Assert
        task.IsCompletedSuccessfully.Should().BeTrue();
        sut.IsAuthenticated.Should().BeTrue();
    }
}
