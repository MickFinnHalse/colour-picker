# Colour Picker

A small Windows colour picker that works like the one in PowerPoint. Pick a colour, see its hex code, and click to copy it.

- Saturation and brightness spectrum with a hue bar
- Hex and R, G, B fields that stay in sync
- Click the hex code, or the colour swatch, to copy it (for example `#4472C4`). Ctrl+C works too.
- **Copy RGB** copies the numbers, for example `68, 114, 196`
- **Pick from screen** eyedropper with a magnifier. It works across every monitor. Click or press Enter to pick, use the arrow keys to nudge by one pixel, and press Esc to cancel.
- Office's ten standard colours, plus your last 12 colours
- **Always on top**, so the picker stays above PowerPoint, Canva or a browser

Settings and recent colours are saved in `%LOCALAPPDATA%\ColourPicker\settings.txt`.

## Install

You need Windows 10 or 11. Nothing else is required: the build uses the C# compiler that comes with Windows (.NET Framework 4.x).

1. Download or clone this repository.
2. In PowerShell, from the repository folder, run:

   ```powershell
   .\build.ps1
   ```

   This draws the icon, compiles `ColourPicker.exe` into `C:\Tools\ColourPicker`, and adds a **Colour Picker** shortcut to the Desktop and the Start menu. To build somewhere else, use `.\build.ps1 -OutDir <folder>`.

3. To pin it to the taskbar, open the app, right-click its taskbar button and choose **Pin to taskbar**.

If PowerShell won't run the script, run `Unblock-File .\build.ps1` once, then try again.

To rebuild after changing the code, close the app first, then run `.\build.ps1` again.

Antivirus tools may scan or question a newly built `.exe`, because it is unsigned and new to them. Building into a folder your antivirus already trusts avoids repeated prompts.

## Files

| File | What it is |
| --- | --- |
| `src\ColourPicker.cs` | The whole app (WinForms, C# 5) |
| `build.ps1` | Draws the icon, compiles the app and creates the shortcuts |
