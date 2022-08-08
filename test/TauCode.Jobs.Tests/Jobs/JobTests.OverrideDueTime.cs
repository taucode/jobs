using NUnit.Framework;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;
using TauCode.Jobs.Schedules;

namespace TauCode.Jobs.Tests.Jobs;

[TestFixture]
public partial class JobTests
{
    /// <summary>
    /// 0---------1---------2---------3--------
    ///                           |_~0.7s_|    
    /// _________________!1_______!2___________ (!1 -moment the due time is overridden, !2 - overridden due time)
    /// _____________________^A_______^B_____^C (GetInfo during lifecycle:
    ///                                            ^A - after due time was overridden
    ///                                            ^B - after run starts due to overridden due time
    ///                                            ^C - after run with overridden due time ends 
    /// </summary>
    [Test]
    public async Task OverrideDueTime_NotNull_StartsAndDiscards()
    {
        // Arrange
        const double runLength = 0.7;
        const double t1 = 1.7;
        const double tA = 2.1;
        const double t2 = 2.5;
        const double tB = 3.1;
        const double tC = 4.0;

        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            var msg = (string)parameter;
            await output.WriteAsync(msg);
            await Task.Delay(TimeSpan.FromSeconds(runLength), token);
        };

        // Act
        await timeMachine.WaitUntilSecondsElapse(start, t1);
        job.OverrideDueTime(start.AddSeconds(t2));

        await timeMachine.WaitUntilSecondsElapse(start, tA);
        var infoA = job.GetInfo(null);

        job.Parameter = "Hello!";

        await timeMachine.WaitUntilSecondsElapse(start, tB);
        var infoB = job.GetInfo(null);

        await timeMachine.WaitUntilSecondsElapse(start, tC);
        var infoC = job.GetInfo(null);

        // Assert
        var DEFECT = TimeSpan.FromMilliseconds(30);

        #region ^A

        Assert.That(infoA.CurrentRun, Is.Null);
        Assert.That(infoA.NextDueTime, Is.EqualTo(start.AddSeconds(t2)));
        Assert.That(infoA.NextDueTimeIsOverridden, Is.True);
        Assert.That(infoA.RunCount, Is.Zero);
        Assert.That(infoA.Runs, Is.Empty);

        #endregion

        #region ^B

        Assert.That(infoB.CurrentRun, Is.Not.Null);
        Assert.That(infoB.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(infoB.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoB.RunCount, Is.Zero);
        Assert.That(infoB.Runs, Is.Empty);

        var currentB = infoB.CurrentRun.Value;
        Assert.That(currentB.RunIndex, Is.EqualTo(0));
        Assert.That(currentB.StartReason, Is.EqualTo(JobStartReason.OverriddenDueTime));
        Assert.That(currentB.DueTime, Is.EqualTo(start.AddSeconds(t2)));
        Assert.That(currentB.DueTimeWasOverridden, Is.True);
        Assert.That(currentB.StartTime, Is.EqualTo(start.AddSeconds(t2)).Within(DEFECT));
        Assert.That(currentB.EndTime, Is.Null);
        Assert.That(currentB.Status, Is.EqualTo(JobRunStatus.Running));
        Assert.That(currentB.Output, Is.EqualTo("Hello!"));
        Assert.That(currentB.Exception, Is.Null);

        #endregion

        #region ^C

        Assert.That(infoC.CurrentRun, Is.Null);
        Assert.That(infoC.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(infoC.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoC.RunCount, Is.EqualTo(1));
        Assert.That(infoC.Runs, Has.Count.EqualTo(1));

        var infoCRun0 = infoC.Runs[0];
        Assert.That(infoCRun0.RunIndex, Is.EqualTo(0));
        Assert.That(infoCRun0.StartReason, Is.EqualTo(JobStartReason.OverriddenDueTime));
        Assert.That(infoCRun0.DueTime, Is.EqualTo(start.AddSeconds(t2)));
        Assert.That(infoCRun0.DueTimeWasOverridden, Is.True);
        Assert.That(infoCRun0.StartTime, Is.EqualTo(currentB.StartTime));
        Assert.That(infoCRun0.EndTime, Is.EqualTo(infoCRun0.StartTime.AddSeconds(runLength)).Within(DEFECT * 2));
        Assert.That(infoCRun0.Status, Is.EqualTo(JobRunStatus.Completed));
        Assert.That(infoCRun0.Output, Is.EqualTo("Hello!"));
        Assert.That(infoCRun0.Exception, Is.Null);

        #endregion
    }

    /// <summary>
    /// 0---------1---------2---------3--------
    ///                           |.no.run.|    
    /// ______________!1___!2_____!3___________    !1 - moment the due time is overridden
    ///                                            !2 - moment the overridden due time is discarded   
    ///                                            !3 - overridden due time value   
    /// _______________________^A_______^B_____    ^A - after overridden due time is discarded
    ///                                            ^B - there should be no runs
    /// </summary>
    [Test]
    public async Task OverrideDueTime_Null_DefaultsToSchedule()
    {
        // Arrange
        const double runLength = 0.7;
        const double t1 = 1.5;
        const double t2 = 1.9;
        const double tA = 2.2;
        const double t3 = 2.5;
        const double tB = 3.3;

        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        job.IsEnabled = true;

        job.Routine = async (parameter, tracker, output, token) =>
        {
            var msg = (string)parameter;
            await output.WriteAsync(msg);
            await Task.Delay(TimeSpan.FromSeconds(runLength), token);
        };

        // Act
        await timeMachine.WaitUntilSecondsElapse(start, t1);
        job.OverrideDueTime(start.AddSeconds(t3));

        job.Parameter = "Hello!";

        await timeMachine.WaitUntilSecondsElapse(start, t2);
        job.OverrideDueTime(null);

        await timeMachine.WaitUntilSecondsElapse(start, tA);
        var infoA = job.GetInfo(null);

        await timeMachine.WaitUntilSecondsElapse(start, tB);
        var infoB = job.GetInfo(null);

        // Assert

        #region ^A

        Assert.That(infoA.CurrentRun, Is.Null);
        Assert.That(infoA.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(infoA.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoA.RunCount, Is.Zero);
        Assert.That(infoA.Runs, Is.Empty);

        #endregion

        #region ^B

        Assert.That(infoB.CurrentRun, Is.Null);
        Assert.That(infoB.NextDueTime, Is.EqualTo(TestHelper.NeverCopy));
        Assert.That(infoB.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoB.RunCount, Is.Zero);
        Assert.That(infoB.Runs, Is.Empty);

        #endregion
    }

    [Test]
    public void OverrideDueTime_ArgumentIsInPast_ThrowsJobException()
    {
        // Arrange

        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => job.OverrideDueTime(start.AddSeconds(-1)));

        // Assert
        Assert.That(ex, Has.Message.EqualTo("Cannot override due time in the past."));
    }

    /// <summary>
    /// 0---------1---------2---------3---------4---------5---
    ///           |_________|    |_________|    |_________|
    /// __________!1___!2________!3_____________!4____________  !1 - started via schedule
    ///                                                         !2 - due time is overridden to !3
    ///                                                         !3 - started via overridden due time
    ///                                                         !4 - started via schedule
    /// ____________^A____^B__^C____^D_______^E____^F_______^G  ^A - after started via schedule
    ///                                                         ^B - after overridden due time is set
    ///                                                         ^C - after scheduled run ends
    ///                                                         ^D - after 'overridden' run starts
    ///                                                         ^E - after 'overridden' run ends
    ///                                                         ^F - after scheduled run starts
    ///                                                         ^G - after scheduled run ends; disposed.
    /// </summary>
    [Test]
    public async Task OverrideDueTime_DuringRun_AppliesToNextRun()
    {
        // Arrange
        const double runLength = 1.0;

        const double t1 = 1.0;
        const double tA = 1.2;
        const double t2 = 1.4;
        const double tB = 1.7;
        const double tC = 2.3;
        const double t3 = 2.5;
        const double tD = 2.8;
        const double tE = 3.8;
        const double t4 = 4.0;
        const double tF = 4.3;
        const double tG = 5.3;

        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        job.IsEnabled = true;

        job.Schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

        job.Routine = async (parameter, tracker, output, token) =>
        {
            var msg = (string)parameter;
            await output.WriteAsync(msg);
            await Task.Delay(TimeSpan.FromSeconds(runLength), token);
        };

        // Act
        job.Parameter = "Scheduled1";

        await timeMachine.WaitUntilSecondsElapse(start, tA);
        var infoA = job.GetInfo(null);

        await timeMachine.WaitUntilSecondsElapse(start, t2);
        job.OverrideDueTime(start.AddSeconds(t3));
        job.Parameter = "Overridden";

        await timeMachine.WaitUntilSecondsElapse(start, tB);
        var infoB = job.GetInfo(null);

        await timeMachine.WaitUntilSecondsElapse(start, tC);
        var infoC = job.GetInfo(null);

        await timeMachine.WaitUntilSecondsElapse(start, tD);
        job.Parameter = "Scheduled2";
        var infoD = job.GetInfo(null);

        await timeMachine.WaitUntilSecondsElapse(start, tE);
        var infoE = job.GetInfo(null);

        await timeMachine.WaitUntilSecondsElapse(start, tF);
        var infoF = job.GetInfo(null);

        await timeMachine.WaitUntilSecondsElapse(start, tG);
        var infoG = job.GetInfo(null);

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
        Assert.That(currentA.DueTime, Is.EqualTo(start.AddSeconds(1.0)));
        Assert.That(currentA.DueTimeWasOverridden, Is.False);
        Assert.That(currentA.StartTime, Is.EqualTo(start.AddSeconds(t1)).Within(DEFECT));
        Assert.That(currentA.EndTime, Is.Null);
        Assert.That(currentA.Status, Is.EqualTo(JobRunStatus.Running));
        Assert.That(currentA.Output, Is.EqualTo("Scheduled1"));
        Assert.That(currentA.Exception, Is.Null);

        #endregion

        #region ^B

        Assert.That(infoB.CurrentRun, Is.Not.Null);
        Assert.That(infoB.NextDueTime, Is.EqualTo(start.AddSeconds(t3)));
        Assert.That(infoB.NextDueTimeIsOverridden, Is.True);
        Assert.That(infoB.RunCount, Is.Zero);
        Assert.That(infoB.Runs, Is.Empty);

        var currentB = infoB.CurrentRun.Value;
        Assert.That(currentA, Is.EqualTo(currentB));

        #endregion

        #region ^C

        Assert.That(infoC.CurrentRun, Is.Null);
        Assert.That(infoC.NextDueTime, Is.EqualTo(start.AddSeconds(t3)));
        Assert.That(infoC.NextDueTimeIsOverridden, Is.True);
        Assert.That(infoC.RunCount, Is.EqualTo(1));
        Assert.That(infoC.Runs, Has.Count.EqualTo(1));

        var runC0 = infoC.Runs.Single();

        Assert.That(runC0.RunIndex, Is.EqualTo(0));
        Assert.That(runC0.StartReason, Is.EqualTo(JobStartReason.ScheduleDueTime));
        Assert.That(runC0.DueTime, Is.EqualTo(start.AddSeconds(1.0)));
        Assert.That(runC0.DueTimeWasOverridden, Is.False);
        Assert.That(runC0.StartTime, Is.EqualTo(currentA.StartTime));
        Assert.That(runC0.EndTime, Is.EqualTo(runC0.StartTime.AddSeconds(runLength)).Within(DEFECT));
        Assert.That(runC0.Status, Is.EqualTo(JobRunStatus.Completed));
        Assert.That(runC0.Output, Is.EqualTo("Scheduled1"));
        Assert.That(runC0.Exception, Is.Null);

        #endregion

        #region ^D

        Assert.That(infoD.CurrentRun, Is.Not.Null);
        Assert.That(infoD.NextDueTime, Is.EqualTo(start.AddSeconds(3.0)));
        Assert.That(infoD.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoD.RunCount, Is.EqualTo(1));
        Assert.That(infoD.Runs, Has.Count.EqualTo(1));

        var currentD = infoD.CurrentRun.Value;
        Assert.That(currentD.RunIndex, Is.EqualTo(1));
        Assert.That(currentD.StartReason, Is.EqualTo(JobStartReason.OverriddenDueTime));
        Assert.That(currentD.DueTime, Is.EqualTo(start.AddSeconds(t3)));
        Assert.That(currentD.DueTimeWasOverridden, Is.True);
        Assert.That(currentD.StartTime, Is.EqualTo(start.AddSeconds(t3)).Within(DEFECT));
        Assert.That(currentD.EndTime, Is.Null);
        Assert.That(currentD.Status, Is.EqualTo(JobRunStatus.Running));
        Assert.That(currentD.Output, Is.EqualTo("Overridden"));
        Assert.That(currentD.Exception, Is.Null);

        Assert.That(infoD.Runs.Single(), Is.EqualTo(runC0));

        #endregion

        #region ^E

        Assert.That(infoE.CurrentRun, Is.Null);
        Assert.That(infoE.NextDueTime, Is.EqualTo(start.AddSeconds(t4)));
        Assert.That(infoE.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoE.RunCount, Is.EqualTo(2));
        Assert.That(infoE.Runs, Has.Count.EqualTo(2));

        Assert.That(infoE.Runs[0], Is.EqualTo(infoD.Runs[0]));

        var runE1 = infoE.Runs[1];

        Assert.That(runE1.RunIndex, Is.EqualTo(1));
        Assert.That(runE1.StartReason, Is.EqualTo(JobStartReason.OverriddenDueTime));
        Assert.That(runE1.DueTime, Is.EqualTo(start.AddSeconds(t3)));
        Assert.That(runE1.DueTimeWasOverridden, Is.True);
        Assert.That(runE1.StartTime, Is.EqualTo(currentD.StartTime));
        Assert.That(runE1.EndTime, Is.EqualTo(runE1.StartTime.AddSeconds(runLength)).Within(DEFECT));
        Assert.That(runE1.Status, Is.EqualTo(JobRunStatus.Completed));
        Assert.That(runE1.Output, Is.EqualTo("Overridden"));
        Assert.That(runE1.Exception, Is.Null);

        #endregion

        #region ^F

        Assert.That(infoF.CurrentRun, Is.Not.Null);
        Assert.That(infoF.NextDueTime, Is.EqualTo(start.AddSeconds(5.0)));
        Assert.That(infoF.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoF.RunCount, Is.EqualTo(2));
        Assert.That(infoF.Runs, Has.Count.EqualTo(2));

        var currentF = infoF.CurrentRun.Value;
        Assert.That(currentF.RunIndex, Is.EqualTo(2));
        Assert.That(currentF.StartReason, Is.EqualTo(JobStartReason.ScheduleDueTime));
        Assert.That(currentF.DueTime, Is.EqualTo(start.AddSeconds(t4)));
        Assert.That(currentF.DueTimeWasOverridden, Is.False);
        Assert.That(currentF.StartTime, Is.EqualTo(start.AddSeconds(t4)).Within(DEFECT));
        Assert.That(currentF.EndTime, Is.Null);
        Assert.That(currentF.Status, Is.EqualTo(JobRunStatus.Running));
        Assert.That(currentF.Output, Is.EqualTo("Scheduled2"));
        Assert.That(currentF.Exception, Is.Null);

        Assert.That(infoF.Runs[0], Is.EqualTo(infoE.Runs[0]));
        Assert.That(infoF.Runs[1], Is.EqualTo(infoE.Runs[1]));

        #endregion

        #region ^G

        Assert.That(infoG.CurrentRun, Is.Null);
        Assert.That(infoG.NextDueTime, Is.EqualTo(start.AddSeconds(6.0)));
        Assert.That(infoG.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoG.RunCount, Is.EqualTo(3));
        Assert.That(infoG.Runs, Has.Count.EqualTo(3));

        Assert.That(infoG.Runs[0], Is.EqualTo(infoF.Runs[0]));
        Assert.That(infoG.Runs[1], Is.EqualTo(infoF.Runs[1]));

        var runG2 = infoG.Runs[2];

        Assert.That(runG2.RunIndex, Is.EqualTo(2));
        Assert.That(runG2.StartReason, Is.EqualTo(JobStartReason.ScheduleDueTime));
        Assert.That(runG2.DueTime, Is.EqualTo(start.AddSeconds(t4)));
        Assert.That(runG2.DueTimeWasOverridden, Is.False);
        Assert.That(runG2.StartTime, Is.EqualTo(currentF.StartTime));
        Assert.That(runG2.EndTime, Is.EqualTo(runG2.StartTime.AddSeconds(runLength)).Within(DEFECT));
        Assert.That(runG2.Status, Is.EqualTo(JobRunStatus.Completed));
        Assert.That(runG2.Output, Is.EqualTo("Scheduled2"));
        Assert.That(runG2.Exception, Is.Null);

        #endregion
    }

    /// <summary>
    /// 0---------1---------2---------3---------4--------
    ///    |____________________________________|
    /// _______!1______!2________________________________ !1 - due time is overridden
    ///                                                   !2 - overridden due time
    /// __________________^A________________________^B___ ^A - overridden due time is discarded
    ///                                                   ^B - single run was performed
    /// </summary>
    [Test]
    public async Task OverrideDueTime_DuringLongRun_HasNoEffect()
    {
        // Arrange
        const double t1 = 0.8;
        const double t2 = 1.5;
        const double tA = 1.9;
        const double tB = 4.5;

        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        job.IsEnabled = true;

        job.Schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync("Hello!");
            await timeMachine.WaitUntilSecondsElapse(start, 4.0, token);
        };

        // Act
        job.ForceStart();

        await timeMachine.WaitUntilSecondsElapse(start, t1);
        job.OverrideDueTime(start.AddSeconds(t2));

        await timeMachine.WaitUntilSecondsElapse(start, tA);
        var infoA = job.GetInfo(null);

        await timeMachine.WaitUntilSecondsElapse(start, tB);
        var infoB = job.GetInfo(null);

        job.Dispose();

        // Assert

        #region ^A

        Assert.That(infoA.CurrentRun, Is.Not.Null);
        Assert.That(infoA.NextDueTime, Is.EqualTo(start.AddSeconds(2.0)));
        Assert.That(infoA.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoA.RunCount, Is.Zero);
        Assert.That(infoA.Runs, Is.Empty);

        #endregion

        #region ^B

        Assert.That(infoB.CurrentRun, Is.Null);
        Assert.That(infoB.NextDueTime, Is.EqualTo(start.AddSeconds(5.0)));
        Assert.That(infoB.NextDueTimeIsOverridden, Is.False);
        Assert.That(infoB.RunCount, Is.EqualTo(1));
        Assert.That(infoB.Runs, Has.Count.EqualTo(1));

        #endregion
    }

    /// <summary>
    /// 0---------1---------2---------3---------4--------
    ///                 |.no.run.|    
    /// _______!1___!2__!3_______________________________ !1 - due time is overridden
    ///                                                   !2 - disposed
    ///                                                   !3 - overridden due time
    /// __________________^A________________________^B___ ^A - doesn't start, overridden due time remains
    ///                                                   ^B - never starts, overridden due time remains.
    /// </summary>
    [Test]
    public async Task OverrideDueTime_SetThenDisposed_NeverDiscardedNeverRuns()
    {
        // Arrange
        const double runLength = 1.0;

        const double t1 = 0.8;
        const double t2 = 1.1;
        const double t3 = 1.5;
        const double tA = 1.9;
        const double tB = 4.5;

        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

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
        job.Dispose();

        await timeMachine.WaitUntilSecondsElapse(start, tA);
        var infoA = job.GetInfo(null);

        await timeMachine.WaitUntilSecondsElapse(start, tB);
        var infoB = job.GetInfo(null);

        job.Dispose();

        // Assert

        #region ^A

        Assert.That(infoA.CurrentRun, Is.Null);
        Assert.That(infoA.NextDueTime, Is.EqualTo(start.AddSeconds(1.5)));
        Assert.That(infoA.NextDueTimeIsOverridden, Is.True);
        Assert.That(infoA.RunCount, Is.Zero);
        Assert.That(infoA.Runs, Is.Empty);

        #endregion

        #region ^B

        Assert.That(infoB.CurrentRun, Is.Null);
        Assert.That(infoB.NextDueTime, Is.EqualTo(start.AddSeconds(1.5)));
        Assert.That(infoB.NextDueTimeIsOverridden, Is.True);
        Assert.That(infoB.RunCount, Is.Zero);
        Assert.That(infoB.Runs, Is.Empty);

        #endregion
    }

    [Test]
    public void OverrideDueTime_Disposed_ThrowsJobObjectDisposedException()
    {
        // Arrange
        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var job = jobManager.Create("my-job");
        job.Dispose();

        // Act
        var ex = Assert.Throws<ObjectDisposedException>(() => job.OverrideDueTime(DateTimeOffset.UtcNow.AddHours(3)));

        // Assert
        Assert.That(ex, Has.Message.StartWith("Cannot access a disposed object."));
        Assert.That(ex.ObjectName, Is.EqualTo("my-job"));
    }

    /// <summary>
    /// 0---------1---------2---------3---------4--------
    ///                 |.no.run.|    
    /// _______!1_______!2_______________________________ !1 - due time is overridden
    ///                                                   !2 - overridden due time
    /// __________________^A________________________^B___ ^A - doesn't start, overridden due time discarded
    ///                                                   ^B - never starts
    /// </summary>
    [Test]
    public async Task OverrideDueTime_JobIsDisabled_DoesNotStartThenDiscarded()
    {
        // Arrange
        const double runLength = 1.0;

        const double t1 = 0.8;
        const double t2 = 1.5;
        const double tA = 1.9;
        const double tB = 4.5;

        using IJobManager jobManager = TestHelper.CreateJobManager(true, _logger);

        var start = "2000-01-01Z".ToUtcDateOffset();
        var timeMachine = ShiftedTimeProvider.CreateTimeMachine(start);
        TimeProvider.Override(timeMachine);

        var job = jobManager.Create("my-job");

        job.Schedule = new SimpleSchedule(SimpleScheduleKind.Second, 1, start);

        job.Routine = async (parameter, tracker, output, token) =>
        {
            await output.WriteAsync("Hello!");
            await Task.Delay(TimeSpan.FromSeconds(runLength), token);
        };

        // Act
        await timeMachine.WaitUntilSecondsElapse(start, t1);
        job.OverrideDueTime(start.AddSeconds(t2));

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
}