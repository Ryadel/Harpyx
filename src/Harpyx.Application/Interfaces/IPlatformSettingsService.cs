using Harpyx.Application.DTOs;

namespace Harpyx.Application.Interfaces;

public interface IPlatformSettingsService
{
    Task<PlatformSettingsDto> GetAsync(CancellationToken cancellationToken);
    Task<PlatformSettingsDto> SaveAsync(PlatformSettingsSaveRequest request, CancellationToken cancellationToken);
    Task<bool> IsSelfRegistrationAllowedAsync(CancellationToken cancellationToken);
    Task<bool> IsQuarantineEnabledAsync(CancellationToken cancellationToken);
    Task<bool> IsUrlDocumentsEnabledAsync(CancellationToken cancellationToken);
}
