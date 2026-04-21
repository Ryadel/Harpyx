using Harpyx.Application.DTOs;

namespace Harpyx.Application.Interfaces;

public interface IUserInvitationService
{
    Task<IReadOnlyList<UserInvitationDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<UserInvitationDto> InviteAsync(Guid invitedByUserId, InviteUserRequest request, CancellationToken cancellationToken);
    Task RevokeAsync(Guid invitationId, CancellationToken cancellationToken);
}
