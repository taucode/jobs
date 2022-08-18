using NUnit.Framework;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;
using TauCode.Jobs.Schedules;

namespace TauCode.Jobs.Tests.Jobs;

[TestFixture]
public partial class JobTests
{
    [Test]
    public void GetInfo_JustCreated_ReturnsValidResult()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        // Act
        var info = job.GetInfo(null);

        // Assert
        Assert.That(info.CurrentRun, Is.Null);
        Assert.That(info.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(info.NextDueTimeIsOverridden, Is.False);
        Assert.That(info.RunCount, Is.Zero);
        Assert.That(info.Runs, Is.Empty);
    }

    [Test]
    public async Task GetInfo_ArgumentIsNull_ReturnsAllRuns()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync(parameter.ToString());
        };

        for (var i = 0; i < 10; i++)
        {
            var copy = i;
            job.Parameter = copy;
            job.ForceStart();
            await Task.Delay(100); // let job finish.
        }

        // Act
        var info = job.GetInfo(null);

        // Assert
        Assert.That(info.CurrentRun, Is.Null);
        Assert.That(info.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(info.NextDueTimeIsOverridden, Is.False);
        Assert.That(info.RunCount, Is.EqualTo(10));
        Assert.That(info.Runs, Has.Count.EqualTo(10));

        var DEFECT = TimeSpan.FromMilliseconds(30);

        for (var i = 0; i < 10; i++)
        {
            var run = info.Runs[i];

            Assert.That(run.RunIndex, Is.EqualTo(i));
            Assert.That(run.StartReason, Is.EqualTo(JobStartReason.Force));
            Assert.That(run.DueTime, Is.EqualTo(TestHelper.NeverCopy));
            Assert.That(run.DueTimeWasOverridden, Is.False);
            Assert.That(run.StartTime, Is.InRange(start, start.AddSeconds(2)));
            Assert.That(run.EndTime, Is.InRange(run.StartTime, run.StartTime.Add(DEFECT)));
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Completed));
            Assert.That(run.Output, Is.EqualTo(i.ToString()));
            Assert.That(run.Exception, Is.Null);
        }
    }

    [Test]
    public void GetInfo_ArgumentIsNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => job.GetInfo(-1));

        // Assert
        Assert.That(ex.ParamName, Is.EqualTo("maxRunCount"));
    }

    [Test]
    public async Task GetInfo_ArgumentIsZero_ReturnsEmptyRuns()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync(parameter.ToString());
        };

        for (var i = 0; i < 10; i++)
        {
            var copy = i;
            job.Parameter = copy;
            job.ForceStart();
            await Task.Delay(100); // let job finish.
        }

        // Act
        var info = job.GetInfo(0);

        // Assert
        Assert.That(info.CurrentRun, Is.Null);
        Assert.That(info.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(info.NextDueTimeIsOverridden, Is.False);
        Assert.That(info.RunCount, Is.EqualTo(10));
        Assert.That(info.Runs, Is.Empty);
    }

    [Test]
    public async Task GetInfo_ArgumentIsVeryLarge_ReturnsAllRuns()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync(parameter.ToString());
        };

        for (var i = 0; i < 10; i++)
        {
            var copy = i;
            job.Parameter = copy;
            job.ForceStart();
            await Task.Delay(100); // let job finish.
        }

        // Act
        var info = job.GetInfo(int.MaxValue);

        // Assert
        Assert.That(info.CurrentRun, Is.Null);
        Assert.That(info.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(info.NextDueTimeIsOverridden, Is.False);
        Assert.That(info.RunCount, Is.EqualTo(10));
        Assert.That(info.Runs, Has.Count.EqualTo(10));

        var DEFECT = TimeSpan.FromMilliseconds(30);

        for (var i = 0; i < 10; i++)
        {
            var run = info.Runs[i];

            Assert.That(run.RunIndex, Is.EqualTo(i));
            Assert.That(run.StartReason, Is.EqualTo(JobStartReason.Force));
            Assert.That(run.DueTime, Is.EqualTo(TestHelper.NeverCopy));
            Assert.That(run.DueTimeWasOverridden, Is.False);
            Assert.That(run.StartTime, Is.InRange(start, start.AddSeconds(2)));
            Assert.That(run.EndTime, Is.InRange(run.StartTime, run.StartTime.Add(DEFECT)));
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Completed));
            Assert.That(run.Output, Is.EqualTo(i.ToString()));
            Assert.That(run.Exception, Is.Null);
        }
    }

    /// <summary>
    ///                     v         v         v         v                    (v - should start via schedule unless interfered)
    /// 0---------1---------2---------3---------4---------5---------6---------
    ///                |_~0.7s_|          |_~0.7s_|       |_~0.7s_|           
    /// _______________!1__________!2_____!3______________!4__________________ (!1 - force start, !2 - due time is overridden, !3 - overridden due time, !4 - starts by schedule)
    /// ___________^A____^B______^C____^D____^E________^F_____^G_______^H_____ (GetInfo during lifecycle:
    ///                                                                             ^A - before all runs
    ///                                                                             ^B - right after force start
    ///                                                                             ^C - after forced run completes, but before time override
    ///                                                                             ^D - after time override, but before overridden-due-time start
    ///                                                                             ^E - after overridden-due-time start
    ///                                                                             ^F - after overridden-due-time run completes, and before schedule-due-time start
    ///                                                                             ^G - after schedule-due-time start
    ///                                                                             ^H - after schedule-due-time run completes
    /// </summary>
    [Test]
    public async Task GetInfo_RunsSeveralTimes_ReturnsAllRuns()
    {
        // Arrange
        const double runTime = 0.7;

        const double t1 = 1.6; // !1 - force start
        const double t2 = 2.8; // !2 - due time is overridden
        const double t3 = 3.4; // !3 - overridden due time
        const double t4 = 5.0; // !4 - starts by schedule

        const double tA = 1.1; // ^A - before all runs
        const double tB = 1.9; // ^B - right after force start
        const double tC = 2.6; // ^C - after forced run completes, but before time override
        const double tD = 3.1; // ^D - after time override, but before overridden-due-time start
        const double tE = 3.9; // ^E - after overridden-due-time start
        const double tF = 4.8; // ^F - after overridden-due-time run completes, and before schedule-due-time start
        const double tG = 5.3; // ^G - after schedule-due-time start
        const double tH = 6.6; // ^H - after schedule-due-time run completes

        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            var msg = (string)parameter;
            await output.WriteAsync(msg);
            await Task.Delay(TimeSpan.FromSeconds(runTime), token);
        };

        ISchedule schedule = new ConcreteSchedule(
            start.AddSeconds(2),
            start.AddSeconds(3),
            start.AddSeconds(4),
            start.AddSeconds(5));

        job.Schedule = schedule;

        // Act

        // ^A - before all runs
        await timeMachine.WaitUntilSecondsElapse(start, tA);
        var infoA = job.GetInfo(null);

        // !1 - force start
        await timeMachine.WaitUntilSecondsElapse(start, t1);
        job.Parameter = "force";
        job.ForceStart();

        // ^B - right after force start
        await timeMachine.WaitUntilSecondsElapse(start, tB);
        var infoB = job.GetInfo(null);

        // ^C - after forced run completes, but before time override
        await timeMachine.WaitUntilSecondsElapse(start, tC);
        var infoC = job.GetInfo(null);

        // !2 - due time is overridden
        await timeMachine.WaitUntilSecondsElapse(start, t2);
        job.Parameter = "overridden";
        job.OverrideDueTime(start.AddSeconds(t3));

        // ^D - after time override, but before overridden-due-time start
        await timeMachine.WaitUntilSecondsElapse(start, tD);
        var infoD = job.GetInfo(null);

        // !3 - overridden due time
        await timeMachine.WaitUntilSecondsElapse(start, t3);

        // ^E - after overridden-due-time start
        await timeMachine.WaitUntilSecondsElapse(start, tE);
        var infoE = job.GetInfo(null);

        // ^F - after overridden-due-time run completes, and before schedule-due-time start
        await timeMachine.WaitUntilSecondsElapse(start, tF);
        var infoF = job.GetInfo(null);
        job.Parameter = "schedule";

        // !4 - starts by schedule
        await timeMachine.WaitUntilSecondsElapse(start, t4);

        // ^G - after schedule-due-time start
        await timeMachine.WaitUntilSecondsElapse(start, tG);
        var infoG = job.GetInfo(null);

        // ^H - after schedule-due-time run completes
        await timeMachine.WaitUntilSecondsElapse(start, tH);
        var infoH = job.GetInfo(null);

        // dispose
        jobManager.Dispose();
        var infoFinal = job.GetInfo(null);

        // Assert
        var DEFECT = TimeSpan.FromMilliseconds(30);


        #region ^A - before all runs

        Assert.That(infoA.CurrentRun, Is.Null);
        Assert.That(infoA.NextDueTime, Is.EqualTo(start.AddSeconds(2)));
        Assert.That(infoA.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoA.RunCount, Is.Zero);
        Assert.That(infoA.Runs, Is.Empty);

        #endregion

        #region ^B - right after force start

        Assert.That(infoB.CurrentRun, Is.Not.Null);
        Assert.That(infoB.NextDueTime, Is.EqualTo(start.AddSeconds(2)));
        Assert.That(infoB.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoB.RunCount, Is.Zero);
        Assert.That(infoB.Runs, Is.Empty);

        var currentB = infoB.CurrentRun.Value;
        Assert.That(currentB.RunIndex, Is.EqualTo(0));
        Assert.That(currentB.StartReason, Is.EqualTo(JobStartReason.Force));
        Assert.That(currentB.DueTime, Is.EqualTo(start.AddSeconds(2)));
        Assert.That(currentB.DueTimeWasOverridden, Is.False);
        Assert.That(currentB.StartTime, Is.EqualTo(start.AddSeconds(t1)).Within(DEFECT));
        Assert.That(currentB.EndTime, Is.Null);
        Assert.That(currentB.Status, Is.EqualTo(JobRunStatus.Running));
        Assert.That(currentB.Output, Is.EqualTo("force"));
        Assert.That(currentB.Exception, Is.Null);

        #endregion

        #region ^C - after forced run completes, but before time override

        Assert.That(infoC.CurrentRun, Is.Null);
        Assert.That(infoC.NextDueTime, Is.EqualTo(start.AddSeconds(3)));
        Assert.That(infoC.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoC.RunCount, Is.EqualTo(1));
        Assert.That(infoC.Runs, Has.Count.EqualTo(1));

        var infoCRun0 = infoC.Runs[0];
        Assert.That(infoCRun0.RunIndex, Is.EqualTo(0));
        Assert.That(infoCRun0.StartReason, Is.EqualTo(JobStartReason.Force));
        Assert.That(infoCRun0.DueTime, Is.EqualTo(start.AddSeconds(2)));
        Assert.That(infoCRun0.DueTimeWasOverridden, Is.False);
        Assert.That(infoCRun0.StartTime, Is.EqualTo(currentB.StartTime));
        Assert.That(infoCRun0.EndTime, Is.EqualTo(infoCRun0.StartTime.AddSeconds(runTime)).Within(DEFECT * 2));
        Assert.That(infoCRun0.Status, Is.EqualTo(JobRunStatus.Completed));
        Assert.That(infoCRun0.Output, Is.EqualTo("force"));
        Assert.That(infoCRun0.Exception, Is.Null);

        #endregion

        #region ^D - after time override, but before overridden-due-time start

        Assert.That(infoD.CurrentRun, Is.Null);
        Assert.That(infoD.NextDueTime, Is.EqualTo(start.AddSeconds(t3)));
        Assert.That(infoD.NextDueTimeIsOverridden, Is.True);
        Assert.That(infoD.RunCount, Is.EqualTo(1));
        Assert.That(infoD.Runs, Has.Count.EqualTo(1));

        Assert.That(infoD.Runs[0], Is.EqualTo(infoC.Runs[0]));

        #endregion

        #region ^E - after overridden-due-time start

        Assert.That(infoE.CurrentRun, Is.Not.Null);
        Assert.That(infoE.NextDueTime, Is.EqualTo(start.AddSeconds(4)));
        Assert.That(infoE.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoE.RunCount, Is.EqualTo(1));
        Assert.That(infoE.Runs, Has.Count.EqualTo(1));

        var currentE = infoE.CurrentRun.Value;
        Assert.That(currentE.RunIndex, Is.EqualTo(1));
        Assert.That(currentE.StartReason, Is.EqualTo(JobStartReason.OverriddenDueTime));
        Assert.That(currentE.DueTime, Is.EqualTo(start.AddSeconds(t3)));
        Assert.That(currentE.DueTimeWasOverridden, Is.True);
        Assert.That(currentE.StartTime, Is.EqualTo(start.AddSeconds(t3)).Within(DEFECT));
        Assert.That(currentE.EndTime, Is.Null);
        Assert.That(currentE.Status, Is.EqualTo(JobRunStatus.Running));
        Assert.That(currentE.Output, Is.EqualTo("overridden"));
        Assert.That(currentE.Exception, Is.Null);


        #endregion

        #region ^F - after overridden-due-time run completes, and before schedule-due-time start

        Assert.That(infoF.CurrentRun, Is.Null);
        Assert.That(infoF.NextDueTime, Is.EqualTo(start.AddSeconds(5)));
        Assert.That(infoF.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoF.RunCount, Is.EqualTo(2));
        Assert.That(infoF.Runs, Has.Count.EqualTo(2));

        var infoFRun0 = infoF.Runs[0];
        var infoFRun1 = infoF.Runs[1];

        Assert.That(infoFRun0, Is.EqualTo(infoD.Runs[0]));

        Assert.That(infoFRun1.RunIndex, Is.EqualTo(1));
        Assert.That(infoFRun1.StartReason, Is.EqualTo(JobStartReason.OverriddenDueTime));
        Assert.That(infoFRun1.DueTime, Is.EqualTo(start.AddSeconds(t3)));
        Assert.That(infoFRun1.DueTimeWasOverridden, Is.True);
        Assert.That(infoFRun1.StartTime, Is.EqualTo(currentE.StartTime));
        Assert.That(infoFRun1.EndTime, Is.EqualTo(infoFRun1.StartTime.AddSeconds(runTime)).Within(DEFECT * 2));
        Assert.That(infoFRun1.Status, Is.EqualTo(JobRunStatus.Completed));
        Assert.That(infoFRun1.Output, Is.EqualTo("overridden"));
        Assert.That(infoFRun1.Exception, Is.Null);


        #endregion

        #region ^G - after schedule-due-time start

        Assert.That(infoG.CurrentRun, Is.Not.Null);
        Assert.That(infoG.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(infoG.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoG.RunCount, Is.EqualTo(2));
        Assert.That(infoG.Runs, Has.Count.EqualTo(2));

        var currentG = infoG.CurrentRun.Value;
        Assert.That(currentG.RunIndex, Is.EqualTo(2));
        Assert.That(currentG.StartReason, Is.EqualTo(JobStartReason.ScheduleDueTime));
        Assert.That(currentG.DueTime, Is.EqualTo(start.AddSeconds(5)));
        Assert.That(currentG.DueTimeWasOverridden, Is.False);
        Assert.That(currentG.StartTime, Is.EqualTo(start.AddSeconds(5)).Within(DEFECT));
        Assert.That(currentG.EndTime, Is.Null);
        Assert.That(currentG.Status, Is.EqualTo(JobRunStatus.Running));
        Assert.That(currentG.Output, Is.EqualTo("schedule"));
        Assert.That(currentG.Exception, Is.Null);

        CollectionAssert.AreEqual(infoF.Runs, infoG.Runs);

        #endregion

        #region ^H - after schedule-due-time run completes

        Assert.That(infoH.CurrentRun, Is.Null);
        Assert.That(infoH.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(infoH.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoH.RunCount, Is.EqualTo(3));
        Assert.That(infoH.Runs, Has.Count.EqualTo(3));

        CollectionAssert.AreEqual(infoG.Runs.Take(2), infoH.Runs.Take(2));

        var infoHRun2 = infoH.Runs[2];

        Assert.That(infoHRun2.RunIndex, Is.EqualTo(2));
        Assert.That(infoHRun2.StartReason, Is.EqualTo(JobStartReason.ScheduleDueTime));
        Assert.That(infoHRun2.DueTime, Is.EqualTo(start.AddSeconds(5)));
        Assert.That(infoHRun2.DueTimeWasOverridden, Is.False);
        Assert.That(infoHRun2.StartTime, Is.EqualTo(currentG.StartTime));
        Assert.That(infoHRun2.EndTime, Is.EqualTo(infoHRun2.StartTime.AddSeconds(runTime)).Within(DEFECT * 2));
        Assert.That(infoHRun2.Status, Is.EqualTo(JobRunStatus.Completed));
        Assert.That(infoHRun2.Output, Is.EqualTo("schedule"));
        Assert.That(infoHRun2.Exception, Is.Null);

        #endregion

        #region after disposal

        Assert.That(infoFinal.CurrentRun, Is.EqualTo(infoH.CurrentRun));
        Assert.That(infoFinal.NextDueTime, Is.EqualTo(infoH.NextDueTime));
        Assert.That(infoFinal.NextDueTimeIsOverridden, Is.EqualTo(infoH.NextDueTimeIsOverridden));
        Assert.That(infoFinal.RunCount, Is.EqualTo(infoH.RunCount));
        Assert.That(infoFinal.Runs.Count, Is.EqualTo(infoH.Runs.Count));

        CollectionAssert.AreEqual(infoH.Runs, infoFinal.Runs);

        #endregion
    }

    [Test]
    public async Task GetInfo_ArgumentIsGreaterThanRunCount_ReturnsRequestedNumberOfRuns()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.CreateJob("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            var n = (int)parameter;
            await output.WriteAsync(n.ToString());
        };


        // Act
        for (var i = 0; i < 10; i++)
        {
            job.Parameter = i;
            job.ForceStart();
            await Task.Delay(50);
        }

        var info = job.GetInfo(5);

        // Assert

        for (var i = 0; i < 5; i++)
        {
            var runInfo = info.Runs[i];
            Assert.That(runInfo.RunIndex, Is.EqualTo(i));
            Assert.That(runInfo.Output, Is.EqualTo(i.ToString()));
        }
    }
}