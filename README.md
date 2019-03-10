# Ignorance: A reliable UDP Transport for Mirror Networking.

Welcome to Ignorance, a reliable UDP Transport for Mirror Networking. Powered by the ENET C Library and ENET-C# Wrapper, it provides reliable UDP communications for your Unity projects. Just as long as they use Mirror, of course. This has been a coding project for over a good few months now. This transport is currently developed and actively used by [Oiran Studio](http://www.oiran.studio) in a few development projects.

If you feel so inclined, I'd appreciate [a coffee to keep me caffeinated](https://ko-fi.com/coburn) if you use this transport. 

## Mac OS Editor Compatibility Issues
Refer to my ticket upstream with ENET-C# [located here](https://github.com/nxrighthere/ENet-CSharp/issues/46) where the developer and I talk about the situation and workarounds. 
When using Ignorance inside the MacOS environment, it will run in a compatibility mode to ensure things work correctly.

## Mirror Compatibility
You **must** use a Mirror master snapshot. Ignorance is unlikely to work correctly with the Asset Store version of Mirror. **You need to be on Unity 2018.3.x or higher to use Ignorance 1.2.0+**

## Target compatibility
- Android 32Bit ARMv7 and 64Bit ARM64, Windows x64, Mac OS x64, Linux x64 platforms are supported.
- 32Bit Standalone targets are **not supported** as I am not able to get a DLL compiled that supports 32Bit targets. Please make sure you target 64Bit for standalone builds.
- Tested and confirmed to work on both Unity 2018.3 and 2019.1 Beta (using the respective branches of Mirror).

## Installation
### Release Method
1. Grab a release from the [releases page](https://github.com/SoftwareGuy/Ignorance/releases). They will now be packaged as UnityPackage format. You can grab a release candidate if you're feeling lucky... or maybe just brave enough to help bug test before the stable version hits the shelf.
2. Make sure you have [Mirror](https://github.com/vis2k/Mirror) installed in your project. **Do not use** the asset store version as that one is incompatible. Use the latest auto-build and extract that into your project's folder.
3. Unpack the Ignorance UnityPackage file by double clicking it, or importing package inside the editor.
4. Let Unity do the dirty work.
5. Attach IgnoranceTransport to your NetworkManager GameObject, drag the script's name to the "Transport" field of the NetworkManager. Remove any other transport scripts on that game object.
6. You're good to go, welcome aboard!

### Git Clone Method
Bleeding edge, woohoo! Prepare the bandaids.

Do the same as the release method but instead of downloading from the releases page, clone the repository and copy the Assets folder to your project root. This will install Ignorance in whatever state the code is in.

## How to use
*Uhh, did you check the installation instructions?*
- Add the Ignorance Transport script onto your NetworkManager game object and remove any existing ones, like Telepathy, TCP, etc. Drag the script into the Transport slot in the NetworkManager script.
- Configure to your liking
- You're good to go!

## Dependencies
- [Mirror](https://github.com/vis2k/Mirror)
- [ENet-CSharp](https://github.com/nxrighthere/ENet-CSharp)
- ENET itself. As in the C library.

## I have a bug.
[Report the sucker here.](https://github.com/SoftwareGuy/Ignorance/issues) Please provide as much detail as possible, bugs that cannot be replicated will be flagged as invalid.

## I have questions, I want answers.
[You're welcome.](https://vis2k.github.io/Mirror/Transports/Ignorance)

## Credits
- **Coffee Donators**: Thank you so much.
- **[Petris](https://github.com/MichalPetryka)**: Code refactoring and tidy up (you rock man!)
- **[BigBoxVR](https://github.com/GabeBigBoxVR)**: Pull requests.
- **[nxrighthere](https://github.com/nxrighthere)**: Helped debug some things.
- **[Draknith](https://github.com/FizzCube)**: Testing and mapping Reliable/Unreliable channels in Mirror to ENET Channels, testing.
- **[vis2k](https://github.com/vis2k)** and **[Paul](https://github.com/paulpach)**: Mirror lead developers.
- **[shiena](https://github.com/shiena)**: Pull requests for bug fixes and oversights.
- **The folks at the Mirror discord**: Memes, Courage, LOLs, drama and all-round awesome folks to chat with.
- To all the others who I have missed. Thanks a lot, you know who you are.
- You too can have your name here if you send in a PR. Don't delay, get your PR in today!