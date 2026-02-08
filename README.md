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

`TODO: add screenshot - Timer screen`

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

`TODO: add screenshot - History screen`

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

`TODO: add screenshot - Day Detail screen`

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

`TODO: add screenshot - Settings screen`

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
