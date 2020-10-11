using NUnit.Framework;
using System;
using System.IO;
using System.Text;
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
        public void Output_JustCreated_EqualsToNull()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            // Act
            var output = job.Output;

            // Assert
            Assert.That(output, Is.Null);
        }

        [Test]
        public void Output_ValueIsSet_EqualsToThatValue()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            // Act
            TextWriter writer1 = new StringWriterWithEncoding(Encoding.UTF8);
            job.Output = writer1;
            var readOutput1 = job.Output;

            job.IsEnabled = true;
            TextWriter writer2 = new StringWriterWithEncoding(Encoding.UTF8);
            job.Output = writer2;
            var readOutput2 = job.Output;

            job.IsEnabled = false;
            TextWriter writer3 = null;
            job.Output = writer3;
            var readOutput3 = job.Output;

            // Assert
            Assert.That(writer1, Is.EqualTo(readOutput1));
            Assert.That(writer2, Is.EqualTo(readOutput2));
            Assert.That(writer3, Is.EqualTo(readOutput3));
        }

        /// <summary>
        /// 0---------1---------2---------3---------4---------5---------
        ///           |___~1s____|        |___~1s____|               
        /// _____!1______!2_____________________________________________ (!1 - writer1, !2 - writer2)
        /// </summary>
        [Test]
        public async Task Output_SetOnTheFly_RunsWithOldParameterAndNextTimeRunsWithNewOutput()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            ISchedule schedule = new ConcreteSchedule(
                start.AddSeconds(1),
                start.AddSeconds(3));

            job.Schedule = schedule;

            var writer1 = new StringWriterWithEncoding(Encoding.UTF8);
            var writer2 = new StringWriterWithEncoding(Encoding.UTF8);

            job.Routine = async (parameter, tracker, writer, token) =>
            {
                for (var i = 0; i < 5; i++)
                {
                    await writer.WriteAsync(i.ToString());
                }

                await Task.Delay(200, token);
            };

            job.IsEnabled = true;

            // Act
            var inTime = await timeMachine.WaitUntilSecondsElapse(start, 0.8);
            if (!inTime)
            {
                throw new Exception("Test failed. TPL was too slow.");
            }

            job.Output = writer1;

            await timeMachine.WaitUntilSecondsElapse(start, 1.3);
            job.Output = writer2;

            await timeMachine.WaitUntilSecondsElapse(start, 4.8);

            var info = job.GetInfo(null);

            // Assert
            try
            {
                Assert.That(info.CurrentRun, Is.Null);

                Assert.That(info.RunCount, Is.EqualTo(2));
                Assert.That(info.Runs, Has.Count.EqualTo(2));

                Assert.That(writer1.ToString(), Is.EqualTo("01234"));
                Assert.That(writer2.ToString(), Is.EqualTo("01234"));
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();
                sb.AppendLine("*** Test Failed ***");
                sb.AppendLine(ex.ToString());
                sb.AppendLine("*** Log: ***");

                var log = _logWriter.ToString();

                sb.AppendLine(log);

                Assert.Fail(sb.ToString());
            }
        }

        /// <summary>
        /// 0---------1---------2---------3---------4---------5---------
        ///           |___~1s____|        |___~1s____|               
        /// _____!1___________________!2________________________________ (!1 - writer1, !2 - writer2)
        /// </summary>
        [Test]
        public async Task Output_SetAfterFirstRun_NextTimeRunsWithNewOutput()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            ISchedule schedule = new ConcreteSchedule(
                start.AddSeconds(1),
                start.AddSeconds(3));

            job.Schedule = schedule;

            var writer1 = new StringWriterWithEncoding(Encoding.UTF8);
            var writer2 = new StringWriterWithEncoding(Encoding.UTF8);

            job.Routine = async (parameter, tracker, writer, token) =>
            {
                for (var i = 0; i < 5; i++)
                {
                    await writer.WriteAsync(i.ToString());
                }

                await Task.Delay(200, token);
            };

            job.IsEnabled = true;

            // Act
            await timeMachine.WaitUntilSecondsElapse(start, 0.8);
            job.Output = writer1;

            await timeMachine.WaitUntilSecondsElapse(start, 2.7);
            job.Output = writer2;

            await timeMachine.WaitUntilSecondsElapse(start, 4.8);

            var info = job.GetInfo(null);

            // Assert
            Assert.That(info.CurrentRun, Is.Null);

            Assert.That(info.RunCount, Is.EqualTo(2));
            Assert.That(info.Runs, Has.Count.EqualTo(2));

            Assert.That(writer1.ToString(), Is.EqualTo("01234"));
            Assert.That(writer2.ToString(), Is.EqualTo("01234"));
        }

        [Test]
        public void Output_JobIsDisposed_CanBeRead()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            // Act
            var writer = new StringWriterWithEncoding(Encoding.UTF8);
            job.Output = writer;
            job.Dispose();

            // Assert
            Assert.That(job.Output, Is.EqualTo(writer));
        }

        [Test]
        public void Output_JobIsDisposedThenValueIsSet_ThrowsJobObjectDisposedException()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var start = "2000-01-01Z".ToUtcDateOffset();
            var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
            TimeProvider.Override(timeMachine);

            var job = jobManager.Create("my-job");

            var writer1 = new StringWriterWithEncoding(Encoding.UTF8);
            var writer2 = new StringWriterWithEncoding(Encoding.UTF8);

            // Act
            job.Output = writer1;
            job.Dispose();
            var ex = Assert.Throws<JobObjectDisposedException>(() => job.Output = writer2);

            // Assert
            Assert.That(job.Output, Is.EqualTo(writer1));
            Assert.That(ex.ObjectName, Is.EqualTo("my-job"));
        }
    }
}
