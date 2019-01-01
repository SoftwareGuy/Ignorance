This folder (Redist) contains a compiled DLL for x86_64 (64-Bit) Windows, MacOS and Linux distros.
Unfortunately x86 (32-Bit) targets are not supported. So, ensure you build a 64-Bit player or you will get a TypeLoadException.

Android is supported (Kit-Kat (OS 4.4) minimum requirement!) but iOS is not yet supported.

They have been custom compiled by myself (SoftwareGuy) on my own Windows 10 machine running Visual Studio Community 2017, my own MacBook Pro using CMake + Xcode and a Ubuntu linux machine.

As of 2018-12-31, Android ARMv7 and ARM64 ENET libraries are now included. Please read the setup guide below for correct configuration.

MAKE SURE ONLY ONE REDIST BUNDLE OF THESE PLUGINS ARE IN YOUR PROJECT. OTHERWISE YOU WILL HAVE WEIRD ODDITIES HAPPENING. ALSO MAKE SURE YOU CORRECTLY CONFIGURE THE PLUGIN TARGETS:

Windows: Exclude enet.bundle (inside macOS folder) and libenet.so (inside Linux folder) from Unity Editor.
Mac OS: Exclude enet.dll (inside Windows folder) and libenet.so (inside Linux folder) from Unity Editor.
Linux: Exclude enet.dll (inside Windows folder) and enet.bundle (inside macOS folder) from Unity Editor.

Android: 
- Set the 'libenet.so' in 'arm64-v8a' folder to only be ARM64 platform and exclude it from Editor and Standalone.
- Set the 'libenet.so in 'armeabi-v7a' folder to only be ARMv7 platform and exclude it from Editor and Standalone.

NOTE: If you get an error about a DLL not having valid meta data, make sure you have done above steps correctly. Otherwise you can use replace the files in your project with the ones inside Failsfe.zip.

If weird shit starts happening after doing these above steps, please restart Unity. When a native DLL is loaded, it cannot be unloaded unless you restart the editor - a disappointing Unity limitation. Otherwise, restart Unity and if it persists open a issue on the GitHub.

Failsafe.zip contains NX-supplied DLLs that may not be up to date and they do not include Android native plugins.