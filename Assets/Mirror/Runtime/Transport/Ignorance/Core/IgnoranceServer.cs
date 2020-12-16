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
    public class IgnoranceServer
    {
        // Server Properties
        // - Bind Settings
        public string BindAddress = "127.0.0.1";
        public int BindPort = 7777;
        // - Maximum allowed channels, peers, etc.
        public int MaximumChannels = 2;
        public int MaximumPeers = 100;
        public int MaximumPacketSize = 33554432;    // ENet.cs: uint maxPacketSize = 32 * 1024 * 1024 = 33554432
        // - Native poll waiting time
        public int PollTime = 1;
        public int Verbosity = 1;

        public bool IsAlive => WorkerThread != null ? WorkerThread.IsAlive : false;

        private volatile bool CeaseOperation = false;

        // Queues
        public ConcurrentQueue<IgnoranceIncomingPacket> Incoming = new ConcurrentQueue<IgnoranceIncomingPacket>();
        public ConcurrentQueue<IgnoranceOutgoingPacket> Outgoing = new ConcurrentQueue<IgnoranceOutgoingPacket>();
        public ConcurrentQueue<IgnoranceCommandPacket> Commands = new ConcurrentQueue<IgnoranceCommandPacket>();
        public ConcurrentQueue<IgnoranceConnectionEvent> ConnectionEvents = new ConcurrentQueue<IgnoranceConnectionEvent>();

        // Thread
        private Thread WorkerThread;

        public void Start()
        {
            if (WorkerThread != null && WorkerThread.IsAlive)
            {
                // Cannot do that.
                Debug.LogError("A worker thread is already running. Cannot start another.");
                return;
            }

            CeaseOperation = false;
            ThreadParamInfo threadParams = new ThreadParamInfo()
            {
                Address = BindAddress,
                Port = BindPort,
                Peers = MaximumPeers,
                Channels = MaximumChannels,
                PollTime = PollTime,
                PacketSizeLimit = MaximumPacketSize,
                Verbosity = Verbosity
            };

            // Drain queues.
            if (Incoming != null) while (Incoming.TryDequeue(out _)) ;
            if (Outgoing != null) while (Outgoing.TryDequeue(out _)) ;
            if (Commands != null) while (Commands.TryDequeue(out _)) ;
            if (ConnectionEvents != null) while (ConnectionEvents.TryDequeue(out _));

            WorkerThread = new Thread(ThreadWorker);
            WorkerThread.Start(threadParams);

            // Announce
            if (Verbosity > 0)
                Debug.Log("Server has dispatched worker thread.");
        }

        public void Stop()
        {
            if (Verbosity > 0)
                Debug.Log("Telling server thread to stop, this may take a while depending on network load");
            CeaseOperation = true;
        }

        private void ThreadWorker(Object parameters)
        {
            if (Verbosity > 0)
                Debug.Log("Server thread has begun startup.");

            ThreadParamInfo setupInfo;
            Address serverAddress = new Address();
            Host serverENetHost;
            Event serverENetEvent;
            Peer[] serverPeerArray;

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
                if (Verbosity > 0)
                    Debug.Log("Server Worker Thread: ENet initialized.");
            }
            else
            {
                Debug.LogError("Server Worker Thread: Failed to initialize ENet.");
                return;
            }

            // Configure the server address.
            serverAddress.SetHost(setupInfo.Address);
            serverAddress.Port = (ushort)setupInfo.Port;
            serverPeerArray = new Peer[setupInfo.Peers];

            using (serverENetHost = new Host())
            {
                // Create the server object.
                serverENetHost.Create(serverAddress, setupInfo.Peers, setupInfo.Channels);

                // Loop until we're told to cease operations.
                while (!CeaseOperation)
                {
                    // Step One:
                    // ---> Sending to peers
                    while (Outgoing.TryDequeue(out IgnoranceOutgoingPacket outgoingPacket))
                    {
                        // Only create a packet if the server knows the peer.
                        if (serverPeerArray[outgoingPacket.NativePeerId].IsSet)
                        {
                            // Standard packet.
                            Packet packet = default;
                            packet.Create(outgoingPacket.RentedArray, outgoingPacket.Length, outgoingPacket.Flags);

                            // Send it to the peer.
                            int ret = serverPeerArray[outgoingPacket.NativePeerId].Send(outgoingPacket.Channel, ref packet);
                            if (ret < 0 && setupInfo.Verbosity > 0)
                                Debug.LogWarning($"Server Worker Thread: Failed sending a packet to Peer {outgoingPacket.NativePeerId}, error code {ret}");
                        }
                        else
                        {
                            if (setupInfo.Verbosity > 0)
                                Debug.LogWarning("Server Worker Thread: Can't send packet, a native peer is not set.");
                        }

                        // Cleanup.
                        if(outgoingPacket.WasRented)
                            ArrayPool<byte>.Shared.Return(outgoingPacket.RentedArray, true);
                        break;
                    }

                    // Step 2
                    // <--- Receiving from peers
                    bool pollComplete = false;

                    while (!pollComplete)
                    {
                        // Any events happening?
                        if (serverENetHost.CheckEvents(out serverENetEvent) <= 0)
                        {
                            // If service time is met, break out of it.
                            if (serverENetHost.Service(setupInfo.PollTime, out serverENetEvent) <= 0) break;

                            pollComplete = true;
                        }

                        switch (serverENetEvent.Type)
                        {
                            // Idle.
                            case EventType.None:
                            default:
                                break;

                            // Connection Event.
                            case EventType.Connect:
                                ConnectionEvents.Enqueue(new IgnoranceConnectionEvent
                                {
                                    NativePeerId = serverENetEvent.Peer.ID,
                                    IP = serverENetEvent.Peer.IP,
                                    Port = serverENetEvent.Peer.Port
                                });

                                // Assign a reference to the Peer.
                                serverPeerArray[serverENetEvent.Peer.ID] = serverENetEvent.Peer;
                                break;

                            // Disconnect/Timeout. Mirror doesn't care if it's either, so we lump them together.
                            case EventType.Disconnect:
                            case EventType.Timeout:
                                ConnectionEvents.Enqueue(new IgnoranceConnectionEvent
                                {
                                    WasDisconnect = true,
                                    NativePeerId = serverENetEvent.Peer.ID
                                });

                                // Can't null a Peer struct, but we can reset it, I guess.
                                // TODO: Does default work or can we just use 'new' ??
                                serverPeerArray[serverENetEvent.Peer.ID] = new Peer();
                                break;

                            case EventType.Receive:
                                // Firstly check if the packet is too big. If it is, do not process it - drop it.
                                if (serverENetEvent.Packet.Length > setupInfo.PacketSizeLimit)
                                {
                                    if (setupInfo.Verbosity > 0)
                                        Debug.LogWarning($"Server Worker Thread: Received a packet too big to process: {serverENetEvent.Packet.Length} bytes; limit: {setupInfo.PacketSizeLimit} byte(s).");

                                    serverENetEvent.Packet.Dispose();
                                    break;
                                }

                                // Grab a new fresh array from the ArrayPool, at least the length of our packet coming in.
                                // Try for 1200 (2048) pooled items first. If not, then we should try for 100KB (131072).
                                // Failing that, it's Unity's funeral. 1200 is the sane UDP packet buffer size. (source: FSE_Vincenzo, Mirror Discord)
                                // I could probably do that if in a one-liner but I'll leave it with commentary to explain what's going on (also stops me going insane debugging)
                                byte[] storageBuffer;
                                if (serverENetEvent.Packet.Length <= 1200)
                                {
                                    // This will attempt to allocate us at least 1200 byte array. Which will most likely give us 2048 bytes
                                    // from ArrayPool's 2048 byte bucket.
                                    storageBuffer = ArrayPool<byte>.Shared.Rent(1200);
                                }
                                else if (serverENetEvent.Packet.Length <= 102400)
                                {
                                    storageBuffer = ArrayPool<byte>.Shared.Rent(102400);
                                }
                                else
                                {
                                    // If you get down here what the heck are you doing with UDP packets...
                                    // Let Unity GC spike and reap it later.
                                    storageBuffer = new byte[serverENetEvent.Packet.Length];
                                }

                                // Grab a fresh struct.
                                IgnoranceIncomingPacket incomePacket = new IgnoranceIncomingPacket
                                {
                                    NativePeerId = serverENetEvent.Peer.ID,
                                    Channel = serverENetEvent.ChannelID,
                                    Length = serverENetEvent.Packet.Length,
                                    WasRented = serverENetEvent.Packet.Length <= 102400 ? true : false
                                };

                                // Copy the packet to the fresh array.
                                serverENetEvent.Packet.CopyTo(storageBuffer);
                                incomePacket.RentedArray = storageBuffer;

                                // Dispose of the original packet. We've got the data and everything, no need to hold onto it.
                                serverENetEvent.Packet.Dispose();

                                // Enqueue.
                                Incoming.Enqueue(incomePacket);
                                break;
                        }
                    }

                    // Step 3
                    // Command Handling
                    while (Commands.TryDequeue(out IgnoranceCommandPacket commandPacket))
                    {
                        switch (commandPacket.Type)
                        {
                            default:
                                break;

                            // Boot a Peer off the Server.
                            case IgnoranceCommandType.ServerKickPeer:
                                if (!serverPeerArray[commandPacket.PeerId].IsSet) continue;
                                serverPeerArray[commandPacket.PeerId].DisconnectNow(0);
                                break;
                        }
                    }
                }

                if (Verbosity > 0)
                    Debug.Log("Server thread is finishing up.");

                // Cleanup and flush everything.
                serverENetHost.Flush();

                // Kick everyone.
                for (int i = 0; i < serverPeerArray.Length; i++)
                {
                    if (!serverPeerArray[i].IsSet) continue;
                    serverPeerArray[i].DisconnectNow(0);
                }
            }

            if (setupInfo.Verbosity > 0)
                Debug.Log("Server Worker Thread: Shutdown.");
            Library.Deinitialize();
        }

        // TO BE CONTINUED...
        // <------

        private struct ThreadParamInfo
        {
            public string Address;
            public int Channels;
            public int Peers;
            public int PollTime;
            public int Port;
            public int PacketSizeLimit;
            public int Verbosity;
        }
    }
}
