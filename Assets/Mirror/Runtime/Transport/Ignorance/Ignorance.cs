// Ignorance 1.4.x
// Ignorance. It really kicks the Unity LLAPIs ass.
// https://github.com/SoftwareGuy/Ignorance
// -----------------
// Copyright (c) 2019 - 2020 Matt Coburn (SoftwareGuy/Coburn64)
// Ignorance Transport is licensed under the MIT license. Refer
// to the LICENSE file for more information.
// -----------------
// Ignorance Experimental (New) Version
// -----------------
using ENet;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IgnoranceTransport
{
    [DisallowMultipleComponent]
    public class Ignorance : Transport
    {
        #region Inspector options
        public int port = 7777;

        [Header("Debug & Logging Configuration")]
        [Tooltip("How verbose do you want Ignorance to be?")]
        public IgnoranceLogType LogType = IgnoranceLogType.Standard;
        [Tooltip("Uses OnGUI to present you with statistics for Server and Client backend instances.")]
        public bool DebugDisplay = false;

        [Header("Server Configuration")]
        [Tooltip("Should the server bind to all interfaces?")]
        public bool serverBindsAll = true;
        [Tooltip("This is only used if Server Binds All is unticked.")]
        public string serverBindAddress = string.Empty;
        [Tooltip("This tells ENet how many Peer slots to create. Helps performance, avoids looping over huge native arrays. Recommended: Max Mirror players, rounded to nearest 10. (Example: 16 -> 20).")]
        public int serverMaxPeerCapacity = 50;
        [Tooltip("How long ENet waits in native world. The higher this value, the more CPU usage. Lower values may/may not impact performance at high packet load.")]
        public int serverMaxNativeWaitTime = 1;

        [Header("Client Configuration")]
        [Tooltip("How long ENet waits in native world. The higher this value, the more CPU usage used. This is for the client, unlike the one above. Higher value probably trades CPU for more responsive networking.")]
        public int clientMaxNativeWaitTime = 3;
        [Tooltip("Interval between asking ENet for client status updates. Set to -1 to disable.")]
        public int clientStatusUpdateInterval = -1;

        [Header("Channel Configuration")]
        [Tooltip("You must define your channels in the array shown here, otherwise ENet will not know what channel delivery type to use.")]
        public IgnoranceChannelTypes[] Channels;

        [Header("Low-level Tweaking")]
        [Tooltip("Used internally to keep allocations to a minimum with ArrayPool. Don't touch this unless you know what you're doing.")]
        public int PacketBufferCapacity = 4096;

        [Tooltip("For UDP based protocols, it's best to keep your data under the safe MTU of 1200 bytes. You can increase this, however beware this may open you up to allocation attacks.")]
        public int MaxAllowedPacketSize = 33554432;
        #endregion

        #region Public Statistics
        public IgnoranceClientStats ClientStatistics;
        #endregion

#if MIRROR_26_0_OR_NEWER
        public override bool Available()
        {
            // Ignorance is not available for Unity WebGL, the PS4 (no dev kit to confirm) or Switch (port exists but I have no access to said code).
            // Ignorance is available for most other operating systems.
#if (UNITY_WEBGL || UNITY_PS4 || UNITY_SWITCH)
            return false;
#else
            return true;
#endif
        }

        public void Awake()
        {
            if (LogType != IgnoranceLogType.Nothing)
                print($"Thanks for using Ignorance {IgnoranceInternals.Version}. Keep up to date, report bugs and support the developer at https://github.com/SoftwareGuy/Ignorance!");
        }

        public override string ToString()
        {
            return $"Ignorance v{IgnoranceInternals.Version}";
        }

        public override void ClientConnect(string address)
        {
            ClientState = ConnectionState.Connecting;
            cachedConnectionAddress = address;

            // Initialize.
            InitializeClientBackend();

            // Get going.            
            ignoreDataPackets = false;
            NextStatusRequestUpdate = 0f;

            // Start!
            Client.Start();
        }

        public override void ClientConnect(Uri uri)
        {
            if (uri.Scheme != IgnoranceInternals.Scheme)
                throw new ArgumentException($"You used an invalid URI: {uri}. Please use {IgnoranceInternals.Scheme}://host:port instead", nameof(uri));

            if (!uri.IsDefaultPort)
            {
                // Set the communication port to the one specified.
                port = uri.Port;
            }

            ClientConnect(uri.Host);
        }

        public override bool ClientConnected() => ClientState == ConnectionState.Connected;

        public override void ClientDisconnect()
        {
            if (Client != null)
            {
                // Client.Commands.Enqueue(new IgnoranceCommandPacket { Type = IgnoranceCommandType.ClientWantsToStop });
                Client.Stop();
            }

            ignoreDataPackets = true;
            ClientState = ConnectionState.Disconnected;
        }

        public override void ClientSend(int channelId, ArraySegment<byte> segment)
        {
            if (Client == null)
            {
                Debug.LogError("Client object is null, this shouldn't really happen but it did...");
                return;
            }

            if (channelId < 0 || channelId > Channels.Length)
            {
                Debug.LogError("Channel ID is out of bounds.");
                return;
            }

            // Packet Struct
            Packet clientOutgoingPacket = default;
            int byteCount = segment.Count;
            int byteOffset = segment.Offset;
            PacketFlags desiredFlags = (PacketFlags)Channels[channelId];

            // Warn if over recommended MTU
            bool flagsSet = (desiredFlags & ReliableOrUnreliableFragmented) > 0;

            if (LogType != IgnoranceLogType.Nothing && byteCount > 1200 && !flagsSet)
                Debug.LogWarning($"Warning: Server trying to send a Unreliable packet bigger than the recommended ENet 1200 byte MTU ({segment.Count} > 1200). ENet will force Reliable Fragmented delivery.");

            // Create the packet.
            clientOutgoingPacket.Create(segment.Array, byteOffset, byteCount + byteOffset, desiredFlags);
            // byteCount

            // Enqueue the packet.
            IgnoranceOutgoingPacket dispatchPacket = new IgnoranceOutgoingPacket
            {
                Channel = (byte)channelId,
                Payload = clientOutgoingPacket
            };

            Client.Outgoing.Enqueue(dispatchPacket);
        }

        public override bool ServerActive()
        {
            return Server != null && Server.IsAlive;
        }

        public override bool ServerDisconnect(int connectionId)
        {
            // Debug.LogWarning("TODO: Server Disconnect");

            if (Server == null)
            {
                Debug.LogError("Server object is null, this shouldn't really happen but it did...");
                return false;
            }

            IgnoranceCommandPacket kickPacket = new IgnoranceCommandPacket
            {
                Type = IgnoranceCommandType.ServerKickPeer,
                PeerId = (uint)connectionId - 1 // ENet's native peer ID will be ConnID - 1
            };

            Server.Commands.Enqueue(kickPacket);

            return true;
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            if (ConnectionLookupDict.TryGetValue(connectionId, out PeerConnectionData details))
                return $"{details.IP}:{details.Port}";

            return "(unavailable)";
        }

        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            // Debug.Log($"ServerSend({connectionId}, {channelId}, <{segment.Count} byte segment>)");

            if (Server == null)
            {
                Debug.LogError("Server object is null, this shouldn't really happen but it did...");
                return;
            }

            if (channelId < 0 || channelId > Channels.Length)
            {
                Debug.LogError("Channel ID is out of bounds.");
                return;
            }

            // Packet Struct
            Packet serverOutgoingPacket = default;
            int byteCount = segment.Count;
            int byteOffset = segment.Offset;
            PacketFlags desiredFlags = (PacketFlags)Channels[channelId];

            // Warn if over recommended MTU
            bool flagsSet = (desiredFlags & ReliableOrUnreliableFragmented) > 0;

            if (LogType != IgnoranceLogType.Nothing && byteCount > 1200 && !flagsSet)
                Debug.LogWarning($"Warning: Server trying to send a Unreliable packet bigger than the recommended ENet 1200 byte MTU ({segment.Count} > 1200). ENet will force Reliable Fragmented delivery.");

            // Create the packet.
            serverOutgoingPacket.Create(segment.Array, byteOffset, byteCount + byteOffset, (PacketFlags)Channels[channelId]);

            // Enqueue the packet.
            IgnoranceOutgoingPacket dispatchPacket = new IgnoranceOutgoingPacket
            {
                Channel = (byte)channelId,
                NativePeerId = (uint)connectionId - 1, // ENet's native peer ID will be ConnID - 1
                Payload = serverOutgoingPacket
            };

            Server.Outgoing.Enqueue(dispatchPacket);

        }

        public override void ServerStart()
        {
            if (LogType != IgnoranceLogType.Nothing)
                print("Ignorance Server instance is starting up...");

            InitializeServerBackend();

            Server.Start();
        }

        public override void ServerStop()
        {
            if (Server != null)
            {
                if (LogType != IgnoranceLogType.Nothing)
                    print("Ignorance Server instance is shutting down...");

                Server.Stop();
            }

            // ENetPeerToMirrorLookup.Clear();
            ConnectionLookupDict.Clear();
        }

        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder
            {
                Scheme = IgnoranceInternals.Scheme,
                Host = serverBindAddress,
                Port = port
            };

            return builder.Uri;
        }

        public override void Shutdown()
        {

        }

        // Check to ensure channels 0 and 1 mimic LLAPI. Override this at your own risk.
        private void OnValidate()
        {
            if (Channels != null && Channels.Length >= 2)
            {
                // Check to make sure that Channel 0 and 1 are correct.
                if (Channels[0] != IgnoranceChannelTypes.Reliable) Channels[0] = IgnoranceChannelTypes.Reliable;
                if (Channels[1] != IgnoranceChannelTypes.Unreliable) Channels[1] = IgnoranceChannelTypes.Unreliable;
            }
            else
            {
                Channels = new IgnoranceChannelTypes[2]
                {
                    IgnoranceChannelTypes.Reliable,
                    IgnoranceChannelTypes.Unreliable
                };
            }

            // ENet only supports a maximum of 32MB packet size.
            if (MaxAllowedPacketSize > 33554432) MaxAllowedPacketSize = 33554432;
        }

        #region Inner workings
        private bool ignoreDataPackets;
        private string cachedConnectionAddress = string.Empty;
        private IgnoranceServer Server = new IgnoranceServer();
        private IgnoranceClient Client = new IgnoranceClient();

        private Dictionary<int, PeerConnectionData> ConnectionLookupDict = new Dictionary<int, PeerConnectionData>();
        // private Dictionary<uint, int> ENetPeerToMirrorLookup = new Dictionary<uint, int>();
        // private int ConnId = 1;
        private float NextStatusRequestUpdate = 0f;

        private void InitializeServerBackend()
        {
            if (Server == null)
            {
                Debug.LogWarning("IgnoranceServer reference for Server mode was null. This shouldn't happen, but to be safe we'll reinitialize it.");
                Server = new IgnoranceServer();
            }

            // Set up the new IgnoranceServer reference.
            if (serverBindsAll)
                // MacOS is special. It's also a massive thorn in my backside.
                Server.BindAddress = IgnoranceInternals.BindAllFuckingAppleMacs;
            else
                // Use the supplied bind address.
                Server.BindAddress = serverBindAddress;

            Server.BindPort = port;
            Server.MaximumPeers = serverMaxPeerCapacity;
            Server.MaximumChannels = Channels.Length;
            Server.PollTime = serverMaxNativeWaitTime;
            Server.MaximumPacketSize = MaxAllowedPacketSize;

            // Initializes the packet buffer.
            if (InternalPacketBuffer == null)
                InternalPacketBuffer = new byte[PacketBufferCapacity];
        }

        private void InitializeClientBackend()
        {
            if (Client == null)
            {
                Debug.LogWarning("Ignorance: IgnoranceClient reference for Client mode was null. This shouldn't happen, but to be safe we'll reinitialize it.");
                Client = new IgnoranceClient();
            }

            Client.ConnectAddress = cachedConnectionAddress;
            Client.ConnectPort = port;
            Client.ExpectedChannels = Channels.Length;
            Client.PollTime = clientMaxNativeWaitTime;
            Client.MaximumPacketSize = MaxAllowedPacketSize;
            Client.Verbosity = (int)LogType;

            // Initializes the packet buffer.
            if (InternalPacketBuffer == null)
                InternalPacketBuffer = new byte[PacketBufferCapacity];
        }

        private void ProcessServerPackets()
        {
            IgnoranceIncomingPacket incomingPacket;
            IgnoranceConnectionEvent connectionEvent;
            int adjustedConnectionId;

            // print($"Reached processing Server ConnectionEvents queue.");
            // incoming connection events.
            while (Server.ConnectionEvents.TryDequeue(out connectionEvent))
            {
                adjustedConnectionId = (int)connectionEvent.NativePeerId + 1;

                if (LogType == IgnoranceLogType.Verbose)
                    print($"Processing a server connection event from ENet native peer {connectionEvent.NativePeerId}.");

                // Was this a Disconnection?
                if (connectionEvent.WasDisconnect)
                {
                    if (LogType == IgnoranceLogType.Verbose)
                        print($"ProcessServerPackets fired; handling disconnection event from native peer {connectionEvent.NativePeerId}.");

                    // If it doesn't exist in our dictionary, then it's likely a ghost or malicious.
                    /* if (!ENetPeerToMirrorLookup.ContainsKey(connectionEvent.NativePeerId))
                    {
                        Debug.LogWarning("Disconnection event from unknown peer");
                        return;
                    }
                    */

                    // int key = (int)connectionEvent.NativePeerId + 1;

                    ConnectionLookupDict.Remove(adjustedConnectionId);
                    // ENetPeerToMirrorLookup.Remove(connectionEvent.NativePeerId);

                    // Invoke Mirror handler.
                    OnServerDisconnected?.Invoke(adjustedConnectionId);
                }
                else
                {
                    // Nah mate, just a regular connection.
                    if (LogType == IgnoranceLogType.Verbose)
                        print($"ProcessServerPackets fired; handling connection event from native peer {connectionEvent.NativePeerId}. This peer would be Mirror ConnID {adjustedConnectionId}.");

                    ConnectionLookupDict.Add(adjustedConnectionId, new PeerConnectionData
                    {
                        NativePeerId = connectionEvent.NativePeerId,
                        IP = connectionEvent.IP,
                        Port = connectionEvent.Port
                    });

                    /*
                    if (ENetPeerToMirrorLookup.ContainsKey(connectionEvent.NativePeerId))
                    {
                        Debug.LogWarning($"This is weird - we already know Native Peer {connectionEvent.NativePeerId} as Conn {ConnId}. Replacing, but this may cause issues.");
                        ENetPeerToMirrorLookup[connectionEvent.NativePeerId] = ConnId;
                    }
                    else
                    {
                        ENetPeerToMirrorLookup.Add(connectionEvent.NativePeerId, ConnId);
                    }
                    */

                    OnServerConnected?.Invoke(adjustedConnectionId);
                    // ConnId++;
                }
            }

            // Handle incoming data packets.
            // Console.WriteLine($"Server Incoming Queue is {Server.Incoming.Count}");
            while (Server.Incoming.TryDequeue(out incomingPacket))
            {
                adjustedConnectionId = (int)incomingPacket.NativePeerId + 1;

                // print($"Server got one. It's a {incomingPacket.Type}");
                // if (ENetPeerToMirrorLookup.ContainsKey(incomingPacket.NativePeerId))
                // {
                // We know who's it is from, let's process it.                 
                // int conn = ENetPeerToMirrorLookup[incomingPacket.NativePeerId];
                int length = incomingPacket.Payload.Length;

                // Copy to working buffer and dispose of it.
                incomingPacket.Payload.CopyTo(InternalPacketBuffer);
                incomingPacket.Payload.Dispose();

                ArraySegment<byte> dataSegment = new ArraySegment<byte>(InternalPacketBuffer, 0, length);
                OnServerDataReceived?.Invoke(adjustedConnectionId, dataSegment, incomingPacket.Channel);
                // }
                // else
                // {
                //    Debug.LogWarning($"Data received from ENet Peer {incomingPacket.NativePeerId} but this isn't in our lookup.");
                // }

                // Some messages can disable the transport
                // If the transport was disabled by any of the messages, we have to break out of the loop and wait until we've been re-enabled.
                if (!enabled)
                    break;
            }
        }

        private void ProcessServerConnectionEvents()
        {
            ;
        }

        private void ProcessClientPackets()
        {
            IgnoranceIncomingPacket incomingPacket;
            IgnoranceCommandPacket commandPacket;
            IgnoranceClientStats clientStats;

            while (Client.Incoming.TryDequeue(out incomingPacket))
            {
                // Temporary fix: if ENet thread is too fast for Mirror, then ignore the packet.
                // This is seen sometimes if you stop the client and there's still stuff in the queue.
                if (ignoreDataPackets)
                {
                    if (LogType == IgnoranceLogType.Verbose)
                        print("ProcessClientPackets cycle skipped; ignoring data packet");
                    break;
                }


                // Otherwise client recieved data, advise Mirror.
                // print($"Byte array: {incomingPacket.RentedByteArray.Length}. Packet Length: {incomingPacket.Length}");
                int length = incomingPacket.Payload.Length;

                // Copy to working buffer and dispose of it.
                incomingPacket.Payload.CopyTo(InternalPacketBuffer);
                incomingPacket.Payload.Dispose();

                ArraySegment<byte> dataSegment = new ArraySegment<byte>(InternalPacketBuffer, 0, length);
                OnClientDataReceived?.Invoke(dataSegment, incomingPacket.Channel);

                // Some messages can disable the transport
                // If the transport was disabled by any of the messages, we have to break out of the loop and wait until we've been re-enabled.
                if (!enabled)
                    break;
            }

            // Step 3: Handle other commands.
            while (Client.Commands.TryDequeue(out commandPacket))
            {
                switch (commandPacket.Type)
                {
                    // ...
                    default:
                        break;
                }
            }

            // Step 4: Handle status updates.
            if (Client.StatusUpdates.TryDequeue(out clientStats))
            {
                ClientStatistics = clientStats;
            }
        }

        private void ProcessClientConnectionEvents()
        {
            IgnoranceConnectionEvent connectionEvent;

            // Step 2: Handle connection events.
            while (Client.ConnectionEvents.TryDequeue(out connectionEvent))
            {
                if (LogType == IgnoranceLogType.Verbose)
                    print($"ProcessClientConnectionEvents fired: processing a client ConnectionEvents queue item.");

                if (connectionEvent.WasDisconnect)
                {
                    // Disconnected from server.
                    ClientState = ConnectionState.Disconnected;

                    if (LogType != IgnoranceLogType.Nothing)
                        print($"Client has been disconnected from server.");

                    ignoreDataPackets = true;
                    OnClientDisconnected?.Invoke();
                }
                else
                {
                    // Connected to server.
                    ClientState = ConnectionState.Connected;

                    if (LogType != IgnoranceLogType.Nothing)
                        print($"Client successfully connected to server: {connectionEvent.IP}:{connectionEvent.Port}");

                    ignoreDataPackets = false;
                    OnClientConnected?.Invoke();
                }
            }
        }

        private void LateUpdate()
        {
            if (enabled)
            {
                if (Server.IsAlive)
                {
                    ProcessServerConnectionEvents();
                    ProcessServerPackets();
                }

                if (Client.IsAlive)
                {
                    ProcessClientConnectionEvents();
                    ProcessClientPackets();

                    if (ClientState == ConnectionState.Connected && clientStatusUpdateInterval > -1 && Time.time >= NextStatusRequestUpdate)
                    {
                        Client.Commands.Enqueue(new IgnoranceCommandPacket { Type = IgnoranceCommandType.ClientRequestsStatusUpdate });
                        NextStatusRequestUpdate = Time.time + clientStatusUpdateInterval;
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (DebugDisplay)
                GUI.Box(new Rect(
                    new Vector2(32, Screen.height - 240), new Vector2(200, 160)),

                    "-- CLIENT --\n" +
                    $"State: {ClientState}\n" +
                    $"Incoming Queue: {Client.Incoming.Count}\n" +
                    $"Outgoing Queue: {Client.Outgoing.Count}\n\n" +

                    "-- SERVER --\n" +
                    $"Incoming Queue: {Server.Incoming.Count}\n" +
                    $"Outgoing Queue: {Server.Outgoing.Count}\n" +
                    $"ConnEvent Queue: {Server.ConnectionEvents.Count}"
                );
        }
        #endregion

        public override int GetMaxPacketSize(int channelId = 0) => MaxAllowedPacketSize;

#if IGNORANCE_BATCHING
        // UDP Recommended Max MTU = 1200.
        public override int GetMaxBatchSize(int channelId) => 1200;
#endif

        private enum ConnectionState { Connecting, Connected, Disconnecting, Disconnected }
        private ConnectionState ClientState = ConnectionState.Disconnected;
        private byte[] InternalPacketBuffer;

        private const PacketFlags ReliableOrUnreliableFragmented = PacketFlags.Reliable | PacketFlags.UnreliableFragmented;
#endif

    }
}
