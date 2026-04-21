namespace Harpyx.Application.Filters;

public sealed class TenantFilter : BaseFilter
{
    public bool IncludeVisibleToAllUsers { get; init; }
}