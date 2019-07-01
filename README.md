# Ignorance 1.3
[![Ko-Fi](https://img.shields.io/badge/donate-Ko--Fi-red.svg)](https://ko-fi.com/coburn)
[![PayPal](https://drive.google.com/uc?id=1OQrtNBVJehNVxgPf6T6yX1wIysz1ElLR)](https://www.paypal.me/coburn64)
![GitHub issues](https://img.shields.io/github/issues/SoftwareGuy/Ignorance.svg)
![GitHub last commit](https://img.shields.io/github/last-commit/SoftwareGuy/Ignorance.svg)
![MIT Licensed](https://img.shields.io/badge/license-MIT-green.svg) 

*If you feel so inclined, I'd appreciate [a coffee to keep me caffeinated](https://ko-fi.com/coburn) if you use this transport.*

Welcome to Ignorance, a plug-in Transport system for [Mirror Networking](https://github.com/vis2k/Mirror) that allows you to harness reliable UDP communications over the vast seas that is the internet. Harnessing the ENET Backend, it provides reliable and unreliable UDP packet delivery with up to 255 individual channels and 4096 theortical maximum clients connected at any time.

Let's face the music: Unity's Low-level API networking sucks. Ignorance aims to do what LLAPI did... and a little bit more.

Please read the FAQ (towards the bottom of this wall of text) before using the Ignorance Transport.

## Compatiblity
### Desktop
- Windows 7 x64 Minimum.
- Anything recent for MacOS; make sure you're up to date with the latest version of MacOS that your device allows.
- 64Bit Ubuntu recommended, but should work on Debian, Arch, Gentoo...

### Console
- Nintendo Switch: **Likely possible.** [See here](https://github.com/SoftwareGuy/ENet-CSharp/blob/master/BUILD-FOR-SWITCH.txt)
- Xbox One: **In-Doubt.** Probably incompatible unless you can natively compile the ENET library against the Xbox One SDK; UWP Applications may not work with Ignorance.
- PlayStation 4 & Vita: **Unknown.** I do not have a PS4 Development Kit to test.

### Mobile
- Android 4.4 "KitKat" onwards.
- iOS 8.0 Minimum (ARMv7), compatible all the way to the the iPhone X family on ARM64e.

## Dependencies
- [Mirror](https://github.com/vis2k/Mirror)
- [ENet-CSharp](https://github.com/SoftwareGuy/ENet-CSharp), our custom fork of NX's code.
- ENET Native C Library Blobs are included in the repository and package formats for Windows/Mac/Linux/Android/iOS, no manual compile required.

## Mirror Compatibility
Please use the Mirror Asset Store releases. The master branch of Mirror can also be used but please beware that it's bleeding edge and massive breakage can be expected on a bad day.
### Mac OS Editor Compatibility Issues
When using Ignorance inside the MacOS environment, it will run in a compatibility mode to ensure things work correctly. This is due to some Unity Editor thread managed-to-native issues that were fixed in 2018.3+. Windows and Linux do not suffer this problem.

## Installation
Download the Unity Package from Releases that is the latest one. Simply import the Unity Package and Unity will do the rest. Follow the instructions below.

## How to use
As of Ignorance 1.3, I have included a pre-configured sample scene so you can get started easily.

**Hard mode:**

If you haven't already, make a empty GameObject with the Mirror **Network Manager**. It will automatically add Telepathy as the default transport. Add a **new component** and look for **Ignorance**. You will have Ignorance added as a transport, but it will not be activated. Now **drag the Ignorance script** into the **Transport** field of the **Network Manager inspector**. If all goes well, you should see **GameObjectName (Ignorance)** in that field. 

## FAQ (aka Please Read This First)

### Why should I use Ignorance over Unity LLAPI?
Unity LLAPI is old, obsolete and no longer mantained by Unity Technologies. It is currently on heavy life-support until they get their act together with the new Unity Multiplay system. Plus, it was held together by bandaids and bubble-gum. Depending on what you threw over the network, you'd get random latency spikes and packets would go missing even in Reliable delivery mode.

Ignorance aims to replace it with a stable and high performance transport system that allows you to forget about low-level networking stress and spend more time focus on **making** the game you want to make.

### Why are you using Mirror's LateUpdate vs Threads?
This is Mirror's design, not mine. Ignorance tries it's best to address all the packets coming in and out every frame.

Since LateUpdate is frame-rate dependent, the lower the framerate the more latency you'll encounter due to how the backend is polled. Ideally if you're going to use this transport you're better off using setting your server to use at least a tickrate of 30 ticks per second. Any less (ie. below 30) and you're risking the server getting choked. All transports have this design issue.

I do have a threaded version of Ignorance that unfortunately suffers Unity shitting the bed under heavy load due to the really disappointing Mono implementation of threading support.

### I have a bug!
[Check the current open bug reports and/or report a new one here](https://github.com/SoftwareGuy/Ignorance/issues) and I also recommend you be present in the Discord so I can ask for further info and/or you can test fixes for your bug reports.

Bugs that do not have enough details will be closed with #wontfix. Details like your OS, Unity Editor version, any Ignorance errors, etc is essential for a good bug report.

## I have questions, I want answers.
[You're welcome.](https://vis2k.github.io/Mirror/Transports/Ignorance)

### I am not satisfied with Ignorance. 
Please let me know why as I can't improve my code if I don't get feedback. *However, if you're just here to troll me and my code, then please move on.*

See also: [LiteNetLib4Mirror](https://github.com/MichalPetryka/LiteNetLib4Mirror).

## Credits
- **Coffee Donators**: Thank you so much.
- **[vis2k](https://github.com/vis2k)** and **[Paul](https://github.com/paulpach)**: Mirror lead developers.
- **[Katori](https://github.com/katori)**: Helped kickstart the threaded version that evolved into Ignorance 2.
- **[BigBoxVR](https://github.com/GabeBigBoxVR)**: Pull requests and found a race condition that threatened stability.
- **[c6burns](https://github.com/c6burns)**: Words cannot describe how much help he's been. Bloody oath mate.
- **[Petris](https://github.com/MichalPetryka)**: Code refactoring and tidy up (you rock man!)
- **[BigBoxVR](https://github.com/GabeBigBoxVR)** and **[Martin](https://github.com/martindevans)**: Pull requests.
- **The folks at the Mirror discord**: Memes, Courage, laughs, drama and all-round awesome folks to chat with.
- To all the others who I have missed. Thanks a lot, you know who you are.
- You too can have your name here if you send in a PR. Don't delay, get your PR in today!
### Previous hall of fame:
- **[FSE_Vincenzo](https://github.com/Vincenz099)**: Providing insight into proper usage of ENET, rebuttal, improvements, etc.
- **[shiena](https://github.com/shiena)**: Pull requests for bug fixes and oversights.
- **[Draknith](https://github.com/FizzCube)**: Testing and mapping Reliable/Unreliable channels in Mirror to ENET Channels, testing.

### And last but not least...
- **[nxrighthere](https://github.com/nxrighthere)**: Helped debug some things in early versions of Ignorance, before going full rampage and blacklisting everyone on the Mirror team from his repos. RIP.
