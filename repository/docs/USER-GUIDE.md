# Simple Tasks — User Guide

A small Windows desktop to-do list with a classic Windows appearance. It runs locally, has no sign-in, and requires no internet connection.

## Run

For a downloaded release, open **Simple Tasks.exe** in the extracted folder. For a source build, open **dist/Simple Tasks.exe**. No installer is required. Windows 10 or 11 with .NET Framework 4.8 is supported. The appearance uses traditional Windows controls; Windows XP itself is not a supported operating system.

To move the app to another Windows computer, extract **Simple-Tasks-Windows.zip** and open **Simple Tasks.exe**. Keep the `.exe.config` file alongside it. Your tasks remain in your Windows user profile rather than in the application folder.

## Help

Open **Help → User Guide** or press **F1** in the main window for an offline guide with a quick start, examples, and selectable topics for every feature. The guide covers columns, dates and notes, weights, color settings, repeats, counters and restoration, multiple deletion, Canvas imports, backups, shortcuts, and local time. **Help → About Simple Tasks** describes the current app, offers a copyable saved-data path, and also opens the guide.

## Tasks

- Enter a task name, choose its weight (**None**, **Low**, **Medium**, or **High**; default Low), and click **Add task**. Pressing Enter in the task name also adds it.
- **Created** defaults to the current date and time. Change it to backdate either type of task.
- Check **Due date** to create a dated task. The date fields support keyboard editing and a calendar picker; times can be edited directly.
- Add an optional multiline **Note** when creating or editing a task, for example “Bring up symptom x” for a doctor's visit. A small page icon marks tasks with notes. Double-click a task to read or change its note, or hover to preview it. Clear the note field to remove the note. Notes are saved with tasks and included in backups; existing task files remain compatible.
- Dated tasks show their age and time remaining until the deadline. Passed deadlines show time overdue. They sort by due date, earliest first.
- Undated tasks show their age. They sort by creation date, oldest first.
- With age/deadline coloring off, None and Low are black, Medium is yellow-orange, and High is red. None is exempt from age/deadline color changes. Hover over a task to see its full text, named weight, and exact dates. Existing numeric weights remain compatible: 0 = None, 1/2 = Low, 3 = Medium, 4/5 = High. Editing other task details preserves the original numeric value.
- Select a task to edit, complete, or delete it. Double-click to edit. Completed tasks are stored under **View → Completed tasks**, where **Restore task** brings them back and **Edit...** opens their details and notes. Select one or more completed tasks with Ctrl/Shift-click (or **Ctrl+A** for all), then click **Delete...** or press **Delete**. Review the selection and confirm to permanently remove the saved task details; Cancel keeps them. Restore and Edit work with one selected task.
- **Ctrl+N** focuses new task entry; **Ctrl+E** edits a selection. In a task list, **Space** completes a selection and **Delete** asks to delete it.

The bottom bar shows lifetime **completed total**, **completed dated**, and **completed undated** counts, excluding daily tasks. Deleting completed tasks preserves these totals: the save retains only a small dated/undated tally for deleted completions. Existing saved completed tasks are counted automatically, and the tallies are included in backups. Restoring a saved task undoes its completion and reduces the relevant total; completing it again counts it again. Changing the type of a saved completed task moves its count between dated and undated.

**Tab** visits task name, weight, each creation date/time field (month, day, year, hour, minute, AM/PM), the due-date checkbox, each enabled due-date/time field, note, daily and repeat options, and Add task. **Shift+Tab** goes backward. Disabled or hidden fields are skipped. Type a month name or abbreviation such as `Sep` or `September`, or a month number. Date fields automatically adjust invalid day counts when changing months or leap years. The `...` button also opens a calendar picker.

Use **Ctrl-click** to add or remove individual task selections, including selections in different columns. **Shift-click** selects a range within a column; **Ctrl+A** selects all tasks in the focused column. Click **Delete** or press **Delete** to review the selected tasks and delete them in one confirmation. Cancel keeps every task. Editing and completion operate on a single selection. Deleting repeat occurrences keeps their future schedule intact.

Timers refresh each second. For long intervals, they display days, hours, and minutes. Task dates use the computer's local timezone; stored dates retain their UTC offset.

## Task color settings

Open **Settings → Task colors...** to optionally enable **Color tasks by age / time until due**. It is off by default; existing on/off preferences are retained. None remains black in either mode. Daily tasks keep the static priority colors and turn gray when done, including daily None tasks.

With it on, undated tasks start black and progress through light green, yellow-green, orange, and red as they age. Dated tasks start black when far from their deadline and use the same progression as the deadline approaches; all overdue Low/Medium/High dated tasks are red. Colors apply to task titles. Completed tasks use their completion instant for color timing so they do not continue aging or become overdue in the archive.

The **Undated tasks** and **Dated tasks** tabs normally show only the red threshold for each priority. Undated intermediate colors start at 25%, 50%, and 75% of the red age; dated intermediate colors start at 4×, 3×, and 2× the red time remaining. Red thresholds accept positive days with up to two decimal places (dated maximum 9125 days, undated maximum 36500). Days mean 24 elapsed hours, not midnight changes; 0.5 = 12 hours. Every transition is inclusive at the exact threshold.

Check **Advanced** to manually set all four transitions for Low, Medium, and High independently. Undated values must increase from light green through red; dated values must decrease. Advanced thresholds support up to four decimal places and a maximum of 36500 days; dated red may be zero. Turning Advanced off recalculates the intermediate colors from red and rounds red to two decimal places (minimum 0.01 day), taking effect when saved.

Default thresholds below list **light green / yellow-green / orange / red**, in days:

| Weight | Undated: days old | Dated: days remaining |
| --- | --- | --- |
| Low | 3 / 6 / 9 / 12 | 4 / 3 / 2 / 1 |
| Medium | 1.5 / 3 / 4.5 / 6 | 12 / 9 / 6 / 3 |
| High | 0.75 / 1.5 / 2.25 / 3 | 28 / 21 / 14 / 7 |

**Save** immediately recolors existing tasks and saves settings with your task data and exported backups. Turning the feature off retains your thresholds and Advanced preference. **Cancel** discards changes; **Reset defaults** restores the new defaults, simple settings, and static coloring, taking effect only when saved. Old five-weight custom threshold tables are replaced with these new defaults on opening; tasks, repeat schedules, notes, and completion totals are preserved. New saved custom thresholds are retained. Titles update automatically when a threshold is crossed, without refreshing their text on every timer tick. The four named weights are available in task entry, editing, repeating tasks, and calendar import.

## Repeating tasks

Check **Repeat** when adding or editing a task, then click **Settings...**. Choose a daily interval, a weekly interval, or selected days of the week. Choose one ending rule: indefinitely, a total number of occurrences, a number of weeks, or a specific end date.

- The **total count includes the first task**. For example, five total occurrences means the first task plus four repeats.
- The first task uses the dates you enter and is kept even if its weekday differs from the selected repeat days. Its due date (for a dated task) or creation date (for an undated task) anchors the repeat schedule.
- Subsequent occurrences appear at **midnight on their scheduled day**. Their age starts at that intended creation time. Dated occurrences are due that day at the same chosen local time; undated occurrences have no deadline. Future occurrences are not listed before they start.
- A duration of two weeks covers fourteen calendar days starting on the first occurrence date. A specific ending date is inclusive.
- Every occurrence is independent. Unfinished tasks remain visible. Completing, restoring, or deleting one occurrence does not change other occurrences or restart the count.
- Notes and weight carry forward. Editing an occurrence updates that task and the details and settings for future repeats. Previously generated other occurrences keep their details. Date changes affect that occurrence; future deadlines use the chosen due time on scheduled days.
- **View → Repeating tasks** lists active, finished, and stopped series, including the next scheduled date and number already created. **Edit future...** changes the template and schedule without changing existing occurrences. **Stop repeating** keeps existing tasks and prevents new ones. Turning Repeat back on in the editor resumes the schedule, catching up any eligible missed dates within its ending rule.
- Repeats are created while the app is open, checked at least once a minute, and caught up when it reopens. Missed occurrences use their intended creation dates. No separate background service is installed. Large catch-up runs are processed in batches.

New occurrences use the system's current local time zone and retain the chosen local due time, including when moving to a computer in a different time zone. Already-created tasks keep their saved instant and display in local time. Local due times stay consistent through daylight-saving changes. If a selected time falls in the spring-forward gap, that occurrence moves to the next valid local time.

## Daily tasks

Check **Daily task** when adding or editing a task to place it in the third column. Daily tasks have no due date, due time, or age timer. Notes and weights are supported.

Click a daily task's checkbox, select it and click **Complete**, or press **Space** to mark it done. It stays in the same place and turns gray. Click its checkbox again, click **Undo**, or press **Space** to undo completion. Completed daily tasks remain in the Daily column rather than moving to the completed-task archive.

Daily tasks automatically become incomplete at **midnight in the system's local time zone**, even if the app was closed at midnight or for several days. The app stores which local date a daily task was completed on; it does not create duplicate tasks or a backlog for missed days. Daily tasks are separate from the existing repeat-schedule feature. Converting an occurrence from a repeating series to a daily task stops that series' future repeats and keeps its other existing occurrences.

## Saved data

Tasks save after each change to `%LOCALAPPDATA%\Simple Tasks\tasks.json`. Closing or restarting the app preserves them. The previous saved version is kept at `tasks.json.bak`. The app writes a temporary file and replaces the current save atomically; a failed save leaves the previous in-memory tasks unchanged.

Use **File → Export backup** to save a JSON copy elsewhere. To restore a backup manually, close the app, preserve a copy of the current task file, and replace `tasks.json` with the exported file. If a task file cannot be read, the app reports the error and preserves the file instead of overwriting it.

## Import a Canvas calendar file

Use **File → Import calendar (.ics)...**, choose a downloaded calendar file, and set **From** and **Through**. Both dates are inclusive and filter by the assignment's due date in the computer's local time zone. Review the checklist, choose a weight, and click **Import selected**. You can deselect individual entries before importing. The note preview shows the selected entry's description and link.

**Canvas assignments only** excludes other calendar events; uncheck it to import other entries. Titles, due dates, plain-text descriptions, and event links become ordinary dated tasks. Their creation date is the import time, since the calendar's modification timestamp is not a reliable task creation date. Creation dates can still be edited afterward. Descriptions are treated as text; no page, script, or attachment is opened or fetched.

Already imported entries are skipped, including completed tasks. Re-importing preserves your existing titles, notes, weights, completion status, and edited due dates. It does not synchronize changes from Canvas; edit an existing task if its deadline changes. Matching uses calendar identifiers and the event link's host, rather than the downloaded filename. Events without an identifier use a content-based fallback and display a warning.

The importer handles folded lines, escaped text, UTC dates, local dates, common named time zones, all-day dates, and VTODO deadlines. Date-only events are due at the end of their final included day. Timed VEVENT entries use their start time, which is how the provided Canvas assignments encode deadlines. Cancelled entries, unsupported recurrence rules, invalid dates, and unknown time zones are reported under **Skipped entries...**. Recurrence rules in calendar files are not expanded; the app's own repeating tasks remain a separate feature.

Format reference: [iCalendar, RFC 5545](https://www.rfc-editor.org/rfc/rfc5545.html).

## Canvas workflow

The manual `.ics` importer is the app's Canvas workflow. Automatic Canvas synchronization and expanded imports for other calendar/task suites are not planned.

