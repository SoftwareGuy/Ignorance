# Ignorance: A reliable UDP Transport for Mirror Networking.
NOTE: You're looking at the next generation branch, which removes ENET as a dependency in favor for a modern packet engine, codenamed the "Cornflake Packet Engine".
This branch is best described as experimental, and is highly likely to randomly fail in production. If you are using this branch and encounter a problem, **PLEASE SPECIFY THAT YOU ARE USING THE NEXT-GEN BRANCH**.

## What is Ignorance?
Ignorance provides a reliable UDP transport layer for the famous [Mirror Networking HLAPI](https://github.com/vis2k/Mirror). 
Under the blankets it uses the Cornflake Packet Engine, a wrapper and Transport code to plug in and snuggle up to its big sister, Mirror.

This transport is currently developed by and actively used by [Oiran Studio](http://www.oiran.studio) in a few development projects.

## Donations
I'd appreciate [a coffee to keep me caffeinated](https://ko-fi.com/coburn) if you use this transport. 

## Mac OS Editor Compatibility Issues
TODO.

## Mirror compatibility
**You need to be on Unity 2018.3.x or higher to use this version of Ignorance.**

## Target compatibility
We are targeting 64Bit platforms first, 32Bit platforms are lower priority.
- iOS
- Android (ARMv7, ARM64, x86, x86_64)
- Windows (x86 (possibly), x86_64)
- MacOS
- Linux (x86 (possibly), x86_64)

## Installation
TODO: Update this.
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
- Cornflake Packet Engine

## I want more info on Ignorance.
[You're welcome.](https://vis2k.github.io/Mirror/Transports/Ignorance)

## Credits
- **Coffee Donators**: Thank you so much.
- **Petris**: Code refactoring and tidy up (you rock man!)
- **[vis2k](https://github.com/vis2k)**: The mad man behind the scenes that made Mirror happen. Much respect.
- **Mirror Discord**: Memes, Courage, LOLs, awesome folks to chat with
- Others who I have missed. Thanks a lot, you know who you are.
