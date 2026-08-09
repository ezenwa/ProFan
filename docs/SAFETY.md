# Safety

## Invariants

- Manual speed cannot be set below 20%.
- Closing the window preserves the current mode because ProFan remains active in the notification area.
- Automatic restoration is attempted on Exit, suspend, sign-out, process exit, installer upgrade, and uninstall.
- ProFan uses the ASUS firmware interface; it does not install a kernel driver.
- Only one ProFan instance can control the firmware.

## Limitations

- Abrupt power loss or forced process termination cannot run cleanup code.
- ASUS firmware may clamp, reinterpret, or override requested percentages.
- RPM availability depends on BIOS and driver behavior.
- Other ASUS utilities may overwrite the selected profile.

## Recovery

If behavior appears incorrect:

1. Choose **Automatic**.
2. Exit ProFan from its notification-area menu.
3. Restart Windows.
4. Select a normal profile in ProArt Creator Hub or BIOS.
5. Do not continue testing if temperatures or fan noises are abnormal.
