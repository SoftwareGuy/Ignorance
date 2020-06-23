/*
 * This file is part of the Ignorance 1.4.x Mirror Network Transport system.
 * Copyright (c) 2019 Matt Coburn (SoftwareGuy/Coburn64)
 * 
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */
using UnityEngine;
using ENet;
using System;
using System.Collections.Concurrent;

namespace OiranStudio.Ignorance4
{
    /// <summary>
    /// Defines a Ignorance Data Packet. We later use this in many other classes.
    /// </summary>
    public class Definitions
    {
        public struct IgnoranceDataPacket
        {
            public PacketEventType Type;    // The type of the data
            public bool IsOutgoing;         // Is this outgoing? If not, it's incoming.
            public byte ChannelId;          // The channel id that we're sending on.
            public bool DestinedPeerId;     // Server: What peer are we going to send it to?
            public ArraySegment<byte> Payload;          // The payload contained in this packet.
            public PacketFlags Flags;       // The packet flags.
        }

        public enum PacketEventType
        {
            Connect,
            Disconnect,
            Data
        }

        public enum IgnoranceChannelType
        {
            Reliable = PacketFlags.Reliable,                                        // TCP Emulation.
            ReliableUnsequenced = PacketFlags.Reliable | PacketFlags.Unsequenced,   // TCP Emulation, but no sequencing.
            ReliableUnbundledInstant = PacketFlags.Reliable | PacketFlags.Instant,  // Experimental: Reliablity + Instant hybrid packet type.
            Unreliable = PacketFlags.Unsequenced,                                   // Pure UDP + ENet's Protocol.
            UnreliableFragmented = PacketFlags.UnreliableFragmented,                // Pure UDP, but fragmented.
            UnreliableSequenced = PacketFlags.None,                                 // Pure UDP, but sequenced.
            UnbundledInstant = PacketFlags.Instant,                                 // Instant packet, will not be bundled with others.
            Crucial = PacketFlags.Crucial,											// ???
        }
    }

}
