using System;
using System.Collections.Generic;
using System.IO;
using HiddenType.Windows.Models;

namespace HiddenType.Windows.Services
{
    public sealed class SettingsStore
    {
        private readonly string _path;

        public SettingsStore(string dataRoot)
        {
            Directory.CreateDirectory(dataRoot);
            _path = Path.Combine(dataRoot, "settings.ini");
        }

        public AppSettings Load()
        {
            var settings = new AppSettings();
            try
            {
                if (!File.Exists(_path)) return settings;
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(_path))
                {
                    int separator = line.IndexOf('=');
                    if (separator > 0) values[line.Substring(0, separator)] = line.Substring(separator + 1);
                }
                bool dark;
                int number;
                if (values.ContainsKey("DarkMode") && bool.TryParse(values["DarkMode"], out dark)) settings.DarkMode = dark;
                if (values.ContainsKey("RevealCount") && int.TryParse(values["RevealCount"], out number)) settings.RevealCount = Math.Max(1, Math.Min(50, number));
                if (values.ContainsKey("WindowWidth") && int.TryParse(values["WindowWidth"], out number)) settings.WindowWidth = Math.Max(720, number);
                if (values.ContainsKey("WindowHeight") && int.TryParse(values["WindowHeight"], out number)) settings.WindowHeight = Math.Max(480, number);
            }
            catch (IOException) { }
            return settings;
        }

        public void Save(AppSettings settings)
        {
            string temporary = _path + ".tmp";
            File.WriteAllLines(temporary, new[]
            {
                "DarkMode=" + settings.DarkMode,
                "RevealCount=" + settings.RevealCount,
                "WindowWidth=" + settings.WindowWidth,
                "WindowHeight=" + settings.WindowHeight
            });
            if (File.Exists(_path)) File.Replace(temporary, _path, null);
            else File.Move(temporary, _path);
        }
    }
}
