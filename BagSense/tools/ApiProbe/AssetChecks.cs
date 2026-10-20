using Lumina;
using Lumina.Data.Files;

internal static class AssetChecks
{
    public static void Run(string directory, string output)
    {
        using var game = new GameData(directory);
        Directory.CreateDirectory(output);
        foreach (uint id in new uint[] { 68001, 68288, 4001 })
        {
            string path = $"ui/icon/{id / 1000 * 1000:000000}/{id:000000}.tex";
            var tex = game.GetFile<TexFile>(path);
            if (tex == null) throw new Exception("Missing mount preview: " + path);
            File.WriteAllBytes(Path.Combine(output, $"{id}-{tex.Header.Width}x{tex.Header.Height}.bgra"), tex.ImageData);
            Console.WriteLine($"ASSET {path}: {tex.Header.Width}x{tex.Header.Height}");
        }
    }
}
