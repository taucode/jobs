using NUnit.Framework;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;

using TimeProvider = TauCode.Infrastructure.Time.TimeProvider;

namespace TauCode.Jobs.Tests.Jobs;

[TestFixture]
public partial class JobTests
{
    [Test]
    public async Task Cancel_WasRunning_CancelsAndReturnsTrue()
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
            await Task.Delay(TimeSpan.FromHours(1), token);
        };

        job.IsEnabled = true;

        job.ForceStart();

        // Act
        await timeMachine.WaitUntilSecondsElapse(start, 2.0);
        var canceled = job.Cancel();

        await timeMachine.WaitUntilSecondsElapse(start, 2.2);

        var info = job.GetInfo(null);

        // Assert
        var DEFECT = TimeSpan.FromMilliseconds(30);

        Assert.That(canceled, Is.True);

        Assert.That(job.IsDisposed, Is.False);

        Assert.That(info.CurrentRun, Is.Null);
        Assert.That(info.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(info.NextDueTimeIsOverridden, Is.False);
        Assert.That(info.RunCount, Is.EqualTo(1));
        Assert.That(info.Runs, Has.Count.EqualTo(1));

        var run = info.Runs.Single();

        Assert.That(run.RunIndex, Is.EqualTo(0));
        Assert.That(run.StartReason, Is.EqualTo(JobStartReason.Force));
        Assert.That(run.DueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(run.DueTimeWasOverridden, Is.False);
        Assert.That(run.Status, Is.EqualTo(JobRunStatus.Canceled));
        Assert.That(run.Output, Is.EqualTo("Hello!"));
        Assert.That(run.Exception, Is.Null);
    }

    [Test]
    public void Cancel_NotRunning_ReturnsFalse()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");
        job.IsEnabled = true;

        // Act
        var canceled = job.Cancel();

        // Assert
        Assert.That(canceled, Is.False);

        var info = job.GetInfo(null);

        Assert.That(info.CurrentRun, Is.Null);
        Assert.That(info.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(info.NextDueTimeIsOverridden, Is.False);
        Assert.That(info.RunCount, Is.Zero);
        Assert.That(info.Runs, Is.Empty);
    }

    [Test]
    public void Cancel_JobIsDisposed_ThrowsJobObjectDisposedException()
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
        var ex = Assert.Throws<ObjectDisposedException>(() => job.Cancel());

        // Assert
        Assert.That(ex, Has.Message.StartWith("Cannot access a disposed object."));
        Assert.That(ex.ObjectName, Is.EqualTo("my-job"));
    }
}