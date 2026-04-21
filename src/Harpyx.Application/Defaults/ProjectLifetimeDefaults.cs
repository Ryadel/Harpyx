using Harpyx.Domain.Enums;

namespace Harpyx.Application.Defaults;

public static class ProjectLifetimeDefaults
{
    public const bool DefaultAutoExtendOnActivity = true;
    public static readonly ProjectLifetimePreset DefaultPreset = ProjectLifetimePreset.Day30;

    public static readonly IReadOnlyList<ProjectLifetimePreset> OrderedPresets =
    [
        ProjectLifetimePreset.Hour1,
        ProjectLifetimePreset.Hour2,
        ProjectLifetimePreset.Hour3,
        ProjectLifetimePreset.Hour4,
        ProjectLifetimePreset.Hour5,
        ProjectLifetimePreset.Hour6,
        ProjectLifetimePreset.Hour7,
        ProjectLifetimePreset.Hour8,
        ProjectLifetimePreset.Hour9,
        ProjectLifetimePreset.Hour10,
        ProjectLifetimePreset.Hour11,
        ProjectLifetimePreset.Hour12,
        ProjectLifetimePreset.Hour24,
        ProjectLifetimePreset.Day2,
        ProjectLifetimePreset.Day3,
        ProjectLifetimePreset.Day4,
        ProjectLifetimePreset.Day5,
        ProjectLifetimePreset.Day6,
        ProjectLifetimePreset.Day7,
        ProjectLifetimePreset.Day10,
        ProjectLifetimePreset.Day15,
        ProjectLifetimePreset.Day30,
        ProjectLifetimePreset.Day60,
        ProjectLifetimePreset.Day90,
        ProjectLifetimePreset.Month6,
        ProjectLifetimePreset.Year1
    ];

    public static int GetDurationHours(ProjectLifetimePreset preset) => (int)preset;

    public static DateTimeOffset CalculateExpirationUtc(ProjectLifetimePreset preset, DateTimeOffset fromUtc)
        => fromUtc.AddHours(GetDurationHours(preset));

    public static string GetLabel(ProjectLifetimePreset preset)
    {
        return preset switch
        {
            ProjectLifetimePreset.Hour1 => "1 hour",
            ProjectLifetimePreset.Hour2 => "2 hours",
            ProjectLifetimePreset.Hour3 => "3 hours",
            ProjectLifetimePreset.Hour4 => "4 hours",
            ProjectLifetimePreset.Hour5 => "5 hours",
            ProjectLifetimePreset.Hour6 => "6 hours",
            ProjectLifetimePreset.Hour7 => "7 hours",
            ProjectLifetimePreset.Hour8 => "8 hours",
            ProjectLifetimePreset.Hour9 => "9 hours",
            ProjectLifetimePreset.Hour10 => "10 hours",
            ProjectLifetimePreset.Hour11 => "11 hours",
            ProjectLifetimePreset.Hour12 => "12 hours",
            ProjectLifetimePreset.Hour24 => "24 hours",
            ProjectLifetimePreset.Day2 => "2 days",
            ProjectLifetimePreset.Day3 => "3 days",
            ProjectLifetimePreset.Day4 => "4 days",
            ProjectLifetimePreset.Day5 => "5 days",
            ProjectLifetimePreset.Day6 => "6 days",
            ProjectLifetimePreset.Day7 => "7 days",
            ProjectLifetimePreset.Day10 => "10 days",
            ProjectLifetimePreset.Day15 => "15 days",
            ProjectLifetimePreset.Day30 => "30 days (1 month)",
            ProjectLifetimePreset.Day60 => "60 days",
            ProjectLifetimePreset.Day90 => "90 days",
            ProjectLifetimePreset.Month6 => "6 months",
            ProjectLifetimePreset.Year1 => "1 year",
            _ => $"{GetDurationHours(preset)} hours"
        };
    }
}
