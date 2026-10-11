# BepInEx Mod Launcher Demo

A standalone Windows launcher, validated with _Potion Craft: Alchemist Simulator_
and BepInEx 5.4.23.2. Plugin discovery does not depend on a predefined list of
Potion Craft mods. For other Windows games using BepInEx 5, select the game
executable manually. BepInEx 6, IL2CPP, and other platforms have not been validated.

## Usage

Run `BepInExModLauncher.exe` and keep `Mono.Cecil.dll` in the same directory.
The launcher uses .NET Framework 4.8 on Windows; installing .NET 10 is unnecessary.
The interface defaults to English and also supports Simplified Chinese. Use the
Settings tab to choose a language or explicitly follow the system language.
Changes apply immediately and are saved locally for the next launch. Unsupported
system languages fall back to English. DLL metadata and configuration contents
retain their original text.

Buttons size to their translated labels, action rows wrap, and headers and status
messages support multiple lines. The plugin list scrolls horizontally when its
minimum column widths exceed the available space. Both windows support DPI scaling.

1. The launcher checks the `PotionCraftPath` environment variable at process,
   user, and machine scope, then the previously selected executable and the
   launcher's directory. An executable supplied on the command line takes
   precedence. If discovery fails, use the game selection button to select the
   main game executable, rather than UnityCrashHandler.
2. Select the plugins to enable. The details panel shows GUIDs, declared
   dependencies, conflicts, process restrictions, assembly metadata, and
   configuration locations. Selecting a plugin recursively selects its installed
   required dependencies. Deselecting a dependency also deselects all plugins
   that require it. Optional dependencies are not selected automatically.
   Missing dependencies, insufficient versions, duplicate GUIDs, cycles, or
   conflicts reject the selection change and preserve the previous selection.
   The launcher does not download missing plugins.
3. Use the apply-and-launch button to apply the selection and start the game,
   or the apply button to save the selection without launching. Selections and
   configuration edits cannot be applied while the game is running.

All mods use the same discovery and dependency logic regardless of who wrote
or modified them. Plugin names, GUIDs, versions, dependencies, and conflicts
come from DLL attributes. Assembly names, assembly versions, companies, and
descriptions are read from the DLL when available. Company metadata is shown
as declared, rather than interpreted as verified authorship. Missing metadata
is not inferred from filenames or external inventories.

Only the enable checkbox is editable in the plugin list. Names, versions, and
paths are read-only. Sorting, row and column resizing, copying, and adding or
removing rows are disabled. Configuration editing uses a separate window.

Selections persist through the actual DLL state. The last game path and language
preference are stored in `%LOCALAPPDATA%/BepInExModLauncher/profile.json`.
English is used for new settings and legacy settings without a language choice.
Following the system language is an explicit option; saved language choices
remain effective.

## Discovery and activation

- Recursively read metadata from `.dll` and `.dll.disabled` files in
  `BepInEx/plugins`, without executing mod code.
- Identify plugins through `BepInPlugin` and read dependencies, minimum versions,
  declared conflicts, and process restrictions. Supporting libraries, PDBs,
  archives, and resource files are not listed as plugins.
- Group multiple plugins in one DLL into a single activation entry. The details
  panel lists their metadata separately.
- Disable a plugin by renaming `Example.dll` to `Example.dll.disabled`; enable it
  by restoring the original name. Resources, supporting libraries, and
  configurations stay in place.
- Block application when required dependencies are missing or too old, or when
  declared conflicts, duplicate GUIDs, or required dependency cycles exist.
  Optional dependencies do not need to be enabled together.
- Check DLL fingerprints and destination collisions before applying changes.
  Record a transaction journal and attempt rollback on failure. After an
  interrupted operation, use the recovery button to restore the previous state.
- Exclude preloader patchers in `BepInEx/patchers` from the activation list.

This controls BepInEx plugin discovery. Standard metadata cannot fully describe
custom loaders, direct DLL loading by filename, or undeclared dependencies.
Supporting libraries remain enabled when a plugin is disabled; dependencies
without metadata require manual inspection. Avoid running multiple launchers
simultaneously or updating mods while applying a selection.

## Configuration

Scan existing `.cfg` files in `BepInEx/config`. Associate them using the
`Plugin GUID` header when present, otherwise match `<GUID>.cfg`.

The configuration editor edits the original text, preserving comments, sections,
unknown keys, encoding, and line endings. Saving creates a timestamped backup
beside the configuration file. External changes are detected to prevent
overwriting another program's edits.

The launcher does not execute plugins to generate missing configurations or
infer their settings. Automatic association of custom JSON/TXT files, dynamic
configuration paths, READMEs, icons, and author information is not implemented.

## Information sources

Discovery and activation use the current scan results. Developer-specific
installation paths, installed mod inventories, and scan reports are not stored
in the source tree.

| Information | Source |
| --- | --- |
| Plugin name, GUID, and version | `BepInPlugin` attributes in the DLL |
| Required and optional dependencies, minimum versions | `BepInDependency` attributes in the DLL |
| Declared conflicts and process restrictions | `BepInIncompatibility` and `BepInProcess` attributes in the DLL |
| Assembly name, version, company, and description | Assembly metadata and attributes in the DLL, when provided |
| Enabled state and file location | Files in the selected game's `BepInEx/plugins` directory |
| Configuration location and contents | Existing `.cfg` files associated with a GUID read from the DLL |

SHA-256 is computed internally to guard against file changes during activation.
The launcher does not assign source categories or maintain editable provenance
labels. Missing DLL metadata is left unspecified. Configuration definitions
cannot always be recovered from DLL metadata, so editing uses existing files.

## Build

```powershell
# Use the configured PotionCraftPath environment variable.
dotnet build src/BepInExModLauncher/BepInExModLauncher.csproj -c Release -o artifacts/mod-launcher-demo

# Alternatively, pass the game directory as an MSBuild property.
dotnet build src/BepInExModLauncher/BepInExModLauncher.csproj -c Release -o artifacts/mod-launcher-demo "-p:PotionCraftPath=$env:PotionCraftPath"
```

The build references `Mono.Cecil.dll` supplied with the game's BepInEx installation
and copies it into the output directory. The launcher does not reference game
assemblies or the BepInEx runtime. Build this project separately to avoid
deploying it through the game plugin build workflow.

Mono.Cecil uses the MIT license. Keep the supplied third-party notices when
distributing the launcher.

## Validation

Scan without a window, reading the game directory and writing a report:

```powershell
Start-Process artifacts/mod-launcher-demo/BepInExModLauncher.exe -ArgumentList ('--scan "{0}" "artifacts/mod-launcher-demo/scan-report.tsv"' -f $env:PotionCraftPath) -WindowStyle Hidden -Wait
```

Copy installed DLLs and configuration files into a fresh test directory, then
verify activation, dependency selection and rejection, collision protection,
transaction recovery, configuration backups, external change detection,
multiple plugins per DLL, dependency cycles, DLL metadata, and language preferences:

```powershell
Start-Process artifacts/mod-launcher-demo/BepInExModLauncher.exe -ArgumentList ('--self-test "{0}" "artifacts/launcher-fixture-new"' -f $env:PotionCraftPath) -WindowStyle Hidden -Wait
```

The test destination must not already exist or be inside the real game
directory. Results are written to `test-results.txt` in the test directory.
Failed commands return a nonzero exit code and write `launcher-error.txt` in
the current working directory.

Validation leaves the real game installation unchanged. Actual game startup and
in-game behavior still require user verification.

Build output, test copies, scan reports, and previews belong in the Git-ignored
`artifacts/` directory. Machine-specific settings belong in the user directory;
do not copy them into the source tree or commit them to Git.
