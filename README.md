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

**IMPORTANT:** Ignorance does not support building for x86 (32bit) targets. Majority of devices come with 64bit operating systems now. The native library, ENet,
does not support 32bit targets on desktop. To work around this, build your Unity project and target **x86_64** in the Unity Build Settings window. There are a lot
of other benefits to be using a 64bit runtime as well. If you cannot build for 64bit, open a support ticket.

Not using Mirror? Read This First
------------

*tl;dr: If you are using Mirror, skip this. If you're using Mirage, there's a Ignorance port available for Mirage. This mini-rant is solely to 
cover my ass because some developers are trying to use this Mirror-oriented transport with their own networking stack and it pains me inside.*

Please allow me to make this *very* clear: Ignorance is **not designed to be used outside of the Mirror Networking stack**. Why is this, you ask?

A few people have approached me and asked "hey can I use this with my own networking solution?" or they go ahead and try to shoehorn Ignorance into their own network stack,
make a mess of it and then either blame me for the failure, come crying to me and beg for me to figure out what they did wrong or why Ignorance doesn't do what they want it to
on their own stack. Some others don't even know half of what Ignorance does under the bonnet and they look at me strangely when shit breaks.

If you are going to use your own network stack, you are **much better off using the [ENet-CSharp](https://github.com/SoftwareGuy/ENet-CSharp) repository** to create
your own ENet bridge between Managed C# and Native C ENet worlds. You can study Ignorance as a implementation example of a high-performance glue layer that sits between your code
and ENet, but trying to shoehorn Ignorance into your own network stack will most likely lead you to pain, misery and probably questioning yourself why you are even doing this in
the first place.

In short: Ignorance is not designed out of the box to work with anything other than Mirror (and by extension, Mirage if you count the port). Do not expect support, if any at all. 
I have enough to deal with keeping Ignorance up to date and running as smoothly as possible in Mirror environments than worry about other developers that don't 

Thank you for attending my TED talk, now on with the show.

Ignorance in Action
------------

- **If you own a copy of Population One, congrats.** That game uses Ignorance as its primary network transport layer. It also earns its spot into the first major game that is using Ignorance.

Ignorance was used in a "Vinesauce is Hope" walking simulation demo which had multiple 
server instances running with over 300 CCU each. [Video clips available here.](https://clips.twitch.tv/UglyColdbloodedAlfalfaAllenHuhu)

What devices are supported?
------------

- 64Bit Desktop Platforms (Windows, Mac, Linux), Android and iOS (ARMv7/ARM64).

- Android-powered VR devices

- If ENet native can run on it and it's supported by Unity, you're good with Ignorance.

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