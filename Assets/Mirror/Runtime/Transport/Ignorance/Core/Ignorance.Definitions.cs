// ----------------------------------------
// Ignorance Transport by Matt Coburn, 2018 - 2019
// This Transport uses other dependencies that you can
// find references to in the README.md of this package.
// ----------------------------------------
// This file is part of the Ignorance Transport.
// ----------------------------------------
#define IGNORANCE_PRESENT

using ENet;

namespace Mirror.Ignorance
{
    // Incoming Event Class
    public struct QueuedIncomingEvent
    {
        public uint peerId;
        public EventType eventType;
        public byte[] databuff;
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
        public byte channelId;
        public Packet contents;
    }

    // ENET Commands
    public struct QueuedCommand
    {
        public ushort Type;
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
}
