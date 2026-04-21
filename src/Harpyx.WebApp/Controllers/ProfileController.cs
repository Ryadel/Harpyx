using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Controllers;

/// <summary>
/// Authenticated endpoints for per-user LLM configuration (tenant-independent).
/// Each user can configure multiple providers, each with its own API key.
/// Not Configured contract: HTTP 412 with { code: "LLM_NOT_CONFIGURED", profileUrl: "/Profile" }.
/// </summary>
[ApiController]
[Route("api/profile")]
[IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class ProfileController : ControllerBase
{
    private readonly IUserLlmProviderService _providerService;
    private readonly IUserService _userService;

    public ProfileController(IUserLlmProviderService providerService, IUserService userService)
    {
        _providerService = providerService;
        _userService = userService;
    }

    [HttpGet("llm")]
    public async Task<IActionResult> GetLlmProviders(CancellationToken cancellationToken)
    {
        var userId = await ResolveCurrentUserIdAsync(cancellationToken);
        if (userId is null) return Unauthorized();

        var providers = await _providerService.GetAllAsync(userId.Value, cancellationToken);
        return Ok(providers);
    }

    [HttpPut("llm")]
    public async Task<IActionResult> SaveLlmProvider([FromBody] LlmProviderSaveRequest request, CancellationToken cancellationToken)
    {
        var userId = await ResolveCurrentUserIdAsync(cancellationToken);
        if (userId is null) return Unauthorized();

        try
        {
            var result = await _providerService.SaveAsync(userId.Value, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("llm/{provider}")]
    public async Task<IActionResult> DeleteLlmProvider(
        LlmProvider provider,
        CancellationToken cancellationToken = default)
    {
        var userId = await ResolveCurrentUserIdAsync(cancellationToken);
        if (userId is null) return Unauthorized();

        var result = await _providerService.DeleteAsync(userId.Value, provider, cancellationToken);
        return Ok(result);
    }

    [HttpPost("llm/models/{modelId:guid}/default")]
    public async Task<IActionResult> SetDefault(
        Guid modelId,
        [FromQuery] LlmProviderType type = LlmProviderType.Chat,
        CancellationToken cancellationToken = default)
    {
        var userId = await ResolveCurrentUserIdAsync(cancellationToken);
        if (userId is null) return Unauthorized();

        await _providerService.SetDefaultAsync(userId.Value, type, modelId, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Checks whether the current user has any LLM provider configured.
    /// Returns 200 if configured; 412 Precondition Failed with LLM_NOT_CONFIGURED if not.
    /// </summary>
    [HttpGet("llm/check")]
    public async Task<IActionResult> CheckLlmConfig(CancellationToken cancellationToken)
    {
        var userId = await ResolveCurrentUserIdAsync(cancellationToken);
        if (userId is null) return Unauthorized();

        var hasAny = await _providerService.HasAnyChatConfiguredAsync(userId.Value, cancellationToken);
        if (!hasAny)
            return StatusCode(412, LlmNotConfiguredResponse.Instance);

        var providers = await _providerService.GetAllAsync(userId.Value, cancellationToken);
        return Ok(providers);
    }

    private async Task<Guid?> ResolveCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        return await _userService.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            cancellationToken);
    }
}
