// Compatibility for older external calls. Current runtime and editor scripts use
// WorkyardTree.Timing directly and do not require this file to be imported.
public static class WorkyardTiming
{
    public static float Phase(double progress){return WorkyardTree.Timing.Phase(progress);}
    public static float CycleSeconds(double logsPerSecond){return WorkyardTree.Timing.CycleSeconds(logsPerSecond);}
    public static float FallSeconds(float cycle){return WorkyardTree.Timing.FallSeconds(cycle);}
    public static float GrowStart(float cycle){return WorkyardTree.Timing.GrowStart(cycle);}
    public static float ReadySeconds(float cycle){return WorkyardTree.Timing.ReadySeconds(cycle);}
    public static float LumberSwing(float progress){return WorkyardTree.Timing.LumberSwing(progress);}
}
