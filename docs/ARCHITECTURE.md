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

A named mutex prevents multiple controller instances. A named event powers `ProFan.exe --exit`, allowing the installer/uninstaller to request a safe restore and shutdown.

The installer stores the chosen UI language in `ProFan.ini` beside the installed executable.
