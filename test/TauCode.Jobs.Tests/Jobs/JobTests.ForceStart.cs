using NUnit.Framework;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;
using TauCode.Jobs.Schedules;

using TimeProvider = TauCode.Infrastructure.Time.TimeProvider;

namespace TauCode.Jobs.Tests.Jobs;

[TestFixture]
public partial class JobTests
{
    [Test]
    public async Task ForceStart_IsEnabledAndNotRunning_Starts()
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
            await Task.Delay(300, token);
        };

        // Act
        job.ForceStart();

        var info1 = job.GetInfo(null);

        await timeMachine.WaitUntilSecondsElapse(start, 0.6);

        var info2 = job.GetInfo(null);

        // Assert
        Assert.That(info1.CurrentRun, Is.Not.Null);
        Assert.That(info1.CurrentRun.Value.StartReason, Is.EqualTo(JobStartReason.Force));

        var run = info2.Runs.Single();
        Assert.That(run.StartReason, Is.EqualTo(JobStartReason.Force));
    }

    [Test]
    public void ForceStart_IsDisabled_ThrowsJobException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => job.ForceStart());

        // Assert
        Assert.That(ex, Has.Message.EqualTo("Job 'my-job' is disabled."));
    }

    [Test]
    public void ForceStart_AlreadyStartedByForce_ThrowsJobException()
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
            await Task.Delay(800, token);
        };

        job.ForceStart();

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => job.ForceStart());

        // Assert
        Assert.That(ex, Has.Message.EqualTo("Job 'my-job' is already running."));
    }

    [Test]
    public async Task ForceStart_AlreadyStartedBySchedule_ThrowsJobException()
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
            await Task.Delay(TimeSpan.FromSeconds(0.7), token);
        };

        job.IsEnabled = true;

        job.Schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

        // Act
        await timeMachine.WaitUntilSecondsElapse(start, 1.1);
        var ex = Assert.Throws<InvalidOperationException>(() => job.ForceStart());

        // Assert
        Assert.That(ex, Has.Message.EqualTo("Job 'my-job' is already running."));
    }

    [Test]
    public void ForceStart_JobIsDisposed_ThrowsJobObjectDisposedException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");
        job.IsEnabled = true;
        job.Dispose();

        // Act
        var ex = Assert.Throws<ObjectDisposedException>(() => job.ForceStart());

        // Assert
        Assert.That(ex, Has.Message.StartWith("Cannot access a disposed object."));
        Assert.That(ex.ObjectName, Is.EqualTo("my-job"));
    }
}