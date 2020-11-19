Ignorance 1.3
=============
[![Ko-Fi](https://img.shields.io/badge/Donate-Ko--Fi-red)](https://ko-fi.com/coburn) 
[![PayPal](https://img.shields.io/badge/Donate-PayPal-blue)](https://paypal.me/coburn64) 
[![GitHub issues](https://img.shields.io/github/issues/SoftwareGuy/Ignorance.svg)](https://github.com/SoftwareGuy/Ignorance/issues)
![GitHub last commit](https://img.shields.io/github/last-commit/SoftwareGuy/Ignorance.svg) ![MIT Licensed](https://img.shields.io/badge/license-MIT-green.svg)

_If you feel so inclined, I'd appreciate [**a coffee** to keep me
caffeinated](https://ko-fi.com/coburn) if you use this transport._

Welcome to Ignorance, a Enet-powered Transport system for the [Mirror
Networking](https://github.com/vis2k/Mirror) project. By utilizing the tried
and true ENet native C library, Ignorance provides reliable UDP communications 
for your game projects where the default Telepathy TCP transport would choke.

Ignorance scales up to 4096 theortical CCU with up to 255 channels per client (also known as a peer in Enet terms). Channels 
allow you to split up network communications so you can have channels for data that are 
mission critical as well as non-essential data. Mirror and by extension, Unity, choke 
before hitting that CCU mark in the real world unless one carefully optimizes their network code carefully.

Ignorance was used in a recent "Vinesauce is Hope" walking simulation demo which had multiple 
server instances running with over 300 CCU each. [Video clips available here.](https://clips.twitch.tv/UglyColdbloodedAlfalfaAllenHuhu)

Ignorance was originally intended to replace Unity's LLAPI Transport that ships with
Mirror, however since it uses native libraries it was deemed too risky to ship with Mirror itself.

Enet has been proven to be superior when compared to Unity LLAPI. Please read the FAQ (towards the bottom of this wall of text) before using the
Ignorance Transport.

Compatiblity
------------

### Desktop

-   **Windows 7, 8.1, 10 x64**
    -   32bit (x86) Environments not supported. **Use 64bit instead.**
    
    -   ARM64 Environments not currently supported (ie. Windows 10 on Raspberry Pi 4)
        
-   **macOS 10.12 onwards**
    -   El Captain and below **ARE NOT** supported.
    
    -   Compiled on a GitHub autobuilder MacOS instance.
    
    -   Tested and confirmed working on High Sierra, Mojave, Catalina. Big Sur probably works too?

-   **Linux x64**

    -   GCC-compiled x86_64 libraries are included, compiled on a Ubuntu buildbot.

    -   Should *just work* on various Linux distros, as long as they aren’t too
        exotic. Main testing has been done on Debian/Ubuntu/Fedora/CentOS/Arch.

    -   If your distro uses a different C library instead of GNU C then you’ll
        need to recompile the blobs manually. See the ENet-CSharp repository for
        more information.

### Console

-   **Nintendo Switch**
    
    -   Manual compile of Enet native binaries required, [see this document from the ENet-CSharp
        repository.](https://github.com/SoftwareGuy/ENet-CSharp/blob/master/BUILD-FOR-SWITCH.txt)

-   **Xbox One**

    -   UWP: Tested and confirmed working for **LAN Client only**. LAN Host doesn't work.

    -   Native: Possibly, however I don’t have development kit or license to
        test.

-   **PlayStation**

    -   Vita: Dead platform.

    -   PS4: Likely would require custom version of Enet due to the console using a BSD-based OS.
    
### Mobile

-   **Android**

    -   Android 4.4 "KitKat" onwards.
    
    -   ARMv7, ARM64 and x86 blobs are included.

    -   x86_64 Blobs are not included since Unity 2018.4 LTS does not support
        that platform yet.

-   **Apple iOS**

    -   System version 8.0 minimum

    -   Supports iPhone 4S to the latest iPhone. Tested and working on iPhone
        4S, iPhone 5s, iPad 2 WiFi + 3G and iPad (5th Gen)

    -   Compiled as a FAT library.

Dependencies
------------

-   [ENet-CSharp](https://github.com/SoftwareGuy/ENet-CSharp)

-   [Mirror Networking](https://github.com/vis2k/Mirror)

Mirror Compatibility
--------------------

Please use the Mirror Asset Store releases. The master branch of Mirror can also
be used but please beware that it's bleeding edge.

Installation
------------

Download the Unity Package from Releases that is the latest one. Simply import
the Unity Package and Unity will do the rest. Follow the instructions below.

How to use
----------

As of Ignorance 1.3, I have included a pre-configured sample scene so you can
get started easily. Otherwise add the script called **Ignorance Threaded** to your NetworkManager object,
removing any TCP-based or other UDP-based transport. Then set the script to be used in NetworkManagers' "Transport" field.

FAQ (aka Please Read This First)
--------------------------------

### Why should I use Ignorance over Unity LLAPI?

Don't even talk about Unity LLAPI - it's obsolete and dead in the eyes of Unity Technologies.
Depending on what you threw over the network, you'd get random latency spikes and packets would 
go missing even in reliable delivery mode.

Ignorance aimed to be the LLAPI replacement that harnesses ENet's stable and high performance 
networking so you can forget about low-level networking stress and spend more time
focusing on development of the high-level portion of your networked game.

### Ignorance Classic is no more. Ignorance Threaded is here to take its place.

As of Ignorance 1.3.9.2, Ignorance Classic is no longer in this repo. It was causing Access Violations and
since it was on the chopping block for quite some time, I decided it was dead weight and gave it the axe.

In short, use Ignorance Threaded and enjoy high performance threaded networking.

### Important note

Since Mirror and all transports use LateUpdate to process their network code,
there might be a chance that at very low framerate (ie. you are really stressing
the server or creating lots of GameObjects in which Unity has to load from disk)
that the networking gets overwhelmed, regardless of how ENet is coping in the 
native world. 

It is essential to keep your server's frame rate running as high
as possible as this will reduce latency and in-game lag. You will be able to
tell when the server is under heavy stress when networked objects get very
choppy and/or the client starts hanging.

Mirror using LateUpdate for polling is a design flaw that the main developers have
little interest in fixing. Until they decide to change it, we are stuck with
having to deal with this design issue.

### I have a bug!

[Check the current open bug reports and/or report a new one
here](https://github.com/SoftwareGuy/Ignorance/issues). I strongly also recommend you
be present in the [Mirror Discord](https://discord.gg/N9QVxbM) so I can ask for further info and/or you can test
fixes for your bug reports. Trolls are not welcome.

Bugs that do not have enough details will be closed with \#wontfix. Details like
your OS, Unity Editor version, any Ignorance errors, etc is essential for a good
bug report.

I have questions, I want answers.
---------------------------------
[Here's a quick primer.](https://vis2k.github.io/Mirror/Transports/Ignorance)

It might be slightly out of date, but it covers the basics of Ignorance.

### I am not satisfied with Ignorance.

Please let me know why as I can't improve my code if I don't get feedback.
*However, if you're just here to troll me and my code, then please move on.*

See also:
[LiteNetLib4Mirror](https://github.com/MichalPetryka/LiteNetLib4Mirror).

Credits
-------

-   **Coffee Donators**: Thank you so much.

-   [vis2k](https://github.com/vis2k) and [Paul](https://github.com/paulpach):
    Mirror lead developers.

-   [Katori](https://github.com/katori): Helped kickstart the threaded version
    that evolved into Ignorance 2.

-   [BigBoxVR](https://github.com/GabeBigBoxVR): Pull requests and found a race
    condition that threatened stability.

-   [c6burns](https://github.com/c6burns): Words cannot describe how much help
    he's been. Bloody oath mate.

-   [Petris](https://github.com/MichalPetryka): Code refactoring and tidy up
    (you rock man!)

-   [BigBoxVR](https://github.com/GabeBigBoxVR) and
    [Martin](https://github.com/martindevans): Pull requests.

-   **The folks at the Mirror discord**: Memes, Courage, laughs, drama and
    all-round awesome folks to chat with.

-   To all the others who I have missed. Thanks a lot, you know who you are.

-   You too can have your name here if you send in a PR. Don't delay, get your
    PR in today! 

### Previous hall of fame:
-   [FSE_Vincenzo](https://github.com/Vincenz099): Providing insight into proper
    usage of ENET, rebuttal, improvements, etc.
    
-   [shiena](https://github.com/shiena): Pull requests for bug fixes and
    oversights.
    
-   [Draknith](https://github.com/FizzCube): Testing and mapping
    Reliable/Unreliable channels in Mirror to ENET Channels, testing.
    
-   [nxrighthere](https://github.com/nxrighthere): Helped debug some things in
    early versions of Ignorance, author of the upstream ENet-CSharp repository 
    my fork is based on
