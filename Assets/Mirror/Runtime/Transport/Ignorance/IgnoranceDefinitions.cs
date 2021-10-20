// Ignorance 1.4.x
// https://github.com/SoftwareGuy/Ignorance
// -----------------
// Copyright (c) 2019 - 2021 Matt Coburn (SoftwareGuy/Coburn64)
// Ignorance Transport is licensed under the MIT license. Refer
// to the LICENSE file for more information.
using System;
using System.Collections.Generic;
using ENet;

namespace IgnoranceCore
{
    // Snipped from the transport files, as this will help
    // me keep things up to date.
    [Serializable]
    public enum IgnoranceChannelTypes
    {
        Reliable = PacketFlags.Reliable,                                        // TCP Emulation.
        ReliableUnsequenced = PacketFlags.Reliable | PacketFlags.Unsequenced,   // TCP Emulation, but no sequencing.
        Unreliable = PacketFlags.Unsequenced,                                   // Pure UDP.
        UnreliableFragmented = PacketFlags.UnreliableFragmented,                // Pure UDP, but fragmented.
        UnreliableSequenced = PacketFlags.None,                                 // Pure UDP, but sequenced.
        Unthrottled = PacketFlags.Unthrottled,                                  // Apparently ENet's version of Taco Bell.
    }

    public class IgnoranceInternals
    {
        public const string Version = "1.4.0b13";
        public const string Scheme = "enet";
        public const string BindAnyAddress = "::0";
    }

    public enum IgnoranceLogType
    {
        Nothing,
        Standard,
        Verbose
    }

    public struct IgnoranceIncomingPacket
    {
        public byte Channel;
        public uint NativePeerId;
        public Packet Payload;
    }

    public struct IgnoranceOutgoingPacket
    {
        public byte Channel;
        public uint NativePeerId;
        public Packet Payload;
    }

    public struct IgnoranceConnectionEvent
    {
        public byte EventType;
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
        ClientStatusRequest,
        // Server
        ServerKickPeer,
        ServerStatusRequest
    }

    public struct IgnoranceServerStats
    {
        // Stats only - may not always be used!
        public ulong BytesReceived;
        public ulong BytesSent;
        public ulong PacketsReceived;
        public ulong PacketsSent;
        public ulong PeersCount;

        public Dictionary<int, IgnoranceClientStats> PeerStats;
    }

    // TODO: Optimize struct for Cache performance.
    public struct PeerConnectionData
    {        
        public ushort Port;
        public uint NativePeerId;
        // public bool IsOccupied;
        public string IP;
    }
}
