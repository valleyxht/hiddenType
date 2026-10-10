using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using HiddenType.Windows.Models;
using HiddenType.Windows.Services;

namespace HiddenType.Windows.UI
{
public sealed class MainForm : Form
{
    private readonly NoteRepository _repository;
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly BindingList<Note> _notes = new BindingList<Note>();
    private readonly System.Windows.Forms.Timer _saveTimer = new System.Windows.Forms.Timer { Interval = 500 };

    private readonly Panel _sidebar = new Panel();
    private readonly Panel _windowBar = new Panel();
    private readonly Panel _toolbar = new Panel();
    private readonly Panel _settingsBar = new Panel();
    private readonly Panel _statusBar = new Panel();
    private readonly ListBox _noteList = new ListBox();
    private readonly MaskedRichTextBox _editor = new MaskedRichTextBox();
    private readonly Label _titleLabel = new Label();
    private readonly Label _countLabel = new Label();
    private readonly Label _statusLabel = new Label();
    private readonly Label _updatedLabel = new Label();
    private readonly Button _revealButton = new Button();
    private readonly Button _themeButton = new Button();
    private readonly Button _settingsButton = new Button();
    private readonly Button _minimizeButton = new Button();
    private readonly Button _maximizeButton = new Button();
    private readonly Button _closeButton = new Button();
    private readonly Label _windowTitle = new Label();
    private readonly PictureBox _appIcon = new PictureBox();

    private Note _current;
    private bool _loading;
    private bool _revealed;

    public MainForm(NoteRepository repository, SettingsStore settingsStore)
    {
        _repository = repository;
        _settingsStore = settingsStore;
        _settings = settingsStore.Load();

        Text = "HiddenType · 隐藏码字";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 500);
        Size = new Size(_settings.WindowWidth, _settings.WindowHeight);
        Font = new Font("Microsoft YaHei UI", 9f);
        KeyPreview = true;
        FormBorderStyle = FormBorderStyle.None;
        Padding = new Padding(1);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        BuildUi();
        WireEvents();
        ApplyTheme();
        LoadNotes();
    }

    private void BuildUi()
    {
        BuildWindowBar();

        _sidebar.Dock = DockStyle.Left;
        _sidebar.Width = 252;
        _sidebar.Padding = new Padding(12);

        var sidebarTitle = new Label
        {
            Text = "私密笔记",
            Dock = DockStyle.Top,
            Height = 34,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font, FontStyle.Bold)
        };
        var newButton = MakeButton("＋ 新建笔记", 38);
        newButton.Dock = DockStyle.Bottom;
        newButton.Click += (sender, args) => CreateNote();

        var noteActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        var renameButton = MakeButton("重命名", 32, 70);
        var exportButton = MakeButton("导出", 32, 62);
        var deleteButton = MakeButton("删除", 32, 62);
        renameButton.Click += (sender, args) => RenameCurrent();
        exportButton.Click += (sender, args) => ExportCurrent();
        deleteButton.Click += (sender, args) => DeleteCurrent();
        noteActions.Controls.AddRange(new Control[] { renameButton, exportButton, deleteButton });

        _noteList.Dock = DockStyle.Fill;
        _noteList.BorderStyle = BorderStyle.None;
        _noteList.IntegralHeight = false;
        _noteList.DrawMode = DrawMode.OwnerDrawFixed;
        _noteList.ItemHeight = 58;
        _noteList.DataSource = _notes;

        _sidebar.Controls.Add(_noteList);
        _sidebar.Controls.Add(noteActions);
        _sidebar.Controls.Add(newButton);
        _sidebar.Controls.Add(sidebarTitle);

        _toolbar.Dock = DockStyle.Top;
        _toolbar.Height = 58;
        _toolbar.Padding = new Padding(20, 9, 12, 9);

        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        _titleLabel.Font = new Font(Font.FontFamily, 11f, FontStyle.Bold);
        _titleLabel.Cursor = Cursors.Hand;

        var toolbarActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 300,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        ConfigureToolbarButton(_themeButton, _settings.DarkMode ? "☀  浅色" : "●  深色", 86);
        ConfigureToolbarButton(_settingsButton, "⚙  设置", 82);
        ConfigureToolbarButton(_revealButton, "◉  显示全文", 108);
        toolbarActions.Controls.AddRange(new Control[] { _themeButton, _settingsButton, _revealButton });
        _toolbar.Controls.Add(_titleLabel);
        _toolbar.Controls.Add(toolbarActions);

        _settingsBar.Dock = DockStyle.Top;
        _settingsBar.Height = 46;
        _settingsBar.Padding = new Padding(20, 6, 12, 6);
        _settingsBar.Visible = false;
        var settingCaption = new Label
        {
            Text = "可见字符",
            AutoSize = false,
            Width = 86,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var minus = MakeButton("−", 32, 36);
        var plus = MakeButton("＋", 32, 36);
        minus.Dock = DockStyle.Left;
        plus.Dock = DockStyle.Left;
        _countLabel.Dock = DockStyle.Left;
        _countLabel.Width = 48;
        _countLabel.TextAlign = ContentAlignment.MiddleCenter;
        _countLabel.Font = new Font("Consolas", 10f, FontStyle.Bold);
        minus.Click += (sender, args) => ChangeRevealCount(-1);
        plus.Click += (sender, args) => ChangeRevealCount(1);
        _settingsBar.Controls.Add(plus);
        _settingsBar.Controls.Add(_countLabel);
        _settingsBar.Controls.Add(minus);
        _settingsBar.Controls.Add(settingCaption);

        _statusBar.Dock = DockStyle.Bottom;
        _statusBar.Height = 38;
        _statusBar.Padding = new Padding(20, 0, 20, 0);
        _statusLabel.Dock = DockStyle.Left;
        _statusLabel.Width = 260;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _updatedLabel.Dock = DockStyle.Right;
        _updatedLabel.Width = 180;
        _updatedLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusBar.Controls.Add(_statusLabel);
        _statusBar.Controls.Add(_updatedLabel);

        var editorHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 24, 22, 12) };
        _editor.Dock = DockStyle.Fill;
        _editor.RevealCount = _settings.RevealCount;
        editorHost.Controls.Add(_editor);

        var content = new Panel { Dock = DockStyle.Fill };
        content.Controls.Add(editorHost);
        content.Controls.Add(_statusBar);
        content.Controls.Add(_settingsBar);
        content.Controls.Add(_toolbar);

        var workspace = new Panel { Dock = DockStyle.Fill };
        workspace.Controls.Add(content);
        workspace.Controls.Add(_sidebar);
        Controls.Add(workspace);
        Controls.Add(_windowBar);
    }

    private void BuildWindowBar()
    {
        _windowBar.Dock = DockStyle.Top;
        _windowBar.Height = 42;
        _windowBar.Padding = new Padding(12, 0, 0, 0);

        _appIcon.Dock = DockStyle.Left;
        _appIcon.Width = 30;
        _appIcon.SizeMode = PictureBoxSizeMode.CenterImage;
        _appIcon.Image = new Icon(Icon, new Size(20, 20)).ToBitmap();

        _windowTitle.Text = "HiddenType";
        _windowTitle.Dock = DockStyle.Fill;
        _windowTitle.TextAlign = ContentAlignment.MiddleLeft;
        _windowTitle.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);

        ConfigureWindowButton(_minimizeButton, "—");
        ConfigureWindowButton(_maximizeButton, "□");
        ConfigureWindowButton(_closeButton, "×");
        _minimizeButton.Click += (sender, args) => WindowState = FormWindowState.Minimized;
        _maximizeButton.Click += (sender, args) => ToggleMaximize();
        _closeButton.Click += (sender, args) => Close();

        _windowBar.Controls.Add(_windowTitle);
        _windowBar.Controls.Add(_minimizeButton);
        _windowBar.Controls.Add(_maximizeButton);
        _windowBar.Controls.Add(_closeButton);
        _windowBar.Controls.Add(_appIcon);

        _windowBar.MouseDown += BeginWindowDrag;
        _windowTitle.MouseDown += BeginWindowDrag;
        _appIcon.MouseDown += BeginWindowDrag;
        _windowBar.DoubleClick += (sender, args) => ToggleMaximize();
        _windowTitle.DoubleClick += (sender, args) => ToggleMaximize();
    }

    private void WireEvents()
    {
        _noteList.SelectedIndexChanged += (sender, args) =>
        {
            Note note = _noteList.SelectedItem as Note;
            if (!_loading && note != null && note != _current) OpenNote(note);
        };
        _noteList.DoubleClick += (sender, args) => RenameCurrent();
        _noteList.DrawItem += DrawNoteItem;
        _editor.TextChanged += (sender, args) => EditorTextChanged();
        _titleLabel.DoubleClick += (sender, args) => RenameCurrent();
        _revealButton.Click += (sender, args) => ToggleReveal();
        _settingsButton.Click += (sender, args) => _settingsBar.Visible = !_settingsBar.Visible;
        _themeButton.Click += (sender, args) =>
        {
            _settings.DarkMode = !_settings.DarkMode;
            ApplyTheme();
            SaveSettings();
        };
        _saveTimer.Tick += (sender, args) =>
        {
            _saveTimer.Stop();
            SaveCurrent();
        };
        Deactivate += (sender, args) =>
        {
            SaveCurrent();
            if (_revealed) SetRevealed(false);
        };
        Shown += (sender, args) =>
        {
            if (_current != null) _noteList.SelectedItem = _current;
            UpdateStatus();
            _editor.Focus();
        };
        FormClosing += (sender, args) =>
        {
            SaveCurrent();
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
            SaveSettings();
        };
        KeyDown += HandleShortcut;
    }

    private void LoadNotes()
    {
        _loading = true;
        foreach (Note note in _repository.LoadAll()) _notes.Add(note);
        if (_notes.Count == 0) _notes.Add(_repository.Create());
        _loading = false;
        if (_notes.Count > 0) OpenNote(_notes[0]);
    }

    private void OpenNote(Note note)
    {
        SaveCurrent();
        _current = note;
        _loading = true;
        _editor.Text = note.Content;
        _editor.SelectionStart = _editor.TextLength;
        _loading = false;
        _titleLabel.Text = note.Title;
        SetRevealed(false);
        UpdateStatus();
        _editor.Focus();
    }

    private void CreateNote()
    {
        SaveCurrent();
        Note note = _repository.Create();
        _notes.Insert(0, note);
        _noteList.SelectedItem = note;
    }

    private void EditorTextChanged()
    {
        if (_loading || _current == null) return;
        _current.Content = _editor.Text;
        _current.UpdatedAt = DateTime.Now;
        _saveTimer.Stop();
        _saveTimer.Start();
        UpdateStatus();
    }

    private void SaveCurrent()
    {
        _saveTimer.Stop();
        if (_current == null) return;
        try
        {
            _current.Content = _editor.Text;
            _repository.Save(_current);
            _noteList.Invalidate();
            UpdateStatus();
        }
        catch (IOException ex)
        {
            MessageBox.Show(this, "无法保存笔记：" + ex.Message, "HiddenType", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RenameCurrent()
    {
        if (_current == null) return;
        string title = PromptDialog.Show(this, "重命名笔记", _current.Title);
        if (title == null) return;
        _current.Title = title.Replace('\r', ' ').Replace('\n', ' ');
        SaveCurrent();
        _titleLabel.Text = _current.Title;
        _noteList.Invalidate();
    }

    private void DeleteCurrent()
    {
        if (_current == null) return;
        if (_notes.Count <= 1)
        {
            MessageBox.Show(this, "至少保留一个笔记。", "HiddenType", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(this, "确定要删除「" + _current.Title + "」吗？", "删除笔记",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        Note deleted = _current;
        int nextIndex = Math.Max(0, _noteList.SelectedIndex - 1);
        _repository.Delete(deleted);
        _notes.Remove(deleted);
        _noteList.SelectedIndex = Math.Min(nextIndex, _notes.Count - 1);
    }

    private void ExportCurrent()
    {
        if (_current == null) return;
        SaveCurrent();
        using (var dialog = new SaveFileDialog
        {
            Title = "导出纯文本",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            FileName = SanitizeFileName(_current.Title) + ".txt",
            AddExtension = true,
            DefaultExt = "txt"
        })
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                File.WriteAllText(dialog.FileName, _current.Content, new System.Text.UTF8Encoding(false));
            }
            catch (IOException ex)
            {
                MessageBox.Show(this, "无法导出笔记：" + ex.Message, "HiddenType", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ToggleReveal() { SetRevealed(!_revealed); }

    private void SetRevealed(bool revealed)
    {
        _revealed = revealed;
        _editor.Revealed = revealed;
        _revealButton.Text = revealed ? "◌  恢复遮罩" : "◉  显示全文";
        UpdateStatus();
    }

    private void ChangeRevealCount(int delta)
    {
        _settings.RevealCount = Math.Max(1, Math.Min(50, _settings.RevealCount + delta));
        _countLabel.Text = _settings.RevealCount.ToString();
        _editor.RevealCount = _settings.RevealCount;
        SaveSettings();
    }

    private void UpdateStatus()
    {
        int count = _editor.TextLength;
        string privacy = _revealed ? "已显示全文" : "● 保护中";
        _statusLabel.Text = count + " 字  ·  " + privacy;
        _updatedLabel.Text = _current == null ? string.Empty : FormatRelativeTime(_current.UpdatedAt);
    }

    private void ApplyTheme()
    {
        var palette = _settings.DarkMode
            ? new Palette(Color.FromArgb(14, 14, 17), Color.FromArgb(20, 20, 25), Color.FromArgb(28, 28, 35),
                Color.FromArgb(225, 225, 230), Color.FromArgb(145, 145, 158), Color.FromArgb(92, 92, 108), Color.FromArgb(232, 197, 71))
            : new Palette(Color.FromArgb(250, 249, 247), Color.White, Color.FromArgb(240, 237, 232),
                Color.FromArgb(35, 35, 40), Color.FromArgb(105, 103, 99), Color.FromArgb(153, 153, 153), Color.FromArgb(190, 139, 0));

        BackColor = palette.Canvas;
        ForeColor = palette.Text;
        _sidebar.BackColor = palette.Surface;
        _windowBar.BackColor = palette.Surface;
        _toolbar.BackColor = palette.Surface;
        _settingsBar.BackColor = palette.Highlight;
        _statusBar.BackColor = palette.Surface;
        _noteList.BackColor = palette.Surface;
        _noteList.ForeColor = palette.Text;
        _editor.SetPalette(palette.Text, palette.Canvas, palette.Mask);
        SetColorsRecursive(this, palette);
        _statusLabel.ForeColor = palette.Accent;
        _windowTitle.ForeColor = palette.Text;
        _minimizeButton.BackColor = palette.Surface;
        _maximizeButton.BackColor = palette.Surface;
        _closeButton.BackColor = palette.Surface;
        _minimizeButton.FlatAppearance.BorderColor = palette.Surface;
        _maximizeButton.FlatAppearance.BorderColor = palette.Surface;
        _closeButton.FlatAppearance.BorderColor = palette.Surface;
        _minimizeButton.FlatAppearance.MouseOverBackColor = palette.Highlight;
        _maximizeButton.FlatAppearance.MouseOverBackColor = palette.Highlight;
        _closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(196, 43, 28);
        _themeButton.Text = _settings.DarkMode ? "☀  浅色" : "●  深色";
        _countLabel.Text = _settings.RevealCount.ToString();
        _noteList.Invalidate();
    }

    private static void SetColorsRecursive(Control parent, Palette palette)
    {
        foreach (Control control in parent.Controls)
        {
            Button button = control as Button;
            Label label = control as Label;
            if (button != null)
            {
                button.BackColor = palette.Highlight;
                button.ForeColor = palette.Text;
                button.FlatAppearance.BorderColor = palette.Highlight;
                button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(palette.Highlight, .08f);
            }
            else if (label != null)
            {
                label.ForeColor = palette.Muted;
            }
            SetColorsRecursive(control, palette);
        }
    }

    private void DrawNoteItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _notes.Count) return;
        Note note = _notes[e.Index];
        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color background = selected
            ? (_settings.DarkMode ? Color.FromArgb(37, 37, 47) : Color.FromArgb(235, 231, 225))
            : _noteList.BackColor;
        using (var backgroundBrush = new SolidBrush(background))
        using (var titleBrush = new SolidBrush(_noteList.ForeColor))
        using (var mutedBrush = new SolidBrush(_settings.DarkMode ? Color.FromArgb(125, 125, 140) : Color.FromArgb(120, 116, 110)))
        {
            e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
            var titleBounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 8, e.Bounds.Width - 20, 23);
            var timeBounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 33, e.Bounds.Width - 20, 18);
            TextRenderer.DrawText(e.Graphics, note.Title, Font, titleBounds, titleBrush.Color,
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, FormatRelativeTime(note.UpdatedAt), Font, timeBounds, mutedBrush.Color,
                TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
        }
        e.DrawFocusRectangle();
    }

    private void HandleShortcut(object sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.N) { CreateNote(); e.SuppressKeyPress = true; }
        else if (e.Control && e.KeyCode == Keys.S) { SaveCurrent(); e.SuppressKeyPress = true; }
        else if (e.Control && e.Shift && e.KeyCode == Keys.E) { ExportCurrent(); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.F2) { RenameCurrent(); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.F10) { ToggleReveal(); e.SuppressKeyPress = true; }
    }

    private void SaveSettings()
    {
        try { _settingsStore.Save(_settings); }
        catch (IOException) { /* Keep writing uninterrupted; retry on the next setting change. */ }
    }

    private static Button MakeButton(string text, int height, int width = 0)
    {
        return new Button
        {
            Text = text,
            Height = height,
            Width = width,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabStop = false,
            Margin = new Padding(0, 0, 6, 0)
        };
    }

    private static void ConfigureToolbarButton(Button button, string text, int width)
    {
        button.Text = text;
        button.Width = width;
        button.Height = 38;
        button.FlatStyle = FlatStyle.Flat;
        button.Cursor = Cursors.Hand;
        button.TabStop = false;
        button.Margin = new Padding(6, 0, 0, 0);
    }

    private static void ConfigureWindowButton(Button button, string text)
    {
        button.Text = text;
        button.Dock = DockStyle.Right;
        button.Width = 46;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Font = new Font("Segoe UI", 10f);
        button.TabStop = false;
    }

    private void BeginWindowDrag(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || WindowState == FormWindowState.Maximized) return;
        ReleaseCapture();
        SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal : FormWindowState.Maximized;
        _maximizeButton.Text = WindowState == FormWindowState.Maximized ? "❐" : "□";
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int preference = 2;
        DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
    }

    protected override void WndProc(ref Message message)
    {
        const int WmNcHitTest = 0x0084;
        const int HtClient = 1;
        if (message.Msg == WmNcHitTest && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref message);
            if (message.Result.ToInt32() != HtClient) return;
            Point point = PointToClient(new Point((short)(message.LParam.ToInt64() & 0xFFFF),
                (short)((message.LParam.ToInt64() >> 16) & 0xFFFF)));
            const int edge = 7;
            bool left = point.X <= edge;
            bool right = point.X >= ClientSize.Width - edge;
            bool top = point.Y <= edge;
            bool bottom = point.Y >= ClientSize.Height - edge;
            if (left && top) message.Result = new IntPtr(13);
            else if (right && top) message.Result = new IntPtr(14);
            else if (left && bottom) message.Result = new IntPtr(16);
            else if (right && bottom) message.Result = new IntPtr(17);
            else if (left) message.Result = new IntPtr(10);
            else if (right) message.Result = new IntPtr(11);
            else if (top) message.Result = new IntPtr(12);
            else if (bottom) message.Result = new IntPtr(15);
            return;
        }
        base.WndProc(ref message);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

    private static string FormatRelativeTime(DateTime time)
    {
        TimeSpan elapsed = DateTime.Now - time;
        if (elapsed.TotalMinutes < 1) return "刚刚";
        if (elapsed.TotalHours < 1) return Math.Max(1, (int)elapsed.TotalMinutes) + " 分钟前";
        if (elapsed.TotalDays < 1) return (int)elapsed.TotalHours + " 小时前";
        return time.ToString("MM月dd日 HH:mm");
    }

    private static string SanitizeFileName(string title)
    {
        string value = string.Concat(title.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim();
        if (string.IsNullOrWhiteSpace(value)) value = "无题";
        return value.Length > 80 ? value.Substring(0, 80).Trim() : value;
    }

    private sealed class Palette
    {
        public readonly Color Canvas;
        public readonly Color Surface;
        public readonly Color Highlight;
        public readonly Color Text;
        public readonly Color Muted;
        public readonly Color Mask;
        public readonly Color Accent;
        public Palette(Color canvas, Color surface, Color highlight, Color text, Color muted, Color mask, Color accent)
        {
            Canvas = canvas; Surface = surface; Highlight = highlight; Text = text;
            Muted = muted; Mask = mask; Accent = accent;
        }
    }
}
}
