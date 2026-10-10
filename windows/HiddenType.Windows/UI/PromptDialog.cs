using System.Drawing;
using System.Windows.Forms;

namespace HiddenType.Windows.UI
{
    internal static class PromptDialog
    {
        public static string Show(IWin32Window owner, string title, string value)
        {
            using (var dialog = new Form
            {
                Text = title, StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false,
                ShowInTaskbar = false, ClientSize = new Size(420, 116), Font = new Font("Microsoft YaHei UI", 9f)
            })
            {
                var input = new TextBox { Left = 16, Top = 18, Width = 388, Text = value };
                var ok = new Button { Text = "保存", DialogResult = DialogResult.OK, Left = 238, Top = 67, Width = 78 };
                var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 326, Top = 67, Width = 78 };
                dialog.Controls.AddRange(new Control[] { input, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                dialog.Shown += delegate { input.SelectAll(); input.Focus(); };
                return dialog.ShowDialog(owner) == DialogResult.OK
                    ? (string.IsNullOrWhiteSpace(input.Text) ? "无题" : input.Text.Trim()) : null;
            }
        }
    }
}
