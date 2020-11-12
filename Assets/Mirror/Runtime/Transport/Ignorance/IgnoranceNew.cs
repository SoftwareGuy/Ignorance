// Ignorance 1.3.x
// Ignorance. It really kicks the Unity LLAPIs ass.
// https://github.com/SoftwareGuy/Ignorance
// -----------------
// Copyright (c) 2019 - 2020 Matt Coburn (SoftwareGuy/Coburn64)
// Ignorance Transport is licensed under the MIT license. Refer
// to the LICENSE file for more information.
// -----------------
// Ignorance Experimental (New) Version
// -----------------
using UnityEngine;
using Mirror;
using ENet;
using System;

namespace Mirror
{
    public class IgnoranceNew : Transport
    {
        #region Inspector options
        public int port = 7777;

        [Header("Server Configuration")]
        [Tooltip("Should the server bind to all interfaces?")]
        public bool serverBindsAll = true;
        [Tooltip("This is only used if Server Binds All is unticked.")]
        public string serverBindAddress = string.Empty;
        [Tooltip("This tells ENet how many Peer slots to create. Helps performance, avoids looping over huge native arrays. Recommended: Max Mirror players, rounded to nearest 10. (Example: 16 -> 20).")]
        public int serverMaxPeerCapacity = 100;
        [Tooltip("How long ENet waits in native world. The higher this value, the more CPU usage. Lower values may/may not impact performance at high packet load.")]
        public int serverMaxNativeWaitTime = 1;

        [Header("Client Configuration")]
        [Tooltip("How long ENet waits in native world. The higher this value, the more CPU usage used. This is for the client, unlike the one above. Higher value probably trades CPU for more responsive networking.")]
        public int clientMaxNativeWaitTime = 3;

        [Header("Channel Configuration")]
        [Tooltip("You must define your channels in the array shown here, otherwise ENet will not know what channel delivery type to use.")]
        public IgnoranceChannelTypes[] Channels;

        [Header("Low-level Tweaking")]
        [Tooltip("For UDP based protocols, it's best to keep your data under the safe MTU of 1200 bytes. You can increase this, however beware this may open you up to allocation attacks.")]
        public int MaximumPacketSize = 1200;
        #endregion

#if MIRROR_26_0_OR_NEWER
        public override bool Available()
        {
            // Ignorance is not available for Unity WebGL, the PS4 (no dev kit to confirm).
            // Ignorance is available for most other operating systems.
#if (UNITY_WEBGL || UNITY_PS4)
            return false;
#else
            return true;
#endif
        }

        public void Awake()
        {
            print($"Thanks for using Ignorance {IgnoranceInternals.Version}. Keep up to date, report bugs and support the developer at https://github.com/SoftwareGuy/Ignorance!");
        }

        public override void ClientConnect(string address)
        {
            throw new NotImplementedException();
        }
        public override void ClientConnect(Uri uri)
        {
            if (uri.Scheme != IgnoranceInternals.Scheme)
                throw new ArgumentException($"Ignorance: You used an invalid URI: {uri}. Please use {IgnoranceInternals.Scheme}://host:port instead", nameof(uri));

            if (!uri.IsDefaultPort)
            {
                // Set the communication port to the one specified.
                port = uri.Port;
            }

            ClientConnect(uri.Host);
        }

        public override bool ClientConnected()
        {
            throw new NotImplementedException();
        }

        public override void ClientDisconnect()
        {
            throw new NotImplementedException();
        }

        public override void ClientSend(int channelId, ArraySegment<byte> segment)
        {
            throw new NotImplementedException();
        }

        public override int GetMaxPacketSize(int channelId = 0) => MaximumPacketSize;

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

        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
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
            throw new NotImplementedException();
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
        }
#endif
    }
}
