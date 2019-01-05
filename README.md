# Ignorance: A reliable UDP Transport for Mirror Networking.
Ignorance is a reliable UDP based transport for vis2k's Mirror High-level API which is a much improved version of UNET. 
This transport uses ENET as the network backend via nxrighthere's ENet-CSharp wrapper.

This branch adds support for async transports, which are part of Mirror 2018.

This transport is currently developed and actively used by Oiran Studio.

[If you use this, I'd appreciate a coffee to keep me caffeinated.](https://ko-fi.com/coburn)

## Known Issues
As with anything, no code is perfect. Please understand while this can be used in production, unfortunately there are a few bugs that may cause some undesirable outcomes, which are as follows:

- **Random refusals to connect**: This one is the most annoying one to fix - it seems that the RNG demon is at play here. Sometimes you can get your first NetworkClient session to refuse to connect to a active server instance, even if it's running on localhost. Subsequent NetworkClient connections are hit and miss - majority of the time you connect fine though.

In my testing, I've been able to get a row of 10 to 15 connections that work, then about 2 or 3 duds, then more successful ones. Maybe there's some operating system things at play here. Simply keep trying to connect, it'll get there.

- **Random pre-connection hangs**: This is a very weird issue where sometimes when the transport is connecting to a server, your application (or the Unity Editor) will hang for about 5 seconds, then resume normal activity and connect to the server. I don't have a clue why this happens, no errors are emitted or anything during the hang.

***If you are a debugger and have a swiss army knife of tools that may eliminate any (or all) of the above, please raise a issue ticket and let me know.*** I appreciate the help.

## Installation
**Note: these instructions have been updated for the Ignorance 2018 branch and are different than the master branch**
1. Ensure Mirror 2018 is up to date and has the latest release from the [official git](https://github.com/vis2k/Mirror). Make sure you use the *2018* branch!
1. Grab a release from the [releases page](https://github.com/SoftwareGuy/Ignorance/releases) that is for **Mirror 2018**. Mirror 2018 uses no more DLLs so you can debug problems a lot easier.
2. Extract the archive somewhere. I recommend a folder called `Ignorance` under `Assets`. Don't modify any file structure.
3. If you are already using UnityAsync in your project, delete the UnityAsync folder under the extracted archives' Dependencies folder. Otherwise skip this step.
4. Let Unity detect the changes and get itself settled.

**There is NO NEED to compile Ignorance 2018.** To get the latest version, clone the repo. Install the latest 2018 release into your project, then copy the `IgnoranceTransport.cs` file over the top of the release `IgnoranceTransport.cs` file. That's all you have to do.

## Compatibility
- Tested and confirmed working on Unity 2018.2.20 with Mirror for Unity 2018 branch.
- Windows, Linux and Mac x64 (64Bit) supported. x86 (32Bit) is not.
- Android ARMv7 and ARM64 (aarch64) supported.
- iOS not currently supported.

## Dependencies
- We rely on [ENet-CSharp](https://github.com/nxrighthere/ENet-CSharp) to do most of the heavy-lifting. Ignorance hooks into the wrapper and perform magic tricks to make it all work.
- We have a dependency on the awesomeness that is [Mirror](https://github.com/vis2k/Mirror) which is a battle-tested, improved MMO-scale version of UNET that is leaps and bounds better than what Unity could ever do. 
- We also use UnityAsync so that we can run our code without it blocking things.

## How to use
1. Follow instructions above. If errors occur, open a Issue ticket.
2. In your Mirror NetworkManager child class, you would do the following to start using Ignorance as the transport mechanism:
```csharp
public override void InitializeTransport() {
  NetworkManager.transport = new IgnoranceTransport();
}
```
3. You should get a "Ignorance Transport initialized" message if all is correctly initializing.
4. Continue programming your stuff as normal.

## Advanced users only: Accessing exposed functions
There are some exposed functions that network people might find nice to fiddle with.
However, you cannot access these functions directly, but if you do the following inside your NetworkManager class, for example OnStartServer or OnStartClient, then you can call the exposed functions that you desire.

```csharp
(NetworkManager.transport as IgnoranceTransport).SomeExposedFunction();
```

**Do not try to cast the Transport.layer as a IgnoranceTransport class if you're using another transport.** It will not work.

## Why Ignorance? Why not name it something something Reliable UDP Transport for Mirror something something?
UDP ignores (hence the name) a lot of stuff that TCP fusses over and since UDP is designed to be a scattershot shotgun approach to networking, there's no promises that UDP packets will get from A to B without going through hell and back. Reliable UDP tries to mimic TCP to some extent with the resending of packets until they land at the destination.

This makes since in some usage cases, but you should really consider TCP if you're using networking in a mission-critical environment. There's a reason why big name MMOs use TCP to keep everything in check. However, for some usage cases TCP may be a little overkill, so UDP is preferred.

## Credits
- Coffee donators: Thank you so much.
- uwee: Offered resources to crank out a build box for compiling ENET for Android.
- nxrighthere: Debugging my broken code and identifying my packet code fuckups
- Draknith (on Mirror Discord): Testing and mapping Reliable/Unreliable channels in Mirror to ENET Channels, testing.
- Paul (on Mirror Discord): A champion beyond words.
- vis2k: The madman behind the scenes that made Mirror happen!
- Mirror Discord: Encouragement, Memes, LOLs and just awesome folks.
