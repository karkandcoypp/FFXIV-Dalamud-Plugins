namespace EorzeaEventTracker;

public static class QuestMarkerPolicy
{
    // EventIconType bases are diagnostic labels. Offset 1 is the actual quest-offer marker.
    // Both map and NPC families follow this layout; use NPC artwork for readable journal icons.
    public static uint Offer(uint familyBase, uint iconRange) => familyBase > 0 && familyBase < 1000000 && iconRange > 1
        ? familyBase + 1 : 0;
}
