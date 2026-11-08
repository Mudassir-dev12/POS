# ShopPOS Prerequisites: Microsoft Visual C++ Redistributable

This folder contains the offline installer for the Microsoft Visual C++ 2015-2022 Redistributable (x64), required by native components (SQLite and Skia/PDF rendering engines) on fresh, clean Windows 10/11 installations.

## File Information
- **Filename**: `vc_redist.x64.exe`
- **Architecture**: x64 (64-bit)
- **Target OS**: Windows 10 Version 1809+, Windows 11
- **Official Download URL**: https://aka.ms/vs/17/release/vc_redist.x64.exe
- **SHA-256 Checksum**: `CC0FF0EB1DC3F5188AE6300FAEF32BF5BEEBA4BDD6E8E445A9184072096B713B`

## Installer Integration
The Inno Setup installer (`installer\ShopPOS.iss`) automatically bundles this binary. During setup, the installer inspects the registry:
`HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64` (value `Installed = 1`)

If the runtime is already installed, the prerequisite step is skipped completely. If missing, it installs silently via:
`vc_redist.x64.exe /install /quiet /norestart`

