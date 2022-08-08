namespace TauCode.Jobs;

public enum JobStartReason
{
    ScheduleDueTime = 1,
    OverriddenDueTime = 2,
    Force = 3,
}