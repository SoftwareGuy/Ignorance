# Ignorance: A reliable UDP Transport for Mirror Networking.
Ignorance: A reliable UDP based transport for vis2k's [Mirror High-level API](https://github.com/vis2k/Mirror) which is a much improved version of UNET. 
This transport uses ENET as the network backend via nxrighthere's ENet-CSharp wrapper.

This transport is currently developed and actively used by Oiran Studio.

[If you use this, I'd appreciate a coffee to keep me caffeinated.](https://ko-fi.com/coburn)

## WARNING!
This version of Ignorance is for 2017 only. Select the "mirror2018" branch under "Branch" [or click here if you're lazy](https://github.com/SoftwareGuy/Ignorance/tree/mirror2018)) to go to the right branch.

## How do I tell if I'm using Mirror 2017 or 2018?
- If you have Mirror related DLLs, such as `Mirror.Runtime.dll` or `Mirror.Editor.dll` then you are using Mirror 2017. **This branch is the one you want.**
- If you have loose C# files, like `NetworkTransform.cs`, then you're using Mirror 2018 as Mirror no longer uses compiled DLLs. **The 2018 branch is the one you want**.

Ignorance 2017 is ***not compatible*** with Mirror 2018 and ***will cause*** initialization failures.

## Installation
1. Grab a release from the [releases page](https://github.com/SoftwareGuy/Ignorance/releases) or compile it from source.
2. Make sure you have [Mirror](https://github.com/vis2k/Mirror) installed in your project. **Ignorance WILL NOT function without it!**
3. Extract the downloaded release archive into your project, maybe under `Assets/Packages/IgnoranceTransport`.
4. Open your project with Unity and let it detect the new transport.
5. Follow "How to use" below.

As of version 1.0.9.1 I now custom bake the ENET Windows, Linux and Mac OS native plugins. These can be found in the project's Redist folder. Make sure the Redist folder sits with the baked DLL from the release, or the cooked DLL from source. They have support for LZ4 compression, which allows you to crunch data and save on bandwidth costs.

You only need `IgnoranceTransport.dll` plus the Redist folder if you're building from source. The PDB/MDB files are debugging symbols and are optional. You DO NOT need `UnityEngine.dll` or `Mirror.Runtime.dll` from the output directory.

## Compatibility
**x64 Runtime Platforms only! This is due to the dependencies only being compiled for x64.**
Tested and confirmed working on Unity LTS 2017.4, the recommended version of Unity to use with Mirror.
May work with newer versions of Unity as long as Mirror supports them.

## Dependencies
Ignorance relies on [ENet-CSharp](https://github.com/nxrighthere/ENet-CSharp) to do most of the heavy-lifting. I simply hook into the wrapper and perform magic tricks to make it all work.

Ignorance also has a dependency on [Mirror](https://github.com/vis2k/Mirror) which is a battle-tested, improved MMO-scale version of UNET that is leaps and bounds better than what Unity could ever do. Mirror was built against Unity LTS 2017.4 but as long as you have a recent release of Mirror in your project, the Ignorance transport will happily be available as a TransportLayer option. This dependency is required to make use of the TransportLayer class.

tldr: DLLs are included to build against for Mirror and Unity Engine are in the repo.
## How to use
1. Follow instructions above. If errors occur, open a Issue ticket.
2. In your Mirror NetworkManager child class, you would do:
```csharp
public override void InitializeTransport() {
  Transport.layer = new IgnoranceTransport();
}
```
...to start using Ignorance as the transport mechanism. The default in Mirror is Telepathy which is TCP. The other out-of-the-box option is Unity's LLAPI but that's like dealing with cancer - **Avoid**.
3. Continue programming your stuff as normal.

## Advanced users only: Accessing exposed functions
There are some exposed functions that network people might find nice to fiddle with. Since version 1.0.1, I have exposed the compression and configurable client timeout options. However, you cannot access these functions directly, but if you do the following inside your NetworkManager class, for example OnStartServer or OnStartClient overrides:

```csharp
(Transport.layer as IgnoranceTransport).SomeExposedFunction();
```
Then you can call the exposed functions that you desire.

**Do not try to cast the Transport.layer as a IgnoranceTransport class if you're using another transport. It will NOT work.**

## Why Ignorance? Why not name it something something Reliable UDP Transport for Mirror something something?
UDP ignores (hence the name) a lot of stuff that TCP fusses over and since UDP is designed to be a scattershot shotgun approach to networking, there's no promises that UDP packets will get from A to B without going through hell and back. Reliable UDP tries to mimic TCP to some extent with the sequencing and retransmission of packets until they land at the destination.

This makes sense in some usage cases like VoIP and multiplayer shooter games where UDP is top dog, but please do consider TCP (Telepathy) if you're doing Mission Critical networking. There's a reason why big name MMOs use TCP to keep everything in check. However, for some usage cases TCP may be a little overkill, so UDP is preferred.

## Credits
- **Coffee Donators**: Thank you so much.
- **[nxrighthere](https://github.com/nxrighthere)**: Debugging my broken code and identifying my packet code fuckups
- **[Draknith](https://github.com/FizzCube)**: Testing and mapping Reliable/Unreliable channels in Mirror to ENET Channels, testing.
- **[vis2k](https://github.com/vis2k)**: The mad man behind the scenes that made Mirror happen. Much respect.
- **Mirror Discord**: Memes, Courage, LOLs, awesome folks to chat with
