# Audiovisual Presenter

Audiovisual Presenter is free software that helps to organise and display multiple PowerPoint, video, picture or audio files.

Main Website: [http://www.minsoft.org](http://www.minsoft.org)

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

On Windows:

```
dotnet build src/Presenter.slnx
dotnet test src/Presenter.slnx
```

The app executable is `src/Presenter.App/bin/Debug/net10.0-windows/Presenter.exe`.

On macOS the Windows-only projects don't build, so build the Avalonia app and
run the test projects directly:

```
dotnet build src/Presenter.App.Avalonia
dotnet test test/Presenter.Core.Tests
dotnet test test/Presenter.Data.Tests
```

To package a self-contained `Audiovisual Presenter.app`:

```
build/package-macos.sh            # osx-arm64 (default); pass osx-x64 for Intel
```

The bundle lands in `artifacts/macos/<rid>/` with an ad-hoc signature (enough
to run locally; distribution needs a Developer ID identity).

## Structure

- `src/Presenter.Core` — domain models and engine/settings abstractions (no WPF/COM/EF)
- `src/Presenter.Data` — EF Core + SQLite persistence, legacy SQL CE import
- `src/Presenter.Engine.Com` — full-fidelity playback by driving PowerPoint via COM (requires Office)
- `src/Presenter.Engine.Uno` — animated no-Office playback: drives live LibreOffice Impress slideshows over UNO
- `src/Presenter.App` — WPF operator UI (Windows)
- `src/Presenter.App.Avalonia` — Avalonia operator UI (macOS; also builds on Windows)
- `src/Presenter.SdfExport` — net48 one-time exporter for the old Database.sdf (SQL CE)
- `test/` — xUnit test projects
- `legacy/` — the original .NET Framework 4.5 application, kept for reference

## Playback engines

PowerPoint (COM) is the default engine and provides full-fidelity animations and
slide timings. Without Office, the LibreOffice UNO engine plays live Impress
slideshows — animations and transitions play, though complex PowerPoint effects
may be approximated (needs [LibreOffice](https://www.libreoffice.org/); no SDK
required, the engine drives LibreOffice through its bundled Python).
The preferred engine can be chosen in Options.

## Data

Schedules are stored in SQLite at `%LocalAppData%\AudiovisualPresenter\presenter.db`
on Windows and `~/Library/Application Support/AudiovisualPresenter/presenter.db` on
macOS; settings in `settings.json` alongside it. On first run an existing
`Database.sdf` from the previous version is imported automatically.
