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

Ignorance 1.4 is a ENet-powered transport that plugs into the [Mirror Networking](https://github.com/vis2k/Mirror) project. It provides a high-performance
implementation of the tried and true native ENet C library which provides reliable UDP communications for your game projects. Variants of ENet are used by Triple-A
game companies for their networking. Reliable UDP has a lot of benefits over TCP which was the default in Mirror.

Ignorance was originally intended to replace Unity's LLAPI Transport that ships with Mirror, however since it uses native libraries it was deemed too risky to ship with 
Mirror itself.

ENet supports a maximum of 4096 peers connected at the same time with up to 255 channels. Channels allow you to split up network communications so you can have channels
for data that are mission critical as well as non-essential data. The native library has also been proven to be superior when compared to Unity's own LLAPI library.

Ignorance in Action
------------

Ignorance was used in a recent "Vinesauce is Hope" walking simulation demo which had multiple 
server instances running with over 300 CCU each. [Video clips available here.](https://clips.twitch.tv/UglyColdbloodedAlfalfaAllenHuhu)

What devices are supported?
------------

- Desktop platforms such as Windows, Mac OS and Linux are supported out of the box along with Android and iOS. 

- Consoles are hit and miss, since they run on slimmed down operating systems.

- If ENet can run on it and it's supported by Unity, you're good with Ignorance.

For more info, see the FAQ.


Dependencies
------------

Please note that the repository doesn't include Mirror, instead it only provides you the Ignorance code.

Make sure you have Mirror installed and up to date before installing Ignorance.

-   [ENet-CSharp](https://github.com/SoftwareGuy/ENet-CSharp): **slightly modified version included**

-   [Mirror Networking](https://github.com/vis2k/Mirror): use Asset Store release please, only use Git Master release for bug fixes that you desperately need

Installation
------------

Download the Unity Package from Releases that is the latest one. Simply import
the Unity Package and Unity will do the rest. Follow the instructions below.

How to use
----------

I have included a pre-configured sample scene so you can get started easily. Otherwise add the script 
called **Ignorance** to your NetworkManager object, removing any TCP-based or other UDP-based transport. 
Then set the script to be used in NetworkManagers' "Transport" field.

FAQ (aka Please Read This First)
--------------------------------

See [FAQ.md](https://github.com/SoftwareGuy/Ignorance/blob/master/FAQ.md).

I found a bug, where do I report it?
--------------------------------
[Check the current open bug reports and/or report a new one here](https://github.com/SoftwareGuy/Ignorance/issues).

Failing that you can always ping me in the Mirror Discord and I'll reply as soon as I can. Don't **excessively** ping me or you'll suffer my wrath. 

Bugs that do not have enough details will be closed with \#wontfix. Details like your OS, Unity Editor version, any Ignorance errors, etc is essential for a good
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

-   [BigBoxVR](https://github.com/GabeBigBoxVR): Pull requests and found a race condition that threatened stability. Also uses Ignorance in Population One, a VR title.

-   [Martin](https://github.com/martindevans): Pull requests, testing with Dissonance. Great VoIP asset for Unity.

-   [vis2k](https://github.com/vis2k) and [Paul](https://github.com/paulpach): Mirror and MirrorNG developers respectively.

-   [c6burns](https://github.com/c6burns), [Petris](https://github.com/MichalPetryka), [shiena](https://github.com/shiena), [Draknith](https://github.com/FizzCube): Former buddies that helped a lot.

-   [nxrighthere](https://github.com/nxrighthere): Author of ENet-CSharp in which I forked and made custom improvements to it.

-   The Mirror Discord and the others who I have missed. Thanks a lot, you know who you are.

-   You too can have your name here if you send in a PR. Don't delay, get your PR in today!

**To be continued...?**