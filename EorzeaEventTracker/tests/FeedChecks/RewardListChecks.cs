using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using EorzeaEventTracker;

internal static class RewardListChecks
{
    public static void Run()
    {
        var rewards = new List<TrackedReward> {
            new() { Id = 1, Name = "Owned Favorite", Price = 1 },
            new() { Id = 2, Name = "Gentlebean Parka", Price = 410 },
            new() { Id = 3, Name = "Gentlebean Knit Cap", Price = 400 },
            new() { Id = 4, Name = "Ignored Shoes", Price = 100 },
            new() { Id = 5, Name = "Owned Shoes", Price = 20 },
            new() { Id = 6, Name = "Unknown Price", Price = 0 },
        };
        var settings = new CharacterSettings { FavoriteRewards = [1, 3], IgnoredRewards = [4] };
        var progress = new Dictionary<uint, RewardProgress> { [1] = new(RewardState.Owned), [5] = new(RewardState.ManuallyObtained) };
        List<uint> Select(string text = "", RewardSortMode sort = RewardSortMode.Recommended, bool remaining = false, bool favorites = false) =>
            RewardListPolicy.Select(rewards, text, sort, settings, progress, remaining, favorites).Select(r => r.Id).ToList();
        Check(Select().SequenceEqual(new uint[] { 3, 2, 6, 4, 1, 5 }), "unobtained favorites first; ignored below active goals; obtained always last");
        Check(Select("gEnTlEbEaN   cap").SequenceEqual(new uint[] { 3 }), "search is case-insensitive and supports multiple words");
        Check(Select(" ").Count == 6, "whitespace-only query is empty");
        Check(Select("missing").Count == 0, "no match is empty without altering rewards");
        Check(Select(favorites: true).SequenceEqual(new uint[] { 3, 1 }), "favorites filter keeps obtained favorites at the end");
        Check(Select(remaining: true).SequenceEqual(new uint[] { 3, 2, 6 }), "remaining filter excludes obtained and ignored");
        Check(Select("gentle", favorites: true, remaining: true).SequenceEqual(new uint[] { 3 }), "search and both filters intersect");
        settings.FavoriteRewards.Clear();
        Check(Select(sort: RewardSortMode.Price).SequenceEqual(new uint[] { 3, 2, 6, 4, 1, 5 }), "known prices ascend and unknown prices follow without crossing status groups");
        Check(Select(sort: RewardSortMode.Name).SequenceEqual(new uint[] { 3, 2, 6, 4, 1, 5 }), "alphabetical sorting stays inside groups");
        rewards[2].Price = 410;
        Check(Select(sort: (RewardSortMode)99).Take(3).SequenceEqual(new uint[] { 2, 3, 6 }), "invalid sort falls back to stable feed order");
        settings.FavoriteRewards.Add(3);
        progress[3] = new(RewardState.ManuallyObtained);
        Check(Select().IndexOf(3) > Select().IndexOf(4), "manual obtained instantly moves a favorite out of unfinished groups");
        var config = new Configuration();
        config.ForCharacter(100).RewardSort = RewardSortMode.Price;
        Check(config.ForCharacter(200).RewardSort == RewardSortMode.Recommended, "sort preference belongs to one character");
        var loaded = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config))!;
        Check(loaded.ForCharacter(100).RewardSort == RewardSortMode.Price && loaded.ForCharacter(200).RewardSort == RewardSortMode.Recommended,
            "character sort preferences survive restart");
        Console.WriteLine("PASS: reward search, stable grouped sorting, filters, manual marks and character preferences");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
