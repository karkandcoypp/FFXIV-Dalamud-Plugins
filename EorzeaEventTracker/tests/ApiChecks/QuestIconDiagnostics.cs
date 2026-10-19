using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Lumina;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;

internal static class QuestIconDiagnostics
{
    public static void Export(string sqpack, string output)
    {
        Directory.CreateDirectory(output);
        using var data = new GameData(sqpack);
        var rows = new List<object>();
        foreach (var type in new uint[] { 1, 8 })
        {
            var marker = data.GetExcelSheet<EventIconType>().GetRow(type);
            foreach (var start in new[] { marker.MapIconAvailable, marker.MapIconInvalid, marker.NpcIconAvailable, marker.NpcIconInvalid })
                for (uint offset = 0; offset < marker.IconRange; offset++)
                {
                    var icon = start + offset;
                    var file = data.GetFile<TexFile>($"ui/icon/{icon / 1000 * 1000:000000}/{icon:000000}.tex");
                    if (file == null) continue;
                    File.WriteAllBytes(Path.Combine(output, icon + ".bgra"), file.ImageData);
                    rows.Add(new { type, start, offset, icon, width = file.Header.Width, height = file.Header.Height });
                }
        }
        File.WriteAllText(Path.Combine(output, "icons.json"), JsonSerializer.Serialize(rows));
        Console.WriteLine("Exported installed quest icon variants for visual inspection: " + rows.Count);
    }
}
