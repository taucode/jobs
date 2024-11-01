﻿using NUnit.Framework;
using Serilog;
using System.Text;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;
using TauCode.IO;
using TauCode.Jobs.Schedules;
using TauCode.Working.Slavery;

using TimeProvider = TauCode.Infrastructure.Time.TimeProvider;

namespace TauCode.Jobs.Tests.Jobs;

#pragma warning disable NUnit1032

[TestFixture]
public class JobManagerTests
{
    private ILogger _logger = null!;
    private StringWriterWithEncoding _writer = null!;

    private string CurrentLog => _logger.ToString()!;

    [SetUp]
    public void SetUp()
    {
        TimeProvider.Reset();

        _writer = new StringWriterWithEncoding(Encoding.UTF8);
        _logger = new LoggerConfiguration()
            .WriteTo.TextWriter(
                _writer,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}]{ObjectTag} {Message}{NewLine}{Exception}"
            )
            .MinimumLevel.Verbose()
            .CreateLogger();
        Log.Logger = _logger;
    }

    #region JobManager.ctor

    [Test]
    public void Constructor_NoArguments_CreatesInstance()
    {
        // Arrange

        // Act
        using IJobManager jobManager = new JobManager(_logger);

        // Assert
        Assert.That(jobManager.State, Is.EqualTo(SlaveState.Stopped));
        Assert.That(jobManager.IsDisposed, Is.False);

        jobManager.Dispose();
    }

    #endregion

    #region IJobManager.Start

    [Test]
    public void Start_NotStarted_Starts()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);

        // Act
        jobManager.Start();

        // Assert
        Assert.That(jobManager.State, Is.EqualTo(SlaveState.Running));
        Assert.That(jobManager.IsDisposed, Is.False);
        Assert.That(jobManager.GetJobNames(), Has.Count.Zero);

        jobManager.Dispose();
    }

    [Test]
    public void Start_AlreadyStarted_ThrowsInvalidJobOperationException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Name = "Mgr";
        jobManager.Start();

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => jobManager.Start())!;

        // Assert
        Assert.That(ex.Message, Is.EqualTo("Cannot perform operation 'Start'. Slave state is 'Running'. Slave name is 'Mgr'."));
        jobManager.Dispose();
    }

    [Test]
    public void Start_AlreadyDisposed_ThrowsException()
    {
        // Arrange
        using var jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Dispose();

        // Act
        var ex = Assert.Throws<ObjectDisposedException>(() => jobManager.Start());

        // Assert
        Assert.That(ex, Has.Message.StartWith("Cannot access a disposed object."));
        Assert.That(ex.ObjectName, Is.EqualTo(typeof(JobManager).FullName));

        jobManager.Dispose();
    }

    #endregion

    #region IJobManager.IsRunning

    [Test]
    public void IsRunning_NotStarted_ReturnsFalse()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);

        // Act
        var isRunning = jobManager.State == SlaveState.Running;

        // Assert
        Assert.That(isRunning, Is.False);
    }

    [Test]
    public void IsRunning_Started_ReturnsTrue()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Start();

        // Act
        var isRunning = jobManager.State == SlaveState.Running;

        // Assert
        Assert.That(isRunning, Is.True);

        jobManager.Dispose(); // otherwise
    }

    [Test]
    public void IsRunning_NotStartedThenDisposed_ReturnsFalse()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Dispose();

        // Act
        var isRunning = jobManager.State == SlaveState.Running;

        // Assert
        Assert.That(isRunning, Is.False);
    }

    [Test]
    public void IsRunning_StartedThenDisposed_ReturnsFalse()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Start();
        jobManager.Dispose();

        // Act
        var isRunning = jobManager.State == SlaveState.Running;

        // Assert
        Assert.That(isRunning, Is.False);
    }

    #endregion

    #region IJobManager.IsDisposed

    [Test]
    public void IsDisposed_NotStarted_ReturnsFalse()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);

        // Act
        var isDisposed = jobManager.IsDisposed;

        // Assert
        Assert.That(isDisposed, Is.False);
    }

    [Test]
    public void IsDisposed_Started_ReturnsFalse()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Start();

        // Act
        var isDisposed = jobManager.IsDisposed;

        // Assert
        Assert.That(isDisposed, Is.False);
        jobManager.Dispose();
    }

    [Test]
    public void IsDisposed_NotStartedThenDisposed_ReturnsTrue()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Dispose();

        // Act
        var isDisposed = jobManager.IsDisposed;

        // Assert
        Assert.That(isDisposed, Is.True);
    }

    [Test]
    public void IsDisposed_StartedThenDisposed_ReturnsTrue()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Start();
        jobManager.Dispose();

        // Act
        var isDisposed = jobManager.IsDisposed;

        // Assert
        Assert.That(isDisposed, Is.True);
    }

    #endregion

    #region IJobManager.Create

    [Test]
    public void Create_NotStarted_ThrowsInvalidJobOperationException()
    {
        // Arrange
        using var jobManager = TestHelper.CreateJobManager(false, _logger);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => jobManager.CreateJob("job1"));

        // Assert
        Assert.That(ex.Message, Is.EqualTo("Cannot perform operation 'CreateJob'. Job Manager is not running."));
    }

    [Test]
    public void Create_Started_ReturnsJob()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Start();

        // Act
        var job = jobManager.CreateJob("job1");

        // Assert
        Assert.That(job.Name, Is.EqualTo("job1"));

        var now = TimeProvider.GetCurrentTime();
        Assert.That(job.Schedule.GetDueTimeAfter(now), Is.EqualTo(JobExtensions.Never));
        Assert.That(job.Routine, Is.Not.Null);
        Assert.That(job.Parameter, Is.Null);
        Assert.That(job.ProgressTracker, Is.Null);
        Assert.That(job.Output, Is.Null);

    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Create_BadJobName_ThrowsArgumentException(string? badJobName)
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => jobManager.CreateJob(badJobName));

        // Assert
        Assert.That(ex.Message, Does.StartWith("Job name cannot be null or empty."));
        Assert.That(ex.ParamName, Is.EqualTo("jobName"));
    }

    [Test]
    public void Create_NameAlreadyExists_ThrowsInvalidJobOperationException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);
        var name = "job1";
        jobManager.CreateJob(name);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => jobManager.CreateJob(name));

        // Assert
        Assert.That(ex.Message, Is.EqualTo($"Job '{name}' already exists."));
    }

    [Test]
    public void Create_Disposed_ThrowsJobObjectIsDisposedException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Dispose();

        // Act
        var ex = Assert.Throws<ObjectDisposedException>(() => jobManager.CreateJob("job1"));

        // Assert
        Assert.That(ex, Has.Message.StartWith("Cannot access a disposed object."));
        Assert.That(ex.ObjectName, Is.EqualTo(typeof(JobManager).FullName));
    }

    #endregion

    #region IJobManager.GetNames

    [Test]
    public void GetNames_NotStarted_ThrowsInvalidJobOperationException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => jobManager.GetJobNames());

        // Assert
        Assert.That(ex.Message, Is.EqualTo("Cannot perform operation 'GetJobNames'. Job Manager is not running."));
    }

    [Test]
    public void GetNames_Started_ReturnsJobNames()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Start();

        jobManager.CreateJob("job1");
        jobManager.CreateJob("job2");

        // Act
        var jobNames = jobManager.GetJobNames();

        // Assert
        Assert.That(jobNames, Is.EquivalentTo(new string[] { "job1", "job2" }));
    }

    [Test]
    public void GetNames_Disposed_ThrowsJobObjectIsDisposedException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);
        jobManager.CreateJob("job1");
        jobManager.CreateJob("job2");
        jobManager.Dispose();

        // Act
        var ex = Assert.Throws<ObjectDisposedException>(() => jobManager.GetJobNames());

        // Assert
        Assert.That(ex, Has.Message.StartWith("Cannot access a disposed object."));
        Assert.That(ex.ObjectName, Is.EqualTo(typeof(JobManager).FullName));
    }

    #endregion

    #region IJobManager.Get

    [Test]
    public void Get_NotStarted_ThrowsInvalidJobOperationException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => jobManager.GetJob("my-job"));

        // Assert
        Assert.That(ex.Message, Is.EqualTo("Cannot perform operation 'GetJob'. Job Manager is not running."));
    }

    [Test]
    public void Get_Started_ReturnsJob()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Start();
        var job1 = jobManager.CreateJob("job1");
        var job2 = jobManager.CreateJob("job2");

        // Act
        var gotJob1 = jobManager.GetJob("job1");

        // Assert
        Assert.That(gotJob1, Is.SameAs(job1));
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Get_BadJobName_ThrowsArgumentException(string? badJobName)
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => jobManager.GetJob(badJobName));

        // Assert
        Assert.That(ex.Message, Does.StartWith("Job name cannot be null or empty."));
        Assert.That(ex.ParamName, Is.EqualTo("jobName"));
    }

    [Test]
    public void Get_NonExistingJobName_ThrowsInvalidJobOperationException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        jobManager.CreateJob("job1");
        jobManager.CreateJob("job2");

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => jobManager.GetJob("non-existing"));

        // Assert
        Assert.That(ex.Message, Is.EqualTo("Job not found: 'non-existing'."));
    }

    [Test]
    public void Get_Disposed_ThrowsJobObjectDisposedException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        jobManager.CreateJob("job1");
        jobManager.CreateJob("job2");
        jobManager.Dispose();

        // Act
        var ex = Assert.Throws<ObjectDisposedException>(() => jobManager.GetJob("job1"));

        // Assert
        Assert.That(ex, Has.Message.StartWith("Cannot access a disposed object."));
        Assert.That(ex.ObjectName, Is.EqualTo(typeof(JobManager).FullName));
    }

    #endregion

    #region IJobManager.Dispose

    [Test]
    public void Dispose_NotStarted_Disposes()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);

        // Act
        jobManager.Dispose();

        // Assert
        Assert.That(jobManager.IsDisposed, Is.True);
    }

    [Test]
    public void Dispose_Started_Disposes()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Start();

        // Act
        jobManager.Dispose();

        // Assert
        Assert.That(jobManager.IsDisposed, Is.True);
    }

    [Test]
    public void Dispose_AlreadyDisposed_RunsOk()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(false, _logger);
        jobManager.Dispose();

        // Act
        jobManager.Dispose();

        // Assert
        Assert.That(jobManager.IsDisposed, Is.True);
    }

    [Test]
    public async Task Dispose_JobsCreated_DisposesAndJobsAreCanceledAndDisposed()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2020-01-01Z".ToUtcDateOffset();
        TimeProvider.Override(ShiftedTimeProvider.CreateTimeMachine(start));


        var job1 = jobManager.CreateJob("job1");
        job1.IsEnabled = true;

        var job2 = jobManager.CreateJob("job2");
        job2.IsEnabled = true;

        job1.Output = new StringWriterWithEncoding(Encoding.UTF8);
        job2.Output = new StringWriterWithEncoding(Encoding.UTF8);

        async Task Routine(object parameter, IProgressTracker tracker, TextWriter output, CancellationToken token)
        {
            for (var i = 0; i < 100; i++)
            {
                var time = TimeProvider.GetCurrentTime();
                await output.WriteLineAsync($"Iteration {i}: {time.Second:D2}:{time.Millisecond:D3}");

                try
                {
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException)
                {
                    time = TimeProvider.GetCurrentTime();
                    await output.WriteLineAsync($"Canceled! {time.Second:D2}:{time.Millisecond:D3}");
                    throw;
                }
            }
        }

        ISchedule schedule = new SimpleSchedule(
            SimpleScheduleKind.Second,
            1,
            start.AddMilliseconds(400));

        job1.Schedule = schedule;
        job2.Schedule = schedule;

        job1.Routine = Routine;
        job2.Routine = Routine;

        job1.IsEnabled = true;
        job2.IsEnabled = true;

        await Task.Delay(2500); // 3 iterations should be completed: ~400, ~1400, ~2400 todo: ut this

        // Act

        jobManager.Dispose();
        await Task.Delay(50); // let background TPL work get done.

        // Assert
        Assert.That(jobManager.IsDisposed, Is.True);

        foreach (var job in new[] { job1, job2 })
        {
            Assert.That(job.IsDisposed, Is.True);
            var info = job.GetInfo(null);
            var run = info.Runs.Single();
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Canceled));
        }
    }

    #endregion
}