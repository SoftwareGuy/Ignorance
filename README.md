# Ignorance
Ignorance: A reliable UDP based transport for vis2k's Mirror High-level API which is a much improved version of UNET.
Ignorance uses ENET as the network backend via nxrighthere's ENet-CSharp wrapper.

[If you use this, I'd appreciate a coffee to keep me caffeinated.](https://ko-fi.com/coburn)

## Installation
1. Grab a release from the releases page.
2. Make sure you have Mirror installed in your project. **Ignorance WILL NOT function without it!**
NOTE: If you compile from source you will need to grab the Windows, MacOS and Linux DLLs from 

## Compatibility
Tested and confirmed working on Unity LTS 2017.4, the recommended version of Unity to use with Mirror.
May work with newer versions of Unity as long as Mirror supports them.

## Dependencies
Ignorance relies on [ENet-CSharp](https://github.com/nxrighthere/ENet-CSharp) to do most of the heavy-lifting. I simply hook into the wrapper and perform magic tricks to make it all work.

Ignorance also has a dependency on [Mirror](https://github.com/vis2k/Mirror) which is a battle-tested, improved MMO-scale version of UNET that is leaps and bounds better than what Unity could ever do. Mirror was built against Unity LTS 2017.4 but as long as you have a recent release of Mirror in your project, the Ignorance transport will happily be available as a TransportLayer option. This dependency is required to make use of the TransportLayer class.

tldr: ENet-CSharp, Mirror, Unity Engine 2017.4 LTS. DLLs are included to build against for Mirror and Unity Engine are in the repo.
## How to use
1. Download the git and compile the project. If errors occur, open a Issue ticket.
2. In your Mirror NetworkManager child class, you would do:
```csharp
public override void InitializeTransport() {
  Transport.layer = new IgnoranceTransport();
}
```
...to start using Ignorance as the transport mechanism. The default in Mirror is Telepathy which is TCP. The other out-of-the-box option is Unity's LLAPI but that's like dealing with cancer - **Avoid**.

3. Continue programming your stuff as normal.

## Why Ignorance? Why not name it something something Reliable UDP Transport for Mirror something something?
UDP ignores (hence the name) a lot of stuff that TCP fusses over and since UDP is designed to be a scattershot shotgun approach to networking, there's no promises that UDP packets will get from A to B without going through hell and back. Reliable UDP tries to mimic TCP to some extent with the resending of packets until they land at the destination.

This makes since in some usage cases, but you should really consider TCP if you're using networking in a mission-critical environment. There's a reason why big name MMOs use TCP to keep everything in check. However, for some usage cases TCP may be a little overkill, so UDP is preferred.

## Credits
nxrighthere: Debugging my broken code and identifying my packet code fuckups
Draknith (on Mirror Discord): Testing and mapping Reliable/Unreliable channels in Mirror to ENET Channels, testing.
vis2k: The madman behind the scenes that made Mirror happen!
The rest of the Mirror Discord: Encouragement, Memes, LOLs and just awesome folks.
