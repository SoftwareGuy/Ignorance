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
    public class IgnoranceServer
    {
        // Server Properties
        // - Bind Settings
        public string BindAddress = "127.0.0.1";
        public int BindPort = 7777;
        // - Maximum allowed channels, peers, etc.
        public int MaximumChannels = 2;
        public int MaximumPeers = 100;
        // - Native poll waiting time
        public int PollTime = 1;

        public volatile bool CeaseOperation = false;
        public volatile bool Active = false;

        // Queues
        public ConcurrentQueue<IgnorancePacket> Incoming;
        public ConcurrentQueue<IgnorancePacket> Outgoing;

        public void Start()
        {
            CeaseOperation = false;
        }

        public void Stop()
        {
            CeaseOperation = true;
        }

        // TO BE CONTINUED...
        // <------
    }
}
