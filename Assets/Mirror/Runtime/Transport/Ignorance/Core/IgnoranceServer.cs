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
        public int MaximumPacketSize = 1200;
        // - Native poll waiting time
        public int PollTime = 1;
        public int Verbosity = 1;

        public bool IsAlive => WorkerThread != null ? WorkerThread.IsAlive : false;

        private volatile bool CeaseOperation = false;

        // Queues
        public ConcurrentQueue<IgnorancePacket> Incoming = new ConcurrentQueue<IgnorancePacket>();
        public ConcurrentQueue<IgnorancePacket> Outgoing = new ConcurrentQueue<IgnorancePacket>();

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
            if (Outgoing != null) while (Incoming.TryDequeue(out _)) ;

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
            Debug.Log("Server thread startup.");

            ThreadParamInfo setupInfo;
            Address serverAddress = new Address();
            Host serverENetHost;
            Event serverENetEvent;
            Peer[] serverPeerArray;

            // Grab the setup information.
            try
            {
                // Attempt to cast it back into our setupInfo
                // This helps avoid a lot of other bullshit.
                setupInfo = (ThreadParamInfo)parameters;
            }
            catch (InvalidCastException)
            {
                // Failure.
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
                    bool pollComplete = false;

                    // ---> Sending to peers
                    while (Outgoing.TryDequeue(out IgnorancePacket outgoingPacket))
                    {
                        if (!outgoingPacket.Outgoing) continue;

                        // Debug.Log($"Okay, got one packet to dispatch - {outgoingPacket.Type}");

                        switch (outgoingPacket.Type)
                        {
                            case IgnorancePacketType.ServerClientKick:
                                // Boot the peer off the server.
                                uint kickee = outgoingPacket.PeerData.NativePeerId;
                                if (serverPeerArray[kickee].IsSet)
                                    serverPeerArray[kickee].DisconnectNow(0);
                                break;

                            default:
                                // Standard packet.
                                Packet packet = default;
                                packet.Create(outgoingPacket.RentedByteArray, outgoingPacket.Length, outgoingPacket.Flags);

                                if (serverPeerArray[outgoingPacket.PeerData.NativePeerId].IsSet)
                                {
                                    // Send it to the peer.
                                    int ret = serverPeerArray[outgoingPacket.PeerData.NativePeerId].Send(outgoingPacket.Channel, ref packet);
                                    if (ret < 0 && setupInfo.Verbosity > 0)
                                        Debug.LogWarning($"Server Worker Thread: Failed sending a packet to Peer {outgoingPacket.PeerData.NativePeerId}, error code {ret}");

                                    // Debug.Log("Dispatch OK");
                                }
                                else
                                {
                                    if (setupInfo.Verbosity > 0)
                                        Debug.LogWarning("Server Worker Thread: Can't send packet, a native peer is not set.");
                                }

                                // Cleanup.
                                ArrayPool<byte>.Shared.Return(outgoingPacket.RentedByteArray, true);
                                break;
                        }
                    }

                    // <--- Receiving from peers
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
                            case EventType.None:
                            default:
                                break;

                            case EventType.Connect:
                                Incoming.Enqueue(new IgnorancePacket()
                                {
                                    Type = IgnorancePacketType.ServerConnect,
                                    PeerData = new PeerConnectionData
                                    {
                                        NativePeerId = serverENetEvent.Peer.ID,
                                        IP = serverENetEvent.Peer.IP,
                                        Port = serverENetEvent.Peer.Port
                                    }
                                });

                                serverPeerArray[serverENetEvent.Peer.ID] = serverENetEvent.Peer;

                                break;

                            case EventType.Disconnect:
                            case EventType.Timeout:
                                Incoming.Enqueue(new IgnorancePacket
                                {
                                    Type = IgnorancePacketType.ServerDisconnect,
                                    PeerData = new PeerConnectionData
                                    {
                                        NativePeerId = serverENetEvent.Peer.ID
                                    }
                                });

                                // Can't null a Peer struct, but we can reset it, I guess.
                                // TODO: Does default work or can we just use 'new' ??
                                serverPeerArray[serverENetEvent.Peer.ID] = new Peer();
                                break;

                            case EventType.Receive:
                                // Firstly check if the packet is too big. If it is, do not process it - drop it.
                                if (serverENetEvent.Packet.Length > setupInfo.PacketSizeLimit)
                                {
                                    if(setupInfo.Verbosity > 0)
                                        Debug.LogWarning($"Server Worker Thread: Received a packet too big to process: {serverENetEvent.Packet.Length} bytes; limit: {setupInfo.PacketSizeLimit} byte(s).");

                                    serverENetEvent.Packet.Dispose();
                                    break;
                                }

                                // Grab a new fresh array from the ArrayPool, at least the length of our packet coming in.                         
                                byte[] storageBuffer = ArrayPool<byte>.Shared.Rent(serverENetEvent.Packet.Length > 1200 ? serverENetEvent.Packet.Length : 1200);

                                // Grab a fresh struct.
                                IgnorancePacket dispatchPacket = new IgnorancePacket
                                {
                                    Type = IgnorancePacketType.ServerData,
                                    Length = serverENetEvent.Packet.Length,
                                    PeerData = new PeerConnectionData {
                                        NativePeerId = serverENetEvent.Peer.ID
                                    }
                                };

                                // Copy the packet to the fresh array.
                                serverENetEvent.Packet.CopyTo(storageBuffer);
                                dispatchPacket.RentedByteArray = storageBuffer;

                                // Dispose of the original packet. We've got the data and everything, no need to hold onto it.
                                serverENetEvent.Packet.Dispose();

                                // Enqueue.
                                Incoming.Enqueue(dispatchPacket);
                                break;
                        }
                    }
                }

                // Cleanup and flush everything.
                serverENetHost.Flush();
                // Kick everyone.
                for (int i = 0; i < serverPeerArray.Length; i++) 
                {
                    if (!serverPeerArray[i].IsSet) continue;
                    serverPeerArray[i].DisconnectNow(0);
                }
            }
            
            if(setupInfo.Verbosity > 0)
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
