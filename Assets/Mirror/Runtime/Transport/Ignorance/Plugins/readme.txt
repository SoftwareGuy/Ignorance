ENET Redist Binary C Blobs
==========================
This folder (Redist) contains a set of compiled binary blobs.
Contained within these folders lies the following:

* iOS
- Universal for iOS 8.x+

* Android
- arm64-v8a: AArch64/ARM64 ENET binary.
- armeabi-v7a: ARMv7 ENET binary.
- x86: 32Bit x86 Android ENET Binary.
- NOTE: Minimum of Android KitKat 4.4 OS required.

* Windows
- enet.dll: Win64 (x86_64 Windows) ENET Binary. Cross compiled using MinGW on Ubuntu.
- NOTE: Unfortunately x86 (32-Bit) targets are not supported. So, ensure you build a 64-Bit player or you will get a TypeLoadException.

* MacOS
- enet.bundle: MacOS compiled ENET Binary using Apple CLang from XCode. (CMake & Make)

* Linux
- libenet.so: Ubuntu 18.04 compiled ENET Shared Binary.

EXCLUSION INSTRUCTIONS
======================
No need, the meta data will cover that for you.

Still don't know what to do with these? Drop by the Mirror discord and post in the #ignorance channel.