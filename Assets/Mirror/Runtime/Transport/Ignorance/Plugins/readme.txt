ENET Redist Binary C Blobs
==========================
This folder (Redist) contains a set of compiled binary blobs.
Contained within these folders lies the following:

* iOS
- Universal (?) (tested by felipejfc on an iPhone X-series)

* Android
- arm64-v8a: AArch64/ARM64 ENET binary.
- armeabi-v7a: ARMv7 ENET binary.
- x86: 32Bit x86 Android (Intel Atom?) ENET Binary.
- NOTE: These have been built with a minimum of Android KitKat 4.4 support. Unlikely to work on really old versions of Android.
- NOTE 2: A x86_64 built library does exist here, but Unity 2018.3.5 doesn't seem to know that Android 64bit is a thing. Oh well, it'll remain compressed.

* Windows
- enet.dll: Win64 (x86_64 Windows) ENET Binary. Cross compiled using MinGW on Ubuntu.
- NOTE: Unfortunately x86 (32-Bit) targets are not supported. So, ensure you build a 64-Bit player or you will get a TypeLoadException.

* MacOS
- enet.bundle: MacOS compiled ENET Binary using Apple CLang from XCode. (CMake & Make)

* Linux
- libenet.so: Ubuntu 18.04 compiled ENET Shared Binary.

EXCLUSION INSTRUCTIONS
======================
Windows: Exclude enet.bundle (inside macOS folder), libenet.so (inside Linux folder) and all Android plugins from Unity Editor.
Mac OS: Exclude enet.dll (inside Windows folder) and libenet.so (inside Linux folder) and all Android plugins from Unity Editor.
Linux: Exclude enet.dll (inside Windows folder) and enet.bundle (inside macOS folder) and all Android plugins from Unity Editor.

Android: 
**DO NOT USE THESE ON LINUX BUILDS**
- Set the 'libenet.so' in 'arm64-v8a' folder to only be ARM64 platform and exclude it from Editor and Standalone on other architectures.
- Set the 'libenet.so in 'armeabi-v7a' folder to only be ARMv7 platform and exclude it from Editor and Standalone on other architectures.
- Set the 'libenet.so' in 'x86' folder to only be x86 platform and exclude it from Editor and Standalone on other architectures.

NOTE: If you get an error about a DLL not having valid meta data, make sure you have done above steps correctly. Otherwise you can use replace the files in your project with the ones inside Failsfe.zip.

If weird shit starts happening after doing these above steps, please restart Unity. When a native DLL is loaded, it cannot be unloaded unless you restart the editor - a disappointing Unity limitation. Otherwise, restart Unity and if it persists open a issue on the GitHub.

Failsafe.zip contains working DLLs that may not be up to date and they do not include Android native plugins.

Still don't know what to do with these? Drop by the discord and post in the #ignorance channel.