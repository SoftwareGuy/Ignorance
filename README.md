# Ignorance 2 (EXPERIMENTAL BRANCH)
If you feel so inclined, I'd appreciate [a coffee to keep me caffeinated](https://ko-fi.com/coburn) if you use this transport.

Rewritten to utilize threads, Ignorance Transport allows you to harness reliable UDP communications over the vast seas that is the internet.

Simply plug this transport into [Mirror Networking](https://github.com/vis2k/Mirror) and you've got the power of reliable UDP in your project.

## Why should I use Ignorance 2 over Unity LLAPI?
Unity LLAPI is old, obsolete and no longer mantained by Unity Technologies. Plus, it was held together by bandaids and bubble-gum.

Ignorance aims to replace it with a stable, high performance transport system that allows you to forget about low-level networking stress and spend more time focus on **making** the game you want.

## Compatiblity
### Windows
- Windows 7, 8.1, 10 x86_64 (32bit, aka x86 not supported).
### MacOS
- Anything recent that is Intel platform. PowerPC not supported.
### Linux
- Anything modern that is x86_64 platform. Ubuntu recommended, but should work on Debian, Arch, Gentoo...
### Android
- Kitkat 4.4 onwards.
### iOS
- iOS 8.0 onwards on ARMv7 all the way to iPhone X on ARM64e

## Dependencies
- [Mirror](https://github.com/vis2k/Mirror)
- RingBuffer from Distruptor3D (insert link here)
- [ENet-CSharp](https://github.com/SoftwareGuy/ENet-CSharp)
- ENET C Library (included as prebaked DLLs)

## Mirror Compatibility
Use a recent Mirror Asset Store snapshot for best results. Mirror's master branch is fine too, but that's an melting pot and lots of things can break in a blink of an eye.

## Installation
### TODO when RC is released

## How to use
### TODO when RC is released 

## Bugs 
### This is the experimental branch!
**DO NOT USE THIS IN PRODUCTION!**
### Mac OS Editor Compatibility Issues
When using Ignorance inside the MacOS environment, it will run in a compatibility mode to ensure things work correctly. This is due to some Unity Editor thread managed-to-native issues that were fixed in 2018.3+.
### I have a bug.
[Report the sucker here.](https://github.com/SoftwareGuy/Ignorance/issues) Please provide as much detail as possible, bugs that cannot be replicated will be flagged as invalid.

## I have questions, I want answers.
[You're welcome.](https://vis2k.github.io/Mirror/Transports/Ignorance)

## Credits
- **Coffee Donators**: Thank you so much.
- **[vis2k](https://github.com/vis2k)** and **[Paul](https://github.com/paulpach)**: Mirror lead developers.
- **[Katori](https://github.com/katori)**: Helped kickstart the threaded version that evolved into Ignorance 2.
- **[FSE_Vincenzo]()**: Providing insight into proper usage of ENET, rebuttal, improvements, etc.
- **[BigBoxVR](https://github.com/GabeBigBoxVR)** and **[Martin](https://github.com/martindevans)**: Pull requests.
- To all the others who I have missed. Thanks a lot, you know who you are.
- You too can have your name here if you send in a PR. Don't delay, get your PR in today!
### Previous hall of fame:
- **[Petris](https://github.com/MichalPetryka)**: Code refactoring and tidy up (you rock man!)
- **[BigBoxVR](https://github.com/GabeBigBoxVR)**: Pull requests.
- **[Draknith](https://github.com/FizzCube)**: Testing and mapping Reliable/Unreliable channels in Mirror to ENET Channels, testing.
- **[shiena](https://github.com/shiena)**: Pull requests for bug fixes and oversights.
- **The folks at the Mirror discord**: Memes, Courage, LOLs, drama and all-round awesome folks to chat with.
### And last but not least...
- **[nxrighthere](https://github.com/nxrighthere)**: Helped debug some things in v1, before going full rampage and blacklisting everyone on the Mirror team from his repos. RIP.