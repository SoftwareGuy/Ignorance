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
        
        private static Dictionary<int, uint> ConnectionIdToPeerIDMappings;
        private static Dictionary<uint, int> PeerIDsToConnectionIdMappings;
        private static ConcurrentDictionary<uint, Peer> PeerIDsToPeerMappings;

        private static Host HostObject = new Host();    // ENET Host Object
        internal static int nextAvailableSlot = 1;
        
        // We create new ringbuffers, but these will be overwritten when the Start() function is called.
        // This prevents nulls, thus saving null checks being heavy on performance.
        public static RingBuffer<QueuedIncomingEvent> Incoming = new RingBuffer<QueuedIncomingEvent>(1024);   // Client -> ENET World -> Mirror
        public static RingBuffer<QueuedOutgoingPacket> Outgoing = new RingBuffer<QueuedOutgoingPacket>(1024);  // Mirror -> ENET World -> Client
        private static RingBuffer<QueuedCommand> CommandQueue = new RingBuffer<QueuedCommand>(50);    // ENET Command Queue.
        public static RingBuffer<QueuedIncomingConnectionEvent> IncommingConnEvents = new RingBuffer<QueuedIncomingConnectionEvent>(4096); // ENET World -> Mirror conn events.

        public static bool IsServerActive()
        {
            return Nozzle.IsAlive;
        }

        public static void Start(ushort port)
        {
            Debug.Log("Ignorance Server Showerhead: Start()");
            CurrentState = ThreadState.Starting;

            Port = port;
            CeaseOperation = false;

            // Refresh dictonaries
            ConnectionIdToPeerIDMappings = new Dictionary<int, uint>();   // Mirror CIDs. -> ENET PeerIDs.
            PeerIDsToConnectionIdMappings = new Dictionary<uint, int>();  // Reverse lookup, ENET Peer IDs -> Mirror CIDs.
            PeerIDsToPeerMappings = new ConcurrentDictionary<uint, Peer>(); // PeerID lookup.

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
            Debug.Log("Server Worker has arrived!");
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
                                case '0':
                                    // Boot to the face.
                                    if (ConnectionIdToPeerIDMappings.TryGetValue(qCmd.ConnId, out uint victim))
                                    {
                                        PeerIDsToPeerMappings[victim].DisconnectLater(0);
                                    }
                                    break;
                            }
                        }

                        // Send any pending packets out first.
                        QueuedOutgoingPacket opkt;
                        while (Outgoing.TryDequeue(out opkt))
                        {
                            // Try mapping the peer id to the peer object.
                            if (PeerIDsToPeerMappings.TryGetValue(opkt.targetPeerId, out Peer p))
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

                            switch (netEvent.Type)
                            {
                                case EventType.None:
                                    // Do I need to say more?
                                    break;

                                case EventType.Connect:
                                    var connevent = new QueuedIncomingConnectionEvent 
                                    {
                                        connectionId = nextAvailableSlot,
                                        eventType = EventType.Connect,
                                        peerIp = peer.IP,
                                        peerPort = peer.Port
                                    };

                                    AddMappings(nextAvailableSlot, peer.ID, peer);
                                    nextAvailableSlot++;

                                    IncommingConnEvents.Enqueue(connevent);
                                    break;
                                case EventType.Timeout:
                                case EventType.Disconnect:
                                    var peerId = peer.ID;
                                    var connectionId = PeerIDsToConnectionIdMappings[peerId];
                                    var disconnevent = new QueuedIncomingConnectionEvent 
                                    {
                                        connectionId = connectionId,
                                        eventType = netEvent.Type
                                    };
                                    
                                    RemoveMappings(connectionId, peerId, peer);

                                    IncommingConnEvents.Enqueue(disconnevent);
                                    break;
                                case EventType.Receive:
                                    QueuedIncomingEvent evt = default;
                                    if (PeerIDsToConnectionIdMappings.ContainsKey(peer.ID))
                                    {
                                        int sender = PeerIDsToConnectionIdMappings[peer.ID];
                                        
                                       // evt.peerId = peer.ID;
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

        public static bool DoesThisConnectionHaveAPeer(int connectionId)
        {
            if (ConnectionIdToPeerIDMappings.ContainsKey(connectionId))
            {
                return true;
            }

            return false;
        }

        public static bool DisconnectThatConnection(int connectionId)
        {
            // This could be improved.
            if(ConnectionIdToPeerIDMappings.ContainsKey(connectionId))
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
        private static void AddMappings(int connectionId, uint peerId, Peer peerObj)
        {
            // ConnID -> PeerID
            ConnectionIdToPeerIDMappings.Add(connectionId, peerId);
            // ConnID <- PeerID
            PeerIDsToConnectionIdMappings.Add(peerId, connectionId);
            // PeerID -> Peer Object
            PeerIDsToPeerMappings.TryAdd(peerId, peerObj);
        }

        private static void RemoveMappings(int connectionId, uint peerId, Peer peerObj)
        {
            ConnectionIdToPeerIDMappings.Remove(connectionId);
            PeerIDsToConnectionIdMappings.Remove(peerId);
            PeerIDsToPeerMappings.TryRemove(peerId, out Peer evictedPeer);
        }
    }
}
