namespace HiddenType.Windows.Models
{
    public sealed class AppSettings
    {
        public bool DarkMode { get; set; }
        public int RevealCount { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }

        public AppSettings()
        {
            DarkMode = true;
            RevealCount = 5;
            WindowWidth = 1000;
            WindowHeight = 700;
        }
    }
}
