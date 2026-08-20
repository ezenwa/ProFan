# Changelog

All notable changes follow the principles of [Keep a Changelog](https://keepachangelog.com/).

## [1.1.17] - 2026-08-20

### Added

- Restores ASUS Automatic control when the lid closes or Windows suspends or hibernates.
- Reapplies the previous manual percentage after the lid opens or Windows resumes, with bounded ATKACPI recovery retries.

### Fixed

- Stops the manual-curve maintenance timer before a low-power transition so it cannot reassert manual control during sleep entry.

## [1.1.16] - 2026-08-13

### Fixed

- Makes the notification-area startup option register an elevated logon task so ProFan actually starts with Windows and opens minimized.
- Migrates the previous Start minimized preference and removes the startup task during uninstall.

## [1.1.15] - 2026-08-12

### Changed

- Removes the redundant ProFan heading from notification messages.

## [1.1.14] - 2026-08-12

### Changed

- Updates the notification-area preview to show Start minimized and Check for updates.
- Aligns the Windows manifest identity with the application release version.
- Makes the build fail when version metadata or required interface previews are out of sync.

## [1.1.13] - 2026-08-12

### Added

- Checks GitHub Releases for updates at startup and provides a manual notification-area check action.

### Fixed

- Restores the main window centered on the active screen from Open ProFan, independently of the Start minimized preference.
- Reopening ProFan from a shortcut now shows the existing instance instead of displaying an already-running message.

## [1.1.9] - 2026-08-12

### Added

- Adds a persistent Start minimized preference to the notification-area menu.

## [1.1.8] - 2026-08-09

### Fixed

- Keeps manual fan speeds stable by refreshing only the CPU and GPU curves without repeatedly switching the ASUS performance profile.
- Recovers manual control if another ASUS service changes the active performance mode.
- Requires both CPU and GPU fans to accept a manual curve before reporting success.

## [1.1.7] - 2026-08-08

### Changed

- Closing the main window now preserves the active Automatic or manual mode while ProFan continues in the tray.
- Automatic restoration remains active for full Exit, suspend, sign-out, process termination, upgrades, and uninstall.

## [1.1.6] - 2026-08-08

### Fixed

- Prevented the initially focused 20% button from appearing selected on startup.
- Shows focus cues only during keyboard navigation, following standard Windows behavior.

## [1.1.5] - 2026-08-08

### Changed

- Highlights Automatic as the active selection when ProFan first opens.
- Changes the Automatic button label according to whether it represents the current state or a return action.

## [1.1.4] - 2026-08-08

### Changed

- Mapped tray-fan animation speed to the selected 20–100% manual preset.
- In Automatic mode, approximates animation speed from the highest reported fan RPM.

### Fixed

- Corrected animation frames to cover a complete 360-degree revolution.

## [1.1.3] - 2026-08-08

### Fixed

- Added consistent vertical spacing between each About information group.
- Removed the inner focus ring from the About action to match the main Automatic button.

## [1.1.2] - 2026-08-08

### Fixed

- Removed the native default-button outline that conflicted with the Fluent border in About.
- Preserved Enter and Escape keyboard behavior without adding a second visual border.

## [1.1.1] - 2026-08-08

### Changed

- Simplified the About dialog by removing the product-description line.

## [1.1.0] - 2026-08-08

### Added

- Added a bilingual Fluent About dialog with version, author, copyright, and license details.

### Fixed

- Explicitly returns fan control to the ASUS firmware whenever ProFan starts.
- Recovers the standard automatic profile if a forced fan mode survived an interrupted session.

## [1.0.10] - 2026-08-08

### Fixed

- Balanced the footer margin against the top title margin.
- Tightened internal spacing while preserving the two-line footer and fixed window size.

## [1.0.9] - 2026-08-08

### Fixed

- Reserved two complete lines for the footer at every supported DPI scale.
- Split the footer message into two short sentences for clearer reading.

## [1.0.8] - 2026-08-08

### Fixed

- Replaced absolute UI positions with a DPI-aware vertical layout calculated from rendered bounds.
- Recomputed button widths and footer wrapping from the actual fixed client area.

## [1.0.7] - 2026-08-08

### Fixed

- Wrapped the footer within the fixed window width in both Spanish and English.

## [1.0.6] - 2026-08-08

### Fixed

- Positioned the subtitle relative to the rendered title bounds to prevent overlap at high DPI.
- Refined title size while preserving the fixed window dimensions.

## [1.0.5] - 2026-08-08

### Changed

- Locked the main window to its designed size and disabled resize, maximize, and Snap layouts.

## [1.0.4] - 2026-08-08

### Fixed

- Added Per-Monitor DPI Awareness V2 for sharp rendering on scaled displays.
- Replaced fallback font-family aliases with native Segoe UI Variable families and weights.
- Enabled DPI autoscaling and double-buffered form painting.

## [1.0.3] - 2026-08-08

### Fixed

- Cleared every animated button frame to prevent paint trails.
- Removed non-Fluent drop shadows that looked like duplicate controls.
- Made Automatic a disabled secondary action when Automatic is already active.

## [1.0.2] - 2026-08-08

### Changed

- Refined percentage buttons with Fluent 2 motion, hover, pressed, selected, focus, border, and shadow states.

## [1.0.1] - 2026-08-08

### Added

- English and Spanish installer/application localization.
- Animated notification-area fan icon.
- Compact two-line mode and RPM status.
- Safe `--exit` command for upgrades and uninstall.
- Fluent-inspired dark blue interface.

### Fixed

- Installer post-install launch now uses ShellExecute for UAC elevation.
- Automatic button contrast in dark mode.

## [1.0.0] - 2026-08-08

- Initial HN7306 fan-control release.
