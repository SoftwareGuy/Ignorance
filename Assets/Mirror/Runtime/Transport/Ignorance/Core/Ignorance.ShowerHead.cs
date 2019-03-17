using ENet;
using Event = ENet.Event;
using EventType = ENet.EventType;

using System;
using System.Collections.Generic;
using System.Threading;

using UnityEngine;
using Mirror.Ignorance.Thirdparty;

namespace Mirror.Ignorance
{
    public class BaseShowerhead
    {
        public static volatile Host HostObject = new Host();    // ENET Host Object
        public static volatile string Address = "127.0.0.1";     // ipv4 or ipv6
        public static volatile ushort Port = 65534;        // valid between ports 0 - 65535
        public static Thread Nozzle;

        // maybe not needed? thread.Pause() / thread.Resume() ....?
        public static volatile bool CeaseOperation = false;

        public virtual void Start(ushort port)
        {
            Port = port;
            CeaseOperation = false;
        }

        public virtual void Stop()
        {
            CeaseOperation = true;
        }

        public static RingBuffer<Packet> Incoming;   // Client -> ENET World -> Mirror
        public static RingBuffer<Packet> Outgoing;   // Mirror -> ENET World -> Client
    
    }

    public class ServerShowerhead : BaseShowerhead
    {
        public override void Start(ushort port)
        {
            // Bootstrap
            base.Start(port);

            // Configure and start thread.
            Nozzle = new Thread(WorkerLoop)
            {
                Name = "Showerhead (Server)"
            };

            Nozzle.Start();
        }

        public override void Stop()
        {
            base.Stop();
        }

        public static void WorkerLoop(object args)
        {
            // Copy pasta
            using (HostObject)
            {
                // Create a new address.
                Address address = new Address();
                address.Port = Port;

                // Create the host object.
                HostObject.Create(address, 1000);

                // Hold the network event that's being emitted.
                Event netEvent;

                while (!CeaseOperation)
                {
                    bool polled = false;

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
                                Console.WriteLine("Client connected - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                                break;

                            case EventType.Disconnect:
                                Console.WriteLine("Client disconnected - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                                break;

                            case EventType.Timeout:
                                Console.WriteLine("Client timeout - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                                break;

                            case EventType.Receive:
                                // Console.WriteLine("Packet received from - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP + ", Channel ID: " + netEvent.ChannelID + ", Data length: " + netEvent.Packet.Length);
                                Incoming.Enqueue(netEvent.Packet);
                                netEvent.Packet.Dispose();
                                break;
                        }
                    }
                }

                hostObject.Flush();
            }
        }

        // server
        public UnityEventInt OnServerConnected;
        public UnityEventInt OnServerDisconnected;

        public UnityEventIntByteArray OnServerDataReceived;
        public UnityEventIntException OnServerError;        
    }

    public class ClientShowerhead : BaseShowerhead
    {
        public UnityEngine.Events.UnityEvent OnClientConnected;
        public UnityEngine.Events.UnityEvent OnClientDisconnected;

        public UnityEventByteArray OnClientDataReceived;
        public UnityEventException OnClientError;
    }

    public class ServerShowerHead
    {
        public static volatile Host hostObject;

        public static readonly Thread Nozzle = new Thread(WorkerThread)
        {
            Name = "Showerhead Packet Engine (Server)"
        };

        public static void Start()
        {
            // start the server
        }

        public static void Stop()
        {
            // stop the server
        }

        private static void WorkerThread()
        {
            ;
        }
    }

    public class Showerhead
    {
        /// <summary>
        /// Server's host object.
        /// </summary>
        public static volatile Host ServerHostObject;

        /// <summary>
        /// Client's host object.
        /// </summary>
        public static volatile Host ClientHostObject;

        /// <summary>
        /// The server's processing thread.
        /// </summary>
        public static readonly Thread ServerNozzle = new Thread(PacketFlow)
        {
            Name = "Showerhead Packet Engine (Server)"
        };

        /// <summary>
        /// The client's processing thread.
        /// </summary>
        public static readonly Thread ClientNozzle = new Thread(PacketFlow)
        {
            Name = "Showerhead Packet Engine (Client)"
        };

        /// <summary>
        /// The incoming queue (Client -> ENET -> Mirror)
        /// </summary>
        public static volatile Queue<Packet> IncomingServerQueue;

        /// <summary>
        /// The outgoing queue (Mirror -> ENET -> Client)
        /// </summary>
        public static volatile Queue<Packet> OutgoingServerQueue;

        /// <summary>
        /// The control switch. If set to true, operations will be performed.
        /// </summary>
        private static volatile bool ContinueOperation = false;

        public static void StartServer()
        {
            if (ServerNozzle.IsAlive)
            {
                Debug.LogError("Cannot start the server processing thread when it's already running, please stop it first.");
            }
            else
            {
                Debug.Log("Starting the server processing thread.");
                ContinueOperation = true;

                ServerNozzle.Start();
            }
        }

        public static void Stop()
        {
            if (!ServerNozzle.IsAlive)
            {
                Debug.Log("Cannot stop the server processing thread when it's already stopped. Please start it first.");
            }
            else
            {
                Debug.Log("Requested server processing thread to stop.");

                // TODO, clean this up.

                ContinueOperation = false;
                ServerNozzle.Abort();
            }
        }

        /// <summary>
        /// Full blast!
        /// </summary>
        /// <param name="obj">I dunno what this is</param>
        private static void PacketFlow(object obj)
        {
            try
            {
                // This needs to be properly implemented.
                while (ContinueOperation)
                {
                    // We talk to ENET and get shit coming out of the nozzle.
                    // TO BE IMPLEMENTED.
                    Debug.Log("PacketFlow run!");
                }
            }
            catch (ThreadInterruptedException)
            {
                // Something interrupted the packet flow.
            }
            catch (ThreadAbortException)
            {
                // We aborted the thread.
            }
            finally
            {
                // Run code here to make sure everything's ok.
            }

            Debug.Log("Showerhead has finished it's loop.");
        }
    }
}
