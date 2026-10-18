using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EorzeaEventTracker;

public readonly record struct BookPage(int Start, int Count);

public static class BookPagination
{
    public static List<BookPage> Pack(IReadOnlyList<float> heights, float available)
    {
        var pages = new List<BookPage>();
        var start = 0;
        var used = 0f;
        for (var i = 0; i < heights.Count; i++)
        {
            var height = float.IsFinite(heights[i]) ? Math.Max(1, heights[i]) : Math.Max(1, available);
            if (i > start && used + height > available)
            {
                pages.Add(new(start, i - start));
                start = i;
                used = 0;
            }
            used += height;
        }
        pages.Add(new(start, heights.Count - start));
        return pages;
    }

    public static List<string> Wrap(string text, float width, Func<string, float> measure)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var current = new StringBuilder();
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.Length > 0 && measure(current + " " + word) <= width) { current.Append(' ').Append(word); continue; }
                if (current.Length > 0) { lines.Add(current.ToString()); current.Clear(); }
                if (measure(word) <= width) { current.Append(word); continue; }
                var elements = StringInfo.GetTextElementEnumerator(word);
                while (elements.MoveNext())
                {
                    var element = elements.GetTextElement();
                    if (current.Length > 0 && measure(current + element) > width)
                    { lines.Add(current.ToString()); current.Clear(); }
                    current.Append(element);
                }
            }
            lines.Add(current.ToString());
        }
        return lines;
    }
}
