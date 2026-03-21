# Open Editors for Visual Studio

> Bring the VS Code "Open Editors" experience into Visual Studio.

![VSIX](https://img.shields.io/badge/Visual%20Studio-VSIX-5C2D91)
![Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-blue)
[![License](https://img.shields.io/badge/license-MIT-green)](src/OpenEditors.VisualStudio.Vsix/LICENSE)

## Overview

Visual Studio does not provide a native **Open Editors** panel equivalent to VS Code.
When working with many files, switching context quickly can become harder than necessary.

**Open Editors for Visual Studio** adds that missing workflow with a focused tool window that tracks open documents in real time and provides quick actions for daily editing tasks.

## Features

- Real-time list of open documents
- Active document highlighting
- Dirty state indicator (`*`) for unsaved files
- Fast actions:
  - Save document
  - Duplicate document
  - Close document
  - Close all documents
- File-type icons resolved from Visual Studio image services (aligned with Solution Explorer)
- Theme-aware UI (Dark, Light, Blue)
- Native Visual Studio context menu (VSCT commands), inheriting IDE theme colors and menu structure
- Menu integration in `View → Other Windows`
- Smooth navigation: click an item to activate its document

## Screenshots

> Add screenshots under `docs/images`.

- [Main panel](docs/images/open-editors.png)
- [Context menu actions](docs/images/open-editors-context-menu.png)
- [Dark theme example](docs/images/open-editors-dark-theme.png)

## Installation

### From Visual Studio Marketplace

1. Open Visual Studio.
2. Go to `Extensions` → `Manage Extensions`.
3. Search for **Open Editors for Visual Studio**. https://marketplace.visualstudio.com/items?itemName=igorcostadev.openeditorsvs2026
4. Install and restart Visual Studio.

### Manual VSIX installation

1. Download the `.vsix` package from Releases or your build output.
2. Double-click the `.vsix` file.
3. Follow the installer prompts.
4. Restart Visual Studio.

## Usage

1. Open the tool window from:
   - `View` → `Other Windows` → `Open Editors`
2. Interact with items:
   - **Click** an item to activate/open it in the editor
    - **Right-click** for actions (Save, Duplicate, Close, Close All)
   - Use the inline **close (X)** button to close a document quickly
3. Watch status indicators:
   - Bold text for the active document
   - `*` marker for unsaved files
   - File icon matching document type

## Architecture

The extension is designed with a clean separation of concerns.

- **UI layer**
  - WPF ToolWindow (`OpenEditorsToolWindowControl.xaml`)
  - Data-bound item template and interaction events
  - Native context menu invocation via `IVsUIShell.ShowContextMenu`
- **Service layer**
  - Document tracking and actions
  - File icon resolution
  - Theme integration
  - File duplication action (`DuplicateDocumentAsync`)

### Technical design

- Built as an `AsyncPackage`
- Uses `DTE` and `RunningDocumentTable` events for real-time updates
- Uses `ObservableCollection<OpenDocumentItem>` for UI synchronization
- Uses VSCT/OleMenuCommand for native context-menu commands
- MVVM-style binding approach (View binds to model/service state)
- Async-first patterns with `JoinableTaskFactory` to respect Visual Studio threading rules

## Roadmap

- Pin files in Open Editors list
- Search/filter open documents
- Git status indicators (modified/added/deleted)
- Error indicators from diagnostics/Error List

## Contributing

Contributions are welcome.

1. Fork the repository
2. Create a feature branch
3. Implement changes with tests/build validation
4. Open a Pull Request with a clear description and screenshots (if UI changes)

Please keep changes focused, production-ready, and aligned with existing architecture.

## License

MIT License. See [LICENSE](LICENSE).

## Author

**Igor Costa**  
Software engineering leader focused on cloud platforms, software architecture, and high-impact developer productivity solutions.
