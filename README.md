# Audiovisual Presenter

Audiovisual Presenter is free software that helps to organise and display multiple PowerPoint, video, picture or audio files.

Main Website: [http://www.minsoft.org](http://www.minsoft.org)

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```
dotnet build src/Presenter.slnx
dotnet test src/Presenter.slnx
```

The app executable is `src/Presenter.App/bin/Debug/net10.0-windows10.0.17763.0/Presenter.exe`.

## Structure

- `src/Presenter.Core` — domain models and engine/settings abstractions (no WPF/COM/EF)
- `src/Presenter.Data` — EF Core + SQLite persistence, legacy SQL CE import
- `src/Presenter.Engine.Com` — full-fidelity playback by driving PowerPoint via COM (requires Office)
- `src/Presenter.Engine.Render` — no-Office fallback: LibreOffice renders slides to static images
- `src/Presenter.App` — WPF operator UI
- `src/Presenter.SdfExport` — net48 one-time exporter for the old Database.sdf (SQL CE)
- `test/` — xUnit test projects
- `legacy/` — the original .NET Framework 4.5 application, kept for reference

## Playback engines

PowerPoint (COM) is the default engine and provides live animations and slide timings.
When Office is not installed, the app falls back to the LibreOffice render engine
(static slides; needs [LibreOffice](https://www.libreoffice.org/)). The preferred
engine can be chosen in Options.

## Data

Schedules are stored in SQLite at `%LocalAppData%\AudiovisualPresenter\presenter.db`;
settings in `settings.json` alongside it. On first run an existing `Database.sdf`
from the previous version is imported automatically.
