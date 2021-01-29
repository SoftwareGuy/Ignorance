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

namespace IgnoranceTransport
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

        // Fired when status request returns something.
        public Action<IgnoranceClientStats> StatusUpdate;

        // Queues
        public ConcurrentQueue<IgnoranceIncomingPacket> Incoming = new ConcurrentQueue<IgnoranceIncomingPacket>();
        public ConcurrentQueue<IgnoranceOutgoingPacket> Outgoing = new ConcurrentQueue<IgnoranceOutgoingPacket>();
        public ConcurrentQueue<IgnoranceCommandPacket> Commands = new ConcurrentQueue<IgnoranceCommandPacket>();
        public ConcurrentQueue<IgnoranceConnectionEvent> ConnectionEvents = new ConcurrentQueue<IgnoranceConnectionEvent>();

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
            if (Commands != null) while (Commands.TryDequeue(out _)) ;
            if (ConnectionEvents != null) while (ConnectionEvents.TryDequeue(out _)) ;

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
            if (Verbosity > 0)
                Debug.Log("Client Work Thread: Startup");

            ThreadParamInfo setupInfo;
            Address clientAddress = new Address();
            Peer clientPeer;
            Host clientENetHost;
            Event clientENetEvent;
            IgnoranceClientStats icsu = default;

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
                if (setupInfo.Verbosity > 0)
                    Debug.Log("Client Work Thread: Initialized ENet.");
            }
            else
            {
                if (setupInfo.Verbosity > 0)
                    Debug.Log("Client Work Thread: Failed to initialize ENet.");
                return;
            }

            // Attempt to connect to our target.
            clientAddress.SetHost(setupInfo.Address);
            clientAddress.Port = (ushort)setupInfo.Port;

            using (clientENetHost = new Host())
            {
                // TODO: Maybe try catch this
                clientENetHost.Create();
                clientPeer = clientENetHost.Connect(clientAddress, setupInfo.Channels);

                while (!CeaseOperation)
                {
                    bool pollComplete = false;

                    // Step 0: Handle commands.
                    while (Commands.TryDequeue(out IgnoranceCommandPacket commandPacket))
                    {
                        switch (commandPacket.Type)
                        {
                            default:
                                break;

                            case IgnoranceCommandType.ClientWantsToStop:
                                // TODO.
                                break;

                            case IgnoranceCommandType.ClientRequestsStatusUpdate:
                                // Respond with statistics so far.
                                if (!clientPeer.IsSet)
                                    break;

                                icsu.RTT = clientPeer.RoundTripTime;

                                icsu.BytesReceived = clientPeer.BytesReceived;
                                icsu.BytesSent = clientPeer.BytesSent;

                                icsu.PacketsReceived = clientENetHost.PacketsReceived;
                                icsu.PacketsSent = clientPeer.PacketsSent;
                                icsu.PacketsLost = clientPeer.PacketsLost;

                                StatusUpdate?.Invoke(icsu);
                                break;
                        }
                    }
                    // Step 1: Send out data.
                    // ---> Sending to Server
                    while (Outgoing.TryDequeue(out IgnoranceOutgoingPacket outPacket))
                    {
                        // TODO: outPacket.Flags.HasFlag(PacketFlags.None) = Unreliable mode?
                        if (setupInfo.Verbosity > 1 && outPacket.Length > 1200 && outPacket.Flags.HasFlag(PacketFlags.None))
                            Debug.LogWarning("Client Worker Thread: This packet is larger than the recommended ENet 1200 byte MTU. It will be sent as Reliable Fragmented.");

                        // TODO: Revise this, could we tell the Peer to disconnect right here?                       
                        // Stop early if we get a client stop packet.
                        // if (outgoingPacket.Type == IgnorancePacketType.ClientWantsToStop) break;
                        // Create the packet.
                        Packet packet = default;
                        packet.Create(outPacket.RentedArray, outPacket.Length, outPacket.Flags);

                        int ret = clientPeer.Send(outPacket.Channel, ref packet);
                        if (ret < 0 && setupInfo.Verbosity > 1) Debug.LogWarning($"Client Worker Thread: ENet failed sending a packet, error code {ret}");

                        if (outPacket.WasRented)
                            ArrayPool<byte>.Shared.Return(outPacket.RentedArray, true);
                    }

                    // Step 2:
                    // <----- Receive Data packets
                    // This loops until polling is completed. It may take a while, if it's
                    // a slow networking day.
                    while (!pollComplete)
                    {
                        Packet incomingPacket;
                        Peer incomingPeer;
                        int incomingPacketLength;

                        // Any events worth checking out?
                        if (clientENetHost.CheckEvents(out clientENetEvent) <= 0)
                        {
                            // If service time is met, break out of it.
                            if (clientENetHost.Service(setupInfo.PollTime, out clientENetEvent) <= 0) break;

                            // Poll is done.
                            pollComplete = true;
                        }

                        // Setup the packet references.
                        incomingPeer = clientENetEvent.Peer;

                        // Now, let's handle those events.
                        switch (clientENetEvent.Type)
                        {
                            case EventType.None:
                            default:
                                break;

                            case EventType.Connect:
                                ConnectionEvents.Enqueue(new IgnoranceConnectionEvent()
                                {
                                    NativePeerId = incomingPeer.ID,
                                    IP = incomingPeer.IP,
                                    Port = incomingPeer.Port
                                });
                                break;

                            case EventType.Disconnect:
                            case EventType.Timeout:
                                ConnectionEvents.Enqueue(new IgnoranceConnectionEvent()
                                {
                                    WasDisconnect = true
                                });
                                break;


                            case EventType.Receive:
                                // Receive event type usually includes a packet; so cache its reference.
                                incomingPacket = clientENetEvent.Packet;
                                if (!incomingPacket.IsSet)
                                {
                                    if (setupInfo.Verbosity > 0)
                                        Debug.LogWarning($"Client Worker Thread: A receive event did not supply us with a packet to work with. This should never happen.");
                                    break;
                                }
                                incomingPacketLength = incomingPacket.Length;

                                // Never consume more than we can have capacity for.
                                if (incomingPacketLength > setupInfo.PacketSizeLimit)
                                {
                                    if (setupInfo.Verbosity > 0)
                                        Debug.LogWarning($"Client Worker Thread: Received a packet too large, {incomingPacketLength} bytes while our limit is {setupInfo.PacketSizeLimit} bytes.");

                                    incomingPacket.Dispose();
                                    break;
                                }

                                // Grab a new fresh array from the ArrayPool, at least the length of our packet coming in.
                                byte[] storageBuffer;
                                if (incomingPacketLength <= 1200)
                                {
                                    // This will attempt to allocate us at least 1200 byte array. Which will most likely give us 2048 bytes
                                    // from ArrayPool's 2048 byte bucket.
                                    storageBuffer = ArrayPool<byte>.Shared.Rent(1200);
                                }
                                else if (incomingPacketLength <= 102400)
                                {
                                    storageBuffer = ArrayPool<byte>.Shared.Rent(incomingPacketLength);
                                }
                                else
                                {
                                    // If you get down here what the heck are you doing with UDP packets...
                                    // Let Unity GC spike and reap it later.

                                    // limit it to the maximum packet size set or we'll have an allocation attack vector.
                                    // vincenzo: [...] limit it to max packet size enet supports maybe 32 mb or less
                                    storageBuffer = new byte[incomingPacketLength];
                                }

                                // Copy the packet to the fresh array.
                                incomingPacket.CopyTo(storageBuffer);
                                incomingPacket.Dispose();

                                IgnoranceIncomingPacket incomingQueuePacket = new IgnoranceIncomingPacket
                                {
                                    WasRented = incomingPacketLength <= 102400 ? true : false,
                                    Channel = clientENetEvent.ChannelID,
                                    NativePeerId = incomingPeer.ID,
                                    Length = incomingPacketLength,
                                    RentedArray = storageBuffer
                                };

                                Incoming.Enqueue(incomingQueuePacket);
                                break;
                        }
                    }
                }

                // Flush the client and disconnect.
                clientPeer.Disconnect(0);
                clientENetHost.Flush();
            }

            // Deinitialize
            Library.Deinitialize();
            if (setupInfo.Verbosity > 0)
                Debug.Log("Client Worker Thread: Shutdown.");
        }

        // TODO: Optimize struct layout.
        private struct ThreadParamInfo
        {
            public int Channels;
            public int PollTime;
            public int Port;
            public int PacketSizeLimit;
            public int Verbosity;
            public string Address;
        }

        // TO BE CONTINUED...
        // <------
    }
}
