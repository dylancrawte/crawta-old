# CrawtaDesktop

Old recovered version of my music site — rebuilt as a desktop app in **C#** / **.NET** with Avalonia.

Just keeping this here for archive. It's not what's live anymore.

## Current version

Repo: [github.com/dylancrawte/crawta](https://github.com/dylancrawte/crawta)

Live site: [crawta.vercel.app](https://crawta.vercel.app/)

## How it's laid out

- **`App.axaml.cs`** — starts the app and opens the main window
- **`MainWindow.axaml`** — the UI (hero, tracks, player, newsletter, links)
- **`MainWindow.axaml.cs`** — handles clicks, scrolling, updating the player on screen
- **`AudioPlayerService.cs`** — plays audio via ffmpeg (`ffplay` / `ffprobe` — you'll need ffmpeg installed)

`Program.cs` boots Avalonia → `App` loads `MainWindow` → track buttons call into `AudioPlayerService` for files in `Assets/audio/`. The `.axaml` has the layout; the `.cs` file hooks it up and keeps the progress bar / now playing text in sync.
