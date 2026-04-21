using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class LlmCatalogRepository : ILlmCatalogRepository
{
    private readonly HarpyxDbContext _dbContext;

    public LlmCatalogRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<LlmConnection>> GetPersonalConnectionsByUserAsync(Guid userId, CancellationToken cancellationToken)
        => await _dbContext.LlmConnections
            .Include(c => c.Models)
            .Where(c => c.Scope == LlmConnectionScope.Personal && c.UserId == userId)
            .OrderBy(c => c.Provider)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LlmConnection>> GetHostedConnectionsAsync(CancellationToken cancellationToken)
        => await _dbContext.LlmConnections
            .Include(c => c.Models)
            .Where(c => c.Scope == LlmConnectionScope.Hosted)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public Task<LlmConnection?> GetConnectionByIdAsync(Guid connectionId, CancellationToken cancellationToken)
        => _dbContext.LlmConnections
            .Include(c => c.User)
            .Include(c => c.Models)
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);

    public Task<LlmConnection?> GetPersonalConnectionByProviderAsync(Guid userId, LlmProvider provider, CancellationToken cancellationToken)
        => _dbContext.LlmConnections
            .Include(c => c.Models)
            .FirstOrDefaultAsync(
                c => c.Scope == LlmConnectionScope.Personal &&
                     c.UserId == userId &&
                     c.Provider == provider,
                cancellationToken);

    public Task AddConnectionAsync(LlmConnection connection, CancellationToken cancellationToken)
        => _dbContext.LlmConnections.AddAsync(connection, cancellationToken).AsTask();

    public void UpdateConnection(LlmConnection connection) => _dbContext.LlmConnections.Update(connection);

    public void RemoveConnection(LlmConnection connection) => _dbContext.LlmConnections.Remove(connection);

    public Task<LlmModel?> GetModelByIdAsync(Guid modelId, CancellationToken cancellationToken)
        => _dbContext.LlmModels
            .Include(m => m.Connection)
            .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(m => m.Id == modelId, cancellationToken);

    public async Task<IReadOnlyList<LlmModel>> GetModelsByConnectionAsync(Guid connectionId, CancellationToken cancellationToken)
        => await _dbContext.LlmModels
            .Include(m => m.Connection)
            .Where(m => m.ConnectionId == connectionId)
            .OrderBy(m => m.Capability)
            .ThenBy(m => m.DisplayName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LlmModel>> GetSelectableModelsAsync(
        IReadOnlyList<Guid> userIds,
        LlmProviderType usage,
        CancellationToken cancellationToken)
    {
        return await _dbContext.LlmModels
            .Include(m => m.Connection)
            .ThenInclude(c => c.User)
            .Where(m =>
                m.Capability == usage &&
                m.IsEnabled &&
                m.Connection.IsEnabled &&
                ((m.Connection.Scope == LlmConnectionScope.Hosted && m.IsPublished) ||
                 (m.Connection.Scope == LlmConnectionScope.Personal &&
                  m.Connection.UserId != null &&
                  userIds.Contains(m.Connection.UserId.Value))))
            .OrderBy(m => m.Connection.Scope)
            .ThenBy(m => m.Connection.User!.Email)
            .ThenBy(m => m.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LlmModel>> GetPublishedHostedModelsAsync(LlmProviderType? usage, CancellationToken cancellationToken)
    {
        var query = _dbContext.LlmModels
            .Include(m => m.Connection)
            .Where(m =>
                m.Connection.Scope == LlmConnectionScope.Hosted &&
                m.Connection.IsEnabled &&
                m.IsEnabled &&
                m.IsPublished);

        if (usage is not null)
            query = query.Where(m => m.Capability == usage.Value);

        return await query
            .OrderBy(m => m.Capability)
            .ThenBy(m => m.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public Task AddModelAsync(LlmModel model, CancellationToken cancellationToken)
        => _dbContext.LlmModels.AddAsync(model, cancellationToken).AsTask();

    public void UpdateModel(LlmModel model) => _dbContext.LlmModels.Update(model);

    public void RemoveModel(LlmModel model) => _dbContext.LlmModels.Remove(model);

    public async Task<IReadOnlyList<UserLlmModelPreference>> GetPreferencesByUserAsync(Guid userId, CancellationToken cancellationToken)
        => await _dbContext.UserLlmModelPreferences
            .Include(p => p.LlmModel)
            .ThenInclude(m => m.Connection)
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

    public Task<UserLlmModelPreference?> GetPreferenceAsync(Guid userId, LlmProviderType usage, CancellationToken cancellationToken)
        => _dbContext.UserLlmModelPreferences
            .Include(p => p.LlmModel)
            .ThenInclude(m => m.Connection)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Usage == usage, cancellationToken);

    public Task AddPreferenceAsync(UserLlmModelPreference preference, CancellationToken cancellationToken)
        => _dbContext.UserLlmModelPreferences.AddAsync(preference, cancellationToken).AsTask();

    public void UpdatePreference(UserLlmModelPreference preference)
        => _dbContext.UserLlmModelPreferences.Update(preference);

    public void RemovePreference(UserLlmModelPreference preference)
        => _dbContext.UserLlmModelPreferences.Remove(preference);
}
