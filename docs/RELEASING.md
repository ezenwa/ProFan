# Release process

1. Update versions in `src/AssemblyInfo.cs` and `installer/ProFan.iss`.
2. Update `CHANGELOG.md` in English and any affected Spanish documentation.
3. Run `Build.ps1` on Windows 11.
4. Test Spanish and English installation.
5. Test 20%, 40%, 60%, 80%, 100%, and Automatic restoration.
6. Test notification icon animation, tooltip, menu, `--exit`, upgrade, and uninstall.
7. Create a signed Git tag such as `v1.0.1`.
8. Push the tag; GitHub Actions builds and uploads the installer artifact.
9. Create a GitHub Release and attach `ProFan-Setup.exe` plus its SHA-256 checksum.
