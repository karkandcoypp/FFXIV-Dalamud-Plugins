using System.Collections.Generic;
using System.Linq;

namespace EorzeaCompass;

public static class CompassSelection
{
    // A detail pane must always describe an item in the currently visible list.
    public static uint KeepVisible(uint selected, IReadOnlyList<ActivityProgress> rows)
        => rows.Any(p => p.Activity.Id == selected) ? selected : rows.FirstOrDefault()?.Activity.Id ?? 0;
}
