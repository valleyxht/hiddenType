using System;

namespace HiddenType.Windows.Services
{
    public static class NoteFileCodec
    {
        private const string TitlePrefix = "#title:";

        public static string Encode(string title, string content)
        {
            string safeTitle = title.Replace('\r', ' ').Replace('\n', ' ');
            return TitlePrefix + safeTitle + "\n" + content;
        }

        public static DecodedNote Decode(string storedText, string fallbackTitle)
        {
            if (!storedText.StartsWith(TitlePrefix, StringComparison.Ordinal))
                return new DecodedNote(fallbackTitle, storedText);
            int newline = storedText.IndexOf('\n');
            if (newline < 0)
                return new DecodedNote(storedText.Substring(TitlePrefix.Length).TrimEnd('\r'), string.Empty);
            return new DecodedNote(
                storedText.Substring(TitlePrefix.Length, newline - TitlePrefix.Length).TrimEnd('\r'),
                storedText.Substring(newline + 1));
        }

        public sealed class DecodedNote
        {
            public string Title { get; private set; }
            public string Content { get; private set; }
            public DecodedNote(string title, string content) { Title = title; Content = content; }
        }
    }
}
