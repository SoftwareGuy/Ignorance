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

        public static int IncomingPacketQueueSize = 4096;
        public static int OutgoingPacketQueueSize = 4096;

        public static RingBuffer<QueuedIncomingPacket> Incoming;    // Server -> Client
        public static RingBuffer<QueuedOutgoingPacket> Outgoing;    // Client -> Server

        public static volatile bool CeaseOperation = false;

        public static Thread Nozzle;

        public static void Start(string addr, ushort port)
        {
            Debug.Log("Ignorance Client Showerhead: Start()");

            // InitializeEventHandlers();

            ClientAddress = addr;
            ClientPort = port;

            Incoming = new RingBuffer<QueuedIncomingPacket>(4096);
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
            Debug.Log("Ignorance Client Showerhead: Starting loop...");
            using (HostObject)
            {
                Address address = new Address();
                address.SetHost(ClientAddress);
                address.Port = ClientPort;

                HostObject.Create();

                ClientPeer = HostObject.Connect(address);

                Event netEvent;

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
                               if(ClientPeer.Send(pkt.channelId, ref pkt.contents))
                                {
                                    Debug.Log("Yay");
                                } else
                                {
                                    Debug.LogWarning("Boo");
                                }
                            }
                        }
                    }

                    // Receive code below.
                    while (!polled)
                    {
                        if (HostObject.CheckEvents(out netEvent) <= 0)
                        {
                            if (HostObject.Service(1, out netEvent) <= 0)
                                break;

                            polled = true;
                        }

                        switch (netEvent.Type)
                        {
                            case EventType.None:
                                // Nothing happened, continue on.
                                break;

                            case EventType.Connect:
                                // We connected to the server.
                                //Debug.Log("We've connected to the server!");
                                OnClientConnected.Invoke();
                                break;

                            case EventType.Disconnect:
                                //Debug.Log("We've disconnected from the server!");
                                OnClientDisconnected.Invoke();
                                break;

                            case EventType.Timeout:
                                //Debug.Log("We timed out...");
                                OnClientDisconnected.Invoke();
                                break;

                            case EventType.Receive:
                                Incoming.Enqueue(new QueuedIncomingPacket() { contents = netEvent.Packet });
                                netEvent.Packet.Dispose();
                                break;
                        }
                    }
                }

                HostObject.Flush();
            }
        }

        public static bool IsClientConnected()
        {
            Debug.Log("IsClientConnected polled");
            return ClientPeer.IsSet && ClientPeer.State == PeerState.Connected;
        }

        internal static void Shutdown()
        {
            if(ClientPeer.IsSet && ClientPeer.State == PeerState.Connected)
            {
                ClientPeer.DisconnectNow(0);
            }

            if(HostObject != null && HostObject.IsSet)
            {
                HostObject.Dispose();
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