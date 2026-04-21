using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Infrastructure.Services;

public class LlmClientResolver : ILlmClientResolver
{
    private readonly ILlmCatalogRepository _catalog;
    private readonly IEncryptionService _encryption;
    private readonly ILlmClientFactory _clientFactory;

    public LlmClientResolver(
        ILlmCatalogRepository catalog,
        IEncryptionService encryption,
        ILlmClientFactory clientFactory)
    {
        _catalog = catalog;
        _encryption = encryption;
        _clientFactory = clientFactory;
    }

    public async Task<LlmResolveResult> ResolveAsync(
        Guid userId,
        Guid? selectedModelId,
        Guid? preferredModelId,
        CancellationToken cancellationToken)
    {
        var model = await ResolveModelAsync(userId, selectedModelId, preferredModelId, cancellationToken);
        if (model is null || !IsConfiguredChatModel(model, userId))
            return LlmResolveResult.NotConfigured();

        var apiKey = string.IsNullOrWhiteSpace(model.Connection.EncryptedApiKey)
            ? string.Empty
            : _encryption.Decrypt(model.Connection.EncryptedApiKey);

        var client = _clientFactory.Create(model.Connection, apiKey);
        return new LlmResolveResult(true, client, model.ModelId);
    }

    private async Task<LlmModel?> ResolveModelAsync(
        Guid userId,
        Guid? selectedModelId,
        Guid? preferredModelId,
        CancellationToken cancellationToken)
    {
        if (selectedModelId is Guid selected && selected != Guid.Empty)
            return await _catalog.GetModelByIdAsync(selected, cancellationToken);

        if (preferredModelId is Guid preferred && preferred != Guid.Empty)
        {
            var preferredModel = await _catalog.GetModelByIdAsync(preferred, cancellationToken);
            if (preferredModel is not null && IsConfiguredChatModel(preferredModel, userId))
                return preferredModel;
        }

        var preference = await _catalog.GetPreferenceAsync(userId, LlmProviderType.Chat, cancellationToken);
        if (preference?.LlmModel is not null && IsConfiguredChatModel(preference.LlmModel, userId))
            return preference.LlmModel;

        var selectable = await _catalog.GetSelectableModelsAsync(new[] { userId }, LlmProviderType.Chat, cancellationToken);
        return selectable.FirstOrDefault(m => IsConfiguredChatModel(m, userId));
    }

    private static bool IsConfiguredChatModel(LlmModel model, Guid userId)
    {
        if (model.Capability != LlmProviderType.Chat ||
            !model.IsEnabled ||
            !model.Connection.IsEnabled ||
            model.Connection.Provider == LlmProvider.None)
        {
            return false;
        }

        return model.Connection.Scope switch
        {
            LlmConnectionScope.Hosted => model.IsPublished,
            LlmConnectionScope.Personal => model.Connection.UserId == userId &&
                                           !string.IsNullOrWhiteSpace(model.Connection.EncryptedApiKey),
            _ => false
        };
    }
}
