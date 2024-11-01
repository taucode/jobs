﻿using NUnit.Framework;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;
using TauCode.Jobs.Schedules;

using TimeProvider = TauCode.Infrastructure.Time.TimeProvider;

namespace TauCode.Jobs.Tests.Jobs;

[TestFixture]
public partial class JobTests
{
    [Test]
    public void WaitInt_WasRunningThenEnds_WaitsAndReturnsCompleted()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await Task.Delay(500, token);
        };

        // Act
        job.ForceStart();

        var waitResult = job.Wait(1000);

        // Assert
        Assert.That(waitResult, Is.EqualTo(JobRunStatus.Completed));
    }

    [Test]
    public void WaitInt_NegativeArgument_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync("Hello!");
            await Task.Delay(TimeSpan.FromSeconds(0.5), token);
        };

        job.IsEnabled = true;

        job.Schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => job.Wait(-1));

        // Assert
        Assert.That(ex.ParamName, Is.EqualTo("millisecondsTimeout"));
    }

    [Test]
    public void WaitInt_WasRunningThenCanceled_WaitsAndReturnsCanceled()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await Task.Delay(500, token);
        };

        // Act
        job.ForceStart();

        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(200);
            job.Cancel();
        });

        var waitResult = job.Wait(1000);

        // Assert
        Assert.That(waitResult, Is.EqualTo(JobRunStatus.Canceled));
    }

    [Test]
    public async Task WaitInt_WasRunningThenFaulted_WaitsAndReturnsFaulted()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await Task.Delay(500, token);
            throw new AbandonedMutexException("Hello there!");
        };

        // Act
        job.ForceStart();

        var waitResult = job.Wait(1000);

        await Task.Delay(50); // let job run get written.

        // Assert
        Assert.That(waitResult, Is.EqualTo(JobRunStatus.Faulted));
        Assert.That(job.GetInfo(null).Runs.Single().Exception, Is.TypeOf<AbandonedMutexException>());
    }

    [Test]
    public void WaitInt_WasRunningThenJobIsDisposed_WaitsAndReturnsCanceled()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await Task.Delay(500, token);
        };

        // Act
        job.ForceStart();

        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(200);
            job.Dispose();
        });

        var waitResult = job.Wait(1000);

        // Assert
        Assert.That(waitResult, Is.EqualTo(JobRunStatus.Canceled));
    }

    [Test]
    public void WaitInt_WasRunningThenJobManagerIsDisposed_WaitsAndReturnsCanceled()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await Task.Delay(500, token);
        };

        // Act
        job.ForceStart();

        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(200);
            jobManager.Dispose();
        });

        var waitResult = job.Wait(1000);

        // Assert
        Assert.That(waitResult, Is.EqualTo(JobRunStatus.Canceled));
    }

    [Test]
    public async Task WaitInt_WasRunningTooLong_WaitsAndReturnsNull()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync("Hello!");
            await Task.Delay(TimeSpan.FromHours(1), token);
        };

        job.ForceStart();

        // Act

        await timeMachine.WaitUntilSecondsElapse(start, 1.0);

        var gotSignal = job.Wait(1000);

        // Assert
        Assert.That(gotSignal, Is.Null);
    }

    [Test]
    public void WaitInt_NotRunning_ReturnsCompletedImmediately()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync("Hello!");
            await Task.Delay(TimeSpan.FromSeconds(0.5), token);
        };

        // Act
        var waitResult = job.Wait(10);

        // Assert
        Assert.That(waitResult, Is.EqualTo(JobRunStatus.Completed));
    }

    [Test]
    public void WaitInt_JobIsDisposed_ThrowsJobObjectDisposedException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync("Hello!");
            await Task.Delay(TimeSpan.FromSeconds(0.5), token);
        };

        job.IsEnabled = true;

        job.Schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

        job.Dispose();

        // Act
        var ex = Assert.Throws<ObjectDisposedException>(() => job.Wait(10));

        // Assert
        Assert.That(ex, Has.Message.StartWith("Cannot access a disposed object."));
        Assert.That(ex.ObjectName, Is.EqualTo("my-job"));
    }
}