# TiHiY Control Center

Clean v0.1 for Star Citizen HOSAM profiles.

## What works in v0.1

- Open Star Citizen `layout_*.xml` files.
- Show device options and action binds.
- Count empty `js1_` joystick binds.
- Apply TiHiY HOSAM axis defaults in memory.
- Create backup of the original XML.
- Export a new XML with **Save As XML**.
- GitHub Actions builds a Windows portable package automatically.

## How to upload to GitHub

Upload the **contents** of this folder to the repository root.
Do not upload this ZIP as a single file.

Required folders/files:

```text
.github/workflows/build-windows.yml
src/TiHiY.ControlCenter/TiHiY.ControlCenter.csproj
src/TiHiY.ControlCenter/*.cs
src/TiHiY.ControlCenter/*.axaml
README.md
CHANGELOG.md
```

## How to build on GitHub

1. Open the repository on GitHub.
2. Go to **Actions**.
3. Choose **Build Windows Portable**.
4. Press **Run workflow**.
5. Download artifact **TiHiY-Control-Center-win-x64**.
6. Run `TiHiY.ControlCenter.exe` from the downloaded artifact.

## How to use

1. Run the app.
2. Click **Open XML** and select your Star Citizen `layout_300126_exported.xml`.
3. Click **Create Backup**.
4. Adjust values if needed.
5. Click **Apply TiHiY Defaults**.
6. Click **Save As XML**.
7. Import the exported XML in Star Citizen.

## Star Citizen import path

Inside the game:

```text
Options → Keybindings → Advanced Controls Customization → Control Profiles → Import
```

## Roadmap

- v0.2: editable list of axis options.
- v0.3: TARGET `.tmc` generator.
- v0.4: button map view.
- v0.5: joystick tester.
