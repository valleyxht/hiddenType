package io.github.valleyxht.hiddentype;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class NoteFileCodecTest {

    @Test
    public void roundTripPreservesBodyIncludingTrailingNewlines() {
        String content = "first line\nsecond line\n";

        NoteFileCodec.DecodedNote decoded = NoteFileCodec.decode(
            NoteFileCodec.encode("A quiet note", content),
            "fallback"
        );

        assertEquals("A quiet note", decoded.title);
        assertEquals(content, decoded.content);
    }

    @Test
    public void decodeSupportsLegacyPlainTextNotes() {
        NoteFileCodec.DecodedNote decoded = NoteFileCodec.decode(
            "legacy content\nwith two lines",
            "Imported note"
        );

        assertEquals("Imported note", decoded.title);
        assertEquals("legacy content\nwith two lines", decoded.content);
    }

    @Test
    public void encodeKeepsTitleOnOneHeaderLine() {
        NoteFileCodec.DecodedNote decoded = NoteFileCodec.decode(
            NoteFileCodec.encode("line one\nline two", "body"),
            "fallback"
        );

        assertEquals("line one line two", decoded.title);
        assertEquals("body", decoded.content);
    }
}
