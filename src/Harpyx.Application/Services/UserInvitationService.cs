using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Services;

public class UserInvitationService : IUserInvitationService
{
    private const int DefaultExpirationDays = 7;
    private readonly IUserInvitationRepository _invitations;
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly IEmailSender _emailSender;
    private readonly IUnitOfWork _unitOfWork;

    public UserInvitationService(
        IUserInvitationRepository invitations,
        IUserRepository users,
        ITenantRepository tenants,
        IEmailSender emailSender,
        IUnitOfWork unitOfWork)
    {
        _invitations = invitations;
        _users = users;
        _tenants = tenants;
        _emailSender = emailSender;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<UserInvitationDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var invitations = await _invitations.GetAllAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return invitations.Select(i => ToDto(i, now)).ToList();
    }

    public async Task<UserInvitationDto> InviteAsync(Guid invitedByUserId, InviteUserRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(request.Email));

        var inviter = await _users.GetByIdAsync(invitedByUserId, cancellationToken) ??
            throw new InvalidOperationException("Inviter not found.");

        var expiresInDays = request.ExpiresInDays <= 0 ? DefaultExpirationDays : request.ExpiresInDays;
        var expiresAt = DateTimeOffset.UtcNow.AddDays(Math.Clamp(expiresInDays, 1, 30));

        Guid? tenantId = request.TenantId;
        if (request.Scope == UserInvitationScope.SelfRegistration)
        {
            tenantId = null;
        }
        else
        {
            if (tenantId is null || tenantId == Guid.Empty)
                throw new InvalidOperationException("Tenant membership invitations require a tenant.");

            var tenant = await _tenants.GetByIdAsync(tenantId.Value, cancellationToken);
            if (tenant is null)
                throw new InvalidOperationException("Selected tenant not found.");
        }

        var invitation = new UserInvitation
        {
            Email = email,
            Scope = request.Scope,
            TenantId = tenantId,
            Status = UserInvitationStatus.Pending,
            ExpiresAt = expiresAt,
            InvitedByUserId = invitedByUserId
        };

        await _invitations.AddAsync(invitation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var registrationUrl = string.IsNullOrWhiteSpace(request.RegistrationUrl) ? "/" : request.RegistrationUrl;
        var scopeLabel = request.Scope == UserInvitationScope.SelfRegistration
            ? "register your personal Harpyx account"
            : "join your Harpyx tenant workspace";
        var body = $"You have been invited to {scopeLabel}. Sign in with this email: {email}\n\nStart here: {registrationUrl}\n\nThis invitation expires on {expiresAt:u}.";
        await _emailSender.SendAsync(email, "Harpyx invitation", body, cancellationToken);

        var saved = await _invitations.GetByIdAsync(invitation.Id, cancellationToken) ?? invitation;
        return ToDto(saved, DateTimeOffset.UtcNow);
    }

    public async Task RevokeAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await _invitations.GetByIdAsync(invitationId, cancellationToken);
        if (invitation is null)
            return;

        if (invitation.Status != UserInvitationStatus.Pending)
            return;

        invitation.Status = UserInvitationStatus.Revoked;
        invitation.UpdatedAt = DateTimeOffset.UtcNow;
        _invitations.Update(invitation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static UserInvitationDto ToDto(UserInvitation invitation, DateTimeOffset now)
        => new(
            invitation.Id,
            invitation.Email,
            invitation.Scope,
            invitation.Status == UserInvitationStatus.Pending && invitation.ExpiresAt < now
                ? UserInvitationStatus.Expired
                : invitation.Status,
            invitation.TenantId,
            invitation.Tenant?.Name,
            invitation.ExpiresAt,
            invitation.CreatedAt,
            invitation.InvitedByUser?.Email ?? "-",
            invitation.AcceptedAt);
}
