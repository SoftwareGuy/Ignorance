This folder (Redist) contains a compiled DLL for x86_64 (64-Bit) Windows, MacOS and Linux distros.
NOT COMPATIBLE WITH x86 (32-Bit) targets. Ensure you build a 64-Bit Player or you will get a TypeLoadException.
Android/iOS not supported.

They have been custom compiled by myself (SoftwareGuy) on my own Windows 10 machine running Visual Studio Community 2017, my own MacBook Pro using CMake + Xcode and a Ubuntu linux machine.

MAKE SURE ONLY ONE COPY OF THE FILES CONTAINED INSIDE THESE FOLDERS ONLY EXIST AT ONE TIME.
IF YOU HAVE MULTIPLE COPIES OF THESE FILES, YOU WILL GET WEIRD SHIT HAPPENING.

MAKE SURE YOU CORRECTLY CONFIGURE THE PLUGIN TARGETS:

Windows: Exclude enet.bundle (inside macOS folder) and libenet.so (inside Linux folder) from Unity Editor.
Mac OS: Exclude enet.dll (inside Windows folder) and libenet.so (inside Linux folder) from Unity Editor.
Linux: Exclude enet.dll (inside Windows folder) and enet.bundle (inside macOS folder) from Unity Editor.

If you get an error about a DLL not having valid meta data, make sure you have done above steps correctly.

If weird shit starts happening after doing these above steps, please restart Unity. When a native DLL is loaded, it cannot be unloaded unless you restart the editor.

Otherwise, restart Unity and if it persists open a issue on the GitHub.

Failsafe.zip contains NX-supplied DLLs that may not be up to date.