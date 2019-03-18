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
        public static volatile Host HostObject = new Host();    // ENET Host Object
        // public static volatile string Address = "127.0.0.1";     // ipv4 or ipv6
        public static volatile ushort Port = 65534;        // valid between ports 0 - 65535
        public static volatile int SendPacketQueueSize = 4096;
        public static volatile int ReceivePacketQueueSize = 4096;
        public static volatile int MaximumConnectionsAllowed = 4095;

        public static volatile bool CeaseOperation = false;

        public static Thread Nozzle;

        internal static Dictionary<int, uint> knownConnIDToPeers;
        internal static Dictionary<uint, int> knownPeersToConnIDs;
        private static ConcurrentDictionary<uint, Peer> knownPeers;
        internal static int nextAvailableSlot = 1;

        public static RingBuffer<QueuedIncomingEvent> Incoming;   // Client -> ENET World -> Mirror
        public static RingBuffer<QueuedOutgoingPacket> Outgoing;  // Mirror -> ENET World -> Client

        public static bool IsServerActive()
        {
            if (Nozzle != null)
            {
                return Nozzle.IsAlive;
            }
            else
            {
                return false;
            }
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
            Incoming = new RingBuffer<QueuedIncomingEvent>(SendPacketQueueSize);
            Outgoing = new RingBuffer<QueuedOutgoingPacket>(ReceivePacketQueueSize);

            // Configure and start thread.
            Nozzle = new Thread(WorkerLoop)
            {
                Name = "Showerhead (Server)"
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
            Debug.Log("Server Worker has arrived!");

            using (HostObject)
            {
                // Create a new address.
                Address address = new Address();
                address.Port = Port;

                // Create the host object with the specifed maximum amount of ENET connections allowed.
                HostObject.Create(address, MaximumConnectionsAllowed);

                // Hold the network event that's being emitted.
                Event netEvent;

                try
                {
                    while (!CeaseOperation)
                    {
                        bool polled = false;

                        // Send any pending packets out first.
                        while (Outgoing.Count > 0)
                        {
                            QueuedOutgoingPacket pkt;
                            if (Outgoing.TryDequeue(out pkt))
                            {
                                if (knownPeers.TryGetValue(pkt.targetPeerId, out Peer peer))
                                {
                                    peer.Send(pkt.channelId, ref pkt.contents);
                                }
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
                                    break;

                                case EventType.Connect:
                                    Debug.Log($"Worker Thread: Server has a new client! Peer ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}, Mirror CID: {nextAvailableSlot}");

                                    knownPeers.TryAdd(peer.ID, peer);

                                    evt.eventType = EventType.Connect;
                                    evt.peerId = peer.ID;
                                    Incoming.Enqueue(evt);

                                    break;


                                case EventType.Disconnect:
                                case EventType.Timeout:
                                    Debug.Log($"Worker Thread: Server had a client disconnect/time out. Peer ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}");

                                    knownPeers.TryRemove(peer.ID, out Peer peerDisconnected);

                                    evt.eventType = EventType.Disconnect;
                                    evt.peerId = peer.ID;
                                    Incoming.Enqueue(evt);

                                    break;

                                /*
                            case EventType.Timeout:
                                Debug.Log($"Worker Thread: Server had a client timeout. ID: Peer ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}");

                                knownPeers.TryRemove(peer.ID, out Peer peerTimedOut);

                                evt.eventType = EventType.Disconnect;
                                evt.peerId = peer.ID;
                                Incoming.Enqueue(evt);

                                break;
                                */

                                case EventType.Receive:
                                    evt.eventType = EventType.Receive;
                                    evt.peerId = peer.ID;

                                    Packet pkt = netEvent.Packet;
                                    evt.databuff = new byte[pkt.Length];
                                    pkt.CopyTo(evt.databuff);
                                    netEvent.Packet.Dispose();

                                    // Enslave a new packet to the queue.
                                    Incoming.Enqueue(evt);

                                    break;
                            }
                        }
                    }

                    HostObject.Flush();
                    Debug.Log("Server Worker finished. Going home.");
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
            // ???
            if (Nozzle != null && Nozzle.IsAlive)
            {
                Nozzle.Abort();
            }
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
                if (knownPeers.TryGetValue(peerId, out Peer peer))
                {
                    peer.DisconnectNow(0);
                    return true;
                }
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