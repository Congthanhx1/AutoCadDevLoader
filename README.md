# AutoCadDevLoader

[English](README.md) | [Tiếng Việt](README.vi.md)

AutoCadDevLoader is a compact development loader for AutoCAD .NET plugins. It shortens the edit-build-test loop by loading a temporary copy of your plugin DLL, discovering its commands, and giving you a searchable quick panel to reload and run them without repeatedly restarting AutoCAD.

It is intended for plugin development and debugging. It is not a replacement for an AutoCAD Application Bundle or a production deployment system.

## Demo

https://github.com/user-attachments/assets/6f62afa9-705f-427a-b9c4-42de544da431

> The video above shows how to load **CadDevLoader** into AutoCAD and inject a plugin DLL for live development.

## Why AutoCadDevLoader

- **Reload without locking the source DLL** — the selected build DLL is copied to a unique folder under `%TEMP%\CadDevLoader` and AutoCAD loads that copy. Your original build output remains available to the compiler.
- **Automatic command discovery** — scans loaded assemblies for parameterless methods marked with AutoCAD's `CommandMethod` attribute.
- **Fast command launcher** — search and run discovered commands from a compact in-AutoCAD panel.
- **Favorites and recent commands** — pin frequently used commands and return to the commands you used most recently.
- **Build watcher** — watches the selected DLL and notifies you when a new build is ready to reload.
- **One-click reload** — Reload is kept as the primary action; less common actions stay in the `⋯` menu.
- **Reload feedback** — clear success, pending-build, and error notifications make the active loader state easy to understand.
- **UI-hook cleanup** — loader-owned UI and event hooks are removed when the panel is replaced or closed, reducing duplicate callbacks during a long development session.
- **Status, log, and cache tools** — inspect the active DLL and loader state, review errors, and clean temporary reload copies from the UI.
- **Vietnamese and English UI** — switch between `Tiếng Việt` and `English` with the `VI/EN` button; the selected language is remembered for later AutoCAD sessions.
- **Two source targets** — the repository contains a Net48 project for AutoCAD 2021–2024 and a Net8 project for AutoCAD 2025–2026.
- **One build location** — generated DLL/PDB output is kept under `Build/` instead of being scattered across project folders.

## How non-locking reload works

1. You select the DLL produced by your plugin project.
2. AutoCadDevLoader copies it to a unique directory under `%TEMP%\CadDevLoader`.
3. AutoCAD loads the temporary copy, not the original build output.
4. AutoCadDevLoader scans the loaded assembly for supported commands and refreshes the quick panel.
5. After the next successful build, the watcher reports that an updated DLL is available. Click **Reload** or run `DEVRELOAD`.

This approach prevents the most common development problem: AutoCAD locking the DLL that your compiler needs to overwrite.

> **Important:** AutoCAD cannot truly unload a managed assembly from its current process. Each reload loads another temporary assembly copy. AutoCadDevLoader keeps the source DLL buildable and cleans up the UI hooks it owns, but it cannot automatically undo arbitrary static state, event subscriptions, windows, timers, or resources created by your plugin. Design development plugins to initialize idempotently and clean up their own resources. Restart AutoCAD when you need a completely clean process.

## Compatibility and binary availability

| AutoCAD version | Runtime | Loader project/DLL | v1.0.0 availability |
|---|---|---|---|
| AutoCAD 2021–2024 | .NET Framework 4.8 | `CadDevLoader.Net48.dll` | Prebuilt binary included in the release |
| AutoCAD 2025–2026 | .NET 8 for Windows | `CadDevLoader.Net8.dll` | Source project included; build locally against the appropriate AutoCAD 2025/2026 managed API |

The **v1.0.0 GitHub Release contains only the prebuilt `CadDevLoader.Net48.dll`**. It does not contain a ready-made Net8 binary because the required AutoCAD 2025/2026 API assemblies were not available in the release build environment.

The Net8 project is available in the repository for AutoCAD 2025–2026 users to build with the managed API assemblies that match their AutoCAD environment. AutoCAD 2027 is not currently supported; do not assume a binary built against the 2025/2026 API is compatible with AutoCAD 2027.

Use only the loader that matches the AutoCAD version currently running. The plugin DLL you are developing must also target a runtime compatible with that AutoCAD release.

## Quick start

### AutoCAD 2021–2024: use the prebuilt release

1. Download the latest package from [GitHub Releases](https://github.com/Congthanhx1/AutoCadDevLoader/releases).
2. If Windows marks the download as blocked, open the ZIP file's **Properties**, select **Unblock**, and then extract it.
3. Keep the extracted files in a stable local folder.
4. Start AutoCAD and run `NETLOAD`.
5. Select `CadDevLoader.Net48.dll`.
6. Run `DEVSHOW` to open the quick panel.
7. Open the `⋯` menu, choose **Load/Change DLL**, and select the build DLL of the plugin you are developing.
8. Build your plugin. When AutoCadDevLoader detects the new output, click **Reload**.
9. Search for a discovered command and run it directly from the panel.

Do not `NETLOAD` the plugin-under-development directly in the same AutoCAD session. Let AutoCadDevLoader load its temporary copy.

### AutoCAD 2025–2026: build the Net8 project

The v1.0.0 release does not provide a prebuilt Net8 asset.

1. Clone or download this repository.
2. Install the .NET 8 SDK and use the managed API assemblies from the appropriate AutoCAD 2025/2026 installation or SDK.
3. Build `CadDevLoader.Net8/CadDevLoader.Net8.csproj` using those references.
4. Run `NETLOAD` in AutoCAD and select `Build/CadDevLoader.Net8.dll`.
5. Run `DEVSHOW`, then follow the same Load/Change DLL and reload workflow above.

See [Build from source](#build-from-source) for the repository build layout.

### Daily development loop

```text
Edit code → Build plugin → Reload notification → Reload → Run command
```

The selected DLL, favorites, recent commands, and UI language are retained so the next iteration stays quick.

## Quick panel

The panel is deliberately small and focused:

- **Reload** is always the main action.
- **Search** filters the discovered command list immediately.
- **Favorites** contains commands you pinned with the star button.
- **Recent** contains commands launched recently.
- **All commands** contains every supported command discovered in the current plugin copy.
- The **star button** pins or unpins a command.
- The **`VI/EN` button** on the header switches the loader interface between Vietnamese and English.
- The **`⋯` menu** contains Load/Change DLL, status/log, and cache actions.
- Errors remain collapsed at the bottom until you need their details.

If the panel is closed, run `DEVSHOW` to bring it back.

## AutoCAD commands

| Command | Purpose |
|---|---|
| `DEVSHOW` | Opens or focuses the AutoCadDevLoader quick panel. |
| `DEVLOAD` | Prompts for a plugin DLL, loads it through a temporary copy, and refreshes discovered commands. |
| `DEVRELOAD` | Reloads the currently selected DLL and refreshes the command list. |
| `DEVLIST` | Lists the supported commands discovered in the current plugin. |
| `DEVRUN` | Prompts for and runs one of the discovered commands. |
| `DEVSTATUS` | Shows the current DLL, watcher, reload, cache, and error status. |

The panel and these command-line commands use the same active loader state, so either workflow can be used at any time.

## Command discovery rules

A method appears in AutoCadDevLoader when:

- it is exposed as an AutoCAD command with `CommandMethod`; and
- it has no method parameters.

For example:

```csharp
using Autodesk.AutoCAD.Runtime;

public class SampleCommands
{
    [CommandMethod("HELLODEV")]
    public void HelloDev()
    {
        // Command implementation
    }
}
```

Commands that are created dynamically, require method parameters, or live in an assembly that failed to load will not appear in the quick panel.

## Language

Click the `VI/EN` button on the quick-panel header to switch between **English** and **Tiếng Việt**. AutoCadDevLoader updates its own interface and stores the choice for the next session. This changes the loader UI only; it does not change AutoCAD's language or the command names supplied by the loaded plugin.

## Build from source

### Requirements

- Windows
- A supported AutoCAD installation or compatible AutoCAD managed reference assemblies
- .NET Framework 4.8 developer tools for the Net48 loader
- .NET 8 SDK plus the appropriate AutoCAD 2025/2026 managed API assemblies for the Net8 loader

On a machine configured with the required Autodesk references, use the central build entry point:

```powershell
.\Build-Loaders.cmd
```

Or call the PowerShell script directly:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-Loaders.ps1
```

The scripts discover a compatible installed AutoCAD reference location when available. If the Net8 references are not installed, build the Net8 project later on a machine that has the appropriate AutoCAD 2025/2026 API. If a direct `dotnet build` cannot find Autodesk assemblies, use the build script or provide the correct AutoCAD installation/reference path to MSBuild.

`Build/` contains only the DLL/PDB outputs that were successfully built:

```text
Build/
├── CadDevLoader.Net48.dll
├── CadDevLoader.Net48.pdb
├── CadDevLoader.Net8.dll      # present after a successful Net8 build
└── CadDevLoader.Net8.pdb      # present after a successful Net8 build
```

Intermediate files are kept outside the repository at `%TEMP%\CadDevLoaderBuild\<ProjectName>` by `Directory.Build.props`. Project folders therefore do not accumulate `bin/` or `obj/` output, and `Build/` stays clean for loading and packaging.

## Repository structure

```text
AutoCadDevLoader/
├── Shared/
│   └── DevLoaderCommands.cs       # shared loader commands, reload logic, and UI
├── CadDevLoader.Net48/
│   └── CadDevLoader.Net48.csproj  # AutoCAD 2021–2024
├── CadDevLoader.Net8/
│   └── CadDevLoader.Net8.csproj   # AutoCAD 2025–2026
├── Directory.Build.props          # Build output + temporary intermediate paths
├── Build-Loaders.ps1              # PowerShell build entry point
├── Build-Loaders.cmd              # convenient Windows build entry point
├── README.md
└── README.vi.md
```

`Build/` is generated locally and intentionally not committed.

## Limitations and recommended plugin design

- A managed assembly already loaded into AutoCAD cannot be removed from that process. Reload creates a new temporary copy.
- AutoCadDevLoader calls the previous plugin's `IExtensionApplication.Terminate()` implementations and, when present, a parameterless static `DevCleanup` or `CloseAllPalettes` hook before initializing the new copy. Your plugin must implement those cleanup paths correctly, unsubscribe its AutoCAD events, dispose windows and timers, and prevent duplicate initialization.
- Changes to global/static state, assembly-level initialization, native dependencies, or complex UI frameworks may still require an AutoCAD restart.
- Only parameterless `CommandMethod` methods are included in automatic discovery.
- Managed dependencies beside the source DLL are copied to the temporary cache and resolved from there. They must still be compatible with the running AutoCAD version. A same-identity dependency already loaded in AutoCAD can remain bound to its old version, so dependency changes may require assembly versioning or an AutoCAD restart.
- The v1.0.0 release includes only the Net48 binary. The Net8 project must be built locally against appropriate AutoCAD 2025/2026 references.
- AutoCAD 2027 compatibility has not been established.
- Reloading is a development convenience, not process isolation. Test the final plugin in a fresh AutoCAD session before release.

## Troubleshooting

### `NETLOAD` rejects the loader

- Confirm that you selected the correct runtime from the compatibility table.
- For AutoCAD 2025–2026, confirm that you built the Net8 project against appropriate API references; do not load the Net48 release binary.
- Unblock the downloaded ZIP or DLL in Windows file properties.
- Check AutoCAD trusted paths and security settings.
- Do not mix the Net48 and Net8 loaders in one AutoCAD session.

### The Net8 DLL is missing from the release

This is expected for v1.0.0. The release contains only `CadDevLoader.Net48.dll`. Build `CadDevLoader.Net8.csproj` locally with the appropriate AutoCAD 2025/2026 managed API assemblies.

### The plugin DLL still appears locked

- Make sure you selected it through `DEVLOAD` or **Load/Change DLL**.
- Do not also load the original plugin DLL directly with `NETLOAD`.
- Another process, debugger, dependency loader, or antivirus scanner may own the lock; use `DEVSTATUS` to confirm the source and temporary paths.

### A command is missing

- Confirm the method has `CommandMethod` and takes no parameters.
- Check the error area or status/log view for assembly or dependency load failures.
- Verify that AutoCadDevLoader is watching the DLL produced by the configuration you just built.
- Reload manually with `DEVRELOAD`.

### AutoCadDevLoader reports an old build

- Confirm the selected source path matches your current project output.
- Wait for the build to complete before reloading.
- Run `DEVSTATUS`, then `DEVRELOAD`.
- If necessary, use the cache cleanup action and restart AutoCAD for a completely clean process.

### An event or UI action runs more than once

The previous plugin assembly is still present in AutoCAD. Make your plugin's initialization idempotent and explicitly unsubscribe/dispose resources that it creates. Restart AutoCAD after structural changes that cannot be cleaned up safely.

### The panel was closed

Run `DEVSHOW`.

### The temporary cache is growing

Use the cache cleanup action in the `⋯` menu. A file that is still loaded by the current AutoCAD process may only be removable after AutoCAD exits.

## Feedback

Please use [GitHub Issues](https://github.com/Congthanhx1/AutoCadDevLoader/issues) for reproducible bugs and feature requests. Include the AutoCAD version, loader runtime, plugin target framework, and the relevant status/error text.
