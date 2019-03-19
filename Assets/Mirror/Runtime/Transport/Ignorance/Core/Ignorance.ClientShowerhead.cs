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
using System.Threading;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace Mirror.Ignorance
{
    public static class ClientShowerhead
    {
        public static volatile Host HostObject = new Host();    // ENET Host Object
        public static Peer ClientPeer = new Peer();             // ENET Client Peer

        public static string ClientAddress = "127.0.0.1";
        public static ushort ClientPort = 65534;
        public static int NumChannels = 1;

        public static bool DebugMode = false;
        public static int IncomingEventQueueCapacity = 524288;  // 512 * 1024
        public static int OutgoingPacketQueueCapacity = 524288;

        public static RingBuffer<QueuedIncomingEvent> Incoming;    // Server -> Client
        public static RingBuffer<QueuedOutgoingPacket> Outgoing;    // Client -> Server

        public static volatile bool CeaseOperation = false;     // Kills threads dead!

        public static Thread Nozzle;

        public static void Start(string addr, ushort port)
        {
            Debug.Log("Ignorance Client Showerhead: Start()");

            CeaseOperation = false;

            ClientAddress = addr;
            ClientPort = port;

            Incoming = new RingBuffer<QueuedIncomingEvent>(IncomingEventQueueCapacity);
            Outgoing = new RingBuffer<QueuedOutgoingPacket>(OutgoingPacketQueueCapacity);

            CeaseOperation = false;

            Nozzle = new Thread(WorkerLoop)
            {
                Name = "Shower Head (Client)"
            };

            Nozzle.Start();
        }

        public static void Stop()
        {
            // Mirror bug: ClientDisconnect gets called on connection ID 0 when it shouldn't.
            if (Nozzle == null) return;

            Debug.Log("Ignorance Client Showerhead: Stop()");
            Debug.Log("Instructing the showerhead worker to stop, this may take a few moments...");
            CeaseOperation = true;
        }

        public static void WorkerLoop(object args)
        {
            Debug.Log("Ignorance Client Showerhead: Starting loop...");
            using (HostObject)
            {
                Address address = new Address();
                address.SetHost(ClientAddress);
                address.Port = ClientPort;

                HostObject.Create();

                ClientPeer = HostObject.Connect(address);

                Event netEvent;

                try
                {
                    while (!CeaseOperation)
                    {
                        bool polled = false;

                        // Send any pending packets out first.
                        QueuedOutgoingPacket opkt;
                        while (Outgoing.TryDequeue(out opkt)) {
                            ClientPeer.Send(opkt.channelId, ref opkt.contents);
                        }

                        // Now, we receive what's going on in the network chatter.
                        while (!polled)
                        {
                            if (HostObject.CheckEvents(out netEvent) <= 0)
                            {
                                if (HostObject.Service(1, out netEvent) <= 0)
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
                                    if (DebugMode)
                                    {
                                        Debug.Log($"Worker Thread: Client has connected! Peer {netEvent.Peer.ID} (that's me) connects to IP: {netEvent.Peer.IP}");
                                    }

                                    evt.eventType = EventType.Connect;
                                    evt.peerId = peer.ID;
                                    Incoming.Enqueue(evt);
                                    break;

                                case EventType.Disconnect:
                                    if (DebugMode)
                                    {
                                        Debug.Log($"Worker Thread: Client has disconnected.");
                                    }

                                    evt.eventType = EventType.Disconnect;
                                    evt.peerId = peer.ID;
                                    Incoming.Enqueue(evt);

                                    break;

                                case EventType.Timeout:
                                    if (DebugMode)
                                    {
                                        Debug.Log($"Worker Thread: Client timed out.");
                                    }

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
                                    netEvent.Packet.Dispose();

                                    // Enslave a new packet to the queue.
                                    Incoming.Enqueue(evt);
                                    break;
                            }
                        }
                    }

                    HostObject.Flush();
                    ClientPeer.DisconnectNow(0);
                    Debug.Log("Client Worker finished. Going home.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Worker thread exception occurred: {ex.ToString()}");
                }
                finally
                {
                    ClientPeer.DisconnectNow(0);
                    Debug.Log("Turned off the Nozzle. Good work out there.");
                }
            }
        }

        public static bool IsClientConnected()
        {
            return ClientPeer.IsSet && ClientPeer.State == PeerState.Connected;
        }

        internal static void Shutdown()
        {
            // ???
            if (Nozzle != null && Nozzle.IsAlive)
            {
                Nozzle.Abort();
            }
        }
    }

}