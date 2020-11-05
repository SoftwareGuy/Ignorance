// Ignorance 1.3.x
// Ignorance. It really kicks the Unity LLAPIs ass.
// https://github.com/SoftwareGuy/Ignorance
// -----------------
// Copyright (c) 2019 - 2020 Matt Coburn (SoftwareGuy/Coburn64)
// Ignorance Transport is licensed under the MIT license. Refer
// to the LICENSE file for more information.

using System.Collections.Concurrent;

namespace Mirror
{
    public class IgnoranceClient
    {
        // Client connection address and port
        public string ConnectAddress = "127.0.0.1";
        public int ConnectPort = 7777;

        // How many channels are expected
        public int ExpectedChannels = 2;
        // Native poll waiting time
        public int PollTime = 1;

        public volatile bool CeaseOperation = false;

        // Queues
        public ConcurrentQueue<IgnorancePacket> Incoming;
        public ConcurrentQueue<IgnorancePacket> Outgoing;

        // TO BE CONTINUED...
        // <------
    }
}
