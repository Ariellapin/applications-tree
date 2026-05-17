# WPFToolbarTree

A WPF desktop application that displays a configurable applications/toolbar tree, with a tray-icon presence (Hardcodet.NotifyIcon.Wpf).

## Requirements

- Windows
- .NET 8 SDK (target framework: `net8.0-windows`)

## Build & run

```powershell
dotnet build
dotnet run
```

Or use the included `run.bat`.

## Project layout

- `Config/` — configuration classes
- `Models/` — domain models
- `Services/` — application services
- `Views/` — XAML dialogs and code-behind
- `Resources/` — app icon
