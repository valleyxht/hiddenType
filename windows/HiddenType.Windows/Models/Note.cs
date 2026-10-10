using System;

namespace HiddenType.Windows.Models
{
    public sealed class Note
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Note()
        {
            Id = string.Empty;
            Title = "无题";
            Content = string.Empty;
            UpdatedAt = DateTime.Now;
        }
    }
}
