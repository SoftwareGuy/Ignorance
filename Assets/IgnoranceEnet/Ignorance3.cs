using Ignorance.Enet;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ignorance
{
    public class Ignorance3 : Transport, ISegmentTransport, Native.IEnetServiceSubscriber, Native.IEnetClientSubscriber
    {
        // Transport Port setting
        public string BindAddress = "";
        public ushort Port = 7778;
        public bool DebugEnabled = false;

        public List<KnownChannelTypes> ChannelDefinitions = new List<KnownChannelTypes>()
        {
            KnownChannelTypes.ReliableSequenced,     // Default channel 0, reliable
            KnownChannelTypes.Unreliable,   // Default channel 1, unreliable
        };

        protected bool ServerInitialized = false;
        protected bool ServerStarted = false;
        protected bool ClientInitialized = false;
        protected bool ClientIsConnected = false;

        protected int MaximumClients = 1000;

        protected Dictionary<int, string> ConnectionAddresses = new Dictionary<int, string>();

        #region Update loop.
        public void LateUpdate()
        {
            if (enabled)
            {
                Native.ServiceUpdate(this);
                Native.ClientUpdate(this);
            }
        }
        #endregion

        #region Client Calls
        public override void ClientConnect(string address)
        {
            if (!Native.ClientSetup())
            {
                Debug.LogError("Client setup failed.");
                return;
            }

            ClientInitialized = true;

            if (!Enet.Native.ClientStart(address, Port))
            {
                Debug.Log($"Client failed to start. IP {address}, Port {Port}");
                return;
            }
        }

        public override bool ClientConnected()
        {
            return ClientInitialized && ClientIsConnected;
        }

        public override void ClientDisconnect()
        {
            if (!Native.ClientStop())
            {
                Debug.LogError("Failed to stop the client. You can probably expect shit to catch fire now.");
                return;
            }

            ClientIsConnected = false;
            ClientInitialized = false;
        }

        public override bool ClientSend(int channelId, byte[] data)
        {
            return ClientSend(channelId, new ArraySegment<byte>(data));
        }

        public bool ClientSend(int channelId, ArraySegment<byte> data)
        {
            if (channelId >= ChannelDefinitions.Count)
            {
                Debug.Log($"Refusing to even attempt to send data on channel {channelId}. It is either greater than or equal to the channel definition count.");
                return false;
            }

            if (!Native.ClientSend(data.Array, data.Count, channelId, MapKnownChannelTypeToENETPacketFlag(ChannelDefinitions[channelId])))
            {
                Debug.LogError($"Send failure: {data.Count} bytes to server!");
                return false;
            }

            return true;
        }
        #endregion

        #region Server Calls
        public override bool ServerActive()
        {
            return ServerInitialized && ServerStarted;
        }

        public override bool ServerDisconnect(int connectionId)
        {
            return Native.ServiceClose(connectionId);
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            if (ConnectionAddresses.TryGetValue(connectionId, out string clientAddr))
            {
                return clientAddr;
            }
            else
            {
                return string.Empty;
            }
        }

        public override bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            return ServerSend(connectionId, channelId, new ArraySegment<byte>(data));
        }

        public bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data)
        {
            if (channelId >= ChannelDefinitions.Count)
            {
                Debug.Log($"Refusing to even attempt to send data on channel {channelId}. It is either greater than or equal to the channel definition count.");
                return false;
            }

            if (!Native.ServiceSend(connectionId, data, channelId, MapKnownChannelTypeToENETPacketFlag(ChannelDefinitions[channelId])))
            {
                Debug.LogError($"Send failure: {data.Count} bytes to connection {connectionId}");
                return false;
            }

            return true;
        }

        public override void ServerStart()
        {
            // Setup the Ignorance 2.1 Server.
            if (!Native.ServiceSetup(MaximumClients))
            {
                ServerInitialized = false;
                Debug.LogError($"Failed to setup the native service with MaximumClients = {MaximumClients}");
                return;
            }

            ServerInitialized = true;
            Debug.Log($"Native service setup with MaximumClients = {MaximumClients}");

            // Give the server some data.
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
            {
                Debug.LogWarning("Macintosh detected, oh no. Workarounds engaged or something idk");
                BindAddress = "0.0.0.0";
            }
            else
            {
                if (string.IsNullOrEmpty(BindAddress))
                {
                    BindAddress = "0.0.0.0";
                }
            }

            if (!Native.ServiceStart(BindAddress, Port))
            {
                Debug.LogError($"Failed to start the service with IP = {BindAddress}, Port = {Port}");
                return;
            }

            Debug.Log($"Started the service with IP = {BindAddress}, Port = {Port}");
            ServerStarted = true;
        }

        public override void ServerStop()
        {
            if (!Native.ServiceStop())
            {
                Debug.LogError("Failed to stop the service. You can probably expect shit to catch fire now.");
                return;
            }

            ServerStarted = false;

            if (!Native.ServiceCleanup())
            {
                Debug.LogError("Failed to cleanup, unexpected behaviour may happen");
                return;
            }

            ServerInitialized = false;
        }

        public override void Shutdown()
        {
            Debug.Log("idk what i should do with shutdown yet lmao");
        }
        #endregion

        #region Ignorance-specific things
        public Ignorance3()
        {
            // Intentionally left blank.
        }

        public override int GetMaxPacketSize(int channelId = 0)
        {
            return IgnoranceConstants.MaximumPacketSize;
        }

        public override string ToString()
        {
            return "Ignorance 2.1";
        }

        public Native.PacketFlags MapKnownChannelTypeToENETPacketFlag(KnownChannelTypes source)
        {
            switch (source)
            {
                case KnownChannelTypes.ReliableSequenced:
                    return Native.PacketFlags.Reliable;         // reliable (tcp-like).
                case KnownChannelTypes.ReliableUnsequenced:
                    return (Native.PacketFlags.Reliable | Native.PacketFlags.Unordered);
                case KnownChannelTypes.Unreliable:
                    return Native.PacketFlags.Unordered;         // completely unreliable.
                case KnownChannelTypes.UnreliableSequenced:
                    return Native.PacketFlags.None;                // unreliable, but sequenced.
                default:
                    return Native.PacketFlags.Reliable;
            }
        }
        #endregion

        #region Native Callbacks
        // Server callbacks.

        // Started.
        public void OnEnetServiceStart()
        {
            Debug.Log("Server started");
            ServerStarted = true;
        }

        // Stopped.
        public void OnEnetServiceStop()
        {
            Debug.Log("Server stopped");
            ServerStarted = false;
        }

        // Server incoming connection.
        public void OnEnetServiceOpen(in Enet.Native.EventClientOpen evt)
        {
            if (DebugEnabled)
            {
                Debug.Log($"Connecting client: ID {evt.client_id} assigned to IP {evt.ip} Port {evt.port}");
            }

            // Invoke Mirror's connection event.
            ConnectionAddresses.Add(evt.client_id, evt.ip);
            OnServerConnected.Invoke(evt.client_id);
        }

        // Server incoming connection closed/timed out.
        public void OnEnetServiceClose(in Enet.Native.EventClientClose evt)
        {
            if (DebugEnabled)
            {
                Debug.Log($"Disconnecting client: ID {evt.client_id}");
            }

            // Invoke Mirror's disconnection event.
            ConnectionAddresses.Remove(evt.client_id);
            OnServerDisconnected.Invoke(evt.client_id);
        }

        // Server receiving some data from a incoming connection.
        public void OnEnetServiceRecv(in Enet.Native.EventClientRecv evt)
        {
            if (DebugEnabled)
            {
                Debug.Log($"OnServiceClientRead: ID {evt.client_id}, data length {evt.buffer.Length}");
            }

            OnServerDataReceived.Invoke(evt.client_id, evt.buffer.ToArray());
        }

        // Server ate a spicy meatball.
        public void OnEnetServiceError(in Enet.Native.EventError evt)
        {
            Debug.Log($"OnServiceError: FATAL ERROR!! Code {evt.error_code} ({evt.error_string})");
            ServerStop();
        }

        // Client calbacks.
        public void OnEnetClientStart()
        {
            Debug.Log("Client Started");
        }

        public void OnEnetClientStop()
        {
            Debug.Log("Client Stopped");
        }

        public void OnEnetClientOpen(in Native.EventClientOpen evt)
        {
            Debug.Log("Client connected");
            ClientIsConnected = true;
            OnClientConnected.Invoke();
        }

        public void OnEnetClientClose(in Native.EventClientClose evt)
        {
            Debug.Log("Client disconnected");
            ClientIsConnected = false;
            OnClientDisconnected.Invoke();
        }

        public void OnEnetClientRecv(in Native.EventClientRecv evt)
        {
            OnClientDataReceived.Invoke(evt.buffer.ToArray());
        }

        public void OnEnetClientError(in Native.EventError evt)
        {
            OnClientError.Invoke(new Exception($"ENET Client Error. Code {evt.error_code}, {evt.error_string}."));
        }
        #endregion
    }

}

