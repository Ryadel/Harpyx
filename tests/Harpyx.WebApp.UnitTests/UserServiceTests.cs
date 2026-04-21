namespace Harpyx.WebApp.UnitTests;

public class UserServiceTests
{
    private static UserService CreateService(
        Mock<IUserRepository> repo,
        Mock<IUserTenantRepository> userTenants,
        Mock<ITenantRepository> tenants,
        Mock<IProjectRepository>? projects = null,
        Mock<IDocumentRepository>? documents = null,
        Mock<IStorageService>? storage = null,
        Mock<IPlatformSettingsService>? platformSettings = null,
        Mock<IUserInvitationRepository>? invitations = null,
        Mock<ILlmCatalogRepository>? llmCatalog = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        var settingsMock = platformSettings ?? new Mock<IPlatformSettingsService>();
        settingsMock.Setup(s => s.IsSelfRegistrationAllowedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var invitationMock = invitations ?? new Mock<IUserInvitationRepository>();
        invitationMock.Setup(i => i.GetLatestPendingByEmailAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserInvitation?)null);

        repo.Setup(r => r.ClearOwnershipReferencesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var llmCatalogMock = llmCatalog ?? new Mock<ILlmCatalogRepository>();
        llmCatalogMock.Setup(c => c.GetPreferencesByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserLlmModelPreference>());

        return new(
            repo.Object,
            userTenants.Object,
            tenants.Object,
            (projects ?? new Mock<IProjectRepository>()).Object,
            (documents ?? new Mock<IDocumentRepository>()).Object,
            (storage ?? new Mock<IStorageService>()).Object,
            settingsMock.Object,
            invitationMock.Object,
            llmCatalogMock.Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }

    [Fact]
    public async Task UserService_AutoProvisionsMissingUser_WithPersonalTenant()
    {
        var repo = new Mock<IUserRepository>();
        var userTenants = new Mock<IUserTenantRepository>();
        var tenants = new Mock<ITenantRepository>();
        var projects = new Mock<IProjectRepository>();
        var documents = new Mock<IDocumentRepository>();
        var storage = new Mock<IStorageService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = CreateService(repo, userTenants, tenants, projects, documents, storage, unitOfWork: unitOfWork);

        repo.Setup(r => r.GetByObjectIdAsync("oid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ObjectId = "oid-1", Email = "user@contoso.com", IsActive = true });
        repo.Setup(r => r.GetByObjectIdAsync("oid-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        repo.Setup(r => r.GetBySubjectIdAsync("sub-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        repo.Setup(r => r.GetByEmailAsync("other@contoso.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        User? createdUser = null;
        Tenant? createdTenant = null;
        repo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => createdUser = u)
            .Returns(Task.CompletedTask);
        tenants.Setup(t => t.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Callback<Tenant, CancellationToken>((t, _) => createdTenant = t)
            .Returns(Task.CompletedTask);

        var allowed = await service.IsAuthorizedAsync("oid-1", "sub-1", "user@contoso.com", "Microsoft", CancellationToken.None);
        var provisioned = await service.IsAuthorizedAsync("oid-2", "sub-2", "other@contoso.com", "Google", CancellationToken.None);

        allowed.Should().BeTrue();
        provisioned.Should().BeTrue();

        createdUser.Should().NotBeNull();
        createdUser!.Email.Should().Be("other@contoso.com");
        createdUser.Role.Should().Be(UserRole.ReadOnly);
        createdUser.IsActive.Should().BeTrue();
        createdTenant.Should().NotBeNull();
        createdTenant!.Name.Should().Be("Personal");
        createdTenant.IsPersonal.Should().BeTrue();

        userTenants.Verify(
            r => r.AddOrUpdateMembershipAsync(
                createdUser.Id,
                createdTenant.Id,
                TenantRole.TenantOwner,
                true,
                createdUser.Id,
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(3));
    }

    [Fact]
    public async Task UserService_DeniesInactiveExistingUser()
    {
        var repo = new Mock<IUserRepository>();
        var userTenants = new Mock<IUserTenantRepository>();
        var tenants = new Mock<ITenantRepository>();
        var projects = new Mock<IProjectRepository>();
        var documents = new Mock<IDocumentRepository>();
        var storage = new Mock<IStorageService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = CreateService(repo, userTenants, tenants, projects, documents, storage, unitOfWork: unitOfWork);

        repo.Setup(r => r.GetByObjectIdAsync("oid-inactive", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ObjectId = "oid-inactive", Email = "inactive@contoso.com", IsActive = false });

        var allowed = await service.IsAuthorizedAsync("oid-inactive", "sub-inactive", "inactive@contoso.com", "Microsoft", CancellationToken.None);

        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesPersonalTenantAndStoredDocuments()
    {
        var userId = Guid.NewGuid();
        var personalTenantId = Guid.NewGuid();
        var sharedTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var repo = new Mock<IUserRepository>();
        var userTenants = new Mock<IUserTenantRepository>();
        var tenants = new Mock<ITenantRepository>();
        var projects = new Mock<IProjectRepository>();
        var documents = new Mock<IDocumentRepository>();
        var storage = new Mock<IStorageService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = CreateService(repo, userTenants, tenants, projects, documents, storage, unitOfWork: unitOfWork);

        repo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = "user@contoso.com", IsActive = true });

        userTenants.Setup(r => r.GetTenantIdsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { personalTenantId, sharedTenantId });

        var personalTenant = new Tenant { Id = personalTenantId, Name = "Personal", IsActive = true, IsPersonal = true };
        var sharedTenant = new Tenant { Id = sharedTenantId, Name = "Shared", IsActive = true };
        tenants.Setup(r => r.GetAsync(It.IsAny<TenantFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { personalTenant, sharedTenant });

        userTenants.Setup(r => r.GetUserIdsByTenantIdAsync(personalTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { userId });
        userTenants.Setup(r => r.GetUserIdsByTenantIdAsync(sharedTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { userId, Guid.NewGuid() });

        projects.Setup(r => r.GetAllAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == personalTenantId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Project { Id = projectId, WorkspaceId = Guid.NewGuid(), Name = "P" } });

        documents.Setup(r => r.GetByProjectAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Document { Id = Guid.NewGuid(), ProjectId = projectId, StorageKey = "minio/doc1", FileName = "doc1.pdf", ContentType = "application/pdf" } });

        await service.DeleteAsync(userId, CancellationToken.None);

        storage.Verify(s => s.DeleteAsync("minio/doc1", It.IsAny<CancellationToken>()), Times.Once);
        tenants.Verify(t => t.Remove(It.Is<Tenant>(x => x.Id == personalTenantId)), Times.Once);
        tenants.Verify(t => t.Remove(It.Is<Tenant>(x => x.Id == sharedTenantId)), Times.Never);
        repo.Verify(r => r.ClearOwnershipReferencesAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.Remove(It.Is<User>(u => u.Id == userId)), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IsAuthorizedAsync_SelfRegistrationDisabled_WithoutInvitation_DeniesRegistration()
    {
        var repo = new Mock<IUserRepository>();
        var userTenants = new Mock<IUserTenantRepository>();
        var tenants = new Mock<ITenantRepository>();
        var settings = new Mock<IPlatformSettingsService>();
        var invitations = new Mock<IUserInvitationRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = CreateService(
            repo,
            userTenants,
            tenants,
            platformSettings: settings,
            invitations: invitations,
            unitOfWork: unitOfWork);

        settings.Setup(s => s.IsSelfRegistrationAllowedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        invitations.Setup(i => i.GetLatestPendingByEmailAsync("new.user@contoso.com", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserInvitation?)null);

        var result = await service.IsAuthorizedAsync(null, null, "new.user@contoso.com", "Google", CancellationToken.None);

        result.Should().BeFalse();
        repo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsAuthorizedAsync_SelfRegistrationDisabled_WithInvitation_ProvisionsAndAcceptsInvitation()
    {
        var repo = new Mock<IUserRepository>();
        var userTenants = new Mock<IUserTenantRepository>();
        var tenants = new Mock<ITenantRepository>();
        var settings = new Mock<IPlatformSettingsService>();
        var invitations = new Mock<IUserInvitationRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = CreateService(
            repo,
            userTenants,
            tenants,
            platformSettings: settings,
            invitations: invitations,
            unitOfWork: unitOfWork);

        repo.Setup(r => r.GetByEmailAsync("invited.user@contoso.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        settings.Setup(s => s.IsSelfRegistrationAllowedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var invitation = new UserInvitation
        {
            Id = Guid.NewGuid(),
            Email = "invited.user@contoso.com",
            Scope = UserInvitationScope.SelfRegistration,
            Status = UserInvitationStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            InvitedByUserId = Guid.NewGuid()
        };
        invitations.Setup(i => i.GetLatestPendingByEmailAsync("invited.user@contoso.com", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        Tenant? createdTenant = null;
        var createdTenantId = Guid.Empty;
        tenants.Setup(t => t.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Callback<Tenant, CancellationToken>((t, _) =>
            {
                createdTenant = t;
                createdTenantId = t.Id;
            })
            .Returns(Task.CompletedTask);

        var result = await service.IsAuthorizedAsync(null, null, "invited.user@contoso.com", "Google", CancellationToken.None);

        result.Should().BeTrue();
        invitation.Status.Should().Be(UserInvitationStatus.Accepted);
        invitation.AcceptedAt.Should().NotBeNull();
        invitations.Verify(i => i.Update(It.Is<UserInvitation>(x => x.Id == invitation.Id)), Times.Once);
        userTenants.Verify(r => r.AddOrUpdateMembershipAsync(
            It.IsAny<Guid>(),
            createdTenantId,
            TenantRole.TenantOwner,
            true,
            It.IsAny<Guid?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
