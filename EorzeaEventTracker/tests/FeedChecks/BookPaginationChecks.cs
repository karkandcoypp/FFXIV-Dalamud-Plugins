using System;
using System.Collections.Generic;
using System.Linq;
using EorzeaEventTracker;

internal static class BookPaginationChecks
{
    public static void Run()
    {
        foreach (var scale in new[] { .75f, 1f, 1.25f, 1.75f })
        {
            var heights = Enumerable.Range(0, 31).Select(i => (i % 3 == 0 ? 170f : 145f) * scale).ToList();
            var pages = BookPagination.Pack(heights, 400);
            Check(pages.Sum(p => p.Count) == 31, "pagination preserves all reward rows at scale " + scale);
            Check(pages.SelectMany(p => Enumerable.Range(p.Start, p.Count)).SequenceEqual(Enumerable.Range(0, 31)), "page ranges have no duplicate or missing rewards");
            Check(pages.All(p => heights.Skip(p.Start).Take(p.Count).Sum() <= 400), "page content fits reserved height at scale " + scale);
        }
        Check(BookPagination.Pack(Array.Empty<float>(), 200).Single() == new BookPage(0, 0), "empty section has a stable single blank leaf");
        Check(BookPagination.Pack(new float[] { 900, 10 }, 100).Count == 2, "oversized block cannot cause an infinite pagination loop");
        var text = "Длинное описание события с условиями участия и наградами. " + new string('Ж', 100);
        foreach (var width in new[] { 60f, 120f, 300f })
        {
            var lines = BookPagination.Wrap(text, width, s => s.Length * 6);
            Check(lines.All(l => l.Length * 6 <= width), "wrapped text stays inside paper width " + width);
            Check(string.Concat(lines).Replace(" ", "") == text.Replace(" ", ""), "text wrapping preserves every non-whitespace character");
        }
        var unicode = "A😀e\u0301B";
        Check(BookPagination.Wrap(unicode, 1, s => new System.Globalization.StringInfo(s).LengthInTextElements).SequenceEqual(new[] { "A", "😀", "e\u0301", "B" }), "long-token wrapping preserves Unicode graphemes");
        Check(BookPagination.Wrap("one\ntwo", 100, s => s.Length).SequenceEqual(new[] { "one", "two" }), "explicit line breaks survive pagination");
    }

    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }
}
