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
        public static int IncomingPacketQueueSize = 4096;
        public static int OutgoingPacketQueueSize = 4096;

        public static RingBuffer<QueuedIncomingEvent> Incoming;    // Server -> Client
        public static RingBuffer<QueuedOutgoingPacket> Outgoing;    // Client -> Server

        public static volatile bool CeaseOperation = false;

        public static Thread Nozzle;

        public static void Start(string addr, ushort port)
        {
            Debug.Log("Ignorance Client Showerhead: Start()");

            CeaseOperation = false;

            ClientAddress = addr;
            ClientPort = port;

            Incoming = new RingBuffer<QueuedIncomingEvent>(4096);
            Outgoing = new RingBuffer<QueuedOutgoingPacket>(4096);

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

                        // Send code below.
                        //if (Outgoing.Count > 0)
                        //{
                        // REMOVE ME
                        //Debug.Log("We've got packets to send!");

                        while (Outgoing.Count > 0)
                        {
                            QueuedOutgoingPacket pkt;
                            if (Outgoing.TryDequeue(out pkt))
                            {
                                ClientPeer.Send(pkt.channelId, ref pkt.contents);

                                /*
                                if (ClientPeer.Send(pkt.channelId, ref pkt.contents))
                                {
                                    Debug.Log("Yay");
                                }
                                else
                                {
                                    Debug.LogWarning("No!");
                                }
                                */
                            }
                        }
                        //}

                        // Receive code below.
                        while (!polled)
                        {
                            if (HostObject.CheckEvents(out netEvent) <= 0)
                            {
                                if (HostObject.Service(1, out netEvent) <= 0)
                                    break;

                                polled = true;
                            }

                            Peer peer = netEvent.Peer;
                            QueuedIncomingEvent evt = default;

                            switch (netEvent.Type)
                            {
                                case EventType.None:
                                    break;

                                case EventType.Connect:
                                    Debug.Log($"Worker Thread: Client has connected! Peer {netEvent.Peer.ID} (that's me) connects to IP: {netEvent.Peer.IP}");

                                    evt.eventType = EventType.Connect;
                                    evt.peerId = peer.ID;
                                    Incoming.Enqueue(evt);
                                    break;

                                case EventType.Disconnect:
                                    Debug.Log($"Worker Thread: Client has disconnected.");

                                    evt.eventType = EventType.Disconnect;
                                    evt.peerId = peer.ID;
                                    Incoming.Enqueue(evt);

                                    break;

                                case EventType.Timeout:
                                    Debug.Log($"Worker Thread: Client timed out.");

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

        /*
        public static void InitializeEventHandlers()
        {
            OnClientConnected = new UnityEngine.Events.UnityEvent();
            OnClientDisconnected = new UnityEngine.Events.UnityEvent();

            OnClientDataReceived = new UnityEventByteArray();
            OnClientError = new UnityEventException();
        }
        */

        public static UnityEngine.Events.UnityEvent OnClientConnected = new UnityEngine.Events.UnityEvent();
        public static UnityEngine.Events.UnityEvent OnClientDisconnected = new UnityEngine.Events.UnityEvent();

        public static UnityEventByteArray OnClientDataReceived = new UnityEventByteArray();
        public static UnityEventException OnClientError = new UnityEventException();
    }

}