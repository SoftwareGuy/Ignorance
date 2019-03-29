// ----------------------------------------
// Ignorance Transport by Matt Coburn, 2018 - 2019
// This Transport uses other dependencies that you can
// find references to in the README.md of this package.
// ----------------------------------------
// This file is part of the Ignorance Transport.
// ----------------------------------------

// using ENet;
using System.Collections.Generic;
using Ignorance.Enet;

namespace Ignorance
{
    public static class IgnoranceConstants
    {
        public readonly static string TransportVersion = "2.1 EXPERIMENTAL";
        public readonly static int ClientIncomingRingBufferSize = 524288;
        public readonly static int ClientOutgoingRingBufferSize = 1024;
        public readonly static int ServerOutgoingRingBufferSize = 524288;   // 512 x 1024
        public readonly static int ServerIncomingRingBufferSize = 524288;   // 512 x 1024
        public readonly static int ServerCommandRingBufferSize = 50;        // Should be enough, this is used to signal things to the ENET Thread
        public readonly static int ServerPollingTimeout = 15;               // ticks, or ms. Higher = more CPU burn. Lower = maybe lesser performance, but lower CPU usage.
        public readonly static int ClientPollingTimeout = 1;                // Leave at 1 for maximum performance. Do not set to 0 as this might cause unpredicatible behaviour.

        public readonly static int MaximumPacketSize = 33554432;
    }

    /*
    // Peer Info Struct
    public struct PeerInfo
    {
        public int connectionId;
        public string PeerIp;
        public ushort PeerPort;
    }

    // Incoming Event Class
    public struct QueuedIncomingEvent 
    {
        public int connectionId;
        public Packet EnetPacket;
    }

    // Incoming Connection Event Class
    public struct QueuedIncomingConnectionEvent 
    {
        public int connectionId;
        public EventType eventType;
        public string peerIp;
        public ushort peerPort;
    }
    
    // Outoging Packet Class
    public struct QueuedOutgoingPacket
    {
        public int targetConnectionId;
        public byte channelId;
        public Packet contents;
    }

    // ENET Commands
    public struct QueuedCommand
    {
        public ushort Type;
        public int ConnId;
    }
    */

    [System.Serializable]
    public enum KnownChannelTypes
    {
        ReliableSequenced = Native.PacketFlags.Reliable, // reliable Sequenced (in order), will auto fragment & reassemble if above MTU (1400 default but scales per peer)
        ReliableUnsequenced = (Native.PacketFlags.Reliable | Native.PacketFlags.Unordered), // Will be reliable but can be out-of-order arriving.
        Unreliable = Native.PacketFlags.Unordered, // Unreliable and unsequenced, If package is above MTU it will be automatically converted to Reliable Fragmented packet.
        // UnreliableFragmented = Native.PacketFlags.UnreliableFragment, // Unreliable and will fragment if above MTU but in an unreliable way.
        UnreliableSequenced = Native.PacketFlags.None, // Same behaviour as normal Unreliable but with sequencing (so above MTU becomes Reliable fragmented)
    }

    public enum ThreadState
    {
        Starting,
        Started,
        Busy,
        Stopping,
        Stopped
    }
}
