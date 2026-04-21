using Harpyx.Application.DTOs;

namespace Harpyx.WebApp.UnitTests;

public class UserInvitationServiceTests
{
    [Fact]
    public async Task InviteAsync_CreatesInvitation_AndSendsEmail()
    {
        var invitations = new Mock<IUserInvitationRepository>();
        var users = new Mock<IUserRepository>();
        var tenants = new Mock<ITenantRepository>();
        var emailSender = new Mock<IEmailSender>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var inviter = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@contoso.com",
            IsActive = true,
            Role = UserRole.Admin
        };
        users.Setup(r => r.GetByIdAsync(inviter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inviter);

        UserInvitation? addedInvitation = null;
        invitations.Setup(r => r.AddAsync(It.IsAny<UserInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<UserInvitation, CancellationToken>((invitation, _) => addedInvitation = invitation)
            .Returns(Task.CompletedTask);
        invitations.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => addedInvitation is null ? null : new UserInvitation
            {
                Id = id,
                Email = addedInvitation.Email,
                Scope = addedInvitation.Scope,
                Status = addedInvitation.Status,
                ExpiresAt = addedInvitation.ExpiresAt,
                TenantId = addedInvitation.TenantId,
                InvitedByUser = inviter
            });

        var service = new UserInvitationService(
            invitations.Object,
            users.Object,
            tenants.Object,
            emailSender.Object,
            unitOfWork.Object);

        var result = await service.InviteAsync(
            inviter.Id,
            new InviteUserRequest("new.user@contoso.com", UserInvitationScope.SelfRegistration, null, 7, "https://app.local/"),
            CancellationToken.None);

        result.Email.Should().Be("new.user@contoso.com");
        result.Scope.Should().Be(UserInvitationScope.SelfRegistration);
        result.Status.Should().Be(UserInvitationStatus.Pending);
        emailSender.Verify(s => s.SendAsync(
            "new.user@contoso.com",
            It.IsAny<string>(),
            It.Is<string>(b => b.Contains("https://app.local/")),
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_RevokesPendingInvitation()
    {
        var invitations = new Mock<IUserInvitationRepository>();
        var users = new Mock<IUserRepository>();
        var tenants = new Mock<ITenantRepository>();
        var emailSender = new Mock<IEmailSender>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var invitation = new UserInvitation
        {
            Id = Guid.NewGuid(),
            Email = "invitee@contoso.com",
            Scope = UserInvitationScope.SelfRegistration,
            Status = UserInvitationStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(3)
        };
        invitations.Setup(r => r.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var service = new UserInvitationService(
            invitations.Object,
            users.Object,
            tenants.Object,
            emailSender.Object,
            unitOfWork.Object);

        await service.RevokeAsync(invitation.Id, CancellationToken.None);

        invitation.Status.Should().Be(UserInvitationStatus.Revoked);
        invitations.Verify(r => r.Update(It.Is<UserInvitation>(i => i.Id == invitation.Id && i.Status == UserInvitationStatus.Revoked)), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
