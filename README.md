# Colour Picker

An eyedropper for Windows that works like the one in PowerPoint. Point at anything on your screen and click to pick its colour. Then click the hex code to copy it.

## Download and install

You need Windows 10 or 11. Nothing else: no admin rights and no extra software.

1. Go to the [latest release](https://github.com/MickFinnHalse/colour-picker/releases/latest) and download **ColourPickerSetup.exe**.
2. Double-click it.
3. Windows may show **"Windows protected your PC"**. That happens because the app is new and isn't signed with a paid certificate, not because anything is wrong with it. Click **More info**, then **Run anyway**.
4. Click **Install**. Colour Picker is added to the Start menu and the Desktop, and opens straight into the eyedropper.

To pin it to the taskbar, open the Start menu, type **Colour Picker**, right-click it and choose **Pin to taskbar**.

To remove it, go to **Settings → Apps → Installed apps**, find **Colour Picker**, and choose **Uninstall**. This removes the app, its shortcuts and its saved colours.

**Rather not install anything?** Download **ColourPicker-…-portable.zip** from the same release, unzip it anywhere you can write to (for example a folder in Documents), and double-click `ColourPicker.exe`. It keeps its settings next to itself.

Some antivirus programs check or question new, unsigned apps the first time they run. If yours quarantines Colour Picker, you can restore it from the antivirus's quarantine and allow it.

## What it does

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

## Build it yourself

The build uses the C# compiler that comes with Windows (.NET Framework 4.x), so there is nothing to install.

1. Download or clone this repository.
2. In PowerShell, from the repository folder, run one of these:

   | Command | What it does |
   | --- | --- |
   | `.\build.ps1` | Compiles `ColourPicker.exe` into `C:\Tools\ColourPicker` and adds Desktop and Start menu shortcuts. Use `-OutDir <folder>` to build somewhere else. |
   | `.\build.ps1 -Package` | Builds the downloads into `C:\Tools\_dev\ColourPicker-dist`: `ColourPickerSetup.exe` (the installer, with the app inside it) and the portable zip. Use `-DistDir <folder>` to choose another folder. |

If PowerShell won't run the script, run `Unblock-File .\build.ps1` once, then try again. Close the app before rebuilding it.

## Files

| File | What it is |
| --- | --- |
| `src\ColourPicker.cs` | The whole app (WinForms, C# 5) |
| `setup\Setup.cs` | The installer and uninstaller (per user, no admin rights) |
| `build.ps1` | Draws the icon, compiles the app, and with `-Package` builds the installer and portable zip |
