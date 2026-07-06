# TiHiY Control Center v0.2

Windows WinForms/.NET 8 editor for Star Citizen HOSAM XML profiles.

## v0.2 functions

- Open `layout_*.xml` Star Citizen files.
- Read T.16000M axis `deadzone` and `saturation` values.
- Show all action binds in a table.
- Count empty `js1_` joystick binds.
- Apply TiHiY HOSAM defaults.
- Backup original XML.
- Save new XML as `layout_TiHiY_HOSAM_v0_2.xml`.

## GitHub build

Upload all files to the repository, then run:

Actions → Build Windows Portable → Run workflow

Download the artifact `TiHiY-Control-Center-win-x64` and run `TiHiY.ControlCenter.exe`.
