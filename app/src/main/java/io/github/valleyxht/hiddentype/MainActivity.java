package io.github.valleyxht.hiddentype;

import android.animation.AnimatorListenerAdapter;
import android.content.ClipData;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.RenderEffect;
import android.graphics.Shader;
import android.net.Uri;
import android.os.Bundle;
import android.text.Editable;
import android.text.Spannable;
import android.text.SpannableStringBuilder;
import android.text.TextWatcher;
import android.text.style.ForegroundColorSpan;
import android.util.Log;
import android.view.View;
import android.widget.EditText;
import android.widget.ImageButton;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import androidx.activity.EdgeToEdge;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;

import com.google.android.material.appbar.MaterialToolbar;

import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class MainActivity extends AppCompatActivity {

    private static final String NOTES_DIR_NAME = "notes";
    private static final String SHARED_NOTES_DIR_NAME = "shared-notes";

    // UI Components
    private MaterialToolbar toolbar;
    private TextView toolbarTitle;
    private ImageButton btnToggleVisibility;
    private ImageButton btnTogglePanel;
    private ImageButton btnToggleSettings;
    private ImageButton btnToggleTheme;
    private LinearLayout settingsStrip;
    private ImageButton btnDecreaseCount;
    private ImageButton btnIncreaseCount;
    private TextView tvRevealCount;
    private EditText editText;
    private MaskedTextView tvMaskedContent;
    private TextView tvPlaceholder;
    private TextView tvCharCount;
    private LinearLayout protectedIndicator;
    private TextView tvUpdateTime;
    private LinearLayout panelContainer;
    private View dimOverlay;
    private View editorContent;
    private ListView listViewNotes;
    private ImageButton btnNewNote;

    private NotesAdapter notesAdapter;

    // Data
    private List<NotesAdapter.Note> notes = new ArrayList<>();
    private String currentId = null;
    private boolean isRevealed = false;
    private int revealCount = 5;
    private boolean isDark = true;
    private SharedPreferences preferences;
    private boolean isPanelAnimating = false;

    // Debounced save: avoid writing to disk on every keystroke
    private static final int SAVE_DELAY_MS = 500;
    private final android.os.Handler saveHandler = new android.os.Handler(android.os.Looper.getMainLooper());
    private final Runnable saveRunnable = this::doSaveCurrentNote;
    private final ExecutorService fileExecutor = Executors.newSingleThreadExecutor();

    // Date formatters (reuse instead of creating on every call)
    private java.text.SimpleDateFormat noteTitleFormat;
    private java.text.SimpleDateFormat dateFormat;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        // Release builds protect note content from screenshots, screen recording,
        // recent-app thumbnails, and non-secure displays. Debug builds remain
        // capturable so maintainers can create documentation screenshots.
        boolean isDebuggable = (getApplicationInfo().flags
            & android.content.pm.ApplicationInfo.FLAG_DEBUGGABLE) != 0;
        if (!isDebuggable) {
            getWindow().addFlags(android.view.WindowManager.LayoutParams.FLAG_SECURE);
        }

        // Load preferences
        preferences = getSharedPreferences("settings", MODE_PRIVATE);
        isDark = preferences.getBoolean("dark_mode", true);
        revealCount = preferences.getInt("reveal_count", 5);

        // Initialize date formatters
        noteTitleFormat = new java.text.SimpleDateFormat(getString(R.string.note_title_format), java.util.Locale.CHINA);
        dateFormat = new java.text.SimpleDateFormat(getString(R.string.date_format), java.util.Locale.CHINA);

        // Apply saved theme (may trigger Activity recreation on first call)
        int theme = preferences.getInt("theme", AppCompatDelegate.MODE_NIGHT_YES);
        AppCompatDelegate.setDefaultNightMode(theme);

        EdgeToEdge.enable(this);
        // EdgeToEdge.enable sets softInputMode to adjustNothing,
        // but we need adjustResize so IME insets are reported correctly
        getWindow().setSoftInputMode(android.view.WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);

        setContentView(R.layout.activity_main);

        // Handle system bars padding on root
        View mainView = findViewById(R.id.main);
        ViewCompat.setOnApplyWindowInsetsListener(mainView, (v, insets) -> {
            Insets systemBars = insets.getInsets(WindowInsetsCompat.Type.systemBars());
            v.setPadding(systemBars.left, 0, systemBars.right, systemBars.bottom);
            return insets;
        });

        // Handle IME (keyboard) padding on editor container so text is never hidden
        View editorContainer = findViewById(R.id.editorContainer);
        ViewCompat.setOnApplyWindowInsetsListener(editorContainer, (v, insets) -> {
            Insets ime = insets.getInsets(WindowInsetsCompat.Type.ime());
            v.setPadding(0, 0, 0, ime.bottom);

            // When keyboard appears, scroll editText so cursor stays visible
            if (ime.bottom > 0 && editText.hasFocus()) {
                editText.post(() -> {
                    int sel = editText.getSelectionEnd();
                    if (sel >= 0) {
                        android.text.Layout layout = editText.getLayout();
                        if (layout != null) {
                            int line = layout.getLineForOffset(sel);
                            int lineTop = layout.getLineTop(line);
                            int lineBottom = layout.getLineBottom(line);
                            int scrollY = editText.getScrollY();
                            int viewHeight = editText.getHeight() - editText.getPaddingTop() - editText.getPaddingBottom();
                            // If cursor line is not visible, scroll to bring it into view
                            if (lineBottom > scrollY + viewHeight || lineTop < scrollY) {
                                editText.scrollTo(0, Math.max(0, lineBottom - viewHeight));
                            }
                        }
                    }
                });
            }
            return insets;
        });

        initViews();
        setupListeners();

        // Restore UI state after Activity recreation (e.g. theme change)
        if (savedInstanceState != null) {
            currentId = savedInstanceState.getString("currentId");
            isRevealed = savedInstanceState.getBoolean("isRevealed", false);
        }

        loadNotes();
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);
        outState.putString("currentId", currentId);
        outState.putBoolean("isRevealed", isRevealed);
    }

    @Override
    protected void onPause() {
        super.onPause();
        saveCurrentNote();
        // Auto-hide text when app goes to background (privacy feature)
        if (isRevealed) {
            isRevealed = false;
            updateVisibilityIcon();
            refreshDisplay();
        }
    }

    private void initViews() {
        toolbar = findViewById(R.id.toolbar);
        toolbarTitle = findViewById(R.id.toolbarTitle);
        btnToggleVisibility = findViewById(R.id.btnToggleVisibility);
        btnTogglePanel = findViewById(R.id.btnTogglePanel);
        btnToggleSettings = findViewById(R.id.btnToggleSettings);
        btnToggleTheme = findViewById(R.id.btnToggleTheme);
        settingsStrip = findViewById(R.id.settingsStrip);
        btnDecreaseCount = findViewById(R.id.btnDecreaseCount);
        btnIncreaseCount = findViewById(R.id.btnIncreaseCount);
        tvRevealCount = findViewById(R.id.tvRevealCount);
        editText = findViewById(R.id.editText);
        tvMaskedContent = findViewById(R.id.tvMaskedContent);
        tvPlaceholder = findViewById(R.id.tvPlaceholder);
        tvCharCount = findViewById(R.id.tvCharCount);
        protectedIndicator = findViewById(R.id.protectedIndicator);
        tvUpdateTime = findViewById(R.id.tvUpdateTime);
        panelContainer = findViewById(R.id.panelContainer);
        dimOverlay = findViewById(R.id.dimOverlay);
        editorContent = findViewById(R.id.editorContent);
        listViewNotes = findViewById(R.id.listViewNotes);
        btnNewNote = findViewById(R.id.btnNewNote);

        tvRevealCount.setText(String.valueOf(revealCount));

        // Enable scrolling on tvMaskedContent
        tvMaskedContent.setMovementMethod(new android.text.method.ScrollingMovementMethod());

        // Disable hyphenation to prevent unexpected line breaks at CJK-Latin boundaries
        tvMaskedContent.setHyphenationFrequency(android.text.Layout.HYPHENATION_FREQUENCY_NONE);
        editText.setHyphenationFrequency(android.text.Layout.HYPHENATION_FREQUENCY_NONE);

        // Sync scroll position from editText to tvMaskedContent
        editText.setOnScrollChangeListener((v, scrollX, scrollY, oldScrollX, oldScrollY) -> {
            tvMaskedContent.scrollTo(scrollX, scrollY);
        });
    }

    private void setupListeners() {
        // Click title to rename
        toolbarTitle.setOnClickListener(v -> showRenameDialog());

        // Toggle visibility
        btnToggleVisibility.setOnClickListener(v -> {
            isRevealed = !isRevealed;
            updateVisibilityIcon();
            refreshDisplay();
        });

        // Toggle panel
        btnTogglePanel.setOnClickListener(v -> {
            if (panelContainer.getVisibility() == View.VISIBLE) {
                closePanel();
            } else {
                openPanel();
            }
        });

        // Click dim overlay to close panel
        dimOverlay.setOnClickListener(v -> closePanel());

        // Toggle settings
        btnToggleSettings.setOnClickListener(v -> {
            if (settingsStrip.getVisibility() == View.VISIBLE) {
                settingsStrip.setVisibility(View.GONE);
            } else {
                settingsStrip.setVisibility(View.VISIBLE);
                closePanel();
            }
        });

        // Toggle theme
        btnToggleTheme.setOnClickListener(v -> {
            isDark = !isDark;
            saveTheme();
            AppCompatDelegate.setDefaultNightMode(isDark ?
                AppCompatDelegate.MODE_NIGHT_YES : AppCompatDelegate.MODE_NIGHT_NO);
        });

        // Decrease reveal count
        btnDecreaseCount.setOnClickListener(v -> {
            if (revealCount > 1) {
                revealCount--;
                tvRevealCount.setText(String.valueOf(revealCount));
                saveRevealCount();
                refreshDisplay();
            }
        });

        // Increase reveal count
        btnIncreaseCount.setOnClickListener(v -> {
            if (revealCount < 50) {
                revealCount++;
                tvRevealCount.setText(String.valueOf(revealCount));
                saveRevealCount();
                refreshDisplay();
            }
        });

        // New note
        btnNewNote.setOnClickListener(v -> createNote());

        // Text input listener
        editText.addTextChangedListener(new TextWatcher() {
            @Override
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {}

            @Override
            public void onTextChanged(CharSequence s, int start, int before, int count) {}

            @Override
            public void afterTextChanged(Editable s) {
                updateCurrentNote();
                refreshDisplay();
            }
        });
    }

    private void loadNotes() {
        // Load from local storage
        notes.clear();
        migrateLegacyNotes();
        File[] noteFiles = getNotesDir().listFiles(
            file -> file.isFile() && file.getName().endsWith(".txt")
        );
        if (noteFiles != null) {
            for (File noteFile : noteFiles) {
                NotesAdapter.Note note = loadNoteFromFile(noteFile);
                if (note != null) {
                    notes.add(note);
                }
            }
        }

        // Sort by updatedAt (newest first)
        notes.sort((a, b) -> Long.compare(b.updatedAt, a.updatedAt));

        // If no notes, create initial one
        if (notes.isEmpty()) {
            createNote();
        } else if (currentId != null && findNoteById(currentId) != null) {
            // Restore previously open note after Activity recreation
            openNote(currentId);
        } else {
            openNote(notes.get(0).id);
        }

        updateNotesList();
        updateVisibilityIcon();
        refreshDisplay();
    }

    private NotesAdapter.Note loadNoteFromFile(File noteFile) {
        try {
            String fullContent = new String(
                java.nio.file.Files.readAllBytes(noteFile.toPath()),
                StandardCharsets.UTF_8
            );
            NoteFileCodec.DecodedNote decoded = NoteFileCodec.decode(
                fullContent,
                getString(R.string.new_note)
            );

            NotesAdapter.Note note = new NotesAdapter.Note();
            String fileName = noteFile.getName();
            note.id = fileName.substring(0, fileName.length() - ".txt".length());
            note.title = decoded.title;
            note.content = decoded.content;
            note.updatedAt = noteFile.lastModified();
            if (note.updatedAt <= 0) {
                note.updatedAt = System.currentTimeMillis();
            }
            return note;
        } catch (Exception e) {
            Log.e(TAG, "Failed to load note from " + noteFile.getName(), e);
            return null;
        }
    }

    private File getNotesDir() {
        File notesDir = new File(getFilesDir(), NOTES_DIR_NAME);
        if (!notesDir.exists() && !notesDir.mkdirs()) {
            Log.e(TAG, "Failed to create notes directory");
        }
        return notesDir;
    }

    /** Moves notes created by versions before 1.0 into the restricted notes directory. */
    private void migrateLegacyNotes() {
        File[] legacyFiles = getFilesDir().listFiles(
            file -> file.isFile() && file.getName().endsWith(".txt")
        );
        if (legacyFiles == null || legacyFiles.length == 0) return;

        File notesDir = getNotesDir();
        for (File legacyFile : legacyFiles) {
            File destination = new File(notesDir, legacyFile.getName());
            if (!destination.exists() && !legacyFile.renameTo(destination)) {
                Log.e(TAG, "Failed to migrate note " + legacyFile.getName());
            }
        }
    }

    private void createNote() {
        NotesAdapter.Note note = new NotesAdapter.Note();
        note.id = UUID.randomUUID().toString().substring(0, 8);
        note.title = noteTitleFormat.format(new java.util.Date());
        note.content = "";
        note.updatedAt = System.currentTimeMillis();

        notes.add(0, note);
        saveNoteToFile(note);
        openNote(note.id);
        updateNotesList();
        closePanel();
    }

    private void openNote(String id) {
        // Save current note first
        if (currentId != null) {
            saveCurrentNote();
        }

        currentId = id;
        NotesAdapter.Note note = findNoteById(id);
        if (note != null) {
            toolbarTitle.setText(note.title);
            editText.setText(note.content);
            refreshDisplay();
        }
    }

    private void updateCurrentNote() {
        if (currentId == null) return;

        NotesAdapter.Note note = findNoteById(currentId);
        if (note != null) {
            note.content = editText.getText().toString();
            note.updatedAt = System.currentTimeMillis();
            // Debounce: cancel pending save and reschedule
            saveHandler.removeCallbacks(saveRunnable);
            saveHandler.postDelayed(saveRunnable, SAVE_DELAY_MS);
        }
    }

    /** Performs the actual file save on a background thread. */
    private void doSaveCurrentNote() {
        if (currentId == null) return;
        NotesAdapter.Note note = findNoteById(currentId);
        if (note != null) {
            saveNoteToFile(note);
        }
    }

    /** Saves immediately (e.g. when switching notes or closing). */
    private void saveCurrentNote() {
        if (currentId == null) return;
        saveHandler.removeCallbacks(saveRunnable);
        NotesAdapter.Note note = findNoteById(currentId);
        if (note != null) {
            saveNoteToFile(note);
        }
    }

    private void saveNoteToFile(NotesAdapter.Note note) {
        // Capture data on the calling thread
        String id = note.id;
        String title = note.title;
        String content = note.content;
        fileExecutor.execute(() -> {
            android.util.AtomicFile atomicFile = new android.util.AtomicFile(
                new File(getNotesDir(), id + ".txt")
            );
            FileOutputStream fos = null;
            try {
                fos = atomicFile.startWrite();
                OutputStreamWriter writer = new OutputStreamWriter(fos, StandardCharsets.UTF_8);
                writer.write(NoteFileCodec.encode(title, content));
                writer.flush();
                atomicFile.finishWrite(fos);
            } catch (Exception e) {
                if (fos != null) {
                    atomicFile.failWrite(fos);
                }
                Log.e(TAG, "Failed to save note " + id, e);
            }
        });
    }

    private NotesAdapter.Note findNoteById(String id) {
        for (NotesAdapter.Note note : notes) {
            if (note.id.equals(id)) {
                return note;
            }
        }
        return null;
    }

    private void refreshDisplay() {
        String content = editText.getText().toString();

        // Update placeholder
        if (content.isEmpty()) {
            tvPlaceholder.setVisibility(View.VISIBLE);
            tvMaskedContent.setVisibility(View.GONE);
            tvMaskedContent.setMaskedCharacterEnd(0);
        } else {
            tvPlaceholder.setVisibility(View.GONE);

            if (isRevealed) {
                // Revealed: show text directly in tvMaskedContent
                tvMaskedContent.setVisibility(View.VISIBLE);
                tvMaskedContent.setMaskedCharacterEnd(0);
                tvMaskedContent.setText(content);
            } else {
                // Masked: show same text in tvMaskedContent but with
                // ForegroundColorSpan on hidden chars to make them look like blocks
                tvMaskedContent.setVisibility(View.VISIBLE);
                applyMaskDisplay(content, revealCount);
            }
        }

        // Update char count
        int charCount = content.length();
        tvCharCount.setText(charCount + " " + getString(R.string.char_count));

        // Update protected indicator
        if (!isRevealed && charCount > 0) {
            protectedIndicator.setVisibility(View.VISIBLE);
        } else {
            protectedIndicator.setVisibility(View.GONE);
        }

        // Update time
        NotesAdapter.Note note = findNoteById(currentId);
        if (note != null) {
            tvUpdateTime.setText(formatDate(note.updatedAt));
        }
    }

    /**
     * Display masked text in tvMaskedContent.
     * Uses the EXACT SAME text as editText (so line wrapping is identical),
     * The real glyphs remain faintly visible while MaskedTextView paints a
     * translucent veil and diagonal texture behind the hidden region.
     */
    private void applyMaskDisplay(String text, int visibleCount) {
        if (text.length() <= visibleCount) {
            tvMaskedContent.setMaskedCharacterEnd(0);
            tvMaskedContent.setText(text);
            return;
        }

        int hiddenCount = findMaskEnd(text, visibleCount);
        SpannableStringBuilder ssb = new SpannableStringBuilder(text);

        // Keep the real glyphs at roughly 24% opacity for nearby context.
        int faintTextColor = (tvMaskedContent.getCurrentTextColor() & 0x00FFFFFF) | (61 << 24);
        ssb.setSpan(new ForegroundColorSpan(faintTextColor), 0, hiddenCount,
                Spannable.SPAN_EXCLUSIVE_EXCLUSIVE);

        tvMaskedContent.setMaskedCharacterEnd(hiddenCount);
        tvMaskedContent.setText(ssb);
    }

    private static int findMaskEnd(String text, int visibleCount) {
        int index = text.length();
        int visible = 0;
        while (index > 0 && visible < visibleCount) {
            int codePoint = text.codePointBefore(index);
            index -= Character.charCount(codePoint);
            if (!Character.isWhitespace(codePoint) && codePoint != 0x3000) {
                visible++;
            }
        }
        return visible < visibleCount ? 0 : index;
    }

    private String formatDate(long timestamp) {
        long diff = System.currentTimeMillis() - timestamp;
        if (diff < 60000) return getString(R.string.just_now);
        if (diff < 3600000) return (diff / 60000) + " " + getString(R.string.minutes_ago);
        if (diff < 86400000) return (diff / 3600000) + " " + getString(R.string.hours_ago);
        return dateFormat.format(new java.util.Date(timestamp));
    }

    private void updateNotesList() {
        if (notesAdapter == null) {
            notesAdapter = new NotesAdapter(this, notes, currentId, new NotesAdapter.OnNoteActionListener() {
                @Override
                public void onNoteClick(NotesAdapter.Note note) {
                    openNote(note.id);
                    notesAdapter.setCurrentId(currentId);
                    closePanel();
                }

                @Override
                public void onNoteRename(NotesAdapter.Note note) {
                    showRenameDialogForNote(note);
                }

                @Override
                public void onNoteDelete(NotesAdapter.Note note) {
                    deleteNote(note.id);
                }

                @Override
                public void onNoteShare(NotesAdapter.Note note) {
                    shareNoteAsText(note);
                }
            });
            listViewNotes.setAdapter(notesAdapter);
        } else {
            notesAdapter.setCurrentId(currentId);
            notesAdapter.notifyDataSetChanged();
        }
    }

    private void deleteNote(String id) {
        if (notes.size() <= 1) {
            Toast.makeText(this, R.string.min_note_count, Toast.LENGTH_SHORT).show();
            return;
        }

        NotesAdapter.Note note = findNoteById(id);
        String name = note != null ? note.title : getString(R.string.untitled);

        View dialogView = getLayoutInflater().inflate(R.layout.dialog_note_action, null);
        TextView dialogTitle = dialogView.findViewById(R.id.dialogTitle);
        TextView dialogMessage = dialogView.findViewById(R.id.dialogMessage);
        TextView btnConfirm = dialogView.findViewById(R.id.btnConfirm);
        TextView btnCancel = dialogView.findViewById(R.id.btnCancel);

        dialogTitle.setText(R.string.confirm_delete);
        dialogMessage.setText(getString(R.string.confirm_delete_message, name));
        dialogMessage.setVisibility(View.VISIBLE);
        btnConfirm.setText(R.string.delete);
        android.util.TypedValue errorColor = new android.util.TypedValue();
        getTheme().resolveAttribute(android.R.attr.colorError, errorColor, true);
        btnConfirm.setTextColor(errorColor.data);

        android.app.Dialog dialog = new android.app.Dialog(this);
        dialog.setContentView(dialogView);
        dialog.getWindow().setBackgroundDrawable(new android.graphics.drawable.ColorDrawable(android.graphics.Color.TRANSPARENT));
        dialog.getWindow().setLayout(
            (int) (getResources().getDisplayMetrics().widthPixels * 0.82),
            android.view.ViewGroup.LayoutParams.WRAP_CONTENT);

        btnCancel.setOnClickListener(v -> dialog.dismiss());
        btnConfirm.setOnClickListener(v -> {
            notes.removeIf(n -> n.id.equals(id));
            new File(getNotesDir(), id + ".txt").delete();
            if (id.equals(currentId) && !notes.isEmpty()) {
                openNote(notes.get(0).id);
            }
            updateNotesList();
            Toast.makeText(this, R.string.file_deleted, Toast.LENGTH_SHORT).show();
            dialog.dismiss();
        });

        dialog.show();
    }

    private void updateVisibilityIcon() {
        if (isRevealed) {
            btnToggleVisibility.setImageResource(R.drawable.ic_visibility);
            btnToggleVisibility.setContentDescription(getString(R.string.hide_text));
        } else {
            btnToggleVisibility.setImageResource(R.drawable.ic_visibility_off);
            btnToggleVisibility.setContentDescription(getString(R.string.show_all));
        }
    }

    private void openPanel() {
        if (isPanelAnimating) return;
        isPanelAnimating = true;
        settingsStrip.setVisibility(View.GONE);

        // Prepare panel: position off-screen right, then make visible
        panelContainer.setTranslationX(panelContainer.getWidth());
        panelContainer.setVisibility(View.VISIBLE);

        // Animate panel sliding in from right
        panelContainer.animate()
            .translationX(0)
            .setDuration(280)
            .setInterpolator(new android.view.animation.DecelerateInterpolator(1.5f))
            .withEndAction(() -> isPanelAnimating = false)
            .start();

        // Animate dim overlay fading in
        dimOverlay.setAlpha(0f);
        dimOverlay.setVisibility(View.VISIBLE);
        dimOverlay.animate()
            .alpha(1f)
            .setDuration(280)
            .start();

        // Apply blur effect to editor content
        editorContent.setRenderEffect(
            RenderEffect.createBlurEffect(8f, 8f, Shader.TileMode.CLAMP)
        );
    }

    private void closePanel() {
        if (isPanelAnimating) return;
        isPanelAnimating = true;

        // Animate panel sliding out to right
        panelContainer.animate()
            .translationX(panelContainer.getWidth())
            .setDuration(220)
            .setInterpolator(new android.view.animation.AccelerateInterpolator(1.2f))
            .withEndAction(() -> {
                panelContainer.setVisibility(View.GONE);
                panelContainer.setTranslationX(0);
                isPanelAnimating = false;
            })
            .start();

        // Animate dim overlay fading out
        dimOverlay.animate()
            .alpha(0f)
            .setDuration(220)
            .withEndAction(() -> {
                dimOverlay.setVisibility(View.GONE);
                dimOverlay.setAlpha(1f);
            })
            .start();

        // Remove blur effect
        editorContent.setRenderEffect(null);
    }

    private static final String TAG = "HiddenType";

    private void shareNoteAsText(NotesAdapter.Note note) {
        // Capture the in-memory values so the latest keystrokes are included even if
        // the debounced disk save has not run yet.
        String title = note.title;
        String content = note.content;

        fileExecutor.execute(() -> {
            try {
                File shareDir = new File(getCacheDir(), SHARED_NOTES_DIR_NAME);
                if (!shareDir.exists() && !shareDir.mkdirs()) {
                    throw new IllegalStateException("Failed to create share cache directory");
                }

                // Remove stale share copies. The original notes remain in private storage.
                File[] staleFiles = shareDir.listFiles();
                if (staleFiles != null) {
                    for (File staleFile : staleFiles) {
                        if (staleFile.isFile() && !staleFile.delete()) {
                            Log.w(TAG, "Failed to delete stale share file " + staleFile.getName());
                        }
                    }
                }

                File shareFile = new File(shareDir, buildShareFileName(title));
                try (OutputStreamWriter writer = new OutputStreamWriter(
                        new FileOutputStream(shareFile), StandardCharsets.UTF_8)) {
                    writer.write(content);
                }

                Uri contentUri = androidx.core.content.FileProvider.getUriForFile(
                    this,
                    getPackageName() + ".fileprovider",
                    shareFile
                );

                Intent sendIntent = new Intent(Intent.ACTION_SEND);
                sendIntent.setType("text/plain");
                sendIntent.putExtra(Intent.EXTRA_STREAM, contentUri);
                sendIntent.putExtra(Intent.EXTRA_TEXT, content);
                sendIntent.putExtra(Intent.EXTRA_SUBJECT, title);
                sendIntent.setClipData(ClipData.newRawUri(title, contentUri));
                sendIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);

                runOnUiThread(() -> {
                    try {
                        startActivity(Intent.createChooser(
                            sendIntent,
                            getString(R.string.share_note_chooser)
                        ));
                    } catch (Exception e) {
                        Log.e(TAG, "Unable to launch share chooser", e);
                        Toast.makeText(this, R.string.share_failed, Toast.LENGTH_SHORT).show();
                    }
                });
            } catch (Exception e) {
                Log.e(TAG, "shareNoteAsText failed", e);
                runOnUiThread(() -> Toast.makeText(
                    this,
                    R.string.share_failed,
                    Toast.LENGTH_SHORT
                ).show());
            }
        });
    }

    private String buildShareFileName(String title) {
        String safeTitle = title
            .replaceAll("[\\\\/:*?\"<>|\\p{Cntrl}]", "_")
            .trim();
        if (safeTitle.isEmpty()) {
            safeTitle = getString(R.string.untitled);
        }
        if (safeTitle.length() > 80) {
            safeTitle = safeTitle.substring(0, 80).trim();
        }
        return safeTitle + ".txt";
    }

    private void showRenameDialog() {
        if (currentId == null) return;
        NotesAdapter.Note note = findNoteById(currentId);
        if (note != null) {
            showRenameDialogForNote(note);
        }
    }

    private void showRenameDialogForNote(NotesAdapter.Note note) {
        View dialogView = getLayoutInflater().inflate(R.layout.dialog_note_action, null);
        TextView dialogTitle = dialogView.findViewById(R.id.dialogTitle);
        EditText dialogInput = dialogView.findViewById(R.id.dialogInput);
        TextView btnConfirm = dialogView.findViewById(R.id.btnConfirm);
        TextView btnCancel = dialogView.findViewById(R.id.btnCancel);

        dialogTitle.setText(R.string.rename);
        dialogInput.setVisibility(View.VISIBLE);
        dialogInput.setText(note.title);
        dialogInput.setSelection(note.title.length());
        btnConfirm.setText(R.string.save_file);

        android.app.Dialog dialog = new android.app.Dialog(this);
        dialog.setContentView(dialogView);
        dialog.getWindow().setBackgroundDrawable(new android.graphics.drawable.ColorDrawable(android.graphics.Color.TRANSPARENT));
        dialog.getWindow().setLayout(
            (int) (getResources().getDisplayMetrics().widthPixels * 0.82),
            android.view.ViewGroup.LayoutParams.WRAP_CONTENT);

        btnCancel.setOnClickListener(v -> dialog.dismiss());
        btnConfirm.setOnClickListener(v -> {
            String newTitle = dialogInput.getText().toString().trim();
            if (newTitle.isEmpty()) {
                newTitle = getString(R.string.untitled);
            }
            renameNote(note.id, newTitle);
            dialog.dismiss();
        });

        dialog.show();
        dialogInput.requestFocus();
        android.view.inputmethod.InputMethodManager imm =
            (android.view.inputmethod.InputMethodManager) getSystemService(INPUT_METHOD_SERVICE);
        imm.showSoftInput(dialogInput, android.view.inputmethod.InputMethodManager.SHOW_IMPLICIT);
    }

    private void renameNote(String id, String newTitle) {
        NotesAdapter.Note note = findNoteById(id);
        if (note != null) {
            note.title = newTitle;
            note.updatedAt = System.currentTimeMillis();
            saveNoteToFile(note);

            if (id.equals(currentId)) {
                toolbarTitle.setText(newTitle);
            }

            updateNotesList();
            Toast.makeText(this, R.string.title_saved, Toast.LENGTH_SHORT).show();
        }
    }

    private void saveTheme() {
        int theme = isDark ? AppCompatDelegate.MODE_NIGHT_YES : AppCompatDelegate.MODE_NIGHT_NO;
        preferences.edit()
                .putBoolean("dark_mode", isDark)
                .putInt("theme", theme)
                .apply();
    }

    private void saveRevealCount() {
        preferences.edit().putInt("reveal_count", revealCount).apply();
    }
}
