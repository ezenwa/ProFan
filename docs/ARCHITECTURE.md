# Architecture

```text
Fluent WinForms UI ─┬─ preset buttons
                    ├─ animated NotifyIcon + quick menu
                    └─ bilingual text provider
                             │
                      Fan control state
                             │
                      ASUS ATKACPI driver
                             │
                      Embedded controller / BIOS
```

ProFan is a single .NET Framework Windows executable. `AsusAcpi` opens `\\.\ATKACPI` and submits ASUS `DSTS`/`DEVS` requests. Entering manual control selects the ASUS full-speed performance endpoint once, waits briefly for the firmware transition, and then requires both CPU and GPU fan curves to be accepted. Automatic changes the performance endpoint back to the profile captured before manual mode.

Two timers are used: a one-second hardware/status timer and a lightweight notification-icon animation timer. While manual control is active, the status timer refreshes only the CPU and GPU curves every two seconds, without repeatedly switching the performance endpoint. If another ASUS service changes that endpoint, ProFan re-enters manual control and reapplies both curves. A failed maintenance request is retried on the next timer tick. Twelve icon frames are created once and disposed at shutdown.

A named mutex prevents multiple controller instances. Named events power `ProFan.exe --exit` and reopening the existing instance. The exit signal allows the installer/uninstaller to request a safe restore and shutdown; the show signal restores the main window, centers it in the working area of the screen containing the pointer, and brings it to the foreground.

The installer stores the chosen UI language in `ProFan.ini` beside the installed executable. The optional Windows-startup preference is stored per user as the `StartWithWindows` DWORD under `HKEY_CURRENT_USER\Software\ProFan`. Enabling it creates the elevated `ProFan-ASUS-HN7306` logon task, which launches `ProFan.exe --startup` directly in the notification area without a UAC prompt. Manual launches continue to show the main window. The previous `StartMinimized` preference is migrated automatically, and the uninstaller removes the task.

At startup, a background request checks the repository's latest GitHub Release with a seven-second timeout. A newer semantic version updates the notification-area action and displays an update balloon; network failures remain silent unless the user requests a manual check.
