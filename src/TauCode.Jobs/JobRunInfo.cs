namespace TauCode.Jobs;

public readonly struct JobRunInfo : IEquatable<JobRunInfo> // todo: remove this 'IEquatable<JobRunInfo>', it is for UT only. UTs should develop their own comparison.
{
    public JobRunInfo(
        int runIndex,
        JobStartReason startReason,
        DateTimeOffset dueTime,
        bool dueTimeWasOverridden,
        DateTimeOffset startTime,
        DateTimeOffset? endTime,
        JobRunStatus status,
        string output,
        Exception exception)
    {
        // todo checks: positive etc
        // todo date is valid

        this.RunIndex = runIndex;
        this.StartReason = startReason;
        this.DueTime = dueTime;
        this.DueTimeWasOverridden = dueTimeWasOverridden;
        this.StartTime = startTime;
        this.EndTime = endTime;
        this.Status = status;
        this.Output = output;
        this.Exception = exception;
    }

    public int RunIndex { get; }
    public JobStartReason StartReason { get; }
    public DateTimeOffset DueTime { get; }
    public bool DueTimeWasOverridden { get; }
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset? EndTime { get; }
    public JobRunStatus Status { get; }
    public string Output { get; }
    public Exception Exception { get; }

    public bool Equals(JobRunInfo other)
    {
        return
            RunIndex == other.RunIndex &&
            StartReason == other.StartReason &&
            DueTime.Equals(other.DueTime) &&
            DueTimeWasOverridden == other.DueTimeWasOverridden &&
            StartTime.Equals(other.StartTime) &&
            Nullable.Equals(EndTime, other.EndTime) &&
            Status == other.Status &&
            Output == other.Output &&
            Equals(Exception, other.Exception);
    }

    public override bool Equals(object? obj)
    {
        return obj is JobRunInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(RunIndex);
        hashCode.Add((int)StartReason);
        hashCode.Add(DueTime);
        hashCode.Add(DueTimeWasOverridden);
        hashCode.Add(StartTime);
        hashCode.Add(EndTime);
        hashCode.Add((int)Status);
        hashCode.Add(Output);
        hashCode.Add(Exception);
        return hashCode.ToHashCode();
    }
}