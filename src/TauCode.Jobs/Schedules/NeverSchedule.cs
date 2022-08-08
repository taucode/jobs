namespace TauCode.Jobs.Schedules;

// todo: internal.
public sealed class NeverSchedule : ISchedule
{
    private const string NeverDescription = "Technical schedule which due time never occurs";

    public static ISchedule Instance = new NeverSchedule();

    private NeverSchedule()
    {
    }

    public string Description
    {
        get => NeverDescription;
        set => throw new NotSupportedException();
    }

    public DateTimeOffset GetDueTimeAfter(DateTimeOffset after) => JobExtensions.Never; // todo: check argument
}