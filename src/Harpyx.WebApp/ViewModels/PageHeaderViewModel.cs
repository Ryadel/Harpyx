namespace Harpyx.WebApp.ViewModels;

public sealed record PageHeaderViewModel(
    string Title,
    string? Intro = null,
    IReadOnlyList<PageActionViewModel>? Actions = null);

public sealed record EmptyStateViewModel(
    string Message,
    PageActionViewModel? Action = null);

public sealed record PageActionViewModel(
    string Label,
    string Page,
    string? Icon = null,
    string CssClass = "btn btn-primary btn-sm",
    IDictionary<string, string>? RouteValues = null);
