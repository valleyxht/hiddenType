using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HiddenType.Windows.UI
{
    public sealed class MaskedRichTextBox : RichTextBox
    {
        private const int WmSetRedraw = 0x000B;
        private const int WmPaint = 0x000F;
        private const int WmPrintClient = 0x0318;
        private bool _applyingMask;
        private int _maskedCharacterEnd;
        private int _revealCount = 5;
        private bool _revealed;
        private Color _textColor = Color.Gainsboro;
        private Color _surfaceColor = Color.FromArgb(15, 15, 18);
        private Color _maskColor = Color.FromArgb(92, 92, 108);

        public int RevealCount
        {
            get { return _revealCount; }
            set { _revealCount = Math.Max(1, Math.Min(50, value)); ApplyMask(); }
        }

        public bool Revealed
        {
            get { return _revealed; }
            set { _revealed = value; ApplyMask(); }
        }

        public MaskedRichTextBox()
        {
            BorderStyle = BorderStyle.None;
            AcceptsTab = true;
            DetectUrls = false;
            EnableAutoDragDrop = false;
            HideSelection = false;
            ScrollBars = RichTextBoxScrollBars.Vertical;
            Font = new Font("Consolas", 13f);
            TextChanged += delegate { QueueMask(); };
        }

        public void SetPalette(Color text, Color surface, Color mask)
        {
            _textColor = text;
            _surfaceColor = surface;
            _maskColor = mask;
            BackColor = surface;
            ApplyMask();
        }

        private void QueueMask()
        {
            if (_applyingMask || !IsHandleCreated) return;
            BeginInvoke(new Action(ApplyMask));
        }

        public void ApplyMask()
        {
            if (_applyingMask || IsDisposed || !IsHandleCreated) return;
            _applyingMask = true;
            int start = SelectionStart;
            int length = SelectionLength;
            SendMessage(Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
            try
            {
                SelectAll();
                SelectionColor = _textColor;
                SelectionBackColor = _surfaceColor;
                _maskedCharacterEnd = _revealed ? 0 : FindMaskEnd(Text, _revealCount);
                if (_maskedCharacterEnd > 0)
                {
                    Select(0, _maskedCharacterEnd);
                    SelectionColor = Blend(_surfaceColor, _textColor, 61);
                    SelectionBackColor = Blend(_surfaceColor, _maskColor, 31);
                }
                Select(Math.Min(start, TextLength), Math.Min(length, Math.Max(0, TextLength - start)));
            }
            finally
            {
                SendMessage(Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
                Invalidate();
                _applyingMask = false;
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg == WmPaint || m.Msg == WmPrintClient) &&
                !_applyingMask && !_revealed && _maskedCharacterEnd > 0)
            {
                DrawMaskTexture();
            }
        }

        private void DrawMaskTexture()
        {
            if (!IsHandleCreated || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int firstVisibleLine = GetLineFromCharIndex(GetCharIndexFromPosition(new Point(1, 1)));
            int lastVisibleLine = GetLineFromCharIndex(GetCharIndexFromPosition(
                new Point(Math.Max(1, ClientSize.Width - 2), Math.Max(1, ClientSize.Height - 2))));
            int lastMaskedLine = GetLineFromCharIndex(Math.Max(0, _maskedCharacterEnd - 1));
            int finalLine = Math.Min(lastVisibleLine, lastMaskedLine);

            using (Graphics graphics = Graphics.FromHwnd(Handle))
            using (Pen stripePen = new Pen(Color.FromArgb(61, _maskColor), 1f))
            {
                graphics.SetClip(ClientRectangle);
                float scale = Math.Max(1f, graphics.DpiX / 96f);
                stripePen.Width = Math.Max(1f, 0.75f * scale);
                float stripeGap = 4f * scale;

                for (int line = Math.Max(0, firstVisibleLine); line <= finalLine; line++)
                {
                    int lineStart = GetFirstCharIndexFromLine(line);
                    if (lineStart < 0 || lineStart >= _maskedCharacterEnd) break;

                    int nextLineStart = GetFirstCharIndexFromLine(line + 1);
                    int lineEnd = nextLineStart < 0 ? TextLength : nextLineStart;
                    int maskedEnd = Math.Min(_maskedCharacterEnd, lineEnd);
                    while (maskedEnd > lineStart &&
                           (Text[maskedEnd - 1] == '\r' || Text[maskedEnd - 1] == '\n'))
                    {
                        maskedEnd--;
                    }
                    if (maskedEnd <= lineStart) continue;

                    Point start = GetPositionFromCharIndex(lineStart);
                    Point end = GetPositionFromCharIndex(maskedEnd);
                    float width = end.Y == start.Y ? end.X - start.X : ClientSize.Width - start.X;
                    if (width <= 0)
                    {
                        Point last = GetPositionFromCharIndex(maskedEnd - 1);
                        Size measured = TextRenderer.MeasureText(Text.Substring(maskedEnd - 1, 1), Font,
                            Size.Empty, TextFormatFlags.NoPadding);
                        width = last.X - start.X + Math.Max(2, measured.Width);
                    }

                    float lineHeight = Font.Height + 3f * scale;
                    if (nextLineStart >= 0)
                    {
                        Point nextLine = GetPositionFromCharIndex(nextLineStart);
                        if (nextLine.Y > start.Y) lineHeight = nextLine.Y - start.Y;
                    }

                    RectangleF maskRectangle = RectangleF.Intersect(
                        new RectangleF(start.X, start.Y, width, lineHeight), ClientRectangle);
                    if (maskRectangle.Width <= 0 || maskRectangle.Height <= 0) continue;

                    GraphicsState state = graphics.Save();
                    graphics.SetClip(maskRectangle, CombineMode.Intersect);
                    for (float x = maskRectangle.Left - maskRectangle.Height;
                         x < maskRectangle.Right + maskRectangle.Height; x += stripeGap)
                    {
                        graphics.DrawLine(stripePen, x, maskRectangle.Bottom,
                            x + maskRectangle.Height, maskRectangle.Top);
                    }
                    graphics.Restore(state);
                }
            }
        }

        private static int FindMaskEnd(string text, int revealCount)
        {
            int index = text.Length;
            int visible = 0;
            while (index > 0 && visible < revealCount)
            {
                char current = text[index - 1];
                if (char.IsLowSurrogate(current) && index > 1 && char.IsHighSurrogate(text[index - 2]))
                {
                    index -= 2;
                    visible++;
                }
                else
                {
                    index--;
                    if (!char.IsWhiteSpace(current) && current != '\u3000') visible++;
                }
            }
            return visible < revealCount ? 0 : index;
        }

        private static Color Blend(Color background, Color foreground, int foregroundAlpha)
        {
            int inverse = 255 - foregroundAlpha;
            return Color.FromArgb(
                (background.R * inverse + foreground.R * foregroundAlpha) / 255,
                (background.G * inverse + foreground.G * foregroundAlpha) / 255,
                (background.B * inverse + foreground.B * foregroundAlpha) / 255);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
