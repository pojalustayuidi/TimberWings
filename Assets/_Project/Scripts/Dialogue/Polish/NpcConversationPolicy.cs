using UnityEngine;

// Task interactions own the NPC until the task has been handed in.
public static class NpcConversationPolicy
{
    public static bool TaskInProgress
    {
        get
        {
            var intro = IntroFlow.Instance;
            if (intro != null && intro.Current != IntroFlow.Step.Done) return true;
            var story = StoryFlow.Instance;
            return story != null && !story.AllDone;
        }
    }

    public static bool DeliveryPending(Component owner)
    {
        if (owner == null) return false;
        var supply = owner.GetComponent<SupplyPoint>();
        return supply != null && supply.isActiveAndEnabled && supply.Accepting;
    }

    public static bool SmallTalkAllowed(Component owner)
    {
        return !TaskInProgress && !DeliveryPending(owner);
    }
}
