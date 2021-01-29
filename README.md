<p align="center">
  <img src="http://oiran.studio/images/ignorance14.png" alt="Ignorance 1.4 Logo"/>
</p>

Ignorance 1.4 Alpha
=============
[![Ko-Fi](https://img.shields.io/badge/Donate-Ko--Fi-red)](https://ko-fi.com/coburn) 
[![PayPal](https://img.shields.io/badge/Donate-PayPal-blue)](https://paypal.me/coburn64) 
[![GitHub issues](https://img.shields.io/github/issues/SoftwareGuy/Ignorance.svg)](https://github.com/SoftwareGuy/Ignorance/issues)
![GitHub last commit](https://img.shields.io/github/last-commit/SoftwareGuy/Ignorance.svg) ![MIT Licensed](https://img.shields.io/badge/license-MIT-green.svg)

_I'd appreciate [a coffee](https://ko-fi.com/coburn) if you use this transport._

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

Compatiblity
------------

### Desktop

-   **Windows x64**
	-	Windows 7 64bit onwards.
	
	-	**Not compatible with 32bit or ARM-based Environments.**
    
	-   ARM64 Environments not currently supported (ie. Windows 10 on Raspberry Pi 4)
        
-   **macOS 10.12 onwards**
	-	High Sierra and upwards are **confirmed working**. El Captain and below are **not supported**.	
	
	-	Big Sur has **not** yet been tested.
	
	-	You may need to **unblock** the native ENet library with GateKeeper if it cannot load it inside your Unity project.

-   **Linux x64**
    -   Should *just work* on various Linux distros, as long as they use GNU libc.
	
	-	Distros using non-GNU libc will most likely fail to work.

### Console

-   **Nintendo Switch**
    - 	Due to NDA, you'll need to manually compile the ENet native library, [see this document from the ENet-CSharp repository.](https://github.com/SoftwareGuy/ENet-CSharp/blob/master/BUILD-FOR-SWITCH.txt)

-   **Xbox One**
    -   UWP: Tested and confirmed working for **LAN Client only**. LAN Host doesn't work.

    -   Native: Possibly, however I don’t have development kit or license to test.

-   **PlayStation**
	- 	Would need patches for native library to work on Sony's BSD OS.
	
### Mobile

-   **Android**

	- 	Works fine on Android 5.0 "Lollipop" onwards.

-   **Apple iOS**

    -   Supports iPhone 4S to the latest iPhone. Tested and working on iPhone 4S, iPhone 5s, iPad 2 WiFi + 3G and iPad (5th Gen)

Dependencies
------------

**All dependencies are included, except Mirror if you are using a release package.**

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

### Does Ignorance do NAT-PMP, UPnP or NAT Punching?

No. In this day and age of the internet, relying on NAT Punching is russian roulette. Many routers ship with UPnP/NAT-PMP disabled due to security concerns. That said,
your ISP (read: the entity that provides you internet access) may have their own firewall that blocks incoming connections. **Punch through will fail on mobile networks, such as 3G/4G/5G**.

If you really need to break through restrictive firewalls, your best bet is to use some sort of Relay mechanism. As far as the firewall is concerned, it just sees it as a single connection. Mirror supports some relays that work well with Ignorance. For more information, check out the Mirror Discord.

### Ignorance doesn't seem to work when building for Standalone target?

Make sure you aren't compiling your game for 32bit Windows. ENet doesn't support 32bit platforms unless you
manually compile it yourself. 

### Why should I use Ignorance over Unity LLAPI?

Unity LLAPI is obsolete and dead in the eyes of Unity Technologies. Depending on what you threw over the network, you'd get random latency spikes and packets would 
go missing even in reliable delivery mode. Yes, it was **that bad**.

That alone says something.  

### What happened to Ignorance Classic?

It died back in the 1.3.x days. The classic version was becoming obsolete and not worth the time keeping it up to date.

### LateUpdate for your transport code? WTF?!

This was a design decision by vis2k when he found Mirror blocking one of his assets' Update loop. So, we kinda got forced into using LateUpdate.

There might be a chance that at very low framerate (ie. you are really stressing the server or creating lots of GameObjects in which Unity has to load from disk)
that the networking gets overwhelmed, regardless of how ENet is coping in the native world. 

It is essential to keep your server's frame rate running as high as possible as this will reduce latency and in-game lag. You will be able to tell when the server 
is under heavy stress when networked objects get very choppy and/or the client starts hanging.

Mirrors' LateUpdate polling is a design flaw will be fixed later on.

### I found a bug, where do I report it?

[Check the current open bug reports and/or report a new one here](https://github.com/SoftwareGuy/Ignorance/issues).

Failing that you can always ping me in the Mirror Discord and I'll reply as soon as I can. Don't **excessively** ping me or you'll suffer my wrath. 

Bugs that do not have enough details will be closed with \#wontfix. Details like your OS, Unity Editor version, any Ignorance errors, etc is essential for a good
bug report.

### I have other questions, I want answers.

[Here's a quick primer.](https://vis2k.github.io/Mirror/Transports/Ignorance). It might be slightly out of date, but it covers the basics of Ignorance.

### I am not satisfied with Ignorance.

Please let me know why as I can't improve my code if I don't get feedback.
*However, if you're just here to troll then please move on.*

Credits
-------

-   **Donators**: Thanks for helping keep the lights on.

-	[FSE_Vincenzo](https://github.com/Vincenz099): Resident ENet guy, also part of Flying Squirrel Entertainment. Go check their games out, he uses
	ENet under the hood.

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