using System.Threading.Tasks;
using NUnit.Framework;

namespace TauCode.Jobs.Tests.Jobs
{
    [TestFixture]
    public partial class JobTests
    {
        [Test]
        public void ProgressTracker_JustCreated_EqualsToNull()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDayOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            // Act
            var progressTracker = job.ProgressTracker;

            // Assert
            Assert.That(progressTracker, Is.Null);
        }

        [Test]
        public void ProgressTracker_ValueIsSet_EqualsToThatValue()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDayOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            // Act
            IProgressTracker tracker1 = new MockProgressTracker();
            job.ProgressTracker = tracker1;
            var readTracker1 = job.ProgressTracker;

            job.IsEnabled = true;
            IProgressTracker tracker2 = new MockProgressTracker();
            job.ProgressTracker = tracker2;
            var readTracker2 = job.ProgressTracker;

            job.IsEnabled = false;
            IProgressTracker tracker3 = null;
            job.ProgressTracker = tracker3;
            var readTracker3 = job.ProgressTracker;

            // Assert
            Assert.That(tracker1, Is.EqualTo(readTracker1));
            Assert.That(tracker2, Is.EqualTo(readTracker2));
            Assert.That(tracker3, Is.EqualTo(readTracker3));
        }

        /// <summary>
        /// 0---------1---------2---------3---------4---------5---------
        ///           |___~1s____|        |___~1s____|               
        /// _____!1______!2_____________________________________________ (!1 - tracker1, !2 - tracker2)
        /// </summary>
        [Test]
        public async Task ProgressTracker_SetOnTheFly_RunsWithOldParameterAndNextTimeRunsWithNewProgressTracker()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDayOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            ISchedule schedule = new ConcreteSchedule(
                start.AddSeconds(1),
                start.AddSeconds(3));

            job.Schedule = schedule;

            var tracker1 = new MockProgressTracker();
            var tracker2 = new MockProgressTracker();

            job.Routine = async (parameter, tracker, writer, token) =>
            {
                for (int i = 0; i < 5; i++)
                {
                    tracker.UpdateProgress((decimal)i * 20, null);
                }

                await Task.Delay(200, token);
            };

            job.IsEnabled = true;

            // Act
            await timeMachine.WaitUntilSecondsElapse(start, 0.8);
            job.ProgressTracker = tracker1;

            await timeMachine.WaitUntilSecondsElapse(start, 1.3);
            job.ProgressTracker = tracker2;

            await timeMachine.WaitUntilSecondsElapse(start, 4.8);

            var info = job.GetInfo(null);

            // Assert
            Assert.That(info.CurrentRun, Is.Null);
            Assert.That(info.RunCount, Is.EqualTo(2));
            Assert.That(info.Runs, Has.Count.EqualTo(2));

            CollectionAssert.AreEquivalent(new decimal[] { 0m, 20m, 40m, 60m, 80m }, tracker1.GetList());
            CollectionAssert.AreEquivalent(new decimal[] { 0m, 20m, 40m, 60m, 80m }, tracker2.GetList());
        }

        /// <summary>
        /// 0---------1---------2---------3---------4---------5---------
        ///           |___~1s____|        |___~1s____|               
        /// _____!1___________________!2________________________________ (!1 - tracker1, !2 - tracker2)
        /// </summary>
        [Test]
        public async Task ProgressTracker_SetAfterFirstRun_NextTimeRunsWithNewProgressTracker()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDayOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            ISchedule schedule = new ConcreteSchedule(
                start.AddSeconds(1),
                start.AddSeconds(3));

            job.Schedule = schedule;

            var tracker1 = new MockProgressTracker();
            var tracker2 = new MockProgressTracker();

            job.Routine = async (parameter, tracker, writer, token) =>
            {
                for (int i = 0; i < 5; i++)
                {
                    tracker.UpdateProgress((decimal)i * 20, null);
                }

                await Task.Delay(200, token);
            };

            job.IsEnabled = true;

            // Act
            await timeMachine.WaitUntilSecondsElapse(start, 0.8);
            job.ProgressTracker = tracker1;

            await timeMachine.WaitUntilSecondsElapse(start, 2.7);
            job.ProgressTracker = tracker2;

            await timeMachine.WaitUntilSecondsElapse(start, 4.8);

            var info = job.GetInfo(null);

            // Assert
            Assert.That(info.CurrentRun, Is.Null);
            Assert.That(info.RunCount, Is.EqualTo(2));
            Assert.That(info.Runs, Has.Count.EqualTo(2));

            CollectionAssert.AreEquivalent(new decimal[] { 0m, 20m, 40m, 60m, 80m }, tracker1.GetList());
            CollectionAssert.AreEquivalent(new decimal[] { 0m, 20m, 40m, 60m, 80m }, tracker2.GetList());
        }

        [Test]
        public void ProgressTracker_JobIsDisposed_CanBeRead()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDayOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            // Act
            var progressTracker = new MockProgressTracker();
            job.ProgressTracker = progressTracker;
            job.Dispose();

            // Assert
            Assert.That(job.ProgressTracker, Is.EqualTo(progressTracker));
        }

        [Test]
        public void ProgressTracker_JobIsDisposedThenValueIsSet_ThrowsJobObjectDisposedException()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDayOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            var tracker1 = new MockProgressTracker();
            var tracker2 = new MockProgressTracker();

            // Act
            job.ProgressTracker = tracker1;
            job.Dispose();
            var ex = Assert.Throws<JobObjectDisposedException>(() => job.ProgressTracker = tracker2);

            // Assert
            Assert.That(job.ProgressTracker, Is.EqualTo(tracker1));
            Assert.That(ex.ObjectName, Is.EqualTo("my-job"));
        }
    }
}
