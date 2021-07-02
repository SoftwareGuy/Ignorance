<p align="center">
  <img src="http://oiran.studio/images/ignorance14.png" alt="Ignorance 1.4 Logo"/>
</p>

Ignorance FAQ
=============

You're reading the FAQ. [Click here](https://github.com/SoftwareGuy/Ignorance) to go back to the repository.

# Runtime-related

## What operating systems are supported?

Right now, Ignorance supports all major platforms on desktop, such as:

- Microsoft Windows
- Apple MacOS
- Linux (Ubuntu, Debian, CentOS, Arch, etc) (as long as they support GNU libc, if they use non-GNU libc then the included ENet blobs may fail)

For desktop only 64 Bit operating systems are supported at this moment in time. When 1.4.0 is fully released, 32 Bit operating systems will be supported, as well as ARM64.

For mobile devices, the following are supported:

- Android (ARMv7 Mono-only, ARMv7 and ARM64 supported for IL2CPP)
- iOS (ARMv7, ARM64)

Note that any devices such as VR/AR/XR headsets that run on Android or iOS will work fine, unless plugins need to be specifically adapted to the platform.

## Why doesn't Ignorance like a 32Bit Windows/Linux OS instance?

- Make sure you're building for **x86_64** build target.
- I don't feel like compiling ENet for 32Bit. If you want to do that yourself, feel free to do so. Instructions are in the [ENet-CSharp](https://github.com/SoftwareGuy/ENet-CSharp) repository.

# Connection-related

## Does Ignorance do NAT-PMP, UPnP or NAT Punching?

No. In this day and age of the internet, relying on NAT Punching is russian roulette. Many routers ship with UPnP/NAT-PMP disabled due to security concerns. That said, your ISP 
(read: the entity that provides you internet access) may have their own firewall that blocks incoming connections. **Punch through will fail on mobile networks, such as 3G/4G/5G**.

If you really need to break through restrictive firewalls, your best bet is to use some sort of Relay mechanism. As far as the firewall is concerned, it just sees it as a single connection. 
Mirror supports some relays that work well with Ignorance. For more information, check out the Mirror Discord.

## Ignorance doesn't connect to the server that's hosting the game.

- Check your firewall. Sometimes Windows Server instances will not report the firewall blocking the UDP connections, so you'll need to manually go through and open the UDP port in Windows Firewall.

- If you're using Google, Amazon or Azure, make sure their external firewall (not the servers' firewall) is not blocking the port.

- Linux users might have some trouble if they are using `iptables` or `ufw`. A simple search for `iptables open udp port` or `ufw allow udp port` should suffice.

- Mobile users can experience problems depending on their devices. Samsung Knox can be a bitch and refuse incoming connections for stupid reasons. iOS seems to be fine, as long as you allow it to connect to devices on your local network via the iOS 14 prompt.

- If this doesn't help, please open a ticket and I'll try to help you out.

# Mirror-related

## What is the minimum version of Mirror that Ignorance supports?

Mirror 35 is the minimum version that Ignorance 1.4.x uses, while it also supports newer Mirror versions.

## I heard Mirror supports batching, does Ignorance take advantage of this?

Yes, Ignorance supports batching and works quite well. Make sure it's enabled on the Network Manager GameObject, and a version of Mirror that supports batching.

## Are you still using LateUpdate for network polling?

No. Ignorance uses it's own network polling method and an awesome little trick to make it literally the fastest reliable UDP transport for Mirror.

## I heard Mirror got a better network polling method. How do I use that over the trick you use?

Take a look at `Ignorance.cs` and you'll see a define that you need to enable in order to use the new Mirror update mechanism.

# Other

## Why should I use Ignorance over Unity LLAPI?

Unity LLAPI is obsolete and dead. Depending on what you threw over the network, you'd get random latency spikes and packets would go missing even in reliable delivery mode. Yes, it was **that bad**.

That alone says something, no?

## What happened to Ignorance Classic?

It died back in the 1.3.x days. The classic version was becoming obsolete and not worth the time keeping it up to date.


*To be continued...*