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

ProFan is a single .NET Framework Windows executable. `AsusAcpi` opens `\\.\ATKACPI` and submits ASUS `DSTS`/`DEVS` requests. Manual presets write CPU and GPU fan curves; Automatic changes the performance endpoint back to the profile captured before manual mode.

Two timers are used: a one-second hardware/status refresh and a lightweight notification-icon animation timer. Twelve icon frames are created once and disposed at shutdown.

A named mutex prevents multiple controller instances. A named event powers `ProFan.exe --exit`, allowing the installer/uninstaller to request a safe restore and shutdown.

The installer stores the chosen UI language in `ProFan.ini` beside the installed executable.
