# Colour Picker

An eyedropper for Windows that works like the one in PowerPoint. Point at anything on your screen and click to pick its colour. Then click the hex code to copy it.

**The eyedropper**
- Opens straight into eyedropper mode. Move the pointer over anything in any program, on any monitor.
- A magnifier follows the pointer and shows the colour underneath and its hex code, live as you move.
- Click (or press Enter) to pick. Arrow keys nudge the pointer by one pixel. Esc or right-click cancels.
- Pick again with the **Pick again** button, by opening the app again from its shortcut, or with **Win+Shift+C** from anywhere while the app is open. If another app already uses Win+Shift+C, the shortcut is Ctrl+Alt+Shift+C; the app shows which.

**After picking**
- Click the hex code, or the colour swatch, to copy it (for example `#4472C4`). Ctrl+C works too.
- **Copy RGB** copies the numbers, for example `68, 114, 196`
- Saturation and brightness spectrum with a hue bar, and Hex and R, G, B fields that stay in sync
- Office's ten standard colours, plus your last 12 colours
- **Always on top** keeps the window above PowerPoint, Canva or a browser. Untick **Start picking when it opens** to open the window without the eyedropper.

The eyedropper reads the live screen. It doesn't use mouse or keyboard hooks.

Settings and recent colours are saved in `settings.txt` next to `ColourPicker.exe`. A short diagnostic log, `log.txt`, is kept there too.

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
