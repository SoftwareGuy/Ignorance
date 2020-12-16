// Ignorance 1.4.x
// Ignorance. It really kicks the Unity LLAPIs ass.
// https://github.com/SoftwareGuy/Ignorance
// -----------------
// Copyright (c) 2019 - 2020 Matt Coburn (SoftwareGuy/Coburn64)
// Ignorance Transport is licensed under the MIT license. Refer
// to the LICENSE file for more information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using ENet;
using UnityEngine;
using Event = ENet.Event;           // fixes CS0104 ambigous reference between the same thing in UnityEngine
using EventType = ENet.EventType;   // fixes CS0104 ambigous reference between the same thing in UnityEngine
using Object = System.Object;       // fixes CS0104 ambigous reference between the same thing in UnityEngine

namespace Mirror
{
    public class IgnoranceClient
    {
        // Client connection address and port
        public string ConnectAddress = "127.0.0.1";
        public int ConnectPort = 7777;
        // How many channels are expected
        public int ExpectedChannels = 2;
        // Native poll waiting time
        public int PollTime = 1;
        // Maximum Packet Size
        public int MaximumPacketSize = 33554432;
        // General Verbosity by default.
        public int Verbosity = 1;

        // Queues
        public ConcurrentQueue<IgnorancePacket> Incoming = new ConcurrentQueue<IgnorancePacket>();
        public ConcurrentQueue<IgnorancePacket> Outgoing = new ConcurrentQueue<IgnorancePacket>();

        public bool IsAlive => WorkerThread != null ? WorkerThread.IsAlive : false;

        private volatile bool CeaseOperation = false;
        private Thread WorkerThread;

        // TO BE CONTINUED...
        // <------

        public void Start()
        {
            Console.WriteLine("IgnoranceClient.Start()");

            if (WorkerThread != null && WorkerThread.IsAlive)
            {
                // Cannot do that.
                Debug.LogError("A worker thread is already running. Cannot start another.");
                return;
            }

            CeaseOperation = false;
            ThreadParamInfo threadParams = new ThreadParamInfo()
            {
                Address = ConnectAddress,
                Port = ConnectPort,
                Channels = ExpectedChannels,
                PollTime = PollTime,
                PacketSizeLimit = MaximumPacketSize,
                Verbosity = Verbosity
            };

            // Drain queues.
            if (Incoming != null) while (Incoming.TryDequeue(out _)) ;
            if (Outgoing != null) while (Outgoing.TryDequeue(out _)) ;

            WorkerThread = new Thread(ThreadWorker);
            WorkerThread.Start(threadParams);

            Debug.Log("Client has dispatched worker thread.");
        }

        public void Stop()
        {
            Debug.Log("Telling client thread to stop, this may take a while depending on network load");
            CeaseOperation = true;
        }

        // This runs in a seperate thread, be careful accessing anything outside of it's thread
        // or you may get an AccessViolation/crash.
        private void ThreadWorker(Object parameters)
        {
            if(Verbosity > 0) Debug.Log("Client Work Thread: Startup");

            ThreadParamInfo setupInfo;
            Address clientAddress = new Address();
            Peer clientPeer;
            Host clientENetHost;
            Event clientENetEvent;

            // Grab the setup information.
            if (parameters.GetType() == typeof(ThreadParamInfo))
            {
                setupInfo = (ThreadParamInfo)parameters;
            }
            else
            {
                Debug.LogError("Thread worker startup failure: Invalid thread parameters. Aborting.");
                return;
            }

            // Attempt to initialize ENet inside the thread.
            if (Library.Initialize())
            {
                if (setupInfo.Verbosity > 0) Debug.Log("Client Work Thread: Initialized ENet.");
            }
            else
            {
                if (setupInfo.Verbosity > 0) Debug.Log("Client Work Thread: Failed to initialize ENet.");
                return;
            }

            // Attempt to connect to our target.
            clientAddress.SetHost(setupInfo.Address);
            clientAddress.Port = (ushort)setupInfo.Port;

            using (clientENetHost = new Host())
            {
                // TODO: Maybe try catch this
                clientENetHost.Create();
                clientPeer = clientENetHost.Connect(clientAddress);

                while (!CeaseOperation)
                {
                    bool pollComplete = false;

                    // Sending
                    while (Outgoing.TryDequeue(out IgnorancePacket outgoingPacket))
                    {
                        if (!outgoingPacket.Outgoing) continue;

                        // TODO: Revise this, could we tell the Peer to disconnect right here?                       
                        // Stop early if we get a client stop packet.
                        if (outgoingPacket.Type == IgnorancePacketType.ClientWantsToStop) break;

                        // It's time to play, guess the packet!
                        switch (outgoingPacket.Type)
                        {
                            case IgnorancePacketType.ClientStatusUpdateRequest:
                                // This isn't going to be sent out. This is a request to get stats from Native back to the main thread.
                                Incoming.Enqueue(new IgnorancePacket
                                {
                                    Type = IgnorancePacketType.ClientStatusUpdateResponse,
                                    StatusData = new PeerHealth { RTT = clientPeer.RoundTripTime, PacketsSent = clientPeer.PacketsSent, BytesSent = clientPeer.BytesSent, BytesReceived = clientPeer.BytesReceived }
                                });;
                                break;

                            default:
                                Packet packet = default;
                                packet.Create(outgoingPacket.RentedByteArray, outgoingPacket.Length, outgoingPacket.Flags);

                                // This can be spammy, so best to make it only if set to verbose.
                                if (setupInfo.Verbosity > 1 && outgoingPacket.Length > 1200)
                                    Debug.LogWarning("Client Worker Thread: This packet is larger than the recommended ENet 1200 byte MTU. As such, it will be sent as Reliable Fragmented.");

                                int ret = clientPeer.Send(outgoingPacket.Channel, ref packet);

                                if (ret < 0 && setupInfo.Verbosity > 1) Debug.LogWarning($"Client Worker Thread: ENet failed sending a packet, error code {ret}");

                                ArrayPool<byte>.Shared.Return(outgoingPacket.RentedByteArray, true);
                                break;
                        }
                    }

                    // TODO: This might not even be used in 1.4.0.
                    // Break out of the loop early if we're shutting down.
                    // This can happen if main thread flipped our kill switch.
                    if (CeaseOperation)
                    {
                        if (setupInfo.Verbosity > 0) Debug.Log("Client Worker Thread: Killswitch activated, exiting early.");
                        break;
                    }

                    // This loops until polling is completed. It may take a while, if it's
                    // a slow networking day.
                    while (!pollComplete)
                    {
                        // Any events worth checking out?
                        if (clientENetHost.CheckEvents(out clientENetEvent) <= 0)
                        {
                            // If service time is met, break out of it.
                            if (clientENetHost.Service(setupInfo.PollTime, out clientENetEvent) <= 0) break;

                            // Poll is done.
                            pollComplete = true;
                        }

                        // Now, let's handle those events.
                        switch (clientENetEvent.Type)
                        {
                            case EventType.None:
                            default:
                                break;

                            case EventType.Connect:
                                Incoming.Enqueue(new IgnorancePacket()
                                {
                                    Type = IgnorancePacketType.ClientConnect,
                                    PeerData = new PeerConnectionData()
                                    {
                                        IP = clientENetEvent.Peer.IP,
                                        Port = clientENetEvent.Peer.Port
                                    }
                                });
                                break;

                            case EventType.Disconnect:
                            case EventType.Timeout:
                                Incoming.Enqueue(new IgnorancePacket()
                                {
                                    Type = IgnorancePacketType.ClientDisconnect
                                });
                                break;


                            case EventType.Receive:
                                // Never consume more than we can have capacity for.
                                if(clientENetEvent.Packet.Length > setupInfo.PacketSizeLimit)
                                {
                                    if (setupInfo.Verbosity > 0)
                                        Debug.LogWarning($"Client Worker Thread: Received a packet too large, {clientENetEvent.Packet.Length} bytes while our limit was {setupInfo.PacketSizeLimit} bytes.");

                                    clientENetEvent.Packet.Dispose();
                                    break;
                                }

                                // Grab a new fresh array from the ArrayPool, at least the length of our packet coming in.
                                byte[] storageBuffer = ArrayPool<byte>.Shared.Rent(clientENetEvent.Packet.Length);

                                // Copy the packet to the fresh array.
                                clientENetEvent.Packet.CopyTo(storageBuffer);

                                Incoming.Enqueue(new IgnorancePacket()
                                {
                                    Type = IgnorancePacketType.ClientData,
                                    Length = clientENetEvent.Packet.Length,
                                    RentedByteArray = storageBuffer
                                });

                                clientENetEvent.Packet.Dispose();
                                break;
                        }
                    }
                }

                // Flush the client and disconnect.
                clientENetHost.Flush();
                clientPeer.DisconnectNow(0);
            }

            // Deinitialize
            Library.Deinitialize();
            if (setupInfo.Verbosity > 0)
                Debug.Log("Client Worker Thread: Shutdown.");
        }

        private struct ThreadParamInfo
        {
            public string Address;
            public int Channels;
            public int PollTime;
            public int Port;
            public int PacketSizeLimit;
            public int Verbosity;
        }

        // TO BE CONTINUED...
        // <------
    }
}
