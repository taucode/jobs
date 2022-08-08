namespace TauCode.Jobs.Schedules;

public class ConcreteSchedule : ISchedule
{
    private readonly List<DateTimeOffset?> _dueTimes;

    public ConcreteSchedule(params DateTimeOffset[] dueTimes)
    {
        _dueTimes = dueTimes
            .Distinct()
            .Cast<DateTimeOffset?>()
            .ToList();
    }

    public string Description { get; set; }

    public DateTimeOffset GetDueTimeAfter(DateTimeOffset after)
    {
        var dueTime = _dueTimes.FirstOrDefault(x => x.Value >= after) ?? JobExtensions.Never;
        return dueTime;
    }
}