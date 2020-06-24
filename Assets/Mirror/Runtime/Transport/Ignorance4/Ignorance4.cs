/*
 * This file is part of the Ignorance 1.4.x Mirror Network Transport system.
 * Copyright (c) 2019 Matt Coburn (SoftwareGuy/Coburn64)
 * 
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace OiranStudio.Ignorance4
{
    public class Ignorance4 : Transport
    {
        [SerializeField] private PumpType TransportPumpMethod = PumpType.MirrorDefault;
        [SerializeField] private int MaximumPacketSize = 1200;
        [SerializeField] private int MaximumWorkBufferSize = 1048576;
        [SerializeField] private ushort CommunicationPort = 7777;

        [SerializeField] private bool DebugLoggingEnabled = true;

        // Channel setup...
        [Header("Channel Definitions")]
        public IgnoranceChannelTypes[] Channels = new IgnoranceChannelTypes[]
        {
            IgnoranceChannelTypes.Reliable,
            IgnoranceChannelTypes.Unreliable
        };

        protected Server ServerInstance = new Server();
        protected Client ClientInstance = new Client();

        private bool nativeInitialized = false;
        private bool clientConnected = false;

        // Used to determine if this tick had network polled or not.       
        private bool networkPumpedThisFrame = false;
        
        public override bool Available()
        {
            // Ignorance is not available on platforms that don't allow native binaries.
            // WebGL is one of those... sorry!
#if UNITY_WEBGL
            return false;
#else
            return true;
#endif
        }

        public override string ToString()
        {
            return "Ignorance 1.4 Experimental";
        }

        #region Transport - Client Functions
        public override void ClientConnect(string address)
        {
            if(!nativeInitialized)
            {
                if(!ENet.Library.Initialize())
                {
                    Debug.LogError("Failed to initialize Enet Native. Cannot continue.");
                    return;
                }
            }

            // Check if the client instance is null, if it is, create a new one...
            if (ClientInstance == null)
            {
                ClientInstance = new Client();
            }

            // Populate the client
            ClientInstance.EmittedLogString += ReceiveClientThreadLogs;

            ClientInstance.Address = address;
            ClientInstance.ChannelCount = Channels.Length > 255 ? (byte)255 : (byte)Channels.Length;
            ClientInstance.Port = CommunicationPort;
            ClientInstance.MaximumPacketSize = MaximumPacketSize;
            ClientInstance.MaximumBufferSize = MaximumWorkBufferSize;

            ClientInstance.StartClient();
        }

        private void ReceiveClientThreadLogs(bool threadDebug, string whatHappened)
        {
            if(threadDebug && DebugLoggingEnabled)
            {
                print($"Client instance thread: [THREAD DEBUG] {whatHappened}");
            } else
            {
                print($"Client instance thread: {whatHappened}");
            }
        }

        public override void ClientConnect(Uri uri)
        {
            if (uri.Scheme != Scheme)
                throw new ArgumentException($"Invalid uri {uri}, use {Scheme}://host:port instead", nameof(uri));

            if (!uri.IsDefaultPort)
            {
                // Set the communication port to the one specified.
                CommunicationPort = (ushort)uri.Port;
            }

            ClientConnect(uri.Host);
        }
        
        public override bool ClientConnected() => clientConnected;

        public override void ClientDisconnect()
        {
            if (ClientInstance != null)
            {
                print("Stopping Ignorance client...");
                ClientInstance.StopClient();
                ClientInstance.EmittedLogString -= ReceiveClientThreadLogs;
            }            
        }

        public override bool ClientSend(int channelId, ArraySegment<byte> segment)
        {
            if(ClientInstance != null)
            {
                ClientInstance.PlaceIntoQueue(channelId, segment);
            }

            return false;
        }

        public override int GetMaxPacketSize(int channelId = 0) => MaximumPacketSize;
        #endregion

        #region Transport - Server Functions
        public override bool ServerActive()
        {
            throw new NotImplementedException();
        }

        public override bool ServerDisconnect(int connectionId)
        {
            throw new NotImplementedException();
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            throw new NotImplementedException();
        }

        public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment)
        {
            throw new NotImplementedException();
        }

        public override void ServerStart()
        {
            throw new NotImplementedException();
        }

        public override void ServerStop()
        {
            throw new NotImplementedException();
        }

        public override Uri ServerUri()
        {
            throw new NotImplementedException();
        }

        public override void Shutdown()
        {
            // ServerInstance.StopServer();
            ClientInstance.StopClient();

            if (nativeInitialized)
            {
                ENet.Library.Deinitialize();
                nativeInitialized = false;
            }
        }
        #endregion

        #region Queue Handling Routines
        private bool ProcessClientMessageQueue()
        {
            if (ClientInstance != null)
            {
                while (Client.IncomingDataPackets.TryDequeue(out Definitions.IgnoranceDataPacket incomingPacket))
                {
                    switch (incomingPacket.Type)
                    {
                        case Definitions.PacketEventType.Connect:
                            clientConnected = true;
                            OnClientConnected?.Invoke();
                            // isClientConnected = true;
                            break;
                        case Definitions.PacketEventType.Disconnect:
                            // isClientConnected = false;
                            clientConnected = false;
                            OnClientDisconnected?.Invoke();
                            break;
                        case Definitions.PacketEventType.Data:
                            OnClientDataReceived?.Invoke(incomingPacket.Payload, incomingPacket.ChannelId);
                            // Release it, since we have consumed it.
                            System.Buffers.ArrayPool<byte>.Shared.Return(incomingPacket.Payload.Array);
                            break;
                    }
                }
            }

            return true;
        }

        private bool ProcessServerMessageQueue()
        {
            if (ServerInstance != null)
            {
                return true;
            }

            return false;
        }
        #endregion

        #region Mirror Transport Update Routines
        private void LateUpdate()
        {
            // Only run while the script is active.
            if (enabled)
            {
                // This method will short-circuit if LateUpdate is not set in the inspector.
                if (TransportPumpMethod != PumpType.MirrorDefault)
                {
                    networkPumpedThisFrame = false;
                    return;
                }

                // Set the network to be not completed.
                networkPumpedThisFrame = false;

                ProcessServerMessageQueue();
                ProcessClientMessageQueue();
            }

            // Set the network is complete flag
            networkPumpedThisFrame = true;
        }

        private void FixedUpdate()
        {
            if (enabled)
            {
                if (TransportPumpMethod != PumpType.FixedUpdate) return;

                if (networkPumpedThisFrame) return;

                ProcessServerMessageQueue();
                ProcessClientMessageQueue();
            }

            // Set the network is complete flag. Unity uses catch up FixedUpdate, so this would cause multi-pumps.
            // Which potentially could put Unity into a bad state of mind
            networkPumpedThisFrame = true;
        }
        #endregion

        public enum PumpType
        {
            MirrorDefault,
            FixedUpdate
        }

        // Backwards compatibility
        public int Port
        {
            get
            {
                return CommunicationPort;
            }
            set
            {
                if (value > ushort.MaxValue) CommunicationPort = ushort.MaxValue;
                else if (value < ushort.MinValue) CommunicationPort = ushort.MinValue;
                else CommunicationPort = (ushort)value;
            }
        }

        // Don't ever touch this.
        public const string Scheme = "enet";
        // I warned you!
    }
}
