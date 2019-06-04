ENET Redist Binary C Blobs
==========================
This folder (Redist) contains a set of compiled binary blobs from https://github.com/SoftwareGuy/ENet-CSharp .

Contained within these folders lies the following:

* iOS
- Universal for iOS 8.x+
* Android
- arm64-v8a: AArch64/ARM64 ENET binary.
- armeabi-v7a: ARMv7 ENET binary.
- x86: 32Bit x86 Android ENET Binary.
- NOTE: Minimum of Android KitKat 4.4 OS required.
* Windows
- - enet.dll: Windows 64-bit, compiled using Visual Studio 2019.
* MacOS
- enet.bundle: MacOS compiled ENET Binary using Apple CLang from XCode. (CMake & Make)
* Linux
- libenet.so: Ubuntu 18.04 compiled ENET Shared Binary.

COMPILE THE CODE YOURSELF
=========================
If you don't trust the above binaries then git clone the repository, nativate to Source/Native and run `cmake`/`ccmake`,
configure to your taste, add any additional herbs and spices and bake accordingly. iOS and Android compiles require
additional work.

EXCLUSION INSTRUCTIONS
======================
No need, the meta data will cover that for you.

Still don't know what to do with these? Drop by the Mirror discord and post in the #ignorance channel.