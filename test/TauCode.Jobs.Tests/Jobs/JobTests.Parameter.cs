using NUnit.Framework;
using System;
using System.Text;
using System.Threading.Tasks;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;
using TauCode.IO;
using TauCode.Jobs.Schedules;

namespace TauCode.Jobs.Tests.Jobs
{
    [TestFixture]
    public partial class JobTests
    {
        [Test]
        public void Parameter_JustCreated_EqualsToNull()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            // Act
            var parameter = job.Parameter;

            // Assert
            Assert.That(parameter, Is.Null);
        }

        [Test]
        public void Parameter_ValueIsSet_EqualsToThatValue()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            // Act
            object parameter1 = 1;
            job.Parameter = parameter1;
            var readParameter1 = job.Parameter;

            job.IsEnabled = true;
            object parameter2 = "hello";
            job.Parameter = parameter2;
            var readParameter2 = job.Parameter;

            job.IsEnabled = false;
            object parameter3 = null;
            job.Parameter = parameter3;
            var readParameter3 = job.Parameter;

            // Assert
            Assert.That(parameter1, Is.EqualTo(readParameter1));
            Assert.That(parameter2, Is.EqualTo(readParameter2));
            Assert.That(parameter3, Is.EqualTo(readParameter3));
        }

        /// <summary>
        /// 0---------1---------2---------3---------4---------5---------
        ///           |_____1.5s_____|    |_____1.5s_____|               
        /// _____!1_________!2__________________________________________ (!1 - parameter1, !2 - parameter2)
        /// </summary>
        [Test]
        public async Task Parameter_SetOnTheFly_RunsWithOldParameterAndNextTimeRunsWithNewParameter()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            ISchedule schedule = new ConcreteSchedule(
                start.AddSeconds(1),
                start.AddSeconds(3));

            job.Schedule = schedule;
            job.Output = new StringWriterWithEncoding(Encoding.UTF8);

            object parameter1 = "Olia";
            object parameter2 = "Ira";

            job.Routine = async (parameter, tracker, writer, token) =>
            {
                await Task.Delay(1500, token);
                await writer.WriteAsync($"Hello, {parameter}!");
            };

            job.IsEnabled = true;

            // Act
            await timeMachine.WaitUntilSecondsElapse(start, 0.8);
            job.Parameter = parameter1;

            await timeMachine.WaitUntilSecondsElapse(start, 1.3);
            job.Parameter = parameter2;

            await timeMachine.WaitUntilSecondsElapse(start, 2.8);
            var output0 = job.Output.ToString();

            await timeMachine.WaitUntilSecondsElapse(start, 4.8);
            var output1 = job.Output.ToString();

            var info = job.GetInfo(null);

            // Assert
            Assert.That(info.CurrentRun, Is.Null);
            Assert.That(info.RunCount, Is.EqualTo(2));
            Assert.That(info.Runs, Has.Count.EqualTo(2));

            var run0 = info.Runs[0];
            var run1 = info.Runs[1];

            Assert.That(run0.Output, Is.EqualTo("Hello, Olia!"));
            Assert.That(output0, Is.EqualTo("Hello, Olia!"));

            Assert.That(run1.Output, Is.EqualTo("Hello, Ira!"));
            Assert.That(output1, Is.EqualTo("Hello, Olia!Hello, Ira!"));
        }

        /// <summary>
        /// 0---------1---------2---------3---------4---------5---------
        ///           |_____1.5s_____|    |_____1.5s_____|               
        /// _____!1_____________________!2______________________________ (!1 - parameter1, !2 - parameter2)
        /// </summary>
        [Test]
        public async Task Parameter_SetAfterFirstRun_NextTimeRunsWithNewParameter()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            ISchedule schedule = new ConcreteSchedule(
                start.AddSeconds(1),
                start.AddSeconds(3));

            job.Schedule = schedule;
            job.Output = new StringWriterWithEncoding(Encoding.UTF8);

            object parameter1 = "Olia";
            object parameter2 = "Ira";

            job.Routine = async (parameter, tracker, writer, token) =>
            {
                await Task.Delay(1500, token);
                await writer.WriteAsync($"Hello, {parameter}!");
            };

            job.IsEnabled = true;

            // Act
            await timeMachine.WaitUntilSecondsElapse(start, 0.8);
            job.Parameter = parameter1;

            await timeMachine.WaitUntilSecondsElapse(start, 2.8);
            var output0 = job.Output.ToString();

            job.Parameter = parameter2;

            await timeMachine.WaitUntilSecondsElapse(start, 4.8);
            var output1 = job.Output.ToString();

            var info = job.GetInfo(null);

            // Assert
            Assert.That(info.CurrentRun, Is.Null);
            Assert.That(info.RunCount, Is.EqualTo(2));
            Assert.That(info.Runs, Has.Count.EqualTo(2));

            var run0 = info.Runs[0];
            var run1 = info.Runs[1];

            Assert.That(run0.Output, Is.EqualTo("Hello, Olia!"));
            Assert.That(output0, Is.EqualTo("Hello, Olia!"));

            Assert.That(run1.Output, Is.EqualTo("Hello, Ira!"));
            Assert.That(output1, Is.EqualTo("Hello, Olia!Hello, Ira!"));
        }

        [Test]
        public void Parameter_JobIsDisposed_CanBeRead()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            // Act
            job.Parameter = 17;
            job.Dispose();


            // Assert
            Assert.That(job.Parameter, Is.EqualTo(17));
        }

        [Test]
        public void Parameter_JobIsDisposedThenValueIsSet_ThrowsJobObjectDisposedException()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            // Act
            job.Parameter = 17;
            job.Dispose();
            var ex = Assert.Throws<ObjectDisposedException>(() => job.Parameter = 101);

            // Assert
            Assert.That(job.Parameter, Is.EqualTo(17));
            Assert.That(ex.ObjectName, Is.EqualTo("my-job"));
        }
    }
}
