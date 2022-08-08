namespace TauCode.Jobs.Schedules;

public interface ISchedule
{
    string Description { get; set; }
    DateTimeOffset GetDueTimeAfter(DateTimeOffset after);
}