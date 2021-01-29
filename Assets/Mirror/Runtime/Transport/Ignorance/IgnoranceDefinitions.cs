using System;
using ENet;

namespace IgnoranceTransport
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
        public const string Version = "1.4.0a1";
        public const string Scheme = "enet";
        public const string BindAllIPv4 = "0.0.0.0";
        public const string BindAllFuckingAppleMacs = "::0";
    }

    public enum IgnorancePacketType
    {
        ServerConnect,      // Server had a new connection.
        ServerDisconnect,   // Server had an existing connection die.
        ServerData,         // Server has an existing connection send data to us.

        ServerClientKick,   // Mirror wants to boot a client off the server.

        ClientConnect,      // Client connected to server.           
        ClientDisconnect,   // Client disconnected from server.
        ClientData,         // Client received from server.

        ClientStatusUpdateRequest, // Main thread asking client thread to report various statistics.
        ClientStatusUpdateResponse, // Client thread reporting various statistics to main thread.

        ClientWantsToStop   // Client thread needs to pack up and go home.
    }

    public enum IgnoranceLogType
    {
        Nothing,
        Standard,
        Verbose
    }

    // Struct optimized for cache efficiency. (Thanks Vincenzo!)
    public struct IgnoranceIncomingPacket
    {
        public bool WasRented;
        public byte Channel;
        public uint NativePeerId;
        public int Length;
        public byte[] RentedArray;        
    }

    // Struct optimized for cache efficiency. (Thanks Vincenzo!)
    public struct IgnoranceOutgoingPacket
    {
        public bool WasRented;
        public byte Channel;
        public uint NativePeerId;
        public PacketFlags Flags;
        public int Length;        
        public byte[] RentedArray;
    }

    // Struct optimized for cache efficiency. (Thanks Vincenzo!)
    public struct IgnoranceConnectionEvent
    {
        public bool WasDisconnect;
        public ushort Port;
        public uint NativePeerId;
        public string IP;
    }

    public struct IgnoranceCommandPacket
    {
        public IgnoranceCommandType Type;
        public uint PeerId;
    }

    public struct IgnoranceClientStats
    {
        // Stats only - may not always be used!
        public uint RTT;
        public ulong BytesReceived;
        public ulong BytesSent;
        public ulong PacketsReceived;
        public ulong PacketsSent;
        public ulong PacketsLost;
    }

    public enum IgnoranceCommandType
    {
        // Client
        ClientWantsToStop,
        ClientRequestsStatusUpdate,
        // ENet internal
        ResponseToClientStatusRequest,
        // Server
        ServerKickPeer
    }

    // TODO: Optimize struct for Cache performance.
    public struct PeerConnectionData
    {
        public uint NativePeerId;
        public ushort Port;
        public string IP;
    }
}
