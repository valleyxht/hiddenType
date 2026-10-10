using System;
using HiddenType.Windows.Services;

internal static class CodecTests
{
    private static void Equal(string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException("Expected [" + expected + "], got [" + actual + "]");
    }

    public static void Main()
    {
        string content = "第一行\nsecond line\n";
        NoteFileCodec.DecodedNote roundTrip = NoteFileCodec.Decode(NoteFileCodec.Encode("安静的笔记", content), "fallback");
        Equal("安静的笔记", roundTrip.Title);
        Equal(content, roundTrip.Content);

        NoteFileCodec.DecodedNote legacy = NoteFileCodec.Decode("旧内容\n第二行", "导入的笔记");
        Equal("导入的笔记", legacy.Title);
        Equal("旧内容\n第二行", legacy.Content);

        NoteFileCodec.DecodedNote oneLineTitle = NoteFileCodec.Decode(NoteFileCodec.Encode("第一行\n第二行", "正文"), "fallback");
        Equal("第一行 第二行", oneLineTitle.Title);
        Equal("正文", oneLineTitle.Content);
        Console.WriteLine("All codec tests passed.");
    }
}
