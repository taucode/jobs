using Moq;
using NUnit.Framework;
using Serilog;
using System.Text;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;
using TauCode.IO;
using TauCode.Jobs.Schedules;

using TimeProvider = TauCode.Infrastructure.Time.TimeProvider;

namespace TauCode.Jobs.Tests.Jobs;

[TestFixture]
public partial class JobTests
{
    // todo: long run, discards overridden due time

    [Test]
    public void Schedule_JustCreatedJob_ReturnsNeverSchedule()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();

        var job = jobManager.CreateJob("my-job");

        // Act
        var schedule = job.Schedule;
        var dueTime = schedule.GetDueTimeAfter(start);

        // Assert
        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule.GetType().FullName, Is.EqualTo("TauCode.Jobs.Schedules.NeverSchedule"));

        Assert.That(dueTime.Year, Is.EqualTo(9000)); // 'Never'

        jobManager.Dispose();
    }

    [Test]
    public void Schedule_SetNull_ThrowsArgumentNullException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var job = jobManager.CreateJob("my-job");

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => job.Schedule = null);

        // Assert
        Assert.That(ex.ParamName, Is.EqualTo(nameof(IJob.Schedule)));
    }

    [Test]
    public async Task Schedule_SetValidValue_SetsSchedule()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var name = "job1";
        var job = jobManager.CreateJob(name);
        job.IsEnabled = true;

        var writer = new StringWriterWithEncoding(Encoding.UTF8);
        job.Output = writer;

        // Act
        var newSchedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start.AddSeconds(2));
        job.Schedule = newSchedule;
        job.Routine = (parameter, tracker, output, token) =>
        {
            output.Write("Hello!");
            return Task.CompletedTask;
        };

        await timeMachine.WaitUntilSecondsElapse(start, 2.5);

        // Assert
        Assert.That(writer.ToString(), Is.EqualTo("Hello!"));
        Assert.That(job.Schedule, Is.SameAs(newSchedule));
    }

    [Test]
    public void Schedule_SetValidValueForEnabledOrDisabledJob_SetsValue()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);
        var job = jobManager.CreateJob("my-job");

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        ISchedule schedule1 = new SimpleSchedule(SimpleScheduleKind.Minute, 1, start);
        ISchedule schedule2 = new SimpleSchedule(SimpleScheduleKind.Day, 1, start);

        // Act
        job.Schedule = schedule1;
        var updatedSchedule1 = job.Schedule;
        var dueTime1 = job.GetInfo(null).NextDueTime;

        job.IsEnabled = true;

        job.Schedule = schedule2;
        var updatedSchedule2 = job.Schedule;
        var dueTime2 = job.GetInfo(null).NextDueTime;

        // Assert
        Assert.That(updatedSchedule1, Is.SameAs(schedule1));
        Assert.That(dueTime1, Is.EqualTo(start.AddMinutes(1)));

        Assert.That(updatedSchedule2, Is.SameAs(schedule2));
        Assert.That(dueTime2, Is.EqualTo(start.AddDays(1)));
    }

    [Test]
    public async Task Schedule_SetAndStarted_ReflectedInCurrentRunAndUpdatesToNextDueTime()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await Task.Delay(10000, token); // long run
        };
        ISchedule schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

        job.IsEnabled = true;

        // Act
        job.Schedule = schedule; // will fire at 00:01

        await timeMachine.WaitUntilSecondsElapse(start, 1.03);

        // Assert
        try
        {
            var info = job.GetInfo(null);
            var currentRunNullable = info.CurrentRun;

            Assert.That(currentRunNullable, Is.Not.Null); // todo: was null once :(
            var currentRun = currentRunNullable.Value;

            Assert.That(currentRun.StartReason, Is.EqualTo(JobStartReason.ScheduleDueTime));
            Assert.That(currentRun.DueTime, Is.EqualTo(start.AddSeconds(1)));
            Assert.That(currentRun.StartTime,
                Is.EqualTo(start.AddSeconds(1)).Within(TimeSpan.FromMilliseconds(20)));

            // due time is 00:02 after start
            Assert.That(info.NextDueTime, Is.EqualTo(start.AddSeconds(2)));
        }
        catch (Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("*** Test Failed ***");
            sb.AppendLine(ex.ToString());
            sb.AppendLine("*** Log: ***");

            var log = this.CurrentLog;

            sb.AppendLine(log);

            Assert.Fail(sb.ToString());
        }
    }

    [Test]
    public async Task Schedule_SetAndStartedAndCompleted_ReflectedInOldRuns()
    {
        // Arrange
        var DEFECT = TimeSpan.FromMilliseconds(30);

        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await Task.Delay(1500, token); // 1.5 second to complete
        };
        ISchedule schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

        job.IsEnabled = true;

        // Act
        job.Schedule = schedule; // will fire at 00:01

        await timeMachine.WaitUntilSecondsElapse(
            start,
            1.0 + DEFECT.TotalSeconds + 1.5 + DEFECT.TotalSeconds);

        // Assert
        try
        {
            var info = job.GetInfo(null);
            Assert.That(info.CurrentRun, Is.Null);

            Assert.That(info.NextDueTime, Is.EqualTo(start.AddSeconds(3)));

            var pastRun = info.Runs.Single();

            Assert.That(pastRun.RunIndex, Is.EqualTo(0));
            Assert.That(pastRun.StartReason, Is.EqualTo(JobStartReason.ScheduleDueTime));
            Assert.That(pastRun.DueTime, Is.EqualTo(start.AddSeconds(1)));
            Assert.That(pastRun.DueTimeWasOverridden, Is.False);

            Assert.That(pastRun.StartTime, Is.EqualTo(start.AddSeconds(1)).Within(DEFECT));
            Assert.That(
                pastRun.EndTime,
                Is.EqualTo(pastRun.StartTime.AddSeconds(1.5)).Within(DEFECT));

            Assert.That(pastRun.Status, Is.EqualTo(JobRunStatus.Completed));
        }
        catch (Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("*** Test Failed ***");
            sb.AppendLine(ex.ToString());
            sb.AppendLine("*** Log: ***");

            var log = this.CurrentLog;

            sb.AppendLine(log);

            Assert.Fail(sb.ToString());
        }
    }

    /// <summary>
    /// 0---------1---------2---------3---------
    ///           |_____1.5s_____|               (was the plan)
    /// ______1.4s____X_________________________ (cancel)
    /// </summary>
    [Test]
    public async Task Schedule_SetAndStartedAndCanceled_ReflectedInOldRuns()
    {
        // Arrange
        var DEFECT = TimeSpan.FromMilliseconds(30);

        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await Task.Delay(1500, token); // 1.5 second to complete
        };
        ISchedule schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

        job.IsEnabled = true;

        // Act
        job.Schedule = schedule; // will fire at 00:01

        await timeMachine.WaitUntilSecondsElapse(start, 1.4 + DEFECT.TotalSeconds);

        var canceled = job.Cancel(); // will be canceled almost right after start

        Assert.That(canceled, Is.True);
        await Task.Delay(DEFECT); // let context finalize

        // Assert
        var info = job.GetInfo(null);
        Assert.That(info.CurrentRun, Is.Null);
        Assert.That(info.NextDueTime, Is.EqualTo(start.AddSeconds(2)));
        Assert.That(info.NextDueTimeIsOverridden, Is.False);

        var pastRun = info.Runs.Single();

        Assert.That(pastRun.RunIndex, Is.EqualTo(0));
        Assert.That(pastRun.StartReason, Is.EqualTo(JobStartReason.ScheduleDueTime));
        Assert.That(pastRun.DueTime, Is.EqualTo(start.AddSeconds(1)));
        Assert.That(pastRun.DueTimeWasOverridden, Is.False);

        Assert.That(pastRun.StartTime, Is.EqualTo(start.AddSeconds(1)).Within(DEFECT));
        Assert.That(
            pastRun.EndTime,
            Is.EqualTo(pastRun.StartTime.AddSeconds(0.4)).Within(DEFECT * 2));

        Assert.That(pastRun.Status, Is.EqualTo(JobRunStatus.Canceled));
    }

    /// <summary>
    /// 0---------1---------2---------3---------
    ///           |_____1.5s_____!              (exception)
    /// </summary>
    [Test]
    public async Task Schedule_SetAndStartedAndFaulted_ReflectedInOldRuns()
    {
        // Arrange
        var DEFECT = TimeSpan.FromMilliseconds(30);

        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await Task.Delay(1500, token); // 1.5 second runs with no problem...
            throw new ApplicationException("BAD_NEWS"); // ...and then throws!
        };
        ISchedule schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

        job.IsEnabled = true;

        // Act
        job.Schedule = schedule; // will fire at 00:01

        await timeMachine.WaitUntilSecondsElapse(start,
            2.8); // by this time, job will end due to the exception "BAD_NEWS" and finalize.

        // Assert
        var info = job.GetInfo(null);
        Assert.That(info.CurrentRun, Is.Null);



        Assert.That(info.NextDueTime, Is.EqualTo(start.AddSeconds(3)));
        // todo
        //Expected: 2000 - 01 - 01 00:00:03 + 00:00
        //But was:  2000 - 01 - 01 00:00:01 + 00:00



        Assert.That(info.NextDueTimeIsOverridden, Is.False);

        var pastRun = info.Runs.Single();

        Assert.That(pastRun.RunIndex, Is.EqualTo(0));
        Assert.That(pastRun.StartReason, Is.EqualTo(JobStartReason.ScheduleDueTime));
        Assert.That(pastRun.DueTime, Is.EqualTo(start.AddSeconds(1)));
        Assert.That(pastRun.DueTimeWasOverridden, Is.False);

        Assert.That(pastRun.StartTime, Is.EqualTo(start.AddSeconds(1)).Within(DEFECT));
        Assert.That(
            pastRun.EndTime,
            Is.EqualTo(pastRun.StartTime.AddSeconds(1.5)).Within(DEFECT * 2));

        Assert.That(pastRun.Status, Is.EqualTo(JobRunStatus.Faulted));
        Assert.That(pastRun.Exception, Is.TypeOf<ApplicationException>());
        Assert.That(pastRun.Exception, Has.Message.EqualTo("BAD_NEWS"));
    }

    [Test]
    public async Task Schedule_DueTimeWasOverriddenThenScheduleIsSet_OverriddenDueTimeIsDiscardedAndScheduleIsSet()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);
        var job = jobManager.CreateJob("my-job");

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);


        job.OverrideDueTime(start.AddSeconds(2.5));

        ISchedule schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

        // Act
        await timeMachine.WaitUntilSecondsElapse(start, 1.8);
        job.Schedule = schedule;

        // Assert
        var info = job.GetInfo(null);
        Assert.That(info.NextDueTime, Is.EqualTo(start.AddSeconds(2)));
        Assert.That(info.NextDueTimeIsOverridden, Is.False);
    }

    /// <summary>
    /// 0---------1-*---*---2-*-------3-*-------4     * - routine checks due time
    ///           |____________1.5s_____________|
    /// ...............!..^.........^.........^..     ! - schedule2 is set; ^ - 'schedule2' due time
    /// </summary>
    [Test]
    public async Task Schedule_SetDuringRun_DoesNotAffectRunButAppliesToDueTime()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");
        var schedule1 = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);
        job.Schedule = schedule1; // job will be started by due time of this schedule

        DateTimeOffset dueTime1 = default;
        DateTimeOffset dueTime2 = default;
        DateTimeOffset dueTime3 = default;
        DateTimeOffset dueTime4 = default;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            Log.Debug("Entered routine.");

            // start + 1.2s: due time is (start + 2s), set by schedule1
            await timeMachine.WaitUntilSecondsElapse(start, 1.2, token);
            dueTime1 = job.GetInfo(0).NextDueTime; // should be 2s

            await timeMachine.WaitUntilSecondsElapse(start, 1.7, token);
            dueTime2 = job.GetInfo(0).NextDueTime;

            await timeMachine.WaitUntilSecondsElapse(start, 2.2, token);
            dueTime3 = job.GetInfo(0).NextDueTime;

            await timeMachine.WaitUntilSecondsElapse(start, 3.2, token);
            dueTime4 = job.GetInfo(0).NextDueTime;

            await Task.Delay(TimeSpan.FromHours(2), token);

            Log.Debug("Exited routine.");
        };

        job.IsEnabled = true;

        // Act
        var schedule2 = new SimpleSchedule(SimpleScheduleKind.Second, 1, start.AddSeconds(1.8));
        await timeMachine.WaitUntilSecondsElapse(start, 1.4);
        job.Schedule = schedule2;

        await timeMachine.WaitUntilSecondsElapse(start, 4);

        // Assert
        try
        {
            Assert.That(dueTime1, Is.EqualTo(start.AddSeconds(2)));
            Assert.That(dueTime2, Is.EqualTo(start.AddSeconds(1.8)).Within(TimeSpan.FromMilliseconds(1)));
            Assert.That(dueTime3, Is.EqualTo(start.AddSeconds(2.8)).Within(TimeSpan.FromMilliseconds(1)));
            Assert.That(dueTime4, Is.EqualTo(start.AddSeconds(3.8)).Within(TimeSpan.FromMilliseconds(1)));
        }
        catch (Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("*** Test Failed ***");
            sb.AppendLine(ex.ToString());
            sb.AppendLine("*** Log: ***");

            var log = this.CurrentLog;

            sb.AppendLine(log);

            Assert.Fail(sb.ToString());
        }
    }

    [Test]
    public void Schedule_SetThenDisposed_EqualsToLastOne()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);
        var job = jobManager.CreateJob("my-job");

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var schedule1 = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);
        var schedule2 = new SimpleSchedule(SimpleScheduleKind.Minute, 1, start);
        var schedule3 = new SimpleSchedule(SimpleScheduleKind.Hour, 1, start);

        job.Schedule = schedule1;
        var readSchedule1 = job.Schedule;

        job.Schedule = schedule2;
        var readSchedule2 = job.Schedule;

        // Act
        job.Schedule = schedule3;
        job.Dispose();
        var readSchedule3 = job.Schedule;

        // Assert
        Assert.That(readSchedule1, Is.SameAs(schedule1));
        Assert.That(readSchedule2, Is.SameAs(schedule2));
        Assert.That(readSchedule3, Is.SameAs(schedule3));
    }

    [Test]
    public void Schedule_DisposedThenSet_ThrowsJobObjectDisposedException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);
        var job = jobManager.CreateJob("my-job");

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        // Act
        job.Dispose();
        var ex = Assert.Throws<ObjectDisposedException>(() =>
            job.Schedule = new SimpleSchedule(SimpleScheduleKind.Minute, 1, start.AddHours(1)));

        // Assert
        Assert.That(ex, Has.Message.StartWith("Cannot access a disposed object."));
        Assert.That(ex.ObjectName, Is.EqualTo("my-job"));
    }

    [Test]
    public async Task Schedule_ScheduleThrows_DueTimeSetToNever()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);
        var job = jobManager.CreateJob("my-job");

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);


        var n = 0;

        job.IsEnabled = true;
        job.Routine = (parameter, tracker, output, token) =>
        {
            n = 100;
            return Task.CompletedTask;
        };

        // set some normal schedule first
        job.Schedule = new SimpleSchedule(SimpleScheduleKind.Minute, 1, start);
        var normalDueTime = job.GetInfo(null).NextDueTime;

        var exception = new NotSupportedException("I do not support this!");

        // Act
        var scheduleMock = new Mock<ISchedule>();
        scheduleMock
            .Setup(x => x.GetDueTimeAfter(It.IsAny<DateTimeOffset>()))
            .Throws(exception);

        job.Schedule = scheduleMock.Object;

        await Task.Delay(3000); // let job try to start (it should not start though)

        var info = job.GetInfo(null);
        var faultedDueTime = info.NextDueTime;

        // Assert
        Assert.That(normalDueTime, Is.EqualTo(start.AddMinutes(1)));
        Assert.That(faultedDueTime.Year, Is.EqualTo(9000));

        // job never ran
        Assert.That(info.CurrentRun, Is.Null);
        Assert.That(info.Runs, Is.Empty);
        Assert.That(info.RunCount, Is.Zero);
        Assert.That(n, Is.EqualTo(0));

        var log = this.CurrentLog;

        Assert.That(log, Does.Contain($"{exception.GetType().FullName}: {exception.Message}"));

        Assert.Pass(log);
    }

    [Test]
    public async Task Schedule_ScheduleReturnsDueTimeBeforeNow_DueTimeSetToNever()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);
        var job = jobManager.CreateJob("my-job");

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var n = 0;

        job.IsEnabled = true;
        job.Routine = (parameter, tracker, output, token) =>
        {
            n = 100;
            return Task.CompletedTask;
        };

        // set some normal schedule first
        job.Schedule = new SimpleSchedule(SimpleScheduleKind.Minute, 1, start);
        var normalDueTime = job.GetInfo(null).NextDueTime;

        // Act
        var scheduleMock = new Mock<ISchedule>();
        scheduleMock
            .Setup(x => x.GetDueTimeAfter(It.IsAny<DateTimeOffset>()))
            .Returns(start.AddSeconds(-1)); // due time in the past

        job.Schedule = scheduleMock.Object;

        await Task.Delay(3000); // let job try to start (it should not start though)

        var info = job.GetInfo(null);
        var faultedDueTime = info.NextDueTime;

        // Assert
        Assert.That(normalDueTime, Is.EqualTo(start.AddMinutes(1)));
        Assert.That(faultedDueTime.Year, Is.EqualTo(9000));

        // job never ran
        Assert.That(info.CurrentRun, Is.Null);
        Assert.That(info.Runs, Is.Empty);
        Assert.That(info.RunCount, Is.Zero);
        Assert.That(n, Is.EqualTo(0));

        var log = this.CurrentLog;
        Assert.That(log, Does.Contain($"Due time is earlier than current time. Due time is changed to 'never'."));

        Assert.Pass(log);
    }

    [Test]
    public async Task Schedule_ScheduleReturnsDueTimeAfterNever_DueTimeSetToNever()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);
        var job = jobManager.CreateJob("my-job");

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);


        var n = 0;

        job.IsEnabled = true;
        job.Routine = (parameter, tracker, output, token) =>
        {
            n = 100;
            return Task.CompletedTask;
        };

        // set some normal schedule first
        job.Schedule = new SimpleSchedule(SimpleScheduleKind.Minute, 1, start);
        var normalDueTime = job.GetInfo(null).NextDueTime;

        // Act
        var scheduleMock = new Mock<ISchedule>();
        scheduleMock
            .Setup(x => x.GetDueTimeAfter(It.IsAny<DateTimeOffset>()))
            .Returns(DateTimeOffset.MaxValue); // due time in the past

        job.Schedule = scheduleMock.Object;

        await Task.Delay(3000); // let job try to start (it should not start though)

        var info = job.GetInfo(null);
        var faultedDueTime = info.NextDueTime;

        // Assert
        Assert.That(normalDueTime, Is.EqualTo(start.AddMinutes(1)));
        Assert.That(faultedDueTime.Year, Is.EqualTo(9000));

        // job never ran
        Assert.That(info.CurrentRun, Is.Null);
        Assert.That(info.Runs, Is.Empty);
        Assert.That(info.RunCount, Is.Zero);
        Assert.That(n, Is.EqualTo(0));

        var log = this.CurrentLog;
        Assert.That(log, Does.Contain("Due time is later than 'never'. Due time is changed to 'never'."));

        Assert.Pass(log);
    }
}