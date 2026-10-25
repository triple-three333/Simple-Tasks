using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SimpleTasks
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--self-test") return SelfTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--preview") return SelfTests.Preview(args[1]);
            if (args.Length == 2 && args[0] == "--check-refresh") return SelfTests.CheckRefresh(args[1]);
            if (args.Length == 2 && args[0] == "--check-interactions") return InteractionTests.Preview(args[1]);
            if (args.Length == 2 && args[0] == "--check-completed") return CompletedTaskTests.Preview(args[1]);
            if (args.Length == 2 && args[0] == "--check-colors") return ColorTests.Preview(args[1]);
            if (args.Length == 5 && args[0] == "--check-calendar") return CalendarTests.CheckFile(args);
            bool first;
            using (var instance = new Mutex(true, "Local\\SimpleTasksDesktop", out first))
            {
                if (!first)
                {
                    if (args.Length == 5 && args[0] == "--import-ics")
                    {
                        var report = Path.GetFullPath(args[4]); Directory.CreateDirectory(Path.GetDirectoryName(report));
                        File.WriteAllText(report, "Calendar import was not applied: close Simple Tasks before importing from the command line.");
                        return 2;
                    }
                    MessageBox.Show("Simple Tasks is already running. Open its window from the taskbar.", "Simple Tasks", MessageBoxButtons.OK, MessageBoxIcon.Information); return 0;
                }
                try
                {
                    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Simple Tasks", "tasks.json");
                    var repository = new TaskRepository(path);
                    if (args.Length == 5 && args[0] == "--import-ics") return CalendarCommands.Import(args, repository);
                    TaskDocument tasks;
                    try { tasks = repository.Load(); }
                    catch (Exception error)
                    {
                        MessageBox.Show("Your saved tasks could not be opened. The file has been preserved.\n\n" + path + "\n\nA previous saved copy may be available at tasks.json.bak.\n\n" + error.Message, "Unable to open tasks", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return 1;
                    }
                    using (var form = new MainForm(repository, tasks)) Application.Run(form);
                    return 0;
                }
                finally { instance.ReleaseMutex(); }
            }
        }
    }
}
