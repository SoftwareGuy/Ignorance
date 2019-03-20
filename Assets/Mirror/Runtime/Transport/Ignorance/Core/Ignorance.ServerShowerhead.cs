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
using System.Threading;
using UnityEngine;
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
        public static volatile bool CeaseOperation = false;
        public static bool DebugMode = false;

        public static Thread Nozzle = new Thread(WorkerLoop)
        {
            Name = "Ignorance Transport Server Worker"
        };

        internal static Dictionary<int, uint> knownConnIDToPeers;
        internal static Dictionary<uint, int> knownPeersToConnIDs;

        internal static int nextAvailableSlot = 1;

        // We create new ringbuffers, but these will be overwritten when the Start() function is called.
        // This prevents nulls, thus saving null checks being heavy on performance.
        public static RingBuffer<QueuedIncomingEvent> Incoming = new RingBuffer<QueuedIncomingEvent>(1024);   // Client -> ENET World -> Mirror
        public static RingBuffer<QueuedOutgoingPacket> Outgoing = new RingBuffer<QueuedOutgoingPacket>(1024);  // Mirror -> ENET World -> Client
        private static RingBuffer<QueuedCommand> CommandQueue = new RingBuffer<QueuedCommand>(50);    // ENET Command Queue.

        private static ConcurrentDictionary<uint, Peer> knownPeers;

        private static Host HostObject = new Host();    // ENET Host Object

        public static bool IsServerActive()
        {
            return Nozzle.IsAlive;
        }

        public static void Start(ushort port)
        {
            Debug.Log("Ignorance Server Showerhead: Start()");

            Port = port;
            CeaseOperation = false;

            // Refresh dictonaries
            knownConnIDToPeers = new Dictionary<int, uint>();
            knownPeersToConnIDs = new Dictionary<uint, int>();
            knownPeers = new ConcurrentDictionary<uint, Peer>();

            // Setup queues.
            Incoming = new RingBuffer<QueuedIncomingEvent>(IgnoranceConstants.ServerIncomingRingBufferSize);
            Outgoing = new RingBuffer<QueuedOutgoingPacket>(IgnoranceConstants.ServerOutgoingRingBufferSize);
            CommandQueue = new RingBuffer<QueuedCommand>(IgnoranceConstants.ServerCommandRingBufferSize);

            // Configure and start thread.
            /* Nozzle = new Thread(WorkerLoop)
            {
                Name = "Ignorance Transport Server Worker"
            };
            */

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
            Debug.Log("Server Worker has arrived!");

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
                        bool polled = false;

                        // Process any commands first.
                        QueuedCommand qCmd;
                        while (CommandQueue.TryDequeue(out qCmd))
                        {
                            switch (qCmd.Type)
                            {
                                case '0':
                                    // Boot to the face.
                                    Peer p;
                                    if (knownPeers.TryGetValue(qCmd.PeerId, out p))
                                    {
                                        p.DisconnectLater(0);
                                    }
                                    break;
                            }
                        }

                        // Send any pending packets out first.
                        QueuedOutgoingPacket opkt;
                        while (Outgoing.TryDequeue(out opkt))
                        {
                            if (knownPeers.TryGetValue(opkt.targetPeerId, out Peer peer))
                            {
                                peer.Send(opkt.channelId, ref opkt.contents);
                            }
                        }

                        // Now, we receive what's going on in the network chatter.
                        while (!polled)
                        {
                            if (HostObject.CheckEvents(out netEvent) <= 0)
                            {
                                if (HostObject.Service(15, out netEvent) <= 0)
                                {
                                    break;
                                }

                                polled = true;
                            }

                            Peer peer = netEvent.Peer;
                            QueuedIncomingEvent evt = default;

                            switch (netEvent.Type)
                            {
                                case EventType.None:
                                    // Do I need to say more?
                                    break;

                                case EventType.Connect:
                                    knownPeers.TryAdd(peer.ID, peer);

                                    evt.eventType = EventType.Connect;
                                    evt.peerId = peer.ID;
                                    Incoming.Enqueue(evt);

                                    break;


                                case EventType.Disconnect:
                                case EventType.Timeout:
                                    knownPeers.TryRemove(peer.ID, out Peer peerDisconnected);

                                    evt.eventType = EventType.Disconnect;
                                    evt.peerId = peer.ID;
                                    Incoming.Enqueue(evt);

                                    break;

                                case EventType.Receive:
                                    evt.eventType = EventType.Receive;
                                    evt.peerId = peer.ID;

                                    Packet pkt = netEvent.Packet;
                                    evt.databuff = new byte[pkt.Length];
                                    pkt.CopyTo(evt.databuff);
                                    // don't dispose the original packet? blame FSE_Vincenzo for memory leaks
                                    // netEvent.Packet.Dispose();
                                    pkt.Dispose();

                                    // Enslave a new packet to the queue.
                                    Incoming.Enqueue(evt);

                                    break;
                            }
                        }
                    }

                    HostObject.Flush();
                    Debug.Log("Server worker finished. Going home.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Worker thread exception occurred: {ex.ToString()}");
                }
                finally
                {
                    Debug.Log("Turned off the Nozzle. Good work out there.");
                }
            }
        }

        internal static void Shutdown()
        {
            CeaseOperation = true;
        }

        internal static string GetClientAddress(int connectionId)
        {
            if (knownConnIDToPeers.TryGetValue(connectionId, out uint peerId))
            {
                if (knownPeers.TryGetValue(peerId, out Peer peer))
                {
                    return peer.IP;
                }
            }

            return "(invalid)";
        }

        internal static bool DisconnectThatConnection(int connectionId)
        {
            if (knownConnIDToPeers.TryGetValue(connectionId, out uint peerId))
            {
                QueuedCommand qc = default;
                qc.Type = 0;
                qc.PeerId = peerId;

                CommandQueue.Enqueue(qc);
                return true;
            }

            return false;
        }

        public static bool IsConnectionIdKnown(int connectionId)
        {
            if (knownConnIDToPeers.ContainsKey(connectionId))
            {
                return true;
            }

            return false;
        }

        internal static uint ResolveConnectionIDToPeer(int connectionId)
        {
            return knownConnIDToPeers[connectionId];
        }

        internal static void PeerDisconnectedInternal(uint peer)
        {
            // Clean up dictionaries.
            knownConnIDToPeers.Remove(knownPeersToConnIDs[peer]);
            knownPeersToConnIDs.Remove(peer);
        }
    }
}
