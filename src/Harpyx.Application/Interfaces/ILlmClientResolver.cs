namespace Harpyx.Application.Interfaces;

public interface ILlmClientResolver
{
    /// <summary>
    /// Resolves the LLM client for the given user.
    /// If selectedModelId is specified, uses that model if available to the user.
    /// If selectedModelId is null and preferredModelId is set, uses that model first.
    /// If neither are set, uses the user's default Chat model.
    /// </summary>
    Task<LlmResolveResult> ResolveAsync(
        Guid userId,
        Guid? selectedModelId,
        Guid? preferredModelId,
        CancellationToken cancellationToken);
}

public record LlmResolveResult(bool IsConfigured, ILlmClient? Client, string? Model)
{
    public static LlmResolveResult NotConfigured() => new(false, null, null);
}
