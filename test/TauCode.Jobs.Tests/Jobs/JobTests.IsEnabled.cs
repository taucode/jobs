using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;
using TauCode.Jobs.Exceptions;
using TauCode.Jobs.Schedules;

namespace TauCode.Jobs.Tests.Jobs
{
    [TestFixture]
    public partial class JobTests
    {
        [Test]
        public void IsEnabled_JustCreatedJob_ReturnsFalse()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);
            var job = jobManager.Create("my-job");

            // Act
            var isEnabled = job.IsEnabled;

            // Assert
            Assert.That(isEnabled, Is.False);
        }

        /// <summary>
        /// 0---------1---------2---------3---------4-------
        ///           |_________|
        /// __________!1__!2________________________________  !1 - started via schedule
        ///                                                   !2 - disabled
        /// _________________^A__________________________^B_  ^A - continues running
        ///                                                   ^B - never starts again
        /// </summary>
        [Test]
        public async Task IsEnabled_SetToFalseDuringRun_RunCompletesThenDoesNotStart()
        {
            // Arrange
            const double runLength = 1.0;

            const double t1 = 1.0;
            const double t2 = 1.3;
            const double tA = 1.7;
            const double tB = 4.5;

            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            job.IsEnabled = true;

            job.Schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

            job.Routine = async (parameter, tracker, output, token) =>
            {
                await output.WriteAsync("Hello!");
                await Task.Delay(TimeSpan.FromSeconds(runLength), token);
            };

            // Act
            await timeMachine.WaitUntilSecondsElapse(start, t2);
            job.IsEnabled = false;

            await timeMachine.WaitUntilSecondsElapse(start, tA);
            var infoA = job.GetInfo(null);

            await timeMachine.WaitUntilSecondsElapse(start, tB);
            var infoB = job.GetInfo(null);

            job.Dispose();

            // Assert

            var DEFECT = TimeSpan.FromMilliseconds(30);

            #region ^A

            Assert.That(infoA.CurrentRun, Is.Not.Null);
            Assert.That(infoA.NextDueTime, Is.EqualTo(start.AddSeconds(2.0)));
            Assert.That(infoA.NextDueTimeIsOverridden, Is.False);
            Assert.That(infoA.RunCount, Is.Zero);
            Assert.That(infoA.Runs, Is.Empty);

            var currentA = infoA.CurrentRun.Value;
            Assert.That(currentA.RunIndex, Is.EqualTo(0));
            Assert.That(currentA.StartReason, Is.EqualTo(JobStartReason.ScheduleDueTime));
            Assert.That(currentA.DueTime, Is.EqualTo(start.AddSeconds(t1)));
            Assert.That(currentA.DueTimeWasOverridden, Is.False);
            Assert.That(currentA.StartTime, Is.EqualTo(start.AddSeconds(t1)).Within(DEFECT));
            Assert.That(currentA.EndTime, Is.Null);
            Assert.That(currentA.Status, Is.EqualTo(JobRunStatus.Running));

            #endregion

            #region ^B

            Assert.That(infoB.CurrentRun, Is.Null);
            Assert.That(infoB.NextDueTime, Is.EqualTo(start.AddSeconds(5.0)));
            Assert.That(infoB.NextDueTimeIsOverridden, Is.False);
            Assert.That(infoB.RunCount, Is.EqualTo(1));
            Assert.That(infoB.Runs, Has.Count.EqualTo(1));

            var runB0 = infoB.Runs.Single();
            Assert.That(runB0.RunIndex, Is.EqualTo(0));
            Assert.That(runB0.StartReason, Is.EqualTo(JobStartReason.ScheduleDueTime));
            Assert.That(runB0.DueTime, Is.EqualTo(start.AddSeconds(t1)));
            Assert.That(runB0.DueTimeWasOverridden, Is.False);
            Assert.That(runB0.StartTime, Is.EqualTo(currentA.StartTime));
            Assert.That(runB0.EndTime, Is.EqualTo(runB0.StartTime.AddSeconds(runLength)).Within(DEFECT));
            Assert.That(runB0.Status, Is.EqualTo(JobRunStatus.Completed));

            #endregion
        }

        [Test]
        public void IsEnabled_ChangedToTrue_ReturnsTrue()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);
            var job = jobManager.Create("my-job");
            job.IsEnabled = true;

            // Act
            var isEnabled = job.IsEnabled;

            // Assert
            Assert.That(isEnabled, Is.True);
        }

        [Test]
        public void IsEnabled_ChangedToTrueThenToFalse_ReturnsFalse()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var job = jobManager.Create("my-job");
            job.IsEnabled = true;
            job.IsEnabled = false;

            // Act
            var isEnabled = job.IsEnabled;

            // Assert
            Assert.That(isEnabled, Is.False);
        }

        [Test]
        public void IsEnabled_WasTrueThenDisposed_ReturnsTrue()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var job = jobManager.Create("my-job");
            job.IsEnabled = true;
            job.Dispose();

            // Act
            var isEnabled = job.IsEnabled;

            // Assert
            Assert.That(isEnabled, Is.True);
        }

        /// <summary>
        /// 0---------1---------2---------3---------4--------
        ///                 |.no.run.|    
        /// _______!1___!2__!3_______________________________ !1 - due time is overridden
        ///                                                   !2 - disabled
        ///                                                   !3 - overridden due time
        /// __________________^A________________________^B___ ^A - doesn't start, overridden due time discarded
        ///                                                   ^B - never starts
        /// </summary>
        [Test]
        public async Task IsEnabled_SetToFalseBeforeOverriddenDueTime_DoesNotStartThenOverriddenDueTimeGetsDiscarded()
        {
            // Arrange
            const double runLength = 1.0;

            const double t1 = 0.8;
            const double t2 = 1.1;
            const double t3 = 1.5;
            const double tA = 1.9;
            const double tB = 4.5;

            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            job.IsEnabled = true;

            job.Schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

            job.Routine = async (parameter, tracker, output, token) =>
            {
                await output.WriteAsync("Hello!");
                await Task.Delay(TimeSpan.FromSeconds(runLength), token);
            };

            // Act
            await timeMachine.WaitUntilSecondsElapse(start, t1);
            job.OverrideDueTime(start.AddSeconds(t3));

            await timeMachine.WaitUntilSecondsElapse(start, t2);
            job.IsEnabled = false;

            await timeMachine.WaitUntilSecondsElapse(start, tA);
            var infoA = job.GetInfo(null);

            await timeMachine.WaitUntilSecondsElapse(start, tB);
            var infoB = job.GetInfo(null);

            job.Dispose();

            // Assert

            #region ^A

            Assert.That(infoA.CurrentRun, Is.Null);
            Assert.That(infoA.NextDueTime, Is.EqualTo(start.AddSeconds(2.0)));
            Assert.That(infoA.NextDueTimeIsOverridden, Is.False);
            Assert.That(infoA.RunCount, Is.Zero);
            Assert.That(infoA.Runs, Is.Empty);

            #endregion

            #region ^B

            Assert.That(infoB.CurrentRun, Is.Null);
            Assert.That(infoB.NextDueTime, Is.EqualTo(start.AddSeconds(5.0)));
            Assert.That(infoB.NextDueTimeIsOverridden, Is.False);
            Assert.That(infoB.RunCount, Is.Zero);
            Assert.That(infoB.Runs, Is.Empty);

            #endregion
        }

        [Test]
        public void IsEnabled_WasFalseThenDisposed_ReturnsFalse()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var job = jobManager.Create("my-job");
            job.Dispose();

            // Act
            var isEnabled = job.IsEnabled;

            // Assert
            Assert.That(isEnabled, Is.False);
        }

        [Test]
        public void IsEnabled_IsDisposed_ThrowsJobObjectDisposedException()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var job = jobManager.Create("my-job");
            job.Dispose();

            // Act
            var ex = Assert.Throws<JobObjectDisposedException>(() => job.IsEnabled = true);

            // Assert
            Assert.That(ex, Has.Message.EqualTo("'my-job' is disposed."));
            Assert.That(ex.ObjectName, Is.EqualTo("my-job"));
        }
    }
}
