// ----------------------------------------
// Ignorance Transport by Matt Coburn, 2018 - 2019
// This Transport uses other dependencies that you can
// find references to in the README.md of this package.
// ----------------------------------------
// This file is part of the Ignorance Transport.
// ----------------------------------------
using ENet;
using Mirror.Ignorance.Thirdparty;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
// using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;
using Event = ENet.Event;
using EventType = ENet.EventType;


namespace Mirror.Ignorance
{
    public static class ServerShowerhead
    {
        public static string Address = "127.0.0.1";     // ipv4 or ipv6
        public static ushort Port = 65534;        // valid between ports 0 - 65535
        public static int MaximumConnectionsAllowed = 4095;

        public static int NumChannels = 1;
        public static bool DebugMode = false;

        public static volatile bool CeaseOperation = false;

        public static Thread Nozzle;

        private static volatile ThreadState CurrentState = ThreadState.Stopped;

        private static Peer[] peers = new Peer[4096];

        //private static Dictionary<int, Peer> ConnectionIdToPeerMappings;
        //private static Dictionary<uint, int> PeerIDsToConnectionIdMappings;

        //private static Dictionary<int, uint> ConnectionIdToPeerIDMappings;
        //private static ConcurrentDictionary<uint, Peer> PeerIDsToPeerMappings;

        private static Host HostObject = new Host();    // ENET Host Object
      //  internal static int nextAvailableSlot = 1;

        // We create new ringbuffers, but these will be overwritten when the Start() function is called.
        // This prevents nulls, thus saving null checks being heavy on performance.
        // public static RingBuffer<QueuedIncomingEvent> Incoming = new RingBuffer<QueuedIncomingEvent>(1024);   // Client -> ENET World -> Mirror
        public static ConcurrentQueue<QueuedIncomingEvent> Incoming = new ConcurrentQueue<QueuedIncomingEvent>();   // Client -> ENET World -> Mirror
        // public static RingBuffer<QueuedOutgoingPacket> Outgoing = new RingBuffer<QueuedOutgoingPacket>(1024);  // Mirror -> ENET World -> Client
        public static ConcurrentQueue<QueuedOutgoingPacket> Outgoing = new ConcurrentQueue<QueuedOutgoingPacket>();  // Mirror -> ENET World -> Client
        // private static RingBuffer<QueuedCommand> CommandQueue = new RingBuffer<QueuedCommand>(50);    // ENET Command Queue.
        private static ConcurrentQueue<QueuedCommand> CommandQueue = new ConcurrentQueue<QueuedCommand>();    // ENET Command Queue.
        // public static RingBuffer<QueuedIncomingConnectionEvent> IncommingConnEvents = new RingBuffer<QueuedIncomingConnectionEvent>(4096); // ENET World -> Mirror conn events.
        public static ConcurrentQueue<QueuedIncomingConnectionEvent> IncommingConnEvents = new ConcurrentQueue<QueuedIncomingConnectionEvent>(); // ENET World -> Mirror conn events.

        private static CustomSampler pollSampler;
        private static CustomSampler outgoingSampler;
        private static CustomSampler eventSampler;

        public static bool IsServerActive()
        {
            return CurrentState == ThreadState.Started;
        }

        public static void Start(ushort port)
        {
            Debug.Log("Ignorance Server Showerhead: Start()");
            CurrentState = ThreadState.Starting;

            Port = port;
            CeaseOperation = false;

            // Refresh dictonaries
            //ConnectionIdToPeerMappings = new Dictionary<int, Peer>(); // Mirror CIDs. -> ENET Peers.
            //PeerIDsToConnectionIdMappings = new Dictionary<uint, int>();  // Reverse lookup, ENET Peer IDs -> Mirror CIDs.
            // PeerIDsToPeerMappings = new ConcurrentDictionary<uint, Peer>(); // PeerID lookup.

            // Setup queues.
            // Incoming = new RingBuffer<QueuedIncomingEvent>(IgnoranceConstants.ServerIncomingRingBufferSize);
            Incoming = new ConcurrentQueue<QueuedIncomingEvent>();
            // Outgoing = new RingBuffer<QueuedOutgoingPacket>(IgnoranceConstants.ServerOutgoingRingBufferSize);
            Outgoing = new ConcurrentQueue<QueuedOutgoingPacket>();
            //CommandQueue = new RingBuffer<QueuedCommand>(IgnoranceConstants.ServerCommandRingBufferSize);
            CommandQueue = new ConcurrentQueue<QueuedCommand>();

            pollSampler = CustomSampler.Create("Incoming");
            outgoingSampler = CustomSampler.Create("Outgoing");
            eventSampler = CustomSampler.Create("Events");

            // reset array
            for (int peerIndex = 0; peerIndex < peers.Length; peerIndex++)
            {
                peers[peerIndex] = default;
            }

            Nozzle = new Thread(WorkerLoop)
            {
                Name = "Ignorance Transport Server Worker"
            };

            Nozzle.Start();
        }

        public static void Stop()
        {
            Debug.Log("Ignorance Server Showerhead: Stop()");
            Debug.Log("Instructing the showerhead worker to stop, this may take a few moments...");
            CeaseOperation = true;
        }

        public static void WorkerLoop(object args)
        {
            Profiler.BeginThreadProfiling("Server Threads", "Worker Thread (Server)");

            Debug.Log("Server worker has arrived!");
            CurrentState = ThreadState.Started;

            using (HostObject)
            {
                // Create a new address.
                Address address = new Address();
                address.Port = Port;

                // Create the host object with the specifed maximum amount of ENET connections allowed.
                HostObject.Create(address, MaximumConnectionsAllowed, NumChannels, 0, 0);

                // Hold the network event that's being emitted.
                Event netEvent;
                
                try
                {
                    while (!CeaseOperation)
                    {
                        pollSampler.Begin();
                        // Process any commands first.
                        QueuedCommand qCmd;
                        while (CommandQueue.TryDequeue(out qCmd))
                        {
                            switch (qCmd.Type)
                            {
                                // Disconnect a peer.
                                case 0:
                                    // Boot to the face.
                                    if(peers[qCmd.ConnId - 1].IsSet)
                                      peers[qCmd.ConnId -1].DisconnectLater(0);
                                    break;

                                case 1:

                                    break;
                            }
                        }
                        pollSampler.End();
                        outgoingSampler.Begin();
                        // Send any pending packets out first.
                        QueuedOutgoingPacket opkt;
                        while (Outgoing.TryDequeue(out opkt))
                        {
                            peers[opkt.targetConnectionId - 1].Send(opkt.channelId, ref opkt.contents);
                        }
                        outgoingSampler.End();

                     //   HostObject.Flush();

                        // Now, we receive what's going on in the network chatter.
                        eventSampler.Begin();

                        bool polled = false;
                        while (!polled)
                        {
                            if (HostObject.CheckEvents(out netEvent) <= 0)
                            {
                                if (HostObject.Service(IgnoranceConstants.ServerPollingTimeout, out netEvent) <= 0)
                                {
                                    break;
                                }

                                polled = true;
                            }

                            switch (netEvent.Type)
                            {
                                case EventType.None:
                                    // Do I need to say more?
                                    break;
                                case EventType.Connect:
                                    var peer = netEvent.Peer;
                                    var peerIdcon = peer.ID;

                                    peers[peerIdcon] = peer;

                                    var connEvent = new QueuedIncomingConnectionEvent
                                    {
                                        connectionId = (int)peerIdcon + 1,
                                        eventType = EventType.Connect,
                                        peerIp = peer.IP,
                                        peerPort = peer.Port
                                    };
                                    
                                    IncommingConnEvents.Enqueue(connEvent);
                                    break;
                                case EventType.Timeout:
                                case EventType.Disconnect:
                                    var peerId = netEvent.Peer.ID;

                                    peers[peerId] = default;

                                    var disconnEvent = new QueuedIncomingConnectionEvent
                                    {
                                        connectionId = (int)peerId + 1,
                                        eventType = netEvent.Type
                                    };
                                    IncommingConnEvents.Enqueue(disconnEvent);
                                    break;

                                case EventType.Receive:
                                    var evt = new QueuedIncomingEvent
                                    {
                                        connectionId = (int)netEvent.Peer.ID + 1,
                                        EnetPacket = netEvent.Packet
                                    };

                                    Incoming.Enqueue(evt);
                                    break;
                            }
                        }
                        eventSampler.End();
                    }

                    Debug.Log("Server worker finished. Going home.");

                    // disconnect everyone
                    for (int peerIndex = 0; peerIndex < peers.Length; peerIndex++) 
                    {
                        if(peers[peerIndex].IsSet)
                            peers[peerIndex].DisconnectNow(0);
                    }

                    HostObject.Flush();

                    HostObject.Dispose();

                    CurrentState = ThreadState.Stopping;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Worker thread exception occurred: {ex.ToString()}");
                }
                finally
                {
                    Debug.Log("Turned off the Nozzle. Good work out there.");
                    if (HostObject.IsSet) HostObject.Dispose();
                    CurrentState = ThreadState.Stopped;
                }
            }

            Profiler.EndThreadProfiling();
        }

        public static void Shutdown()
        {
            CeaseOperation = true;
        }

        public static bool DisconnectThatConnection(int connectionId)
        {
            var qc = new QueuedCommand 
            {
                Type = 0,
                ConnId = connectionId
            };
            CommandQueue.Enqueue(qc);
            return true;
        }
    }
}
