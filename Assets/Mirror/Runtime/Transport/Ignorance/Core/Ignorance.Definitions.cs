// ----------------------------------------
// Ignorance Transport by Matt Coburn, 2018 - 2019
// This Transport uses other dependencies that you can
// find references to in the README.md of this package.
// ----------------------------------------
// This file is part of the Ignorance Transport.
// ----------------------------------------
#define IGNORANCE_PRESENT

using ENet;
using System.Collections.Generic;

namespace Mirror.Ignorance
{
    public class IgnoranceConstants
    {
        public readonly static string TransportVersion = "2.0 EXPERIMENTAL";
        public readonly static int ClientIncomingRingBufferSize = 524288;
        public readonly static int ClientOutgoingRingBufferSize = 1024;
        public readonly static int ServerOutgoingRingBufferSize = 524288;   // 512 x 1024
        public readonly static int ServerIncomingRingBufferSize = 524288;   // 512 x 1024
        public readonly static int ServerCommandRingBufferSize = 50;        // Should be enough, this is used to signal things to the ENET Thread
    }

    // Peer Info Struct
    public struct PeerInfo
    {
        public uint PeerUniqueId;
        public string PeerIp;
        public ushort PeerPort;
    }

    // Incoming Event Class
    public struct QueuedIncomingEvent
    {
        public uint peerId;
        public string peerIp;
        public ushort peerPort;

        public EventType eventType;
        public byte[] databuff;
        public int connectionId;
    }

    // Incoming Packet Class
    public struct QueuedIncomingPacket
    {
        public int connectionId;
        public Packet contents;
    }

    // Outoging Packet Class
    public struct QueuedOutgoingPacket
    {
        public uint targetPeerId;
        public int targetConnectionId;
        public byte channelId;
        public Packet contents;
    }

    // ENET Commands
    public struct QueuedCommand
    {
        public ushort Type;
        public int ConnId;
        public uint PeerId;
    }

    [System.Serializable]
    public enum KnownChannelTypes
    {
        Reliable,
        Unreliable,
        UnreliableFragmented,
        UnreliableSequenced,
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
