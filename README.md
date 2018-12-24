# Ignorance: A reliable UDP Transport for Mirror Networking.
Ignorance is a reliable UDP based transport for vis2k's Mirror High-level API which is a much improved version of UNET. 
This transport uses ENET as the network backend via nxrighthere's ENet-CSharp wrapper.

This branch adds support for async transports, which are part of Mirror 2018.

This transport is currently developed and actively used by Oiran Studio.

[If you use this, I'd appreciate a coffee to keep me caffeinated.](https://ko-fi.com/coburn)

## Installation
**Note: these instructions have been updated for the 2018 branch and are different than the master branch**
1. Grab a release from the [releases page](https://github.com/SoftwareGuy/Ignorance/releases) that is for **Mirror 2018**. Mirror 2018 uses no more DLLs so you can debug problems a lot easier.
2. Extract the archive somewhere. I recommend a folder called `Ignorance` under `Assets`. Don't modify any file structure.
3. If you are already using UnityAsync in your project, delete the UnityAsync folder under the extracted archives' Dependencies folder. Otherwise skip this step.
4. Let Unity detect the changes and get itself settled.

**There is NO NEED to compile Ignorance 2018.** To get the latest version, clone the repo. Install the latest 2018 release into your project, then copy the `IgnoranceTransport.cs` file over the top of the release `IgnoranceTransport.cs` file. That's all you have to do.

## Compatibility
**x64 Runtime Platforms only! This is due to the dependencies only being compiled for x64.**
Tested and confirmed working on Unity 2018.2.20 with Mirror for Unity 2018 branch.

## Dependencies
- We rely on [ENet-CSharp](https://github.com/nxrighthere/ENet-CSharp) to do most of the heavy-lifting. Ignorance hooks into the wrapper and perform magic tricks to make it all work.
- We have a dependency on the awesomeness that is [Mirror](https://github.com/vis2k/Mirror) which is a battle-tested, improved MMO-scale version of UNET that is leaps and bounds better than what Unity could ever do. 
- We also use UnityAsync so that we can run our code without it blocking things.

## How to use
1. Follow instructions above. If errors occur, open a Issue ticket.
2. In your Mirror NetworkManager child class, you would do:
```csharp
public override void InitializeTransport() {
  Transport.layer = new IgnoranceTransport();
}
```
...to start using Ignorance as the transport mechanism. You should get a "Ignorance Transport initialized" message.

3. Continue programming your stuff as normal.

## Advanced users only: Accessing exposed functions
There are some exposed functions that network people might find nice to fiddle with. Since version 1.0.1, I have exposed the compression and configurable client timeout options. However, you cannot access these functions directly, but if you do the following inside your NetworkManager class, for example OnStartServer or OnStartClient overrides:

```csharp
(Transport.layer as IgnoranceTransport).SomeExposedFunction();
```
Then you can call the exposed functions that you desire.

**Do not try to cast the Transport.layer as a IgnoranceTransport class if you're using another transport.** It will not work.

## Why Ignorance? Why not name it something something Reliable UDP Transport for Mirror something something?
UDP ignores (hence the name) a lot of stuff that TCP fusses over and since UDP is designed to be a scattershot shotgun approach to networking, there's no promises that UDP packets will get from A to B without going through hell and back. Reliable UDP tries to mimic TCP to some extent with the resending of packets until they land at the destination.

This makes since in some usage cases, but you should really consider TCP if you're using networking in a mission-critical environment. There's a reason why big name MMOs use TCP to keep everything in check. However, for some usage cases TCP may be a little overkill, so UDP is preferred.

## Credits
- Coffee donators: Thank you so much.
- nxrighthere: Debugging my broken code and identifying my packet code fuckups
- Draknith (on Mirror Discord): Testing and mapping Reliable/Unreliable channels in Mirror to ENET Channels, testing.
- Paul (on Mirror Discord): A champion beyond words.
- vis2k: The madman behind the scenes that made Mirror happen!
- Mirror Discord: Encouragement, Memes, LOLs and just awesome folks.
