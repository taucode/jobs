using NUnit.Framework;
using System.Text;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;
using TauCode.IO;
using TauCode.Jobs.Schedules;

namespace TauCode.Jobs.Tests.Jobs;

[TestFixture]
public partial class JobTests
{
    [Test]
    public async Task Routine_JustCreatedJob_NotNullAndRunsSuccessfully()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        job.Schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start.AddSeconds(2));
        job.IsEnabled = true;

        // Act
        var routine = job.Routine;
        var inTime = await timeMachine.WaitUntilSecondsElapse(start, 2.7);
        if (!inTime)
        {
            throw new Exception("Test failed. TPL was too slow.");
        }

        jobManager.Dispose();

        var info = job.GetInfo(null);

        // Assert
        try
        {
            Assert.That(routine, Is.Not.Null);
            var run = info.Runs.First();
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Completed));
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
    public void Routine_SetNull_ThrowsArgumentNullException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => job.Routine = null);

        // Assert
        Assert.That(ex.ParamName, Is.EqualTo("Routine"));
    }

    /// <summary>
    /// 0---------1---------2---------3---------4---------5---------
    ///           |_R1:_1.5s_____|    |_R2:_1.5s_____|               (R1 - routine1, R2 - routine2)
    /// ______1.4s____!1_______________________________!2___________ (!1 - set routine2, !2 - dispose)
    /// </summary>
    [Test]
    public async Task Routine_SetOnTheFly_CompletesWithOldRoutineAndThenStartsWithNewRoutine()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        var output = new StringWriterWithEncoding(Encoding.UTF8);
        job.Output = output;

        job.Schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

        async Task Routine1(object parameter, IProgressTracker tracker, TextWriter writer, CancellationToken token)
        {
            await writer.WriteAsync("First Routine!");
            await Task.Delay(1500, token);
        }

        async Task Routine2(object parameter, IProgressTracker tracker, TextWriter writer, CancellationToken token)
        {
            await writer.WriteAsync("Second Routine!");
            await Task.Delay(1500, token);
        }

        job.IsEnabled = true;
        job.Routine = Routine1;

        // Act
        await timeMachine.WaitUntilSecondsElapse(start, 1.4);
        job.Routine = Routine2;

        await timeMachine.WaitUntilSecondsElapse(start, 2.8);
        var output1 = output.ToString();

        await timeMachine.WaitUntilSecondsElapse(start, 4.8);
        var output2 = output.ToString();

        jobManager.Dispose();

        var info = job.GetInfo(null);

        // Assert
        try
        {
            Assert.That(info.NextDueTime, Is.EqualTo(start.AddSeconds(5)));

            Assert.That(info.CurrentRun, Is.Null);
            Assert.That(info.RunCount, Is.EqualTo(2));
            Assert.That(info.Runs, Has.Count.EqualTo(2));

            var run0 = info.Runs[0];
            Assert.That(run0.DueTime, Is.EqualTo(start.AddSeconds(1)));
            Assert.That(run0.Output, Is.EqualTo("First Routine!"));
            Assert.That(output1, Is.EqualTo("First Routine!"));

            var run1 = info.Runs[1];
            Assert.That(run1.DueTime, Is.EqualTo(start.AddSeconds(3)));
            Assert.That(run1.Output, Is.EqualTo("Second Routine!"));
            Assert.That(output2, Is.EqualTo("First Routine!Second Routine!"));
        }
        catch (Exception ex)
        {
            // todo: get rid of these try/catch since all ut-s are green now [2022-08-15]

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
    public void Routine_SetValidValueForEnabledOrDisabledJob_SetsValue()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        JobDelegate routine1 = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync("First Routine!");
            await Task.Delay(1500, token);
        };

        JobDelegate routine2 = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync("Second Routine!");
            await Task.Delay(1500, token);
        };


        // Act
        job.Routine = routine1;
        var updatedRoutine1 = job.Routine;

        job.IsEnabled = true;

        job.Routine = routine2;
        var updatedRoutine2 = job.Routine;

        // Assert
        Assert.That(updatedRoutine1, Is.SameAs(routine1));
        Assert.That(updatedRoutine2, Is.SameAs(routine2));
    }

    /// <summary>
    /// 0---------1---------2---------3---------4---------5---------6
    ///           |_R1:_1.5s_____|              |_R2:_1.5s_____|      (R1 - routine1, R2 - routine2)
    /// ______________________________!______________________________ (! - set routine2)
    /// </summary>
    [Test]
    public async Task Routine_SetAfterPreviousRunCompleted_SetsValueAndRunsWithIt()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);
        var job = jobManager.Create("my-job");

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var output = new StringWriterWithEncoding(Encoding.UTF8);
        job.Output = output;

        JobDelegate routine1 = async (parameter, tracker, writer, token) =>
        {
            await writer.WriteAsync("First Routine!");
            await Task.Delay(1500, token);
        };

        JobDelegate routine2 = async (parameter, tracker, writer, token) =>
        {
            await writer.WriteAsync("Second Routine!");
            await Task.Delay(1500, token);
        };

        job.Schedule = new ConcreteSchedule(
            start.AddSeconds(1),
            start.AddSeconds(4));

        job.IsEnabled = true;

        // Act
        job.Routine = routine1;
        var updatedRoutine1 = job.Routine;

        await timeMachine.WaitUntilSecondsElapse(start, 2.9); // job with routine1 will complete
        var output1 = output.ToString();

        await timeMachine.WaitUntilSecondsElapse(start, 3.0);
        job.Routine = routine2;
        var updatedRoutine2 = job.Routine;

        await timeMachine.WaitUntilSecondsElapse(start, 6.0);
        var output2 = output.ToString();

        // Assert
        try
        {
            Assert.That(updatedRoutine1, Is.SameAs(routine1));
            Assert.That(output1, Is.EqualTo("First Routine!"));

            Assert.That(updatedRoutine2, Is.SameAs(routine2));
            Assert.That(output2, Is.EqualTo("First Routine!Second Routine!"));
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
    public async Task Routine_Throws_LogsFaultedTask()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        var output = new StringWriterWithEncoding(Encoding.UTF8);
        job.Output = output;

        var exception = new NotSupportedException("Bye baby!");

        JobDelegate routine = (parameter, tracker, writer, token) =>
        {
            writer.WriteLine("Hi there!");
            throw exception;
        };

        job.Schedule = new ConcreteSchedule(
            start.AddSeconds(1));

        job.IsEnabled = true;

        // Act
        job.Routine = routine;
        var updatedRoutine = job.Routine;

        await timeMachine.WaitUntilSecondsElapse(start, 1.5); // will fail by this time
        var outputResult = output.ToString();

        var info = job.GetInfo(null);

        // Assert
        try
        {
            Assert.That(updatedRoutine, Is.SameAs(routine));
            Assert.That(outputResult, Does.Contain("Hi there!"));
            Assert.That(outputResult, Does.Contain(exception.ToString()));

            Assert.That(info.CurrentRun, Is.Null);

            Assert.That(info.RunCount, Is.EqualTo(1));
            var run = info.Runs.Single();

            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Faulted));
            Assert.That(run.Exception, Is.SameAs(exception));
            Assert.That(run.Output, Does.Contain(exception.ToString()));

            var log = this.CurrentLog;

            Assert.That(log, Does.Contain("Routine has thrown an exception."));
            Assert.That(log,
                Does.Contain($"Job 'my-job' completed synchronously. Reason of start was 'ScheduleDueTime'."));
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
    public async Task Routine_ReturnsCanceledTask_LogsCanceledTask()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        using var source = new CancellationTokenSource();
        source.Cancel();

        var job = jobManager.Create("my-job");

        var output = new StringWriterWithEncoding(Encoding.UTF8);
        job.Output = output;

        JobDelegate routine = (parameter, tracker, writer, token) =>
        {
            writer.WriteLine("Hi there!");
            return Task.FromCanceled(source.Token);
        };

        job.Schedule = new ConcreteSchedule(
            start.AddSeconds(1));

        job.IsEnabled = true;

        // Act
        job.Routine = routine;
        var updatedRoutine = job.Routine;

        var inTime = await timeMachine.WaitUntilSecondsElapse(start, 1.5); // will be canceled by this time
        if (!inTime)
        {
            throw new Exception("Test failed. TPL was too slow.");
        }

        var outputResult = output.ToString();

        var info = job.GetInfo(null);

        // Assert
        try
        {
            Assert.That(updatedRoutine, Is.SameAs(routine));
            Assert.That(outputResult, Does.Contain("Hi there!"));

            Assert.That(info.CurrentRun, Is.Null);

            Assert.That(info.RunCount, Is.EqualTo(1));
            var run = info.Runs.Single();

            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Canceled));
            Assert.That(run.Exception, Is.Null);

            var log = this.CurrentLog;
            Assert.That(
                log,
                Does.Contain($"Job 'my-job' completed synchronously. Reason of start was 'ScheduleDueTime'."));
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
    public async Task Routine_ReturnsFaultedTask_LogsFaultedTask()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);
        var job = jobManager.Create("my-job");

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var output = new StringWriterWithEncoding(Encoding.UTF8);
        job.Output = output;

        var exception = new NotSupportedException("Bye baby!");

        JobDelegate routine = (parameter, tracker, writer, token) =>
        {
            writer.WriteLine("Hi there!");
            return Task.FromException(exception);
        };

        job.Schedule = new ConcreteSchedule(
            start.AddSeconds(1));

        job.IsEnabled = true;

        // Act
        job.Routine = routine;
        var updatedRoutine = job.Routine;

        await timeMachine.WaitUntilSecondsElapse(start, 1.5); // will fail by this time
        var outputResult = output.ToString();

        var info = job.GetInfo(null);

        // Assert
        try
        {
            Assert.That(updatedRoutine, Is.SameAs(routine));
            Assert.That(outputResult, Does.Contain("Hi there!"));
            Assert.That(outputResult, Does.Contain(exception.ToString()));

            Assert.That(info.CurrentRun, Is.Null);

            Assert.That(info.RunCount, Is.EqualTo(1));
            var run = info.Runs.Single();

            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Faulted));
            Assert.That(run.Exception, Is.SameAs(exception));
            Assert.That(run.Output, Does.Contain(exception.ToString()));

            var log = this.CurrentLog;

            Assert.That(log,
                Does.Contain($"Job 'my-job' completed synchronously. Reason of start was 'ScheduleDueTime'."));
        }
        catch (Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("*** Test Failed ***");
            sb.AppendLine(ex.ToString());
            sb.AppendLine("*** Log: ***");

            //var log = _logWriter.ToString();
            var log = this.CurrentLog;

            sb.AppendLine(log);

            Assert.Fail(sb.ToString());
        }
    }

    [Test]
    public async Task Routine_ReturnsCompletedTask_LogsCompletedTask()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);
        var job = jobManager.Create("my-job");

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);


        var output = new StringWriterWithEncoding(Encoding.UTF8);
        job.Output = output;


        JobDelegate routine = (parameter, tracker, writer, token) =>
        {
            writer.WriteLine("Hi there!");
            return Task.CompletedTask;
        };

        job.Schedule = new ConcreteSchedule(
            start.AddSeconds(1));

        job.IsEnabled = true;

        // Act
        job.Routine = routine;
        var updatedRoutine = job.Routine;

        await timeMachine.WaitUntilSecondsElapse(start, 1.5); // will fail by this time
        var outputResult = output.ToString();

        var info = job.GetInfo(null);

        // Assert
        Assert.That(updatedRoutine, Is.SameAs(routine));
        Assert.That(outputResult, Does.Contain("Hi there!"));

        Assert.That(info.CurrentRun, Is.Null);

        Assert.That(info.RunCount, Is.EqualTo(1));
        var run = info.Runs.Single();

        Assert.That(run.Status, Is.EqualTo(JobRunStatus.Completed));

        var log = this.CurrentLog;

        Assert.That(
            log,
            Does.Contain($"Job 'my-job' completed synchronously. Reason of start was 'ScheduleDueTime'."));
    }

    [Test]
    public void Routine_Disposed_CanBeRead()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        JobDelegate routine1 = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync("First Routine!");
            await Task.Delay(1500, token);
        };

        JobDelegate routine2 = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync("Second Routine!");
            await Task.Delay(1500, token);
        };


        // Act
        job.Routine = routine1;
        var updatedRoutine1 = job.Routine;

        job.IsEnabled = true;

        job.Routine = routine2;
        var updatedRoutine2 = job.Routine;

        jobManager.Dispose();

        var updatedRoutineAfterDisposal = job.Routine;

        // Assert
        Assert.That(updatedRoutine1, Is.SameAs(routine1));
        Assert.That(updatedRoutine2, Is.SameAs(routine2));

        Assert.That(updatedRoutineAfterDisposal, Is.SameAs(routine2));
    }

    [Test]
    public void Routine_DisposedAndSet_ThrowsJobObjectDisposedException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        JobDelegate routine1 = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync("First Routine!");
            await Task.Delay(1500, token);
        };

        JobDelegate routine2 = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync("Second Routine!");
            await Task.Delay(1500, token);
        };


        // Act
        job.Routine = routine1;
        var updatedRoutine1 = job.Routine;

        job.IsEnabled = true;

        job.Routine = routine2;
        var updatedRoutine2 = job.Routine;

        jobManager.Dispose();

        var updatedRoutineAfterDisposal = job.Routine;

        var ex = Assert.Throws<ObjectDisposedException>(() => job.Routine = routine1);

        // Assert
        Assert.That(updatedRoutine1, Is.SameAs(routine1));
        Assert.That(updatedRoutine2, Is.SameAs(routine2));

        Assert.That(updatedRoutineAfterDisposal, Is.SameAs(routine2));

        Assert.That(ex.ObjectName, Is.EqualTo("my-job"));
    }
}