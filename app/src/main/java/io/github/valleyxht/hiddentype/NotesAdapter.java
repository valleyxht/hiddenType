package io.github.valleyxht.hiddentype;

import android.content.Context;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.ImageButton;
import android.widget.LinearLayout;
import android.widget.TextView;

import java.util.List;

public class NotesAdapter extends BaseAdapter {

    // Note model
    public static class Note {
        public String id;
        public String title;
        public String content;
        public long updatedAt;
    }

    public interface OnNoteActionListener {
        void onNoteClick(Note note);
        void onNoteRename(Note note);
        void onNoteDelete(Note note);
        void onNoteShare(Note note);
    }

    private Context context;
    private List<Note> notes;
    private String currentId;
    private OnNoteActionListener listener;

    public NotesAdapter(Context context, List<Note> notes, String currentId, OnNoteActionListener listener) {
        this.context = context;
        this.notes = notes;
        this.currentId = currentId;
        this.listener = listener;
    }

    @Override
    public int getCount() {
        return notes.size();
    }

    @Override
    public Object getItem(int position) {
        return notes.get(position);
    }

    @Override
    public long getItemId(int position) {
        return position;
    }

    @Override
    public View getView(int position, View convertView, ViewGroup parent) {
        ViewHolder holder;

        if (convertView == null) {
            convertView = LayoutInflater.from(context).inflate(R.layout.item_note, parent, false);
            holder = new ViewHolder();
            holder.tvNoteTitle = convertView.findViewById(R.id.tvNoteTitle);
            holder.tvNoteTime = convertView.findViewById(R.id.tvNoteTime);
            holder.actionButtons = convertView.findViewById(R.id.actionButtons);
            holder.btnShare = convertView.findViewById(R.id.btnShare);
            holder.btnRename = convertView.findViewById(R.id.btnRename);
            holder.btnDelete = convertView.findViewById(R.id.btnDelete);
            convertView.setTag(holder);
        } else {
            holder = (ViewHolder) convertView.getTag();
        }

        Note note = notes.get(position);

        holder.tvNoteTitle.setText(note.title);
        holder.tvNoteTime.setText(formatDate(note.updatedAt));

        // Highlight current note using theme-aware color
        if (note.id.equals(currentId)) {
            convertView.setBackgroundColor(context.getColor(R.color.note_highlight));
        } else {
            convertView.setBackgroundColor(context.getColor(android.R.color.transparent));
        }

        // Show action buttons on click
        holder.btnShare.setOnClickListener(v -> {
            if (listener != null) listener.onNoteShare(note);
        });

        holder.btnRename.setOnClickListener(v -> {
            if (listener != null) listener.onNoteRename(note);
        });

        holder.btnDelete.setOnClickListener(v -> {
            if (listener != null) listener.onNoteDelete(note);
        });

        convertView.setOnClickListener(v -> {
            if (listener != null) listener.onNoteClick(note);
        });

        return convertView;
    }

    private String formatDate(long timestamp) {
        long diff = System.currentTimeMillis() - timestamp;
        if (diff < 60000) return context.getString(R.string.just_now);
        if (diff < 3600000) return (diff / 60000) + " " + context.getString(R.string.minutes_ago);
        if (diff < 86400000) return (diff / 3600000) + " " + context.getString(R.string.hours_ago);
        return new java.text.SimpleDateFormat(context.getString(R.string.date_format), java.util.Locale.CHINA)
                .format(new java.util.Date(timestamp));
    }

    public void setCurrentId(String currentId) {
        this.currentId = currentId;
        notifyDataSetChanged();
    }

    static class ViewHolder {
        TextView tvNoteTitle;
        TextView tvNoteTime;
        LinearLayout actionButtons;
        ImageButton btnShare;
        ImageButton btnRename;
        ImageButton btnDelete;
    }
}
