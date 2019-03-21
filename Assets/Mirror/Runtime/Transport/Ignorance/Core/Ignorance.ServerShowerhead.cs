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
// using System.Linq;
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
        public static bool DebugMode = false;

        public static volatile bool CeaseOperation = false;

        public static Thread Nozzle;

        private static volatile ThreadState CurrentState = ThreadState.Stopped;

        private static ConcurrentDictionary<int, Peer> ConnectionIdToPeerMappings;
        private static ConcurrentDictionary<uint, int> PeerIDsToConnectionIdMappings;

        //private static Dictionary<int, uint> ConnectionIdToPeerIDMappings;
        //private static ConcurrentDictionary<uint, Peer> PeerIDsToPeerMappings;

        private static Host HostObject = new Host();    // ENET Host Object
        internal static int nextAvailableSlot = 1;
        
        // We create new ringbuffers, but these will be overwritten when the Start() function is called.
        // This prevents nulls, thus saving null checks being heavy on performance.
        public static RingBuffer<QueuedIncomingEvent> Incoming = new RingBuffer<QueuedIncomingEvent>(1024);   // Client -> ENET World -> Mirror
        public static RingBuffer<QueuedOutgoingPacket> Outgoing = new RingBuffer<QueuedOutgoingPacket>(1024);  // Mirror -> ENET World -> Client
        private static RingBuffer<QueuedCommand> CommandQueue = new RingBuffer<QueuedCommand>(50);    // ENET Command Queue.

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
            ConnectionIdToPeerMappings = new ConcurrentDictionary<int, Peer>(); // Mirror CIDs. -> ENET Peers.
            PeerIDsToConnectionIdMappings = new ConcurrentDictionary<uint, int>();  // Reverse lookup, ENET Peer IDs -> Mirror CIDs.
            // PeerIDsToPeerMappings = new ConcurrentDictionary<uint, Peer>(); // PeerID lookup.

            // Setup queues.
            Incoming = new RingBuffer<QueuedIncomingEvent>(IgnoranceConstants.ServerIncomingRingBufferSize);
            Outgoing = new RingBuffer<QueuedOutgoingPacket>(IgnoranceConstants.ServerOutgoingRingBufferSize);
            CommandQueue = new RingBuffer<QueuedCommand>(IgnoranceConstants.ServerCommandRingBufferSize);

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
                        bool polled = false;

                        // Process any commands first.
                        QueuedCommand qCmd;
                        while (CommandQueue.TryDequeue(out qCmd))
                        {
                            switch (qCmd.Type)
                            {
                                // Disconnect a peer.
                                case '0':
                                    // Boot to the face.
                                    if (ConnectionIdToPeerMappings.TryGetValue(qCmd.ConnId, out Peer victim))
                                    {
                                        victim.DisconnectLater(0);
                                    }
                                    break;
                            }
                        }

                        // Send any pending packets out first.
                        QueuedOutgoingPacket opkt;
                        while (Outgoing.TryDequeue(out opkt))
                        {
                            // Try mapping the peer id to the peer object.
                            if (ConnectionIdToPeerMappings.TryGetValue(opkt.targetConnectionId, out Peer p))
                            {
                                p.Send(opkt.channelId, ref opkt.contents);
                                // It worked
                            } 
                            
                            // } else { Debug.Log("You idiot, it didn't work"); }
                            
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
                                    evt.eventType = EventType.Connect;
                                    evt.connectionId = nextAvailableSlot;                                    
                                    // keep for now.
                                    evt.peerId = peer.ID;

                                    // Debug.Log($"nas {nextAvailableSlot} peerid {peer.ID}");

                                    // Update dictonaries
                                    ConnectionIdToPeerMappings.TryAdd(nextAvailableSlot, peer);
                                    PeerIDsToConnectionIdMappings.TryAdd(peer.ID, nextAvailableSlot);

                                    nextAvailableSlot++;
                                    Incoming.Enqueue(evt);
                                    break;

                                case EventType.Timeout:
                                case EventType.Disconnect:
                                    if (PeerIDsToConnectionIdMappings.ContainsKey(peer.ID))
                                    {
                                        int dead = PeerIDsToConnectionIdMappings[peer.ID];

                                        evt.eventType = evt.eventType == EventType.Disconnect ? EventType.Timeout : EventType.Disconnect;
                                        evt.peerId = peer.ID;
                                        evt.connectionId = dead;

                                        Incoming.Enqueue(evt);

                                        ConnectionIdToPeerMappings.TryRemove(dead, out Peer deadPeer);
                                        PeerIDsToConnectionIdMappings.TryRemove(peer.ID, out int deadConnID);
                                    }
                                    break;

                                case EventType.Receive:
                                    if (PeerIDsToConnectionIdMappings.ContainsKey(peer.ID))
                                    {
                                        int sender = PeerIDsToConnectionIdMappings[peer.ID];

                                        evt.eventType = EventType.Receive;
                                        evt.peerId = peer.ID;
                                        evt.connectionId = sender;

                                        Packet pkt = netEvent.Packet;
                                        evt.databuff = new byte[pkt.Length];
                                        pkt.CopyTo(evt.databuff);

                                        // don't dispose the original packet? blame FSE_Vincenzo for memory leaks
                                        // netEvent.Packet.Dispose();
                                        pkt.Dispose();

                                        Incoming.Enqueue(evt);
                                    }
                                    break;
                            }
                        }
                    }

                    HostObject.Flush();
                    Debug.Log("Server worker finished. Going home.");
                    CurrentState = ThreadState.Stopping;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Worker thread exception occurred: {ex.ToString()}");
                }
                finally
                {
                    // What if there's still clients waiting out there for things?
                    /*
                    for(int i = 0; i < ConnectionIdToPeerMappings.Count; i++)
                    {
                        // Disconnect all clients that might be still connected.
                        ConnectionIdToPeerMappings[i].DisconnectNow(0);
                    }

                    Debug.Log("Disconnected all connected clients.");
                    */
                    Debug.Log("Turned off the Nozzle. Good work out there.");
                    CurrentState = ThreadState.Stopped;
                }
            }
        }

        public static void Shutdown()
        {
            CeaseOperation = true;
        }

        /*
        public static string GetClientAddress(int connectionId)
        {
            if (ConnectionIdToPeerIDMappings.TryGetValue(connectionId, out uint pid))
            {
                return PeerIDsToPeerMappings[pid].IP;
            }

            return "(invalid)";
        }
        */

        /*
        public static bool DoesThisConnectionHaveAPeer(int connectionId)
        {
            if (ConnectionIdToPeerIDMappings.ContainsKey(connectionId))
            {
                return true;
            }

            return false;
        }
        */

        public static bool DisconnectThatConnection(int connectionId)
        {
            // This could be improved.
            if(ConnectionIdToPeerMappings.ContainsKey(connectionId))
            {
                QueuedCommand qc = default;
                qc.Type = 0;
                qc.ConnId = connectionId;

                CommandQueue.Enqueue(qc);
                return true;
            }

            return false;
        }

        // -- Hacks -- //
        /*
        private static void AddMappings(int connectionId, uint peerId, Peer peerObj)
        {
            // ConnID -> PeerID
            if(!ConnectionIdToPeerIDMappings.ContainsKey(connectionId))
            {
                ConnectionIdToPeerIDMappings.Add(connectionId, peerId);
            } else
            {
                Debug.LogWarning("WARNING: AddMappings ConnID -> PeerID double dip!!");
            }
            
            // ConnID <- PeerID
            if(!PeerIDsToConnectionIdMappings.ContainsKey(peerId))
            {
                PeerIDsToConnectionIdMappings.Add(peerId, connectionId);
            }
            else
            {
                Debug.LogWarning("WARNING: AddMappings PeerID -> ConnID double dip!!");
            }

            // PeerID -> Peer Object
            if (PeerIDsToPeerMappings.ContainsKey(peerId))
            {
                PeerIDsToPeerMappings.TryAdd(peerId, peerObj);
            }
            else
            {
                Debug.LogWarning("WARNING: AddMappings PeerID -> Peer double dip!!");
            }
        }


        private static void RemoveMappings(int connectionId, uint peerId, Peer peerObj)
        {
            ConnectionIdToPeerIDMappings.Remove(connectionId);
            PeerIDsToConnectionIdMappings.Remove(peerId);
            PeerIDsToPeerMappings.TryRemove(peerId, out Peer evictedPeer);
        }
                */
    }
}
