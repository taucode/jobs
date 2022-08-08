using TauCode.Jobs.Schedules;

namespace TauCode.Jobs;

// todo #regions
internal class Job : IJob
{
    private readonly Employee _employee;

    internal Job(Employee employee)
    {
        _employee = employee;
    }

    public void Dispose() => _employee.Dispose();

    public string Name => _employee.Name;

    public bool IsEnabled
    {
        get => _employee.IsEnabled;
        set => _employee.IsEnabled = value;
    }

    public ISchedule Schedule
    {
        get => _employee.Schedule;
        set => _employee.Schedule = value;
    }

    public JobDelegate Routine
    {
        get => _employee.Routine;
        set => _employee.Routine = value;
    }

    public object Parameter
    {
        get => _employee.Parameter;
        set => _employee.Parameter = value;
    }

    public IProgressTracker ProgressTracker
    {
        get => _employee.ProgressTracker;
        set => _employee.ProgressTracker = value;
    }

    public TextWriter Output
    {
        get => _employee.Output;
        set => _employee.Output = value;
    }

    public JobInfo GetInfo(int? maxRunCount) => _employee.GetInfo(maxRunCount);

    public void OverrideDueTime(DateTimeOffset? dueTime) => _employee.OverrideDueTime(dueTime);

    public void ForceStart() => _employee.ForceStart();

    public bool Cancel() => _employee.Cancel();

    public JobRunStatus? Wait(int millisecondsTimeout) => _employee.Wait(millisecondsTimeout);

    public JobRunStatus? Wait(TimeSpan timeout) => _employee.Wait(timeout);

    public bool IsDisposed => _employee.IsDisposed;
}