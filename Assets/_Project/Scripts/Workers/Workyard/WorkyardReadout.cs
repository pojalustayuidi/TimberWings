using System;

// Presentation only. CampSimulation remains the owner of time, resources and XP.
public enum WorkyardActivity { Empty, Closed, Working, Full, NoLogs }

public static class WorkyardReadout
{
    public static WorkyardActivity Activity(CampSimulation sim, WorkerState worker, bool open)
    {
        if(worker==null) return WorkyardActivity.Empty;
        if(!open || sim==null) return WorkyardActivity.Closed;
        if(worker.role==WorkerRole.Clerk) return WorkyardActivity.Working;
        if(worker.role==WorkerRole.Lumberjack) return sim.logs>=sim.LogCap ? WorkyardActivity.Full : WorkyardActivity.Working;
        if(sim.planks>=sim.PlankCap) return WorkyardActivity.Full;
        return worker.sawActive || sim.logs>=WorkerBalance.SawyerLogsPerPlank ? WorkyardActivity.Working : WorkyardActivity.NoLogs;
    }

    public static int VisibleUnits(int amount,int capacity,int segments)
    {
        if(amount<=0 || capacity<=0 || segments<=0) return 0;
        return Math.Min(segments,(int)Math.Ceiling(Math.Min(1.0,(double)amount/capacity)*segments));
    }
}
