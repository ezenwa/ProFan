# Release process

1. Update versions in `src/AssemblyInfo.cs`, `installer/ProFan.iss`, and `CITATION.cff`; also update the release date in `CITATION.cff`.
2. Update `CHANGELOG.md` in English and any affected Spanish documentation.
3. Run `Build.ps1` on Windows 11.
4. Test Spanish and English installation.
5. Test 20%, 40%, 60%, 80%, 100%, and Automatic restoration.
6. Test notification icon animation, tooltip, menu, minimized startup, centered reopening, update checks, `--exit`, upgrade, and uninstall.
7. Create a version tag such as `v1.0.1`. Sign and verify it when a Git signing key is configured.
8. Push the tag; GitHub Actions builds and uploads the installer artifact.
9. Create a GitHub Release and attach `ProFan-Setup.exe` plus its SHA-256 checksum.
10. Verify the public release is marked **Latest**, both assets download correctly, and the published checksum matches the installer.
