﻿using Serilog;
using TauCode.Jobs.Instruments;
using TauCode.Jobs.Schedules;

namespace TauCode.Jobs;

internal class Employee : IDisposable
{
    #region Fields

    private readonly JobManager _jobManager;
    private readonly Job _job;
    private readonly Runner _runner;

    #endregion

    #region Constructor

    internal Employee(JobManager jobManager, ILogger? logger, string name)
    {
        this.Name = name;

        _jobManager = jobManager;
        _job = new Job(this);
        _runner = new Runner(this.Name, logger);
    }

    #endregion

    #region Internal - IJob Implementation

    /// <summary>
    /// Returns <see cref="IJob"/> instance itself.
    /// </summary>
    /// <returns><see cref="IJob"/> instance itself</returns>
    internal IJob GetJob() => _job;

    internal string Name { get; }

    internal bool IsEnabled
    {
        get => _runner.IsEnabled;
        set => _runner.IsEnabled = value;
    }

    internal ISchedule Schedule
    {
        get => _runner.DueTimeHolder.Schedule;
        set
        {
            _runner.DueTimeHolder.Schedule = value;
            _jobManager.PulseWork($"Pulsing due to '{nameof(Schedule)}'.");
        }
    }

    internal JobDelegate Routine
    {
        get => _runner.JobPropertiesHolder.Routine;
        set => _runner.JobPropertiesHolder.Routine = value;
    }

    internal object Parameter
    {
        get => _runner.JobPropertiesHolder.Parameter;
        set => _runner.JobPropertiesHolder.Parameter = value;
    }

    internal IProgressTracker ProgressTracker
    {
        get => _runner.JobPropertiesHolder.ProgressTracker;
        set => _runner.JobPropertiesHolder.ProgressTracker = value;
    }

    internal TextWriter Output
    {
        get => _runner.JobPropertiesHolder.Output;
        set => _runner.JobPropertiesHolder.Output = value;
    }

    internal JobInfo GetInfo(int? maxRunCount) => _runner.GetInfo(maxRunCount);

    internal void OverrideDueTime(DateTimeOffset? dueTime)
    {
        _runner.DueTimeHolder.OverriddenDueTime = dueTime;
        _jobManager.PulseWork($"Pulsing due to '{nameof(OverrideDueTime)}'.");
    }

    internal void ForceStart() => this.Start(JobStartReason.Force, null);

    internal bool Cancel() => _runner.Cancel();

    internal JobRunStatus? Wait(int millisecondsTimeout) => _runner.Wait(millisecondsTimeout);

    internal JobRunStatus? Wait(TimeSpan timeout) => _runner.Wait(timeout);

    internal bool IsDisposed => _runner.IsDisposed;

    #endregion

    #region Internal - Interface for JobManager

    internal DueTimeInfo? GetDueTimeInfoForJobManager(bool future) => _runner.GetDueTimeInfoForJobManager(future);

    internal JobStartResult Start(JobStartReason startReason, CancellationToken? token) =>
        _runner.Start(startReason, token);

    #endregion

    #region IDisposable Members

    public void Dispose()
    {
        _runner.Dispose();
        _jobManager.Remove(this.Name);
    }

    #endregion
}