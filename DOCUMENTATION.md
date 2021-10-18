<p align="center">
  <img src="http://oiran.studio/images/ignorance14.png" alt="Ignorance 1.4 Logo"/>
</p>

Ignorance Documentation
=============

Here's the current documentation presented in good 'ol markdown for easy viewing on GitHub. I ain't gonna use GitBook for a simple documentation, sorry to disappoint!

Unless noted, all configuration options presented here are based from Ignorance version 1.4.

Ignorance Transport
------------

The *Ignorance Transport* is a component that attaches to your Mirror NetworkManager. It then bridges code-wise between Mirror itself, Ignorance Core and finally ENet.
For those who are technically inclined, the whole chain is as follows:

```
Mirror Networking (High Level) <-> Ignorance Transport + Core (High to Low Level) <-> ENet-CSharp Managed-To-Native Wrapper <-> ENet (Native, x64/ARM Win/Mac/Linux/Android/iOS/etc)
```

Ignorance Transport Configuration
------------

From top to bottom...

![Image](../master/Documentation/ignorance14options.png?raw=true)

### Essentials

This option... | ...does this.
-------------- | -------------
Port | Specifies the listening port that Ignorance will use when Mirror starts Host or Server Only mode.

### Debugging & Logging

This option... | ...does this.
-------------- | -------------
Log Type | Specifies how verbose Ignorance will be when it's running. Default is **Standard**, which is balanced between being *almost* mute to super verbose.
Debug Display | If you would like a OnGUI debug display that shows some information about the low-level world, enable this. You'll see a debug window in the bottom left of your game window.


### Server Configuration

This option... | ...does this.
-------------- | -------------
Server Binds All | If enabled, ENet will ignore the *Server Bind Address* and bind to all IP addresses. If disabled, it will only bind to the specified bind address. This is useful for multiple instances on the same server. Default is enabled.
Server Bind Address | If *Server Binds All* is disabled, this is the IP address that ENet will attempt to bind to. IPv4 and IPv6 is both supported. If this is invalid, then ENet will likely catch fire.
Server Peer Max Capacity | Also known as maximum CCU. This is best kept to the same number as your Mirror maximum players count.
Server Max Native Time | This is how long the servers' native ENet thread will wait for network events. Higher numbers (3 or 5, for example) can save CPU power, while lower numbers keep ENet running as fast as possible. Default is **1** which is a sane default.

### Client Configuration

This option... | ...does this.
-------------- | -------------
Client Max Native Time | This is how long the client' native ENet thread will wait for network events. Higher numbers (3 or 5, for example) can save CPU power, while lower numbers keep ENet running as fast as possible. Default is **1** which is a sane default.
Client Status Update Interval | If set above 0, this interval will trigger Ignorance to request client information. Since this technically can be expensive if done aggressively as it asks native ENet for the stats, every 3 or 5 seconds is recommended minimum values.

### Channel Configuration

This option... | ...does this.
-------------- | -------------
Channels | Defines the channel array. This allows you to divide up your packets into channels with different packet delivery methods.


### Ring Buffer Tweaking

This option... | ...does this.
-------------- | -------------
Client Data Buffer Size | Defines how many slots in the ring buffer for data packets coming from the server. This may require tweaking if the server is sending a lot of packet data to your client or you have a very fast paced game.
Client Conn Event Buffer Size | Defines how many slots in the ring buffer for connection events. The default is sane enough. Probably overkill for a client, now that I think about it...
Server Data Buffer Size | Defines how many slots in the ring buffer for data packets coming from clients. This will require tweaking depending on how chatty your client network code is.
Server Conn Event Buffer Size | Defines how many slots in the ring buffer for connection events. The default should be sane enough. This is only useful if you have lots of clients connecting/disconnecting at once.

NOTE: A connection event is either a ENet Connect or Disconnect. In other words, when a ENet client connects to the server or disconnects, a event is raised and placed into the queue for processing by Mirror.

### Danger Zone

This option... | ...does this.
-------------- | -------------
Packet Buffer Capacity | Value in bytes. This is used as a temporary scratch pad for incoming packet data. The default is 4 Kilobytes (4096 bytes). If you get errors about packets being too big, it may pay to increase this, or try optimizing your netcode data.
Max Allowed Packet Size | Value in bytes. ENet supports sending a UDP Packet up to 32 Megabytes, but this comes at a cost including reduced performance and stressing out ENet. The default is set to the maximum ENet packet size value. Setting this over 33554432 bytes **will not** have any positive effect, and may even cause ENet to complain. 

It is advised to keep your data packets as small as possible. I do not recommend using Ignorance or ENet for real-time video streaming if it's some insane resolution.

*End of configuration options.*

Ignorance Core
------------

Ignorance Core was designed to be able to be used outside of a Unity environment, allowing you to harness the power of Ignorance in your own non-Mirror or non-Unity project. If you remove the Ignorance Transport components, you get Ignorance Standalone.

You'll need to patch the Core to remove `Debug.Log<whatever>` calls, but with a little work you'll be able to do your own IO through Ignorance Core. Use the Ignorance Transport component as a coding reference, how to enqueue packets and dequeue them.


Help improve the documentation!
------------

Confused? Think something can be explained better? Send a issue ticket and tell me what I can elaborate or improve here. 

Other than that, thanks again for using Ignorance!