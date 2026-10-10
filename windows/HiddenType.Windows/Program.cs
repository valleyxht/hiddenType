using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using HiddenType.Windows.Services;
using HiddenType.Windows.UI;

namespace HiddenType.Windows
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool diagnose = args.Any(arg => arg.Equals("--diagnose", StringComparison.OrdinalIgnoreCase));
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
            Action<string> log = delegate(string message)
            {
                if (diagnose) File.AppendAllText(logPath, DateTime.Now.ToString("O") + " " + message + Environment.NewLine);
            };

            try
            {
                log("Starting");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool portable = args.Any(arg => arg.Equals("--portable", StringComparison.OrdinalIgnoreCase));
                string dataRoot = portable
                    ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HiddenType");
                log("Creating MainForm; dataRoot=" + dataRoot);
                var form = new MainForm(new NoteRepository(dataRoot), new SettingsStore(dataRoot));
                log("MainForm constructed");
                form.HandleCreated += delegate { log("Handle created"); };
                form.Load += delegate { log("Load event"); };
                form.Shown += delegate { log("Shown event"); };
                Application.Run(form);
                log("Application.Run returned");
            }
            catch (Exception ex)
            {
                log("Fatal error: " + ex);
                MessageBox.Show("HiddenType 无法启动：\n\n" + ex.Message + "\n\n诊断日志：" + logPath,
                    "HiddenType", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
