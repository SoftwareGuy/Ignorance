using System;
using ENet;

namespace Mirror
{
    // Snipped from the transport files, as this will help
    // me keep things up to date.
    [Serializable]
    public enum IgnoranceChannelTypes
    {
        Reliable = PacketFlags.Reliable,                                        // TCP Emulation.
        ReliableUnsequenced = PacketFlags.Reliable | PacketFlags.Unsequenced,   // TCP Emulation, but no sequencing.
        ReliableUnbundledInstant = PacketFlags.Reliable | PacketFlags.Instant,  // Experimental: Reliablity + Instant hybrid packet type.
        UnbundledInstant = PacketFlags.Instant,                                 // Instant packet, will not be bundled with others.
        Unreliable = PacketFlags.Unsequenced,                                   // Pure UDP.
        UnreliableFragmented = PacketFlags.UnreliableFragmented,                // Pure UDP, but fragmented.
        UnreliableSequenced = PacketFlags.None,                                 // Pure UDP, but sequenced.
        Unthrottled = PacketFlags.Unthrottled,                                  // Apparently ENet's version of Taco Bell.
    }

    [Serializable]
    public class PeerStatistics
    {
        public ulong CurrentPing;
        public ulong PacketsSent;
        public ulong PacketsLost;
        public ulong BytesSent;
        public ulong BytesReceived;
    }

    public class IgnoranceInternals
    {
        public const string Version = "1.3.10";
        public const string Scheme = "enet";
        public const string BindAllIPv4 = "0.0.0.0";
        public const string BindAllFuckingAppleMacs = "::0";
    }

    public struct IgnorancePacket
    {
        // TO BE CONTINUED...
        // <------
    }
}
