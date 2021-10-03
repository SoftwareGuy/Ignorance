<p align="center">
  <img src="http://oiran.studio/images/ignorance14.png" alt="Ignorance 1.4 Logo"/>
</p>

Ignorance 1.4 Beta
=============
[![Ko-Fi](https://img.shields.io/badge/Donate-Ko--Fi-red)](https://ko-fi.com/coburn) 
[![PayPal](https://img.shields.io/badge/Donate-PayPal-blue)](https://paypal.me/coburn64) 
[![GitHub issues](https://img.shields.io/github/issues/SoftwareGuy/Ignorance.svg)](https://github.com/SoftwareGuy/Ignorance/issues)
![GitHub last commit](https://img.shields.io/github/last-commit/SoftwareGuy/Ignorance.svg) ![MIT Licensed](https://img.shields.io/badge/license-MIT-green.svg)

_I'd appreciate [a coffee](https://ko-fi.com/coburn) if you use this transport._

_"Probably the fastest transport out there for Mirror..." - FSE_Vincenzo, 2021 (Mirror Discord)_

Ignorance is a high-performance UDP based transport that plugs into [Mirror Networking](https://github.com/vis2k/Mirror). By harnessing the power of the tried and true ENet native library, it provides reliable and unreliable UDP communications with up to 4096 connected peers (clients) and 255 channels. Reliable UDP has a lot of benefits over TCP which was the default in Mirror until KCP was implemented.

The benefit of Ignorance is that you can utilize channels which allow you to split up network communications. This allows you to have channels for data that are mission critical and must be sent out the door as quickly as possible as well as channels that can send slower non-essential data.

**Mirror LTS v46.x or Mirror Mainline v46.x onwards required.** Ignorance 1.4.0 (Non-Beta) will not work with older Mirror versions.

Ignorance Standalone
------------
See [STANDALONE.md](https://github.com/SoftwareGuy/Ignorance/blob/master/STANDALONE.md).

Licensing Warning
------------
Ignorance is licensed under MIT license. However, there has been recent cases where other developers have been using the full Ignorance source or parts thereof, stripping the MIT licensing and slapping their own license on it instead. This falls in violation of the MIT license as it clearly states that copyright notices must remain intact.

In short, don't be a code thief and respect the MIT license.

Ignorance with Mirror/Mirage/et al.
------------
Ignorance itself was **not** designed to be used outside of Mirror. However, you can utilize *Ignorance Standalone* to get the same performance but without the Mirror Transport bindings.  

Mirage has it's own Ignorance NG port which is mantained by the Mirage team themselves.

🐟-Net uses/used a mashed up version of Ignorance called *Fluidity*. For reasons that won't be disclosed here, do not report any Fludity bugs here.

If you are using your own network stack or you are trying to plumb Ignorance to another networking solution that already exists, you are **much** better off using the [ENet-CSharp wrapper](https://github.com/SoftwareGuy/ENet-CSharp) to talk to ENet directly. You could also try Ignorance Standalone which is mentioned above.

In short: Ignorance is not designed out of the box to work with anything other than Mirror (and by extension, Mirage if you count the port). Ignorance Standalone should work fine.


Ignorance in Action
------------

- **If you own a copy of Population One, congrats.** That game uses Ignorance as its primary network transport layer. It also earns its spot into the first major game that is using Ignorance.

- Ignorance was used in a "Vinesauce is Hope" walking simulation demo which had multiple server instances running with over 300 CCU each. [Video clips available here](https://clips.twitch.tv/UglyColdbloodedAlfalfaAllenHuhu).

What devices are supported?
------------

**IMPORTANT: 32Bit Desktop Targets are NOT supported. Target 64Bit for Desktop or bust.**

The native library, ENet, does not support 32bit targets on desktop. To work around this, build your Unity project and target **x86_64** 
in the Unity Build Settings window. There are a lot of other benefits to be using a 64bit runtime as well. If you cannot build for 64bit, open a support ticket.

**Supported platforms, out of the box:**

- 64Bit Desktop Platforms (Windows, Mac, Linux), Android (ARMv7/ARM64) including VR devices and iOS (ARMv7/ARM64).
- 
- If ENet native can run on it and it's supported by Unity, you're good to go with Ignorance.

**Other platforms that require some extra work:**

- Consoles, like the Nintendo Switch, PlayStation 4 and PlayStation 5. I do not have development clearance to build the native library for these platforms, so they require additional work to get functioning.

For more info, see the FAQ.


Dependencies
------------

Please note that the repository doesn't include Mirror, instead it only provides you the Ignorance code.

Make sure you have Mirror installed and up to date before installing Ignorance.

-   [ENet-CSharp](https://github.com/SoftwareGuy/ENet-CSharp): **Slightly modified version included**

-   [Mirror Networking](https://github.com/vis2k/Mirror): use the latest Asset Store release. Git Master also supported but not recommended.

Installation
------------

Download the Unity Package from Releases that is the latest one. Simply import the Unity Package and Unity will do the rest. 
Follow the instructions below. 

Alternatively you can use the code from the master branch, which is often up to date compared to the releases.

How to use
----------

I have included two pre-configured sample scenes so you can get started easily. One is Pong, one is a copy paste with some modifications
of Mirror's Basic scene. Otherwise add the script called **Ignorance** to your NetworkManager object, removing any TCP-based or other 
UDP-based transport (ie. kcp2k). Then set the script to be used in NetworkManagers' "Transport" field.

FAQ (aka Please Read This First)
--------------------------------

See [FAQ.md](https://github.com/SoftwareGuy/Ignorance/blob/master/FAQ.md).

I found a bug, where do I report it?
--------------------------------
[Check the current open bug reports and/or report a new one here](https://github.com/SoftwareGuy/Ignorance/issues).

Failing that you can always ping me in the Mirror Discord and I'll reply as soon as I can. Don't **excessively** ping me or you'll suffer my wrath. 

Bugs that do not have enough details will be either closed or put as low priority. Details like your OS, Unity Editor version, any Ignorance errors, etc is essential for a good
bug report.

### I have other questions, I want answers.

[Here's a quick primer.](https://vis2k.github.io/Mirror/Transports/Ignorance). It might be slightly out of date, but it covers the basics of Ignorance.


Credits
-------

-   **Donators**: Thanks for helping keep the lights on.

-	[FSE_Vincenzo](https://github.com/Vincenz099): Resident master of the ENet way. They are part of Flying Squirrel Entertainment - go check their games out.
	
-	[JesusLuvsYooh](https://github.com/JesusLuvsYooh) : CCU endurance testing project, ideas, fixes and other awesome stuff

-   [Katori](https://github.com/katori): Helped kickstart the threaded version that evolved into short-lived Ignorance 2.x version, which later became 
	Ignorance Threaded.

-   [PhantomGamers](https://github.com/PhantomGamers): Got Mirror + Ignorance working as a BepInEx client-side side-load modification for a game. Holy shit, that's cool.   

-   [BigBoxVR](https://github.com/GabeBigBoxVR): Pull requests and found a race condition that threatened stability. Also uses Ignorance in Population One, a VR title.

-   [Martin](https://github.com/martindevans): Pull requests, testing with Dissonance. Great VoIP asset for Unity.

-   [c6burns](https://github.com/c6burns), [Petris](https://github.com/MichalPetryka), [shiena](https://github.com/shiena), [Draknith](https://github.com/FizzCube), [nxrighthere](https://github.com/nxrighthere), [vis2k](https://github.com/vis2k), [Paul](https://github.com/paulpach)

-   The Mirror Discord and the others who I have missed. Thanks a lot, you know who you are.

-   You too can have your name here if you send in a PR. Don't delay, get your PR in today!

**To be continued...?**
