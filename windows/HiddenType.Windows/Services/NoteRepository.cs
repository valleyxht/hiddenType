using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HiddenType.Windows.Models;

namespace HiddenType.Windows.Services
{
    public sealed class NoteRepository
    {
        private readonly string _notesDirectory;

        public NoteRepository(string dataRoot)
        {
            _notesDirectory = Path.Combine(dataRoot, "notes");
            Directory.CreateDirectory(_notesDirectory);
        }

        public IList<Note> LoadAll()
        {
            var notes = new List<Note>();
            foreach (string path in Directory.EnumerateFiles(_notesDirectory, "*.txt"))
            {
                try
                {
                    string stored = File.ReadAllText(path, Encoding.UTF8);
                    NoteFileCodec.DecodedNote decoded = NoteFileCodec.Decode(stored, "导入的笔记");
                    notes.Add(new Note
                    {
                        Id = Path.GetFileNameWithoutExtension(path),
                        Title = string.IsNullOrWhiteSpace(decoded.Title) ? "无题" : decoded.Title,
                        Content = decoded.Content,
                        UpdatedAt = File.GetLastWriteTime(path)
                    });
                }
                catch (IOException) { }
            }
            return notes.OrderByDescending(note => note.UpdatedAt).ToList();
        }

        public Note Create()
        {
            var note = new Note
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                Title = "笔记 " + DateTime.Now.ToString("MM月dd日 HH:mm"),
                UpdatedAt = DateTime.Now
            };
            Save(note);
            return note;
        }

        public void Save(Note note)
        {
            Directory.CreateDirectory(_notesDirectory);
            string destination = Path.Combine(_notesDirectory, note.Id + ".txt");
            string temporary = destination + ".tmp";
            File.WriteAllText(temporary, NoteFileCodec.Encode(note.Title, note.Content), new UTF8Encoding(false));
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
            note.UpdatedAt = File.GetLastWriteTime(destination);
        }

        public void Delete(Note note)
        {
            string path = Path.Combine(_notesDirectory, note.Id + ".txt");
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
