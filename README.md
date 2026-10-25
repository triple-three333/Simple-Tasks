# Simple Tasks
FULL DISCLAIMER: THIS WAS VIBE CODED USING AI. 
This software was the exact type of thing I needed to keep track of my tasks, and I figure it may be helpful to others, so I'm uploading it here.
If you prefer your productivity software artisanal, look elsewhere.

A small Windows to-do list with a classic desktop interface. Keep dated, undated, and daily tasks in three columns, with notes, repeating schedules, and optional urgency colors.

Maintained by [triple-three333](https://github.com/triple-three333). Licensed under the [MIT License](LICENSE).

## Download and run

1. Open this repository's **Releases** section.
2. Download **Simple-Tasks-Windows.zip** from a release's **Assets**.
3. Extract the ZIP into a folder.
4. Open **Simple Tasks.exe**. Keep **Simple Tasks.exe.config** alongside it.

No installer or sign-in is required. The app runs locally without an internet connection. Windows 10 or 11 with .NET Framework 4.8 is required. This is a Windows desktop application; it does not run in a phone browser or on macOS/Linux.

If you downloaded GitHub's **Source code** ZIP, follow the [build instructions](docs/BUILDING.md) to create the executable.

## Features

- **Dated tasks:** creation age, deadline countdown, and overdue timers; earliest deadlines first.
- **Undated tasks:** creation age and editable creation dates; oldest tasks first.
- **Daily tasks:** stay visible and turn gray when done, then reset at local midnight.
- **Notes:** optional multiline notes with an indicator and hover preview.
- **Weights:** None, Low, Medium, and High.
- **Optional age/deadline colors:** off by default, with simple red thresholds and an Advanced option for every transition.
- **Repeating tasks:** daily/weekly intervals or selected weekdays, ending after a total count, a number of weeks, a date, or indefinitely.
- **Completion history:** restore or delete completed tasks while retaining lifetime completion totals when entries are deleted.
- **Multiple deletion:** Ctrl/Shift selection and one confirmation.
- **Canvas calendar import:** manually import a downloaded `.ics` file, select an inclusive date range, preview entries, and skip saved duplicates.
- **Local saving and backup export**, plus an offline guide available with **F1**.

## Quick start

Enter a task name, choose a weight, and click **Add task**. Check **Due date** for a dated task or **Daily task** for an everyday checklist item. Leave both unchecked for an undated task. Add an optional note before saving.

Select a task and click **Edit** or double-click it to change its details. Click **Complete** when it is done. Completed dated and undated tasks are under **View → Completed tasks**. Daily tasks remain in their column.

Open **Settings → Task colors** to enable age/deadline colors. Default red thresholds are:

| Weight | Undated: age | Dated: time remaining |
| --- | --- | --- |
| Low | 12 days | 1 day |
| Medium | 6 days | 3 days |
| High | 3 days | 7 days |

None stays black. Intermediate colors are evenly spaced automatically; Advanced settings let you change each transition. When age/deadline coloring is off, None and Low are black, Medium is yellow-orange, and High is red. Completed daily tasks turn gray in either mode.

See the [full user guide](docs/USER-GUIDE.md) or **Help → User Guide** in the app for instructions, examples, and keyboard shortcuts.

## Your data

Tasks and settings save in your Windows profile at:

```text
%LOCALAPPDATA%\Simple Tasks\tasks.json
```

The previous save is kept as `tasks.json.bak`. Use **File → Export backup** for a separate JSON backup. Copying the EXE or downloading an update does not copy or replace your task data. The app uses the system's local time zone for date display, daily resets, and future repeat scheduling.

This download contains no personal task records or calendar exports. The app does not connect to Canvas or synchronize automatically; its Canvas workflow is the manual `.ics` importer. Other calendar-suite import support is not claimed.

## Build from source

See [BUILDING.md](docs/BUILDING.md). The project is written in C# with Windows Forms and builds using the .NET Framework compiler included with Windows. No NuGet packages or downloaded build dependencies are used.

## License

[MIT](LICENSE) — Copyright (c) 2026 triple-three333.
