using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;

namespace TauCode.Jobs.Tests.Jobs
{
    [TestFixture]
    public partial class JobTests
    {
        [Test]
        public void Dispose_NotRunning_Disposes()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");
            job.IsEnabled = true;

            // Act
            job.Dispose();

            // Assert
            Assert.That(job.IsDisposed, Is.True);
            var names = jobManager.GetNames();
            Assert.That(names, Is.Empty);

            var info = job.GetInfo(null);

            Assert.That(info.CurrentRun, Is.Null);
            Assert.That(info.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
            Assert.That(info.NextDueTimeIsOverridden, Is.False);
            Assert.That(info.RunCount, Is.Zero);
            Assert.That(info.Runs, Is.Empty);
        }

        [Test]
        public async Task Dispose_WasRunning_Disposes()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");
            job.IsEnabled = true;

            job.Routine = async (parameter, tracker, output, token) =>
            {
                await output.WriteAsync("Hello!");
                await Task.Delay(TimeSpan.FromHours(1), token);
            };

            // Act
            job.ForceStart();

            await Task.Delay(50);
            job.Dispose();
            await Task.Delay(50);

            // Assert
            Assert.That(job.IsDisposed, Is.True);
            var names = jobManager.GetNames();
            Assert.That(names, Is.Empty);

            var info = job.GetInfo(null);

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
        public void Dispose_WasDisposedAlready_ChangesNothing()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");
            job.IsEnabled = true;

            job.Routine = async (parameter, tracker, output, token) =>
            {
                await output.WriteAsync("Hello!");
                await Task.Delay(TimeSpan.FromHours(1), token);
            };

            // Act
            job.Dispose();
            job.Dispose();

            // Assert
            Assert.That(job.IsDisposed, Is.True);
            var names = jobManager.GetNames();
            Assert.That(names, Is.Empty);

            var info = job.GetInfo(null);

            Assert.That(info.CurrentRun, Is.Null);
            Assert.That(info.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
            Assert.That(info.NextDueTimeIsOverridden, Is.False);
            Assert.That(info.RunCount, Is.Zero);
            Assert.That(info.Runs, Is.Empty);
        }
    }
}
