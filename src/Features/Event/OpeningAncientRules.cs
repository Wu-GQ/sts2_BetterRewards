namespace BetterRewards.Features.Event;

/// <summary>
/// Identifies the run's opening ancient visit.
/// <see cref="MegaCrit.Sts2.Core.Runs.RunManager"/> enters <c>Map.StartingMapPoint</c> only from
/// <c>EnterAct</c> when the act index is 0 and <c>ExtraFields.StartedWithNeow</c> is set.
/// Later acts use their own maps, and any other node in act 1 is not that visit.
/// </summary>
internal static class OpeningAncientRules
{
    public static bool IsOpeningPosition(
        int currentActIndex,
        bool startedWithNeow,
        int col,
        int row,
        int startCol,
        int startRow)
    {
        return currentActIndex == 0
            && startedWithNeow
            && col == startCol
            && row == startRow;
    }
}
