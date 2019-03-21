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

        // We create new ringbuffers, but these will be overwritten when the Start() function is called.
        // This prevents nulls, thus saving null checks being heavy on performance.
        public static RingBuffer<QueuedIncomingEvent> Incoming = new RingBuffer<QueuedIncomingEvent>(1024);    // Server -> Client
        public static RingBuffer<QueuedOutgoingPacket> Outgoing = new RingBuffer<QueuedOutgoingPacket>(1024);    // Client -> Server

        public static volatile bool CeaseOperation = false;     // Kills threads dead!

        private static volatile ThreadState CurrentState = ThreadState.Stopped;   // Goddamnit Mirror.

        public static Thread Nozzle;

        public static void Start(string addr, ushort port)
        {
            Debug.Log("Ignorance Client Showerhead: Start()");

            CeaseOperation = false;
            CurrentState = ThreadState.Starting;

            ClientAddress = addr;
            ClientPort = port;

            Incoming = new RingBuffer<QueuedIncomingEvent>(IgnoranceConstants.ClientIncomingRingBufferSize);
            Outgoing = new RingBuffer<QueuedOutgoingPacket>(IgnoranceConstants.ClientOutgoingRingBufferSize);

            Nozzle = new Thread(WorkerLoop)
            {
                Name = "Ignorance Transport Client Worker"
            };

            Nozzle.Start();
        }

        public static void Stop()
        {
            // Mirror bug: ClientDisconnect gets called on connection ID 0 when it shouldn't.
            // if (Nozzle == null) return;
            if (CurrentState == ThreadState.Stopping)
            {
                Debug.LogWarning("Give me a damn break Mirror, I'm stopping already!");
                return;
            }

            Debug.Log("Ignorance Client Showerhead: Stop()");
            Debug.Log("Instructing the showerhead worker to stop, this may take a few moments...");

            CurrentState = ThreadState.Stopping;
            CeaseOperation = true;

        }

        public static void WorkerLoop(object args)
        {
            Debug.Log("Ignorance Client Showerhead: Starting loop...");
            using (HostObject)
            {
                Event netEvent;

                Address address = new Address();
                address.SetHost(ClientAddress);
                address.Port = ClientPort;

                HostObject.Create();

                ClientPeer = HostObject.Connect(address);

                CurrentState = ThreadState.Started;
                try
                {
                    while (!CeaseOperation)
                    {
                        bool polled = false;

                        // Send any pending packets out first.
                        QueuedOutgoingPacket opkt;
                        while (Outgoing.TryDequeue(out opkt))
                        {
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
                                    evt.eventType = EventType.Connect;
                                    evt.peerId = peer.ID;

                                    Incoming.Enqueue(evt);
                                    break;

                                case EventType.Disconnect:
                                    evt.eventType = EventType.Disconnect;
                                    evt.peerId = peer.ID;
                                    Incoming.Enqueue(evt);

                                    break;

                                case EventType.Timeout:
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
                    Debug.Log("Client worker finished. Going home.");
                    CurrentState = ThreadState.Stopping;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Worker thread exception occurred: {ex.ToString()}");
                }
                finally
                {
                    ClientPeer.DisconnectNow(0);
                    Debug.Log("Turned off the Nozzle. Good work out there.");
                    CurrentState = ThreadState.Stopped;
                }
            }
        }

        public static bool IsClientConnected()
        {
            return ClientPeer.IsSet && ClientPeer.State == PeerState.Connected;
        }

        internal static void Shutdown()
        {
            CeaseOperation = true;
        }
    }

}