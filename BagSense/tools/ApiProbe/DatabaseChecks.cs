using System.Text.Json;
using System.Net;
using BagSense;

internal static class DatabaseChecks
{
    public static CatalogDocument Read(string directory)
    {
        var manifest = JsonSerializer.Deserialize<CatalogManifest>(File.ReadAllText(Path.Combine(directory, "manifest.json")))!;
        return DatabaseCatalog.Decode(manifest, File.ReadAllBytes(Path.Combine(directory, manifest.File)));
    }

    public static void Run(string directory)
    {
        var manifest = JsonSerializer.Deserialize<CatalogManifest>(File.ReadAllText(Path.Combine(directory, "manifest.json")))!;
        var packed = File.ReadAllBytes(Path.Combine(directory, manifest.File));
        var document = DatabaseCatalog.Decode(manifest, packed);
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        void Reject(Action action, string message)
        {
            try { action(); } catch (InvalidDataException) { checks++; return; }
            throw new Exception(message);
        }
        Check(document.Items.Length > 50000, "Complete catalog contains over fifty thousand named items");
        var copper = document.Needs.Single(n => n.QuestId == 66135 && n.ItemId == 5106);
        Check(copper.Count == 10 && copper.Quality == "any", "My First Pickaxe requires ten Copper Ore, not ToDoQty=1");
        Check(copper.TodoIndex == 0 && copper.CompleteSequence == 255 && copper.Objective.Contains("Deliver"), "Copper hand-in objective and final sequence retained");
        Check(copper.Contacts.Any(c => c.Npc == "Adalberta" && c.CanMap), "Exact objective location resolves to Adalberta");
        Check(copper.Evidence.Contains("ten chunks of copper ore"), "Exact amount retains its literal source evidence");
        Check(document.Needs.Any(n => n.Count == null), "Unknown quantities remain explicitly unknown");
        Check(document.Needs.Any(n => n.Quality == "hq"), "Quality requirements retained");
        Check(document.Quests is { Length: >= 5000 }, "Catalog includes all named journal quests, not only RITEM quests");
        Check(document.Needs.Count(n => n.Count.HasValue) >= 600, "Structured supplies and explicit dialogue requests expand exact quantities");
        Check(document.Items.Any(i => i.EventItem && i.Id >= 2000000), "Key items retain their separate ID namespace");
        Check(document.Needs.Any(n => n.Variants?.Any(v => v.Jobs?.Length > 0) == true && n.Count > 0), "Structured profession requirements retain applicable jobs");
        Check(document.Quests!.SelectMany(q => q.Steps).All(s => s.Contacts.All(l => l.CanMap)), "Invalid zero-territory NPC placements are never exposed");
        Reject(() => DatabaseCatalog.Decode(manifest with { Sha256 = new string('0', 64) }, packed), "Corrupt checksum must fail");
        Reject(() => DatabaseCatalog.Decode(manifest with { File = "../other.json.gz" }, packed), "Path traversal must fail");
        Reject(() => DatabaseCatalog.Decode(manifest with { ItemCount = 1 }, packed), "Incomplete count must fail");
        Reject(() => DatabaseCatalog.Decode(manifest with { Schema = 99 }, packed), "Unknown schema must fail");
        string cache = Path.GetFullPath(Path.Combine(directory, "..", "artifacts", "cache-test-" + Guid.NewGuid().ToString("N")));
        using var source = new CatalogSource(JsonSerializer.Serialize(manifest), packed);
        var online = DatabaseCatalog.LoadAsync(cache, CancellationToken.None, source).GetAwaiter().GetResult();
        Check(!online.Offline && online.Document?.Items.Length == document.Items.Length, "Online source is validated and applied: " + online.Error);
        Check(File.Exists(Path.Combine(cache, "catalog-cache.json")), "Validated update persists a cache manifest");
        source.Fail = true;
        var offline = DatabaseCatalog.LoadAsync(cache, CancellationToken.None, source).GetAwaiter().GetResult();
        Check(offline.Offline && offline.Document?.Items.Length == document.Items.Length, "Network loss retains the verified previous catalog");
        source.Fail = false; source.Timeout = true;
        var timedOut = DatabaseCatalog.LoadAsync(cache, CancellationToken.None, source).GetAwaiter().GetResult();
        Check(timedOut.Offline && timedOut.Document?.Items.Length == document.Items.Length, "HTTP timeout retains cached data without being mistaken for plugin shutdown");
        source.Timeout = false;
        source.Fail = false; source.Packed = [1, 2, 3];
        var corrupt = DatabaseCatalog.LoadAsync(cache, CancellationToken.None, source).GetAwaiter().GetResult();
        Check(corrupt.Offline && corrupt.Document?.Items.Length == document.Items.Length, "Corrupt update cannot replace valid cached data");
        var emptyOffline = DatabaseCatalog.LoadAsync(cache + "-empty", CancellationToken.None, new CatalogSource("", []) { Fail = true }).GetAwaiter().GetResult();
        Check(emptyOffline.Offline && emptyOffline.Document == null, "First offline launch falls back to installed data without fabricating a catalog");
        Console.WriteLine($"PASS: {checks} catalog, quest evidence and integrity checks; {document.Items.Length} items, {document.Needs.Length} quest links");
    }

    private sealed class CatalogSource(string manifest, byte[] packed) : HttpMessageHandler
    {
        public bool Fail { get; set; }
        public bool Timeout { get; set; }
        public byte[] Packed { get; set; } = packed;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fail) throw new HttpRequestException("Simulated offline");
            if (Timeout) throw new TaskCanceledException("Simulated HTTP timeout");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = request.RequestUri!.AbsolutePath.EndsWith("manifest.json")
                ? new StringContent(manifest) : new ByteArrayContent(Packed) });
        }
    }
}
