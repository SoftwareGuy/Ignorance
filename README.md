# Ignorance 1.2.x Stable
[![PayPal](https://drive.google.com/uc?id=1OQrtNBVJehNVxgPf6T6yX1wIysz1ElLR)](https://www.paypal.me/coburn64)

If you feel so inclined, I'd appreciate [a coffee to keep me caffeinated](https://ko-fi.com/coburn) if you use this transport.

Ignorance Transport allows you to harness reliable UDP communications over the vast seas that is the internet. Simply plug this transport into [Mirror Networking](https://github.com/vis2k/Mirror) and you've got the power of reliable UDP provided by the ENET C Networking Library in your project.

Ignorance now uses a custom fork of nxrighthere's ENET-CSharp repository due to his recent fallout with the Mirror team over silly things. If you want [my analysis of the situation, you can find it on my blog](https://www.coburnsdomain.com/2019/03/getting-blocked-from-an-upstream-github-repo-nx-edition/).

## Why should I use Ignorance 1.2.x over Unity LLAPI?
Unity LLAPI is old, obsolete and no longer mantained by Unity Technologies. Plus, it was held together by bandaids and bubble-gum. Ignorance aims to replace it with a stable, high performance transport system that allows you to forget about low-level networking stress and spend more time focus on **making** the game you want to make.

## Compatiblity
### Windows
- Windows 7 onwards, x64 platform. x86 (32Bit) not supported, sorry.
### MacOS
- Anything recent that is Intel platform. PowerPC not supported.
### Linux
- Anything modern that is x86_64 platform. Ubuntu recommended, but should work on Debian, Arch, Gentoo...
### Android
- Kitkat 4.4 onwards.
### iOS
- iOS 8.0 onwards on ARMv7 all the way to the iPhone X on ARM64e.

## Dependencies
- [Mirror](https://github.com/vis2k/Mirror)
- [ENet-CSharp](https://github.com/SoftwareGuy/ENet-CSharp), our custom fork of NX's code. Blobs are included in the repository, no manual compile required.

## Mirror Compatibility
Please use the Mirror Asset Store releases. The master branch of Mirror can also be used but please beware that it's bleeding edge and massive breakage can be expected on a bad day.

## Installation
Download the Unity Package from Releases that is the latest one. Simply import the Unity Package and Unity will do the rest. Follow the instructions below.

## How to use
If you haven't already, make a empty GameObject with the Mirror **Network Manager**. It will automatically add Telepathy as the default transport. Add a **new component** and look for **Ignorance Transport**. You will have Ignorance added as a transport, but it will not be activated. Now **drag the Ignorance Transport script** into the **Transport** field of the **Network Manager inspector**. If all goes well, you should see **Ignorance Transport (Transport)** in that field.

## Bugs 
[Check the current open bug reports and/or report a new one here](https://github.com/SoftwareGuy/Ignorance/issues), and I also recommend you be present in the Discord so I can ask for further info and/or you can test fixes for your bug reports.

Bugs that do not have enough details will be closed with #wontfix. Details like your OS, Unity Editor version, any Ignorance errors, etc is essential for a good bug report.

### Mac OS Editor Compatibility Issues
When using Ignorance inside the MacOS environment, it will run in a compatibility mode to ensure things work correctly. This is due to some Unity Editor thread managed-to-native issues that were fixed in 2018.3+.

## I have questions, I want answers.
[You're welcome.](https://vis2k.github.io/Mirror/Transports/Ignorance)

## Credits
- **Coffee Donators**: Thank you so much.
- **[vis2k](https://github.com/vis2k)** and **[Paul](https://github.com/paulpach)**: Mirror lead developers.
- **[Katori](https://github.com/katori)**: Helped kickstart the threaded version that evolved into Ignorance 2.
- **[BigBoxVR](https://github.com/GabeBigBoxVR)**: Pull requests and found a race condition that threatened stability.
- **[c6burns](https://github.com/c6burns)**: Words cannot describe how much help he's been. Bloody oath mate.
- **[FSE_Vincenzo](https://github.com/Vincenz099)**: Providing insight into proper usage of ENET, rebuttal, improvements, etc.
- **[BigBoxVR](https://github.com/GabeBigBoxVR)** and **[Martin](https://github.com/martindevans)**: Pull requests.
- To all the others who I have missed. Thanks a lot, you know who you are.
- You too can have your name here if you send in a PR. Don't delay, get your PR in today!
### Previous hall of fame:
- **[Petris](https://github.com/MichalPetryka)**: Code refactoring and tidy up (you rock man!)
- **[Draknith](https://github.com/FizzCube)**: Testing and mapping Reliable/Unreliable channels in Mirror to ENET Channels, testing.
- **[shiena](https://github.com/shiena)**: Pull requests for bug fixes and oversights.
- **The folks at the Mirror discord**: Memes, Courage, LOLs, drama and all-round awesome folks to chat with.
### And last but not least...
- **[nxrighthere](https://github.com/nxrighthere)**: Helped debug some things in early versions of Ignorance, before going full rampage and blacklisting everyone on the Mirror team from his repos. RIP.
