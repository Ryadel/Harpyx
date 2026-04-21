namespace Harpyx.Application.Filters;

public abstract class BaseFilter : IBaseFilter
{
    public bool? IsActive { get; init; }
    public IReadOnlyList<Guid>? Ids { get; init; }
    public IReadOnlyList<Guid>? IdsToExclude { get; init; }
}