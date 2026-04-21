using Harpyx.Application.DTOs;
using Harpyx.Application.Filters;

namespace Harpyx.Application.Interfaces;

public interface ITenantService
{
    Task<IReadOnlyList<TenantDto>> GetAsync(TenantFilter filter, CancellationToken cancellationToken);
    Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken);
    Task<TenantDto> UpdateAsync(TenantDto tenant, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}