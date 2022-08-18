using TauCode.Extensions;

namespace TauCode.Jobs;

public static class JobExtensions
{
    private const int NeverYear = 9000;

    public static readonly DateTimeOffset Never;

    static JobExtensions()
    {
        Never = $"{NeverYear}-01-01Z".ToUtcDateOffset();
    }

    // todo: internal
    public static Task IdleJobRoutine(
        object parameter,
        IProgressTracker progressTracker,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        output?.WriteLine("Warning: usage of default idle routine.");
        return Task.CompletedTask;
    }

    internal static DateTimeOffset GetEffectiveDueTime(this DueTimeInfo dueTimeInfo) =>
        dueTimeInfo.OverriddenDueTime ?? dueTimeInfo.ScheduleDueTime;

    internal static bool IsDueTimeOverridden(this DueTimeInfo dueTimeInfo) =>
        dueTimeInfo.OverriddenDueTime.HasValue;
}