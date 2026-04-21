namespace Harpyx.Application.Filters;

public interface IBaseFilter
{
    bool? IsActive { get; }
    IReadOnlyList<Guid>? Ids { get; }
    IReadOnlyList<Guid>? IdsToExclude { get; }
}