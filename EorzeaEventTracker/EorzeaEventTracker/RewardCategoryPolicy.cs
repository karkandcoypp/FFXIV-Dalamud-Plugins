namespace EorzeaEventTracker;

public enum RewardCategory { All, Gear, Mounts, Minions, Housing, Other }
public static class RewardCategoryPolicy
{
    // Installed ItemUICategory / ItemAction.Action sheet identifiers, not event-specific names.
    public static RewardCategory Classify(uint equipSlot, uint uiCategory, uint action) => action == 1322
        ? RewardCategory.Mounts : uiCategory == 81 ? RewardCategory.Minions
        : equipSlot != 0 ? RewardCategory.Gear
        : uiCategory == 57 || uiCategory is >= 65 and <= 80 ? RewardCategory.Housing : RewardCategory.Other;
}
