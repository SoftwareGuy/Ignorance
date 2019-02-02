# Ignorance: A reliable UDP Transport for Mirror Networking.
Ignorance is a wrapper that provides ENET-powered reliable UDP transport layer that plugs into the [Mirror Networking HLAPI](https://github.com/vis2k/Mirror). It uses nxrighthere's ENET-C# Wrapper as the glue between the ENET Native C plugin.
[If you use this, I'd appreciate a coffee to keep me caffeinated.](https://ko-fi.com/coburn)

This transport is currently developed and actively used by [Oiran Studio](http://www.oiran.studio).

## Mac OS Editor Compatibility Issue
I can confirm that using Unity 2019.1.0b1 allows 2 Editor instances to happily talk to each other about their day and other general chit-chat. However, there is a caveat. When using Ignorance inside the Unity Editor on MacOS, it has been hard-coded to glob all IP Addresses. Localhost is seen to be very weird on MacOS, you'd think that `localhost` is `127.0.0.1` but on Mac, it's actually `::1` or something else. 
So due to this issue inside the Mac Editor of Unity, if you press Mirror's LAN Host option, you can connect to that Editor instance on all addresses, for example: `192.168.1.2` (the machine's LAN IP), `127.0.0.1`, `::1`, `localhost`.

Refer to my ticket upstream with ENET-C# [located here](https://github.com/nxrighthere/ENet-CSharp/issues/46). *However, I do not recommend using alpha or beta versions of Unity Editor in production. Proceed at your own risk!*

Standalone builds are fine. I only have implemented this work around because some people develop on a Mac, and are affected by Unity Tech's bugs that break Ignorance on MacOS.

## Mirror compatibility
Mirror master branch and 2018 branches are supported. There is no more seperate branch for Mirror 2018, one size fits all right now.

## Target compatibility
- 32Bit Standalone targets are **not supported** as I am not able to get a DLL compiled that supports 32Bit targets. Please make sure you target 64Bit for standalone builds.
- Android 32Bit ARMv7 and 64Bit ARM64, Windows x64, Mac OS x64, Linux x64 platforms are supported.
- Tested and confirmed to work on both Unity 2017.4 and Unity 2018.2 (using the respective branches of Mirror and Ignorance, of course).

Ensure you correctly configure the Redist plugins (included in the repo and releases). You need to make sure that you follow the readme file inside the Redist to the letter or you can expect very weird things to happen...

## Installation
### Release Method
1. Grab a release from the [releases page](https://github.com/SoftwareGuy/Ignorance/releases) that says it's a **Pluggable Transport** version.
2. Make sure you have [Mirror](https://github.com/vis2k/Mirror) installed in your project. **Do not use the asset store version as that one is very outdated.**
3. Extract the downloaded release archive into your project, maybe under `Assets/Packages/Ignorance`.
4. Let Unity detect the new scripts and compile.

### Git Clone Method
Okay fine, I get it. You want the bleeding edge, huh? Sure, sure. Mirror upstream master branch does change a lot in very little time, so I understand.
1. Git clone this repository.
2. If you haven't already done so, make a folder called `Ignorance` in your project. Also make a `Dependencies` folder just for house keeping under that.
3. Copy the repos' `ENet.cs` file from the Dependencies folder into your projects `Ignorance/Dependencies` folder.
4. Copy the repos' Redist folder to your project's `Ignorance` folder.
5. Delete any duplicate Redist folders from your project that may contain duplicate copies of the ENET binary blobs. **Only one copy of each blob must be present in your project at one time.**
6. Import, and let Unity recongize everything. There should be no errors.
7. To get the latest, simply open the cloned repository, do a `git pull` to sync your local copy and you'll get the latest changes. Follow the steps above to update your project to latest Ignorance master.

## Dependencies
- [Mirror](https://github.com/vis2k/Mirror)
- [ENet-CSharp](https://github.com/nxrighthere/ENet-CSharp)
- ENET itself. As in the C library.

## How to use
- Add the Ignorance Transport script onto your NetworkManager game object and remove any existing ones, like Telepathy, TCP, etc.
- Configure to your liking
- You're good to go!

## Why should I use reliable UDP?
Since UDP is designed to be a scattershot shotgun approach to networking, there's no promises that UDP packets will get from A to B without going through hell and back. That is why UDP ignores a lot of stuff that TCP fusses over. 

However, reliable UDP tries to mimic TCP to some extent with the sequencing and retransmission of packets until they land at the destination. This makes sense in some usage cases like VoIP and multiplayer shooter games where you need a data firehose rather than reliablity.

Please do consider TCP (Telepathy) if you're doing Mission Critical networking. There's a reason why big name MMOs use TCP to keep everything in check. However, for some usage cases TCP may be a little overkill, so UDP is recommended.

## Credits
- **Coffee Donators**: Thank you so much.
- **Petris**: Code refactoring and tidy up (you rock man!)
- **[nxrighthere](https://github.com/nxrighthere)**: Debugging my broken code and identifying my packet code fuckups
- **[Draknith](https://github.com/FizzCube)**: Testing and mapping Reliable/Unreliable channels in Mirror to ENET Channels, testing.
- **[vis2k](https://github.com/vis2k)**: The mad man behind the scenes that made Mirror happen. Much respect.
- **Mirror Discord**: Memes, Courage, LOLs, awesome folks to chat with
- Others who I have missed. Thanks a lot, you know you are.
