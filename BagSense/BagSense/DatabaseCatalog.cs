using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BagSense;

public sealed record CatalogItem(uint Id, string Name, uint Icon, string Category, string Description, bool EventItem = false);
public sealed record CatalogNeed(uint QuestId, uint ItemId, string QuestName, bool Repeatable, int? Count, string Quality,
    string Objective, int? TodoIndex, int? CompleteSequence, NpcLocation[] Contacts, string Evidence, string Source,
    bool EventItem = false, QuestVariant[]? Variants = null);
public sealed record CatalogQuestStep(int Index, int CompleteSequence, string Text, NpcLocation[] Contacts);
public sealed record CatalogQuest(uint QuestId, string Name, bool Repeatable, CatalogQuestStep[] Steps, uint[] ItemIds, string Source);
public sealed record CatalogSource(uint ItemId, string Kind, string Name, uint Id = 0, string Detail = "");
public sealed record CatalogDocument(int Schema, DateTimeOffset GeneratedAt, string SourceCommit, CatalogItem[] Items, CatalogNeed[] Needs,
    CatalogQuest[]? Quests = null, CatalogSource[]? Sources = null, string TeamcraftCommit = "");
public sealed record CatalogManifest(int Schema, DateTimeOffset GeneratedAt, string SourceCommit, string File, string Sha256,
    int Bytes, int ItemCount, int QuestItemCount, int ExactCount);
public sealed record DatabaseResult(CatalogDocument? Document, bool Offline, string Error);

public static class DatabaseCatalog
{
    public const string BaseUrl = "https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/BagSense/data/";
    private const int MaxPacked = 16 * 1024 * 1024;
    private const int MaxUnpacked = 64 * 1024 * 1024;

    public static CatalogDocument Decode(CatalogManifest manifest, byte[] packed)
    {
        if (manifest.Schema != 1 || manifest.Bytes != packed.Length || packed.Length > MaxPacked
            || !Regex.IsMatch(manifest.File, "^catalog-[a-f0-9]{16}\\.json\\.gz$")
            || !string.Equals(Convert.ToHexString(SHA256.HashData(packed)), manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Catalog manifest or checksum mismatch");
        using var gzip = new GZipStream(new MemoryStream(packed), CompressionMode.Decompress);
        using var plain = new MemoryStream();
        var buffer = new byte[65536];
        int count;
        while ((count = gzip.Read(buffer)) > 0)
        {
            if (plain.Length + count > MaxUnpacked) throw new InvalidDataException("Catalog is too large");
            plain.Write(buffer, 0, count);
        }
        var document = JsonSerializer.Deserialize<CatalogDocument>(plain.ToArray()) ?? throw new InvalidDataException("Empty catalog");
        if (document.Schema != 1 || document.SourceCommit != manifest.SourceCommit || document.GeneratedAt != manifest.GeneratedAt
            || document.Items == null || document.Needs == null || document.Items.Length != manifest.ItemCount
            || document.Items.Length < 40000 || document.Items.Length > 100000 || document.Needs.Length != manifest.QuestItemCount
            || document.Items.Any(i => i.Id == 0 || string.IsNullOrWhiteSpace(i.Name))
            || document.Items.DistinctBy(i => i.Id).Count() != document.Items.Length
            || document.Needs.Any(n => n.QuestId < 65536 || n.ItemId == 0 || string.IsNullOrWhiteSpace(n.QuestName)
                || n.Count is <= 0 or > 9999 || n.Quality is not ("any" or "hq" or "unknown")
                || n.Contacts == null || n.Contacts.Any(l => !l.CanMap) || n.TodoIndex is < 0 or > 23 || n.CompleteSequence is < 0 or > 255
                || n.Variants?.Any(v => v.Count is <= 0 or > 9999 || v.Jobs?.Any(j => j > 100) == true) == true))
            throw new InvalidDataException("Invalid catalog data");
        var ids = document.Items.Select(i => i.Id).ToHashSet();
        if (document.Needs.Any(n => !ids.Contains(n.ItemId)) || document.Needs.DistinctBy(n => (n.QuestId, n.ItemId)).Count() != document.Needs.Length)
            throw new InvalidDataException("Invalid quest-item references");
        if (document.Quests is { } quests && (quests.Length > 20000 || quests.DistinctBy(q => q.QuestId).Count() != quests.Length
            || quests.Any(q => q.QuestId < 65536 || string.IsNullOrWhiteSpace(q.Name) || q.Steps == null || q.ItemIds == null
                || q.ItemIds.Any(i => !ids.Contains(i)) || q.Steps.Any(s => s.Index is < 0 or > 23 || s.CompleteSequence is < 0 or > 255
                    || s.Contacts == null || s.Contacts.Any(l => !l.CanMap))))) throw new InvalidDataException("Invalid quest catalog");
        if (document.Sources is { } sources && (sources.Length > 200000 || sources.Any(s => !ids.Contains(s.ItemId)
            || string.IsNullOrWhiteSpace(s.Name) || s.Name.Length > 512 || s.Detail.Length > 2048
            || s.Kind is not ("Drop" or "Duty" or "Fate" or "Container" or "Contains")))) throw new InvalidDataException("Invalid source graph");
        return document;
    }

    public static async Task<DatabaseResult> LoadAsync(string directory, CancellationToken cancellation, HttpMessageHandler? handler = null)
    {
        CatalogDocument? cached = null;
        var cachePath = Path.Combine(directory, "catalog-cache.json");
        try
        {
            if (File.Exists(cachePath))
            {
                var manifest = JsonSerializer.Deserialize<CatalogManifest>(await File.ReadAllTextAsync(cachePath, cancellation))!;
                if (Regex.IsMatch(manifest.File, "^catalog-[a-f0-9]{16}\\.json\\.gz$"))
                    cached = Decode(manifest, await File.ReadAllBytesAsync(Path.Combine(directory, manifest.File), cancellation));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested) { /* Invalid cache is never applied. Fetch a fresh copy. */ }
        try
        {
            using var http = handler == null ? new HttpClient() : new HttpClient(handler, false);
            http.Timeout = TimeSpan.FromSeconds(90);
            http.MaxResponseContentBufferSize = MaxPacked;
            http.DefaultRequestHeaders.UserAgent.ParseAdd("BagSense/0.3.0");
            var json = await http.GetStringAsync(BaseUrl + "manifest.json", cancellation);
            if (json.Length > 16384) throw new InvalidDataException("Manifest is too large");
            var manifest = JsonSerializer.Deserialize<CatalogManifest>(json) ?? throw new InvalidDataException("Empty manifest");
            if (!Regex.IsMatch(manifest.File, "^catalog-[a-f0-9]{16}\\.json\\.gz$")) throw new InvalidDataException("Invalid catalog filename");
            if (cached != null && manifest.GeneratedAt < cached.GeneratedAt) throw new InvalidDataException("Catalog rollback refused");
            var packed = await http.GetByteArrayAsync(BaseUrl + manifest.File, cancellation);
            var document = Decode(manifest, packed);
            Directory.CreateDirectory(directory);
            var asset = Path.Combine(directory, manifest.File);
            await File.WriteAllBytesAsync(asset + ".tmp", packed, cancellation);
            File.Move(asset + ".tmp", asset, true);
            await File.WriteAllTextAsync(cachePath + ".tmp", json, cancellation);
            File.Move(cachePath + ".tmp", cachePath, true);
            return new(document, false, "");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
        {
            return new(cached, true, ex.GetType().Name);
        }
    }
}
