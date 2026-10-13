using System.Net;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TradeJ.Models;

namespace TradeJ.Services;

/// <summary>
/// Builds a single PDF (text + embedded note screenshots) meant to be uploaded to ChatGPT / Claude web for analysis.
/// </summary>
public static class TradePdfExporter
{
    public const int MaxTrades = 50;

    static TradePdfExporter() => QuestPDF.Settings.License = LicenseType.Community;

    private abstract record NoteBlock;
    private sealed record TextBlock(string Text) : NoteBlock;
    private sealed record ImageBlock(byte[] Data) : NoteBlock;
    private sealed record MissingImageBlock : NoteBlock;

    private static readonly Regex ImgTag = new(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DataUri = new(@"src\s*=\s*[""']data:image/[a-z0-9.+-]+;base64,([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static byte[] Build(IReadOnlyList<Trade> trades, int totalMatching, string filterSummary)
    {
        var closed = trades.Where(t => t.Status == TradeStatus.Closed).ToList();
        var wins = closed.Count(t => t.NetPnL > 0);
        var net = trades.Sum(t => t.NetPnL);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9.5f));

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ").FontSize(8);
                    x.CurrentPageNumber().FontSize(8);
                    x.Span(" / ").FontSize(8);
                    x.TotalPages().FontSize(8);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text("Trade journal export").FontSize(18).Bold();
                    col.Item().Text(
                        "Instructions for the AI: analyze these trades. Look for recurring mistakes, patterns by symbol / " +
                        "direction / time of day, strategy and tag performance, and how the notes and screenshots relate " +
                        "to outcomes. Give concrete, prioritized suggestions.").Italic().FontColor(Colors.Grey.Darken2);

                    col.Item().Text(filterSummary);
                    col.Item().Text(
                        $"Trades in this file: {trades.Count}" +
                        (totalMatching > trades.Count ? $" (of {totalMatching} matching; capped at {MaxTrades})" : "") +
                        $"  |  Closed: {closed.Count}  |  Win rate: {(closed.Count > 0 ? (wins * 100.0 / closed.Count).ToString("F1") : "n/a")}%" +
                        $"  |  Net P&L: {net:F2}").Bold();

                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);

                    foreach (var t in trades)
                        col.Item().Element(c => TradeBlock(c, t));
                });
            });
        }).GeneratePdf();
    }

    private static void TradeBlock(IContainer c, Trade t)
    {
        var tags = string.Join(", ", t.TradeTags.Select(x => x.Tag?.Name).Where(n => !string.IsNullOrEmpty(n)));
        var strats = string.Join(", ", t.TradeStrategies.Select(x => x.Strategy?.Name).Where(n => !string.IsNullOrEmpty(n)));
        var pnlColor = t.NetPnL >= 0 ? Colors.Green.Darken3 : Colors.Red.Darken3;

        c.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(col =>
        {
            col.Spacing(4);

            col.Item().Row(r =>
            {
                r.RelativeItem().Text($"#{t.Id}  {t.Symbol}  {t.Direction}  ({t.Status})").Bold().FontSize(11);
                r.AutoItem().Text($"Net P&L {t.NetPnL:F2}").Bold().FontColor(pnlColor);
            });

            col.Item().Text(
                $"Entry {t.EntryTime:yyyy-MM-dd HH:mm} @ {t.EntryPrice:0.#####}" +
                (t.ExitTime.HasValue ? $"  ->  Exit {t.ExitTime:yyyy-MM-dd HH:mm} @ {t.ExitPrice:0.#####}" : "  (open)") +
                $"  |  Volume {t.Volume:0.####}");

            col.Item().Text(
                $"Gross {t.GrossPnL:F2}  Commission {t.Commission:F2}  Swap {t.Swap:F2}" +
                (t.StopLoss.HasValue ? $"  |  SL {t.StopLoss:0.#####}" : "") +
                (t.TakeProfit.HasValue ? $"  TP {t.TakeProfit:0.#####}" : "") +
                (t.ActualRR.HasValue ? $"  |  Actual RR {t.ActualRR:F2}" : "") +
                $"  |  Account: {t.Account?.Name}");

            if (tags.Length > 0) col.Item().Text($"Tags: {tags}");
            if (strats.Length > 0) col.Item().Text($"Strategies: {strats}");

            foreach (var block in ParseNotes(t.Notes))
            {
                switch (block)
                {
                    case TextBlock tb:
                        col.Item().Text(tb.Text);
                        break;
                    case ImageBlock ib:
                        col.Item().AlignLeft().MaxHeight(380).Image(ib.Data).FitArea();
                        break;
                    case MissingImageBlock:
                        col.Item().Text("[screenshot not embeddable]").Italic().FontColor(Colors.Grey.Darken1);
                        break;
                }
            }
        });
    }

    /// <summary>Splits Quill HTML into ordered text and image blocks.</summary>
    private static List<NoteBlock> ParseNotes(string? html)
    {
        var blocks = new List<NoteBlock>();
        if (string.IsNullOrWhiteSpace(html)) return blocks;

        var pos = 0;
        foreach (Match m in ImgTag.Matches(html))
        {
            AddText(blocks, html[pos..m.Index]);
            pos = m.Index + m.Length;

            var d = DataUri.Match(m.Value);
            if (!d.Success) { blocks.Add(new MissingImageBlock()); continue; }
            try { blocks.Add(new ImageBlock(Convert.FromBase64String(d.Groups[1].Value))); }
            catch (FormatException) { blocks.Add(new MissingImageBlock()); }
        }
        AddText(blocks, html[pos..]);
        return blocks;
    }

    private static void AddText(List<NoteBlock> blocks, string html)
    {
        html = Regex.Replace(html, @"<(script|style)[^>]*>.*?</(script|style)>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<(br|/p|/div|/li|/tr|/h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<[^>]+>", "");
        var text = WebUtility.HtmlDecode(html);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n\s*\n+", "\n").Trim();
        if (text.Length > 0) blocks.Add(new TextBlock(text));
    }
}
