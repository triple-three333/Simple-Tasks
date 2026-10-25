using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace SimpleTasks
{
    [DataContract]
    public sealed class TaskEntry
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Title { get; set; }
        [DataMember(EmitDefaultValue = false)] public string Note { get; set; }
        [DataMember] public DateTimeOffset CreatedAt { get; set; }
        [DataMember] public DateTimeOffset? DueAt { get; set; }
        [DataMember] public int Weight { get; set; }
        [DataMember] public DateTimeOffset? CompletedAt { get; set; }
        [DataMember(EmitDefaultValue = false)] public string SeriesId { get; set; }
        [DataMember(EmitDefaultValue = false)] public int OccurrenceNumber { get; set; }
        [DataMember(EmitDefaultValue = false)] public string ImportKey { get; set; }
        [DataMember(EmitDefaultValue = false)] public string ImportCalendar { get; set; }
        [DataMember(EmitDefaultValue = false)] public bool IsDaily { get; set; }
        [DataMember(EmitDefaultValue = false)] public string DailyCompletedOn { get; set; }

        public bool HasNote { get { return !String.IsNullOrWhiteSpace(Note); } }

        public TaskEntry Copy()
        {
            return (TaskEntry)MemberwiseClone();
        }

        public override string ToString() { return (Title ?? "") + (IsDaily ? (DailyTasks.IsDone(this, DateTimeOffset.Now) ? " (done today)" : " (daily task)") : "") + (HasNote ? " (note)" : "") + (SeriesId != null ? " (repeat #" + OccurrenceNumber + ")" : ""); }

        public void Validate(DateTimeOffset now)
        {
            if (String.IsNullOrWhiteSpace(Title)) throw new ArgumentException("Enter a task name.");
            if (Title.Trim().Length > 500) throw new ArgumentException("Task names can contain up to 500 characters.");
            if (Weight < 0 || Weight > 5) throw new ArgumentException("Choose None, Low, Medium, or High for the task weight.");
            if (IsDaily && DueAt.HasValue) throw new ArgumentException("Daily tasks do not have a due date or time.");
            if (CreatedAt > now.AddSeconds(2)) throw new ArgumentException("The creation date cannot be in the future.");
            Title = Title.Trim();
            if (String.IsNullOrEmpty(Id)) Id = Guid.NewGuid().ToString("N");
        }
    }

    [DataContract]
    public sealed class TaskDocument
    {
        [DataMember] public int Version { get; set; }
        [DataMember] public List<TaskEntry> Tasks { get; set; }
        [DataMember(EmitDefaultValue = false)] public List<TaskSeries> Series { get; set; }
        [DataMember(EmitDefaultValue = false)] public long DeletedCompletedDated { get; set; }
        [DataMember(EmitDefaultValue = false)] public long DeletedCompletedUndated { get; set; }
        [DataMember(EmitDefaultValue = false)] public TaskColorSettings ColorSettings { get; set; }
        public TaskDocument() { Version = 1; Tasks = new List<TaskEntry>(); Series = new List<TaskSeries>(); }

        public TaskDocument Copy()
        {
            return new TaskDocument { Version = Version, Tasks = Tasks.Select(t => t.Copy()).ToList(), Series = Series.Select(s => s.Copy()).ToList(),
                DeletedCompletedDated = DeletedCompletedDated, DeletedCompletedUndated = DeletedCompletedUndated,
                ColorSettings = ColorSettings == null ? null : ColorSettings.Copy() };
        }
    }

    public sealed class TaskRepository
    {
        public string FilePath { get; private set; }
        public TaskRepository(string path) { FilePath = path; }

        public TaskDocument Load()
        {
            if (!File.Exists(FilePath)) return new TaskDocument();
            using (var stream = File.OpenRead(FilePath))
            {
                var document = (TaskDocument)new DataContractJsonSerializer(typeof(TaskDocument)).ReadObject(stream);
                if (document == null || document.Version != 1 || document.Tasks == null)
                    throw new InvalidDataException("The task file is not a supported Simple Tasks file.");
                if (document.DeletedCompletedDated < 0 || document.DeletedCompletedUndated < 0)
                    throw new InvalidDataException("The task file contains an invalid completion tally.");
                // The previous five-weight tables are deliberately replaced by
                // the agreed priority defaults; retain the user's on/off choice.
                if (document.ColorSettings != null && document.ColorSettings.SchemaVersion < 2)
                    document.ColorSettings = new TaskColorSettings { Enabled = document.ColorSettings.Enabled };
                if (document.ColorSettings != null)
                    try { document.ColorSettings.Validate(); }
                    catch (ArgumentException error) { throw new InvalidDataException("The task file contains invalid color settings.", error); }
                var ids = new HashSet<string>();
                foreach (var task in document.Tasks)
                {
                    if (task == null || String.IsNullOrWhiteSpace(task.Id) || !ids.Add(task.Id) ||
                        String.IsNullOrWhiteSpace(task.Title) || task.Weight < 0 || task.Weight > 5)
                        throw new InvalidDataException("The task file contains an invalid task.");
                    if (task.IsDaily && (task.DueAt.HasValue || task.CompletedAt.HasValue || task.SeriesId != null))
                        throw new InvalidDataException("A daily task has incompatible deadline, completion, or repeat settings.");
                    if (task.DailyCompletedOn != null) Recurrence.ParseDate(task.DailyCompletedOn);
                }
                if (document.Series == null) document.Series = new List<TaskSeries>();
                var seriesIds = new HashSet<string>();
                foreach (var series in document.Series)
                {
                    if (series == null || String.IsNullOrEmpty(series.Id) || !seriesIds.Add(series.Id) || series.Template == null || series.Rule == null || series.GeneratedCount < 1)
                        throw new InvalidDataException("The task file contains an invalid repeating task.");
                    series.Rule.Validate(Recurrence.ParseDate(series.AnchorDate));
                    Recurrence.ParseDate(series.LastDate); Recurrence.ParseDate(series.NextDate);
                    Recurrence.Zone(series);
                    if (String.IsNullOrWhiteSpace(series.Template.Title) || series.Template.Weight < 0 || series.Template.Weight > 5)
                        throw new InvalidDataException("A repeating task has invalid details.");
                }
                foreach (var task in document.Tasks)
                    if (task.SeriesId != null && (!seriesIds.Contains(task.SeriesId) || task.OccurrenceNumber < 1))
                        throw new InvalidDataException("A task refers to an invalid repeating task.");
                return document;
            }
        }

        public void Save(TaskDocument document)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
            Directory.CreateDirectory(directory);
            var temporary = FilePath + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(TaskDocument)).WriteObject(stream, document);
                    stream.Flush(true);
                }
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
                else File.Move(temporary, FilePath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public static class TaskTiming
    {
        public static string Duration(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            if (span.TotalDays >= 1) return String.Format("{0}d {1}h {2}m", (long)span.TotalDays, span.Hours, span.Minutes);
            if (span.TotalHours >= 1) return String.Format("{0}h {1}m", (long)span.TotalHours, span.Minutes);
            if (span.TotalMinutes >= 1) return String.Format("{0}m {1}s", (long)span.TotalMinutes, span.Seconds);
            return String.Format("{0}s", (long)span.TotalSeconds);
        }

        public static string Age(TaskEntry task, DateTimeOffset now)
        {
            if (task.IsDaily) return "";
            return "Age: " + Duration((task.CompletedAt ?? now) - task.CreatedAt);
        }

        public static string Deadline(TaskEntry task, DateTimeOffset now)
        {
            if (task.IsDaily) return "";
            if (task.CompletedAt.HasValue) return "Completed " + task.CompletedAt.Value.LocalDateTime.ToString("MMM d, yyyy");
            if (!task.DueAt.HasValue) return "";
            var remaining = task.DueAt.Value - now;
            return remaining > TimeSpan.Zero ? "Due in: " + Duration(remaining) : "Overdue: " + Duration(-remaining);
        }

        public static List<TaskEntry> ActiveTasks(TaskDocument document, bool dated)
        {
            var tasks = document.Tasks.Where(t => !t.IsDaily && !t.CompletedAt.HasValue && t.DueAt.HasValue == dated);
            return (dated ? tasks.OrderBy(t => t.DueAt).ThenBy(t => t.CreatedAt) : tasks.OrderBy(t => t.CreatedAt)).ToList();
        }
    }

    public static class DailyTasks
    {
        public static string Today(DateTimeOffset now) { return Today(now, TimeZoneInfo.Local); }
        internal static string Today(DateTimeOffset now, TimeZoneInfo zone) { return Recurrence.DateText(TimeZoneInfo.ConvertTime(now, zone).Date); }
        public static bool IsDone(TaskEntry task, DateTimeOffset now) { return IsDone(task, now, TimeZoneInfo.Local); }
        internal static bool IsDone(TaskEntry task, DateTimeOffset now, TimeZoneInfo zone) { return task.IsDaily && task.DailyCompletedOn == Today(now, zone); }
        public static void Toggle(TaskEntry task, DateTimeOffset now)
        {
            Toggle(task, now, TimeZoneInfo.Local);
        }
        internal static void Toggle(TaskEntry task, DateTimeOffset now, TimeZoneInfo zone)
        {
            if (!task.IsDaily) throw new ArgumentException("This is not a daily task.");
            task.DailyCompletedOn = IsDone(task, now, zone) ? null : Today(now, zone);
            task.CompletedAt = null;
        }
        public static List<TaskEntry> List(TaskDocument document) { return document.Tasks.Where(t => t.IsDaily).OrderBy(t => t.CreatedAt).ThenBy(t => t.Id).ToList(); }
    }
}
