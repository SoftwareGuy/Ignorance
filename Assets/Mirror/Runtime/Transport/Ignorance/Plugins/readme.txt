ENET Pre-compiled Binary Library Blobs
==========================
This folder contains pre-compiled binaries for a variety of different platforms.

A brief summary of these folders are as follows:

- Windows
-- 32bit (x86): See dot point 1 below
-- 64bit (x64): Compiled on Windows 10

- macOS
-- 64bit (x86_64): See dot point 2 below

- Linux
- 64bit (x86_64): Compiled on Ubuntu 18.04, should work with any modern Linux distro (exotic distros might not work)

- Android (Kitkat 4.4 minimum target OS)
-- ARMv7 (armeabi-v7a)
-- ARMv8/AArch64 (arm64-v8a)
-- x86 (32bit): Left here for legacy reasons

- iOS 
- FAT Library (armv7 + arm64)
- Targeted for iOS 8 minimum. Unsigned library.

DEBUG VERSIONS
===============
Debug versions are provided for MacOS and iOS targets since they've been the most annoying platforms. Especially iOS.
If you need a debug version for Windows or Linux, please contact me and I can provide them. Otherwise you can also compile
the library yourself with Debug enabled.

DOT POINTS
===========
1. 32bit Support for Ignorance will be eventually removed. Originally, I did not want to support 32bit operating systems,
however due to some countries in the world still stuck in the 32bit era (Brasil, some Russian areas, etc) it was done as a
goodwill gesture. When this happens, you'll need to contact me for a 32bit DLL, unless I can figure out a way to automate it.

2. Compiled on a Mac running Catalina. I don't believe anything below High Sierra will support it.

COMPILE THE CODE YOURSELF
=========================
If you don't trust the above binaries then git clone the ENET-CSharp repository (http://github.com/SoftwareGuy/ENet-CSharp) and read the readme.
We use MSBuild for max awesomeness. iOS and Android compiles require additional work. Make sure you have NET Core SDK 3.0 installed at least
or you'll get errors running the Test suite.

EXCLUSION INSTRUCTIONS
======================
No need, the meta data will cover that for you.

Still don't know what to do with these? Drop by the Mirror discord and post in the #ignorance channel.