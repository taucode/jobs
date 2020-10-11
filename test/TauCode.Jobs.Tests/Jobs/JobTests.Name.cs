using System.Threading.Tasks;
using NUnit.Framework;

namespace TauCode.Jobs.Tests.Jobs
{
    [TestFixture]
    public partial class JobTests
    {
        [Test]
        public void Name_JustCreatedJob_ReturnsValidName()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var job = jobManager.Create("my-job");

            // Act
            var name = job.Name;

            // Assert
            Assert.That(name, Is.EqualTo("my-job"));
        }

        [Test]
        public void Name_JobIsEnabled_ReturnsValidName()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);
            var job = jobManager.Create("my-job");

            job.IsEnabled = true;

            // Act
            var name = job.Name;

            // Assert
            Assert.That(name, Is.EqualTo("my-job"));
        }

        [Test]
        public void Name_JobIsDisabled_ReturnsValidName()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);
            var job = jobManager.Create("my-job");

            job.IsEnabled = true;

            // Act
            var name1 = job.Name;

            job.IsEnabled = false;

            var name2 = job.Name;

            // Assert
            Assert.That(name1, Is.EqualTo("my-job"));
            Assert.That(name2, Is.EqualTo("my-job"));
        }

        [Test]
        public void Name_JobIsRunningOrStopped_ReturnsValidName()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var job = jobManager.Create("my-job");

            job.IsEnabled = true;

            job.Routine = async (parameter, tracker, output, token) => { await Task.Delay(5000, token); };

            job.ForceStart();

            // Act
            var nameWhenRunning = job.Name;
            job.Cancel();
            var nameAfterStopped = job.Name;

            // Assert
            Assert.That(nameWhenRunning, Is.EqualTo("my-job"));
            Assert.That(nameAfterStopped, Is.EqualTo("my-job"));
        }

        [Test]
        public void Name_JobIsDisposed_ReturnsValidName()
        {
            // Arrange
            using IJobManager jobManager = TestHelper.CreateJobManager(true);

            var job = jobManager.Create("my-job");
            job.Dispose();

            // Act
            var name = job.Name;

            // Assert
            Assert.That(name, Is.EqualTo("my-job"));
        }
    }
}
