using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EorzeaEventTracker;

internal static class OnlineFeedChecks
{
    public static async Task Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "EET-online-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var cache = Path.Combine(root, "events.json");
        var bundle = Path.Combine(root, "bundle.json");
        File.WriteAllText(bundle, "{\"schemaVersion\":1,\"events\":[]}");
        var e = new EventInfo { Id = "new-online-only", Title = "A future event", StartsAt = DateTimeOffset.UtcNow.AddDays(-1), EndsAt = DateTimeOffset.UtcNow.AddDays(1),
            ImageUrl = "https://lds-img.finalfantasyxiv.com/test.png",
            Tracking = new TrackingDefinition { EventId = "new-online-only", Asset = "../../unsafe", Quests = new() { new() { Id = 71004, Name = "Online quest" } },
                Rewards = new() { new() { Id = 52402, Name = "Online reward", Icon = 56959, EarnedByQuest = 71004 } } },
            Translations = new() { ["ru"] = new() { ["A future event"] = "Новое событие", ["Online reward"] = "Новая награда" } } };
        var feed = new EventFeed { Events = new() { e } };
        e.Locations.Add(new EventLocation { Id = "trader", Npc = "Online trader", Zone = "Old Gridania", TerritoryId = 133, MapId = 3, X = 10.2f, Y = 9.4f });
        e.Tracking!.Rewards[0].LocationId = "trader";
        var handler = new FakeHandler { Data = JsonSerializer.SerializeToUtf8Bytes(feed) };
        using (var service = new EventFeedService(cache, bundle, handler))
        {
            await service.RefreshAsync("https://example.com/feed");
            var catalog = FeedPolicy.Catalog(service.Feed, new Dictionary<string, TrackingDefinition>());
            Check(catalog["new-online-only"].Rewards[0].Id == 52402, "new event tracking arrives online without a bundled definition");
            Check(catalog["new-online-only"].Asset == "", "online local asset paths are ignored");
            var locale = new Localizer(new Configuration { Language = "ru" });
            Check(locale.Text("A future event", service.Feed.Events[0]) == "Новое событие", "online translation is applied without changing language resources");
            Check(locale.Text("Online reward", service.Feed.Events[0]) == "Новая награда", "online reward names support translation");
            var previous = service.Feed;
            e.Tracking!.CompletionQuestId = 65537;
            handler.Data = JsonSerializer.SerializeToUtf8Bytes(feed);
            await service.RefreshAsync("https://example.com/feed");
            Check(ReferenceEquals(previous, service.Feed), "unknown main completion quest preserves the last valid feed");
            e.Tracking.CompletionQuestId = 71004;
            Check(previous.Events[0].Locations[0].MapId == 3 && catalog[e.Id].Rewards[0].LocationId == "trader", "new NPC locations and reward associations arrive online");
            e.Locations[0].X = 101;
            handler.Data = JsonSerializer.SerializeToUtf8Bytes(feed);
            await service.RefreshAsync("https://example.com/feed");
            Check(ReferenceEquals(previous, service.Feed), "invalid NPC coordinates preserve the last valid feed");
            e.Locations[0].X = 10.2f;
            e.Tracking.Rewards[0].LocationId = "missing";
            handler.Data = JsonSerializer.SerializeToUtf8Bytes(feed);
            await service.RefreshAsync("https://example.com/feed");
            Check(ReferenceEquals(previous, service.Feed), "unknown reward NPC reference preserves the last valid feed");
            e.Tracking.Rewards[0].LocationId = "trader";
            e.Tracking!.Rewards[0].EarnedByQuest = 65537;
            handler.Data = JsonSerializer.SerializeToUtf8Bytes(feed);
            await service.RefreshAsync("https://example.com/feed");
            Check(ReferenceEquals(previous, service.Feed), "bad quest associations preserve the last known good feed");
            handler.Data = "{\"schemaVersion\":2,\"events\":[]}"u8.ToArray();
            await service.RefreshAsync("https://example.com/feed");
            Check(ReferenceEquals(previous, service.Feed), "unsupported schema preserves the last known good feed");
        }
        using (var offline = new EventFeedService(cache, bundle))
        {
            Check(offline.Feed.Events[0].Tracking?.Rewards.Count == 1, "online catalog and translations survive an offline restart");
            Check(offline.Feed.Events[0].Locations[0].Npc == "Online trader", "NPC map locations survive an offline restart");
        }

        foreach (var url in new[] { "http://lds-img.finalfantasyxiv.com/a.png", "https://evil.test/a.png", "https://lds-img.finalfantasyxiv.com.evil.test/a.png", "file:///C:/a.png" })
            Check(!FeedPolicy.IsImageUrl(url), "image URL rejected: " + url);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS1sAAAAASUVORK5CYII=");
        var imageHandler = new FakeHandler { Data = png };
        string? path = null;
        using (var images = new EventImages(root, imageHandler))
        {
            for (var i = 0; i < 100 && path == null; i++) { path = images.GetPath(e.ImageUrl); await Task.Delay(10); }
            Check(path != null && File.Exists(path) && imageHandler.Calls == 1, "image downloads asynchronously once and is cached");
            Check(images.GetPath("https://evil.test/a.png") == null, "image cache refuses unapproved hosts");
        }
        var offlineHandler = new FakeHandler { Data = Array.Empty<byte>() };
        using (var images = new EventImages(root, offlineHandler))
        {
            string? cached = null;
            for (var i = 0; i < 100 && cached == null; i++) { cached = images.GetPath(e.ImageUrl); await Task.Delay(10); }
            Check(cached == path && offlineHandler.Calls == 0, "cached artwork is available offline after restart");
        }
        Check(!EventImages.ValidImage("<html>invalid image</html>"u8.ToArray()), "non-image responses are rejected");
        var oversized = (byte[])png.Clone();
        oversized[16] = 0x7f;
        Check(!EventImages.ValidImage(oversized), "oversized pixel dimensions are rejected before texture loading");
        foreach (var name in new[] { "saints.jpg", "fallguys.jpg" })
        {
            using var resource = typeof(OnlineFeedChecks).Assembly.GetManifestResourceStream("Tests." + name)!;
            using var memory = new MemoryStream();
            resource.CopyTo(memory);
            Check(EventImages.ValidImage(memory.ToArray()), "actual official JPEG passes image validation: " + name);
        }
        foreach (var file in Directory.GetFiles(root)) File.Delete(file);
        Directory.Delete(root);
    }

    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public byte[] Data = Array.Empty<byte>();
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(Data) });
        }
    }
}
