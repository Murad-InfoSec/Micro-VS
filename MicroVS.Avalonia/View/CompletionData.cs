using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Micro_VS.Completion;

namespace Micro_VS.View
{
    /// <summary>One row of the completion popup, backed by an <see cref="ICodeCompletion"/> entry.</summary>
    internal sealed class CompletionData : ICompletionData
    {
        private readonly CompletionEntry entry;
        private readonly ICodeCompletion completion;
        private TextBlock description;

        public CompletionData(CompletionEntry entry, ICodeCompletion completion)
        {
            this.entry = entry;
            this.completion = completion;
        }

        public IImage Image => KindIcons.For(entry.Kind);

        // Matched against what the user types.
        public string Text => entry.FilterText;

        public object Content =>
            entry.InlineDescription.Length == 0
                ? entry.DisplayText
                : new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = entry.DisplayText },
                        new TextBlock { Text = entry.InlineDescription, Foreground = Brushes.Gray }
                    }
                };

        // Loaded lazily when the item is selected; Roslyn builds the signature + XML doc summary.
        public object Description
        {
            get
            {
                if (description == null)
                {
                    description = new TextBlock { Text = entry.DisplayText, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };
                    _ = LoadDescriptionAsync(description);
                }
                return description;
            }
        }

        public double Priority => entry.IsPreselected ? 1 : 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            TextDocument document = textArea.Document;

            CompletionEdit edit = null;
            try
            {
                // Complete() is synchronous; run Roslyn off the UI thread's context and wait (it takes milliseconds).
                edit = Task.Run(() => completion.GetEditAsync(entry)).GetAwaiter().GetResult();
            }
            catch
            {
                // Fall back to inserting the display text below.
            }

            // The edit is computed for the text when the list opened; the user may have typed more of the
            // word since, so extend the replaced range to the end of the current completion segment.
            if (edit == null || edit.Start > completionSegment.Offset + completionSegment.Length || edit.Start < 0)
            {
                document.Replace(completionSegment, entry.DisplayText);
                return;
            }

            int end = Math.Min(document.TextLength, Math.Max(completionSegment.EndOffset, edit.Start + edit.Length));
            document.Replace(edit.Start, end - edit.Start, edit.NewText);

            int insertedEnd = edit.Start + edit.NewText.Length;
            if (edit.NewCaret is int caret && caret >= edit.Start && caret <= insertedEnd)
                textArea.Caret.Offset = caret;
            else
                textArea.Caret.Offset = insertedEnd;
        }

        private async Task LoadDescriptionAsync(TextBlock target)
        {
            try
            {
                string text = await Task.Run(() => completion.GetDescriptionAsync(entry));
                if (!string.IsNullOrWhiteSpace(text))
                    target.Text = text;
            }
            catch
            {
                // Keep the display text.
            }
        }
    }

    /// <summary>Small Visual-Studio-like glyphs: a colored rounded square with a letter.</summary>
    internal static class KindIcons
    {
        private static readonly Dictionary<CompletionKind, IImage> Cache = new Dictionary<CompletionKind, IImage>();

        public static IImage For(CompletionKind kind)
        {
            if (!Cache.TryGetValue(kind, out IImage image))
                Cache[kind] = image = Create(kind);
            return image;
        }

        private static IImage Create(CompletionKind kind)
        {
            (string color, string letter) = kind switch
            {
                CompletionKind.Method => ("#7B3FBF", "M"),
                CompletionKind.ExtensionMethod => ("#7B3FBF", "X"),
                CompletionKind.Property => ("#5A5A5A", "P"),
                CompletionKind.Field => ("#1C7CB8", "F"),
                CompletionKind.Event => ("#D19A00", "E"),
                CompletionKind.Constant => ("#1C7CB8", "c"),
                CompletionKind.Local => ("#1C7CB8", "l"),
                CompletionKind.Parameter => ("#1C7CB8", "p"),
                CompletionKind.Class => ("#D68A00", "C"),
                CompletionKind.Struct => ("#2E8B57", "S"),
                CompletionKind.Interface => ("#1C7CB8", "I"),
                CompletionKind.Enum => ("#D68A00", "E"),
                CompletionKind.EnumMember => ("#1C7CB8", "e"),
                CompletionKind.Delegate => ("#7B3FBF", "D"),
                CompletionKind.TypeParameter => ("#2E8B57", "T"),
                CompletionKind.Namespace => ("#6B6B6B", "N"),
                CompletionKind.Keyword => ("#3A3A3A", "K"),
                CompletionKind.Snippet => ("#3A3A3A", "S"),
                _ => ("#8A8A8A", "•")
            };

            var text = new FormattedText(letter, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                         new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), 10, Brushes.White);
            Geometry glyph = text.BuildGeometry(new Point((16 - text.Width) / 2, (16 - text.Height) / 2));

            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing
            {
                Brush = new SolidColorBrush(Color.Parse(color)),
                Geometry = new RectangleGeometry(new Rect(0, 0, 16, 16), 3, 3)
            });
            if (glyph != null)
                drawing.Children.Add(new GeometryDrawing { Brush = Brushes.White, Geometry = glyph });
            return new DrawingImage(drawing);
        }
    }
}
