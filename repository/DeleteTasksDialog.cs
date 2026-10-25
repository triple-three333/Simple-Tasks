using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SimpleTasks
{
    public sealed class DeleteTasksDialog : Form
    {
        public readonly Button DeleteButton;
        public DeleteTasksDialog(IList<TaskEntry> tasks)
        {
            Text = "Delete tasks"; Font = new Font("Tahoma", 9); ClientSize = new Size(580, 300);
            StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
            var heading = new Label { Text = tasks.Count == 1 ? "Delete this task?" : "Delete these " + tasks.Count + " selected tasks?", Dock = DockStyle.Top, Height = 38, Padding = new Padding(10, 10, 0, 0) };
            var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true, TabStop = false };
            list.Items.AddRange(tasks.Select(t => (object)((t.IsDaily ? "Daily" : t.DueAt.HasValue ? "Dated" : "Undated") + ": " + t.Title)).ToArray());
            DeleteButton = new Button { Text = tasks.Count == 1 ? "Delete task" : "Delete " + tasks.Count + " tasks", Width = 125, DialogResult = DialogResult.Yes };
            var cancel = new Button { Text = "Cancel", Width = 85, DialogResult = DialogResult.Cancel };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(6), FlowDirection = FlowDirection.RightToLeft };
            buttons.Controls.Add(cancel); buttons.Controls.Add(DeleteButton);
            Controls.Add(list); Controls.Add(buttons); Controls.Add(heading);
            if (tasks.Any(t => !t.IsDaily && t.CompletedAt.HasValue))
            {
                var completionHint = new Label { Text = "Deletion is permanent. Lifetime completion totals will be kept.", Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(7) };
                Controls.Add(completionHint); Controls.SetChildIndex(completionHint, 1);
            }
            if (tasks.Any(t => t.SeriesId != null))
            {
                var hint = new Label { Text = "Future repeats will continue. To stop them, use View > Repeating tasks.", Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(7) };
                Controls.Add(hint); Controls.SetChildIndex(hint, 1);
            }
            CancelButton = cancel; AcceptButton = cancel; ActiveControl = cancel;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        }
    }
}
