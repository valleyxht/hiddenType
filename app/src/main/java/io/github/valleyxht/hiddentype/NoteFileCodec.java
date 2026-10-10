package io.github.valleyxht.hiddentype;

/** Encodes the small, backwards-compatible text format used for local notes. */
final class NoteFileCodec {

    private static final String TITLE_PREFIX = "#title:";

    private NoteFileCodec() {}

    static String encode(String title, String content) {
        String safeTitle = title.replace('\r', ' ').replace('\n', ' ');
        return TITLE_PREFIX + safeTitle + "\n" + content;
    }

    static DecodedNote decode(String storedText, String fallbackTitle) {
        if (!storedText.startsWith(TITLE_PREFIX)) {
            return new DecodedNote(fallbackTitle, storedText);
        }

        int newlineIndex = storedText.indexOf('\n');
        if (newlineIndex < 0) {
            return new DecodedNote(storedText.substring(TITLE_PREFIX.length()), "");
        }

        return new DecodedNote(
            storedText.substring(TITLE_PREFIX.length(), newlineIndex),
            storedText.substring(newlineIndex + 1)
        );
    }

    static final class DecodedNote {
        final String title;
        final String content;

        DecodedNote(String title, String content) {
            this.title = title;
            this.content = content;
        }
    }
}
