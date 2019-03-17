using ENet;
using Mirror.Ignorance.Thirdparty;
using System;
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

        public static volatile bool CeaseOperation = false;

        public static Thread Nozzle;

        private static Dictionary<int, Peer> knownConnIDToPeers;
        private static Dictionary<Peer, int> knownPeersToConnIDs;
        private static int nextAvailableSlot = 1;

        public static RingBuffer<QueuedIncomingPacket> Incoming;   // Client -> ENET World -> Mirror
        public static RingBuffer<QueuedOutgoingPacket> Outgoing;  // Mirror -> ENET World -> Client

        internal static void InitializeEventHandlers()
        {
            OnServerConnected = new UnityEventInt();
            OnServerDisconnected = new UnityEventInt();

            OnServerDataReceived = new UnityEventIntByteArray();
            OnServerError = new UnityEventIntException();
        }

        public static bool IsServerActive()
        {
            Debug.Log("IsServerActive() polled");
            if (Nozzle != null)
            {
                return Nozzle.IsAlive;
            } else {
                return false;
            }
        }

        public static void Start(ushort port)
        {
            Debug.Log("Ignorance Server Showerhead: Start()");

            Port = port;
            CeaseOperation = false;

            // Refresh dictonaries
            knownConnIDToPeers = new Dictionary<int, Peer>();
            knownPeersToConnIDs = new Dictionary<Peer, int>();

            // Setup queues.
            Incoming = new RingBuffer<QueuedIncomingPacket>(SendPacketQueueSize);
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

            CeaseOperation = true;
            if (Nozzle.IsAlive)
            {
                Nozzle.Abort();
            }
            else
            {
                Debug.LogError("Tried to call Abort on a dead thread?!");
            }
        }

        public static void WorkerLoop(object args)
        {
            Debug.Log("Nozzle WorkerLoop begins.");
            int deadPeerConnID, timedOutConnID;

            using (HostObject)
            {
                // Create a new address.
                Address address = new Address();
                address.Port = Port;

                // Create the host object.
                HostObject.Create(address, 1000);

                Debug.Log("Created HostObject, hopefully.");

                // Hold the network event that's being emitted.
                Event netEvent;

                try
                {
                    while (!CeaseOperation)
                    {
                        bool polled = false;

                        // Send code below.
                        if (Outgoing.Count > 0)
                        {
                            // REMOVE ME
                            Debug.Log("We've got packets to send!");

                            while (Outgoing.Count > 0)
                            {
                                QueuedOutgoingPacket pkt;
                                if (Outgoing.TryDequeue(out pkt))
                                {
                                    if (IsConnectionIdKnown(pkt.targetConnectionId))
                                    {
                                        if(knownConnIDToPeers[pkt.targetConnectionId].Send(pkt.channelId, ref pkt.contents))
                                        {
                                            Debug.Log("Yay");
                                        } else
                                        {
                                            Debug.LogWarning("No!");
                                        }
                                    }
                                }
                            }
                        }

                        // Receive code below.
                        while (!polled)
                        {
                            if (HostObject.CheckEvents(out netEvent) <= 0)
                            {
                                if (HostObject.Service(15, out netEvent) <= 0)
                                    break;

                                polled = true;
                            }

                            switch (netEvent.Type)
                            {
                                case EventType.None:
                                    break;

                                case EventType.Connect:
                                    Debug.Log($"Server has a new client! Peer ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}, Mirror CID: {nextAvailableSlot}");

                                    knownPeersToConnIDs.Add(netEvent.Peer, nextAvailableSlot);
                                    knownConnIDToPeers.Add(nextAvailableSlot, netEvent.Peer);

                                    OnServerConnected.Invoke(nextAvailableSlot);
                                    nextAvailableSlot++;
                                    break;

                                case EventType.Disconnect:
                                    Debug.Log($"Server had a client disconnect. Peer ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}");
                                    if (knownPeersToConnIDs.TryGetValue(netEvent.Peer, out deadPeerConnID))
                                    {
                                        OnServerDisconnected.Invoke(deadPeerConnID);
                                        PeerDisconnectedInternal(netEvent.Peer);
                                    }
                                    break;

                                case EventType.Timeout:
                                    Debug.Log($"Server had a client timeout. ID: Peer ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}");
                                    if (knownPeersToConnIDs.TryGetValue(netEvent.Peer, out timedOutConnID))
                                    {
                                        OnServerDisconnected.Invoke(timedOutConnID);
                                        PeerDisconnectedInternal(netEvent.Peer);
                                    }
                                    break;

                                case EventType.Receive:
                                    // Enslave a new packet to the queue.
                                    Incoming.Enqueue(new QueuedIncomingPacket() { connectionId = knownPeersToConnIDs[netEvent.Peer], contents = netEvent.Packet });
                                    netEvent.Packet.Dispose();
                                    break;
                            }
                        }
                    }

                    HostObject.Flush();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Thread ERROR: {ex.ToString()}");
                }
                finally
                {
                    Debug.Log("Turned off the Nozzle. Good work out there.");
                }
            }
        }

        internal static void Shutdown()
        {
            if (HostObject != null && HostObject.IsSet)
            {
                HostObject.Dispose();
            }
        }

        internal static string GetClientAddress(int connectionId)
        {
            Peer result;

            if (knownConnIDToPeers.TryGetValue(connectionId, out result))
            {
                return result.IP;
            }

            return "(invalid)";
        }

        internal static bool DisconnectThatConnection(int connectionId)
        {
            Peer result;

            if (knownConnIDToPeers.TryGetValue(connectionId, out result))
            {
                result.DisconnectNow(0);
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

        private static Peer ResolveConnectionIDToPeer(int connectionId)
        {
            return knownConnIDToPeers[connectionId];
        }

        private static void PeerDisconnectedInternal(Peer peer)
        {
            // Clean up dictionaries.
            knownConnIDToPeers.Remove(knownPeersToConnIDs[peer]);
            knownPeersToConnIDs.Remove(peer);
        }

        // server
        public static UnityEventInt OnServerConnected = new UnityEventInt();
        public static UnityEventInt OnServerDisconnected = new UnityEventInt();

        public static UnityEventIntByteArray OnServerDataReceived = new UnityEventIntByteArray();
        public static UnityEventIntException OnServerError = new UnityEventIntException();
    }

    // Incoming Packet Class
    public class QueuedIncomingPacket
    {
        public int connectionId;
        public Packet contents;
    }

    // Outoging Packet Class
    public class QueuedOutgoingPacket
    {
        public int targetConnectionId;
        public byte channelId = 0x00;
        public Packet contents;
    }
}