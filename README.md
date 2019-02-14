# Ignorance: A reliable UDP Transport for Mirror Networking.

Welcome to Ignorance, a reliable UDP Transport for Mirror Networking. Powered by the ENET C Library and ENET-C# Wrapper, it provides reliable UDP communications for your Unity projects. Just as long as they use Mirror, of course.
**You're looking at the master branch, which is bleeding edge. If you want a drop in and go solution, check the releases page. Otherwise, read on!**

## What is Ignorance?
Ignorance is a wrapper that provides ENET-powered reliable UDP transport layer that plugs into the [Mirror Networking HLAPI](https://github.com/vis2k/Mirror). It uses nxrighthere's ENET-C# Wrapper as the glue between the ENET Native C plugin.
This has been a coding project for over a good few months now. I'd appreciate [a coffee to keep me caffeinated](https://ko-fi.com/coburn) if you use this transport. 

This transport is currently developed and actively used by [Oiran Studio](http://www.oiran.studio) in a few development projects.

## Mac OS Editor Compatibility Issues
tl;dr version: Mac OS Editor is buggy!

Long version: Prior to Unity 2018.3.x and 2019.1.0b1, Editor instances could not happily talk to each other about their day over a cup of coffee. After some heavy debugging including upstream tickets, it was found that the Mac OS mono instance was at fault. Unity 2018.3.x and 2019.x seemed to fix this with an updated Mono library. Standalone versions would work fine, just the Editor would not!

There's also weird Mac-specific bugs: When using Ignorance inside the Unity Editor on MacOS, it has been hard-coded to glob all IP Addresses as a workaround for a connection bug. Using `localhost` is seen to be very weird on MacOS, you'd think that `localhost` is `127.0.0.1` but on Mac, it's actually `::1` or something else.
So due to this issue inside the Mac Editor of Unity, if you press Mirror's LAN Host option, you can connect to that Editor instance on all addresses, for example: `192.168.1.2` (the machine's LAN IP), `127.0.0.1`, `::1`, `localhost`.

Refer to my ticket upstream with ENET-C# [located here](https://github.com/nxrighthere/ENet-CSharp/issues/46) where the developer and I talk about the situation and workarounds. 
*It should go without saying that I do not recommend using alpha or beta versions of Unity Editor in production. Mirror networking might not even support it! Proceed at your own risk!*

However, it seems that standalone builds are fine and NOT affected by this bug. I only have implemented this work around because some people develop on a Mac, and are affected by Unity Tech's bugs that break Ignorance on MacOS.

## Mirror compatibility
Mirror master branch and 2018 branches are supported. There is no more seperate branch for Mirror 2018, one size fits all right now.
**You need to be on Unity 2018.2.20 or higher to use Ignorance 1.2.0+**

## Target compatibility
- 32Bit Standalone targets are **not supported** as I am not able to get a DLL compiled that supports 32Bit targets. Please make sure you target 64Bit for standalone builds.
- Android 32Bit ARMv7 and 64Bit ARM64, Windows x64, Mac OS x64, Linux x64 platforms are supported.
- Tested and confirmed to work on both Unity 2018.2, 2018.3, 2019.1 Beta (using the respective branches of Mirror and Ignorance, of course).

## Installation
### Release Method
1. Grab a release from the [releases page](https://github.com/SoftwareGuy/Ignorance/releases). They will now be packaged as UnityPackage format.
2. Make sure you have [Mirror](https://github.com/vis2k/Mirror) installed in your project. **Do not use** the asset store version as that one is very outdated. Use the latest auto-build and extract that into your project's folder.
3. Unpack the Ignorance UnityPackage file by double clicking it, or importing package inside the editor.
4. Let Unity do the dirty work.
5. Attach IgnoranceTransport to your NetworkManager GameObject, drag the script's name to the "Transport" field of the NetworkManager. Remove any other transport scripts on that game object.
6. You're good to go, welcome aboard!

### Git Clone Method
Okay fine, I get it. You want the bleeding edge, huh? Sure, sure. Mirror upstream master branch does change a lot in very little time, so I understand.
1. Git clone this repository.
2. Copy all contents of the Git Repository's assets folder to your project's folder.
3. Let Unity do the dirty work.
4. Attach IgnoranceTransport to your NetworkManager GameObject, drag the script's name to the "Transport" field of the NetworkManager. Remove any other transport scripts on that game object.
5. You're good to go, welcome aboard!

## How to use
*Uhh, did you check the installation instructions?*
- Add the Ignorance Transport script onto your NetworkManager game object and remove any existing ones, like Telepathy, TCP, etc. Drag the script into the Transport slot in the NetworkManager script.
- Configure to your liking
- You're good to go!

## Dependencies
- [Mirror](https://github.com/vis2k/Mirror)
- [ENet-CSharp](https://github.com/nxrighthere/ENet-CSharp)
- ENET itself. As in the C library.

## I want more info on Ignorance.
[You're welcome.](https://vis2k.github.io/Mirror/Transports/Ignorance)

## Credits
- **Coffee Donators**: Thank you so much.
- **Petris**: Code refactoring and tidy up (you rock man!)
- **[nxrighthere](https://github.com/nxrighthere)**: Debugging my broken code and identifying my packet code fuckups
- **[Draknith](https://github.com/FizzCube)**: Testing and mapping Reliable/Unreliable channels in Mirror to ENET Channels, testing.
- **[vis2k](https://github.com/vis2k)**: The mad man behind the scenes that made Mirror happen. Much respect.
- **Mirror Discord**: Memes, Courage, LOLs, awesome folks to chat with
- Others who I have missed. Thanks a lot, you know you are.
