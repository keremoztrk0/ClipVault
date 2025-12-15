# ClipVault

A modern, cross-platform clipboard manager built with Avalonia UI and .NET 9.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey.svg)
![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)

## Features

- **Clipboard History** - Automatically saves text, images, and files you copy
- **Groups** - Organize clipboard items into color-coded groups
- **Global Hotkey** - Quick access with customizable keyboard shortcut (default: `Ctrl+Shift+V`)
- **Search** - Filter clipboard history instantly
- **Dark/Light Theme** - Follows your preference with adjustable window opacity
- **System Tray** - Runs quietly in the background
- **Auto Cleanup** - Configure max items and retention period
- **Start with System** - Optional automatic startup
- **Single Instance** - Prevents multiple instances from running

## Screenshots

*Coming soon*

## Installation

### Prerequisites

- [.NET 9.0 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)

### Build from Source

```bash
# Clone the repository[ClipVault.App.csproj](ClipVault.App/ClipVault.App.csproj)
git clone https://github.com/yourusername/ClipVault.git
cd ClipVault

# Build
dotnet build ClipVault.slnx -c Release

# Run
dotnet run --project ClipVault.App
```

## Usage

1. **Launch** - ClipVault starts minimized in the system tray
2. **Copy anything** - Text, images, or files are automatically saved
3. **Access history** - Press `Ctrl+Shift+V` (or your custom hotkey) to open
4. **Paste an item** - Double-click or press `Enter` to copy and paste
5. **Organize** - Right-click items to move them to groups

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+V` | Show/Hide ClipVault (customizable) |
| `Up/Down` | Navigate clipboard items |
| `Left/Right` | Navigate groups |
| `Enter` | Copy selected item and hide |
| `Delete` | Delete selected item |
| `Escape` | Hide window |

### Settings

Access settings via the gear icon to configure:

- Global hotkey
- Theme (Light/Dark/System)
- Window opacity
- Start with system
- Start minimized
- Show in taskbar
- Maximum history items
- Auto-delete retention period

## Tech Stack

- **UI Framework**: [Avalonia UI](https://avaloniaui.net/) 11.x
- **Architecture**: MVVM with [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- **Database**: SQLite with Dapper
- **Global Hotkeys**: [SharpHook](https://github.com/TolikPyl662/SharpHook)
- **Logging**: [Serilog](https://serilog.net/)

## Project Structure

```
ClipVault.App/
├── Controls/          # Custom UI controls (HotkeyRecorder)
├── Converters/        # Value converters for XAML bindings
├── Data/              # Database context and repositories
├── Helpers/           # Platform-specific helpers (hotkeys, tray, startup)
├── Models/            # Data models
├── Services/          # Clipboard monitoring and services
├── ViewModels/        # MVVM ViewModels
└── Views/             # XAML views and dialogs
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

> **Note**: This application was developed primarily using AI-assisted code generation. It is a non-profit hobby project created for learning and personal use. While functional, it may contain bugs or areas for improvement. Use at your own discretion.

## Acknowledgments

- [Avalonia UI](https://avaloniaui.net/) for the cross-platform UI framework
- [OpenCode](https://opencode.ai/) for AI-assisted development
