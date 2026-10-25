using System;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;

namespace SimpleTasks
{
    public static class TaskPriority
    {
        public static readonly string[] Names = { "None", "Low", "Medium", "High" };
        private static readonly int[] Values = { 0, 1, 3, 5 };
        public static int Group(int weight) { return weight == 0 ? 0 : weight <= 2 ? 1 : weight == 3 ? 2 : 3; }
        public static int Value(int group) { return Values[group]; }
        public static string Name(int weight) { return Names[Group(weight)]; }
        public static Color StaticColor(int weight)
        {
            int group = Group(weight);
            return group == 2 ? Color.FromArgb(202, 150, 0) : group == 3 ? TaskStyle.Weights[5] : Color.Black;
        }
    }

    [DataContract]
    public sealed class TaskColorSettings
    {
        [DataMember] public bool Enabled { get; set; }
        [DataMember] public int SchemaVersion { get; set; }
        [DataMember] public bool Advanced { get; set; }
        [DataMember] public decimal[][] UndatedDays { get; set; }
        [DataMember] public decimal[][] DatedDays { get; set; }
        public TaskColorSettings()
        {
            SchemaVersion = 2;
            UndatedDays = new[] { Row(3m), Row(1.5m), Row(.75m) };
            DatedDays = new[] { Row(1m).Reverse().ToArray(), Row(3m).Reverse().ToArray(), Row(7m).Reverse().ToArray() };
        }
        private static decimal[] Row(decimal interval) { return new[] { interval, interval * 2, interval * 3, interval * 4 }; }
        public TaskColorSettings Copy()
        {
            return new TaskColorSettings { Enabled = Enabled, SchemaVersion = SchemaVersion, Advanced = Advanced,
                UndatedDays = UndatedDays.Select(r => r.ToArray()).ToArray(), DatedDays = DatedDays.Select(r => r.ToArray()).ToArray() };
        }
        public static decimal[] Linear(decimal red, bool dated) { return dated ? Row(red).Reverse().ToArray() : Row(red / 4m); }
        public void Validate()
        {
            if (SchemaVersion != 2) throw new ArgumentException("Unsupported task color settings version.");
            ValidateTable(UndatedDays, false); ValidateTable(DatedDays, true);
        }
        private static void ValidateTable(decimal[][] table, bool dated)
        {
            string name = dated ? "Dated" : "Undated";
            if (table == null || table.Length != 3) throw new ArgumentException(name + " settings need Low, Medium, and High rows.");
            for (int row = 0; row < 3; row++)
            {
                if (table[row] == null || table[row].Length != 4) throw new ArgumentException(name + " settings need four color transitions per weight.");
                for (int col = 0; col < 4; col++)
                {
                    decimal value = table[row][col];
                    if (value < 0 || value > 36500 || (!dated && value == 0) || Decimal.Round(value, 4) != value)
                        throw new ArgumentException(name + ", " + TaskPriority.Names[row + 1] + ": use " + (dated ? "0" : "0.0001") + " to 36500 days, with up to four decimal places.");
                    if (col > 0 && (dated ? value >= table[row][col - 1] : value <= table[row][col - 1]))
                        throw new ArgumentException(name + ", " + TaskPriority.Names[row + 1] + ": days must " + (dated ? "decrease" : "increase") + " from light green through red.");
                }
            }
        }
    }

    public static class TaskColors
    {
        public static Color For(TaskEntry task, TaskColorSettings settings, DateTimeOffset now)
        {
            if (DailyTasks.IsDone(task, now)) return SystemColors.GrayText;
            if (task.Weight == 0) return Color.Black;
            if (settings == null || !settings.Enabled || task.IsDaily) return TaskPriority.StaticColor(task.Weight);
            // Completed tasks keep the color they had at their completion instant.
            var instant = task.CompletedAt ?? now;
            decimal days = (task.DueAt.HasValue ? (task.DueAt.Value - instant).Ticks : (instant - task.CreatedAt).Ticks) / (decimal)TimeSpan.TicksPerDay;
            if (task.DueAt.HasValue && days <= 0) return TaskStyle.Weights[5];
            var thresholds = (task.DueAt.HasValue ? settings.DatedDays : settings.UndatedDays)[TaskPriority.Group(task.Weight) - 1];
            int stage = 1;
            for (int i = 0; i < 4; i++)
                if (task.DueAt.HasValue ? days <= thresholds[i] : days >= thresholds[i]) stage = i + 2;
            return TaskStyle.Weights[stage];
        }
    }
}
