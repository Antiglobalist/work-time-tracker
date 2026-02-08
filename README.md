# Work Time Tracker

A small desktop app for tracking real work time on a Windows PC.

I built this project for interest and learning, using a vibe-coding style. At the same time, it is a practical utility: it helps measure working time more accurately by separating active time from away time.

## Project Idea

Many time trackers only count manual start/stop actions. This app also tracks real activity and inactivity events:
- keyboard and mouse activity
- screen off / sleep / lock states
- wake-up behavior (with false wake protection)
- optional Git branch context

The goal is simple: clear daily history with realistic numbers.

## Tech Stack

- C#
- WPF (.NET `net10.0-windows10.0.22621.0`)
- MVVM (`CommunityToolkit.Mvvm`)
- SQLite (`Microsoft.EntityFrameworkCore.Sqlite`)
- Windows system integrations (power events, tray icon, input hooks)

## Main Screens

### 1. Timer Screen

`Timer` is the main dashboard for today.

What you can see:
- tracking status (`Running` / `Stopped`)
- start/stop button
- active time and away time for current work day
- first and last activity times
- list of activity/inactivity intervals
- Git branch summary for today (if repository path is set)

What you can do:
- start tracking
- stop tracking
- monitor your day in real time

<img width="1225" height="916" alt="image" src="https://github.com/user-attachments/assets/a7701e6d-3a82-493b-9a3a-d45978a2c529" />

### 2. History Screen

`History` shows saved days on the left and selected day details on the right.

What you can see:
- list of tracked days
- total "at PC" time for each day in the list
- for selected day: active/away totals, start/end times, total day time
- session intervals timeline
- Git branch summary for that day

What you can do:
- browse previous days
- compare work patterns
- open full day details

<img width="833" height="536" alt="image" src="https://github.com/user-attachments/assets/7ee5135b-355e-4887-af35-39fd2133fa5f" />

### 3. Day Detail Screen

`Day Detail` is a focused view for one day.

What you can see:
- selected date
- full day totals (active, away, total)
- detailed interval list
- Git branch summary for the day

What you can do:
- inspect a single day deeply
- return back to History

<img width="881" height="753" alt="image" src="https://github.com/user-attachments/assets/c8737c2c-5a9c-4358-a2f3-e8826fccade6" />

### 4. Settings Screen

`Settings` controls behavior and app preferences.

Available options:
- start app with Windows
- minimize to tray when closing window
- app language (`Auto`, `Russian`, `English`)
- work day start hour (for night shifts and late sessions)
- inactivity detection mode:
  - by screen off
  - by inactivity timeout
- timeout value (if timeout mode is selected)
- Git repository folder for branch tracking

What you can do:
- save settings and apply language
- choose your Git project path

<img width="1062" height="720" alt="image" src="https://github.com/user-attachments/assets/b5b95547-a24f-4aa0-a64d-ab4ecda35984" />

## Core Functions

- Automatic tracking starts on app startup.
- Session types are separated into:
  - `Activity`
  - `Inactivity` (with reason)
  - `Sleep`
- Inactivity reasons include:
  - keyboard/mouse idle
  - screen off
  - PC sleep
  - lock/unlock flow
- Wake validation helps ignore false wake-ups, so random wake events do not create fake work intervals.
- Git branch tracker polls current branch and creates branch sessions over time.
- Data is stored locally in SQLite.

## Focus Time

The app also calculates focus quality from continuous `Activity` sessions:
- `High focus`: session duration is equal to or higher than the selected threshold.
- `Medium focus`: session duration is lower than the selected threshold.

You can configure the high-focus threshold in `Settings`:
- `30 min`
- `1 h`
- `2 h`
- `3 h`

The app also shows estimated effective work time:

`Estimated effective work = High focus * High focus % + Medium focus * Medium focus %`

Both percentages are configurable in `Settings`:
- `High focus work percent` (default `95%`)
- `Medium focus work percent` (default `85%`)

This data is visible in the `Focus quality` block on:
- `Timer` screen (current day)
- `Day Detail` screen

## Data Storage

The app stores local data in:
- `%LocalAppData%/WorkTimeTracking/worktimetracking.db`
- `%LocalAppData%/WorkTimeTracking/settings.json`

No cloud sync is required.

## How to Run

1. Install .NET SDK that supports the target framework in this project.
2. Build and run:

```powershell
dotnet restore
dotnet build
dotnet run
```

## Notes

This is an experiment-driven project, but it is useful in daily work. The main focus is practical time tracking with better accuracy than manual timers.
