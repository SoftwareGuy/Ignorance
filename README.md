<p align="center">
  <img src="http://oiran.studio/images/ignorance14.png" alt="Ignorance 1.4 Logo"/>
</p>

Ignorance 1.4 Long Term Support (LTS)
=============
[![Ko-Fi](https://img.shields.io/badge/Donate-Ko--Fi-red)](https://ko-fi.com/coburn) 
[![PayPal](https://img.shields.io/badge/Donate-PayPal-blue)](https://paypal.me/coburn64) 
[![GitHub issues](https://img.shields.io/github/issues/SoftwareGuy/Ignorance.svg)](https://github.com/SoftwareGuy/Ignorance/issues)
![GitHub last commit](https://img.shields.io/github/last-commit/SoftwareGuy/Ignorance.svg) ![MIT Licensed](https://img.shields.io/badge/license-MIT-green.svg)

_I'd appreciate [a coffee](https://ko-fi.com/coburn) if you use this transport in your project and you want to help keep my bills paid and lights on._

_"Probably the fastest transport out there for Mirror..." - FSE_Vincenzo, 2021 (Mirror Discord)_

Ignorance is a high-performance UDP based transport that plugs into [Mirror Networking](https://github.com/vis2k/Mirror). By harnessing the power of the tried and true ENet native library, it provides reliable and unreliable UDP communications with up to 4096 connected peers (clients) and 255 channels. Reliable UDP has a lot of benefits over TCP which was the default in Mirror until KCP was implemented.

The benefit of Ignorance is that you can utilize channels which allow you to split up network communications. This allows you to have channels for data that are mission critical and must be sent out the door as quickly as possible as well as channels that can send slower non-essential data.

Long Term Support
------------
After years of developing Ignorance and all the blood, stress and tears, it's time for me to put this transport into LTS status. **Does that mean I'm no longer working on this?** 

No, Ignorance will continue to get updates. You will get support when you need it and I will try to keep it up to date with any breaking changes the Mirror transport interface.

This somewhat painful decision comes after a grave misunderstanding and over dramatization of an issue between myself and the Mirror project leader over petty things. I'll refrain from explaining it in full here but I have decided given the said persons' actions, it's time for me to move on. There may be a time in the _near?_ future that I return to using Mirror for my projects, but for now I am focusing on what needs to be done: _actually making a game_.

If the said leader reads this, it's never too late to apologize. My door is open.

Licensing
------------
Ignorance is licensed under MIT license. However, there has been recent cases where other developers have been using the full Ignorance source or parts thereof, stripping the MIT licensing and slapping their own license on it instead.

This falls in violation of the MIT license as it clearly states that copyright notices must remain intact. In short, don't be a code thief and respect the MIT license.

Requirements
-----------
- Mirror LTS 46.x, Mirror from Asset Store or Mirror Master
- Unity 2019 LTS minimum, Unity 2020 LTS recommended. Whatever the minimum is for Mirror will do, really
- Basic knowledge of Unity Engine and Mirror Networking

**Ignorance 1.4 LTS will not work with older Mirror versions.** You can backport 1.4 LTS to your older Mirror project, but that support is on you.

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

Documentation
------------
See [DOCUMENTATION.md](https://github.com/SoftwareGuy/Ignorance/blob/master/DOCUMENTATION.md).

For *Ignorance Standalone*, see [STANDALONE.md](https://github.com/SoftwareGuy/Ignorance/blob/master/STANDALONE.md).

For the FAQ (aka Please Read This First), see [FAQ.md](https://github.com/SoftwareGuy/Ignorance/blob/master/FAQ.md).

Ignorance with Mirage/et al.
------------
- Mirage users: You're better off using the new NanoSockets-powered Mirage Reliable UDP transport

- FishNet users: Fluidity (the one you probably used) was a hacked and mashed up version of Ignorance. For reasons that won't be disclosed here, do not report any Fludity bugs here.

- Other usages: Ignorance itself was **not** designed to be used outside of Mirror. However, you can utilize *Ignorance Standalone* to get the same performance but without the Mirror Transport bindings. 

If you are using your own network stack or you are trying to plumb Ignorance to another networking solution that already exists, you are **much** better off using the [ENet-CSharp wrapper](https://github.com/SoftwareGuy/ENet-CSharp) to talk to ENet directly. You could also try Ignorance Standalone which is mentioned above.

Ignorance in Action
------------
- **If you own a copy of Population One, congrats.** That game uses Ignorance as its primary network transport layer. It also earns its spot into the first major game that is using Ignorance.

- Ignorance was used in a "Vinesauce is Hope" walking simulation demo which had multiple server instances running with over 300 CCU each. [Video clips available here](https://clips.twitch.tv/UglyColdbloodedAlfalfaAllenHuhu).

What devices are supported?
------------
**IMPORTANT: 32Bit Desktop Targets are NOT supported. Target 64Bit for Desktop or bust.**

The native library, ENet, does not support 32bit targets on desktop. To work around this, build your Unity project and target **x86_64** platform in the Unity Build Settings window. 
There are a lot of other benefits to be using a 64bit runtime as well. If you cannot build for 64bit, open a support ticket.

**Supported platforms, out of the box:**

- 64Bit Desktop Platforms (Windows, Mac, Linux), Android (ARMv7/ARM64) including VR devices and iOS (ARMv7/ARM64).

- If ENet native can run on it and it's supported by Unity, you're good to go with Ignorance.

- **Apple M1 Device Users** must recompile ENet Native for their M1 processors. Using Ignorance straight out of the box will result not be able to load the x86_64 library on Apple Silicon.

**Other platforms that require some extra work:**

- Consoles like the Nintendo Switch, PlayStation 4 and PlayStation 5. I do not have development clearance to build the native library for these platforms, so they require additional work to get functioning.

For more info, see the FAQ.

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
