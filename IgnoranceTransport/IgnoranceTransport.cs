// SoftwareGuy's Ignorance Reliable UDP Transport
// Uses ENET as the transport backend.
// ----------------------------------------
// Ignorance Transport by Coburn, 2018
// ENet-C# by nxrighthere, 2018
// ENet by the ENet developers, whenever - whenever.
// ----------------------------------------
// THIS IS EXPERIMENTAL PROGRAM CODE
// ----------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using ENet;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace Mirror
{
    /// <summary>
    /// Ignorance rUDP Transport is built upon the ENet-C# wrapper by nxrighthere.
    /// </summary>
    public class IgnoranceTransport : TransportLayer
    {
        Host client;
        Address clientAddress;
        Peer clientPeer;

        Host server;
        Address serverAddress;

        private bool superParanoidMode = true;
        private bool hackyServerActive = false;
        private Dictionary<int, Peer> knownPeerDictionary;

        private int clientConnectionId = -1;

        public IgnoranceTransport()
        {
            Debug.Log("This is the Ignorance rUDP Transport reporting in for duty.");
            Debug.Log("Please note that this highly experimental and may cause your game to spontanously combust into a violent dumpster fire.");
            Library.Initialize();
        }

        /// <summary>
        /// Connects the client to a server.
        /// </summary>
        /// <param name="address">The connection address.</param>
        /// <param name="port">The connection port.</param>
        public void ClientConnect(string address, int port)
        {
            client = new Host();

            if (superParanoidMode) Debug.LogFormat("Ignorance rUDP Transport: ClientConnect({0}, {1})", address, port);

            clientAddress = new Address() { Port = Convert.ToUInt16(port) };
            clientAddress.SetHost(address);

            // Create the client.
            client.Create();
            // Connect the client to the server.
            clientPeer = client.Connect(clientAddress);
        }

        // Not sure about this one?
        /// <summary>
        /// Is the client connected currently?
        /// </summary>
        /// <returns>True if connected, False if not.</returns>
        public bool ClientConnected()
        {
            if (superParanoidMode) Debug.Log("Ignorance rUDP Transport: ClientConnected() - what do?");
            return clientPeer.IsSet && clientPeer.State == PeerState.Connected;
        }

        /// <summary>
        /// Disconnect the client.
        /// </summary>
        public void ClientDisconnect()
        {
            if (superParanoidMode) Debug.Log("Ignorance rUDP Transport: ClientDisconnect()");

            // TODO: I dunno what to put here! nx has something about Reasons for the disconnection??
            clientPeer.DisconnectNow(0);
            client.Dispose();
        }

        /// <summary>
        /// Get the next client data packet.
        /// </summary>
        /// <param name="transportEvent">The transport event to report back to Mirror.</param>
        /// <param name="data">The byte array of the data.</param>
        /// <returns></returns>
        public bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data)
        {
            transportEvent = TransportEvent.Disconnected;
            data = null;

            // Create a placeholder variable for the incoming ENetEvent that will be emitted by ENet Wrapper.
            Event ENetEvent;

            // Get the next message...
            client.Service(0, out ENetEvent);

            // What type is this?
            // FIXME WARNING: Peer.ID is unsigned int, but converting to Int32 causes it to crap itself with a OverFlowException. Why does ENet-C# use unsigned ints?
            switch (ENetEvent.Type)
            {
                case EventType.Connect:
                    Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage() connect with peer ID {0}, IP {1}", Convert.ToInt32(ENetEvent.Peer.ID), ENetEvent.Peer.IP);
                    clientConnectionId = Convert.ToInt32(ENetEvent.Peer.ID);
                    break;
                case EventType.Disconnect:
                    Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage() disconnect with peer ID {0}, IP {1}", Convert.ToInt32(ENetEvent.Peer.ID), ENetEvent.Peer.IP);
                    transportEvent = TransportEvent.Disconnected;
                    clientConnectionId = -1;
                    break;
                case EventType.Timeout:
                    Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage() timeout with peer ID {0}, IP {1}", Convert.ToInt32(ENetEvent.Peer.ID), ENetEvent.Peer.IP);
                    transportEvent = TransportEvent.Disconnected;
                    break;

                case EventType.Receive:
                    transportEvent = TransportEvent.Data;
                    Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage(): data! channel {0}, length: {1}", ENetEvent.ChannelID, ENetEvent.Packet.Length);

                    // Feed the data to the output...
                    data = new byte[ENetEvent.Packet.Length];
                    ENetEvent.Packet.CopyTo(data);
                    break;

                case EventType.None:
                    Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage(): Nothing yet");
                    return false;
            }

            /*
            switch (ENetEvent.Type)
            {
                case EventType.Connect:
                    if (superParanoidMode) Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage() says it connected to the server, with peer ID {0}", Convert.ToInt32(clientPeer.ID));
                    transportEvent = TransportEvent.Connected;
                    break;
                case EventType.Disconnect:
                    if (superParanoidMode) Debug.Log("Ignorance rUDP Transport ClientGetNextMessage() says it disconnected from the server");
                    transportEvent = TransportEvent.Disconnected;
                    break;
                case EventType.Timeout:
                    if (superParanoidMode) Debug.Log("Ignorance rUDP Transport ClientGetNextMessage() says it encountered a timeout");
                    transportEvent = TransportEvent.Disconnected;
                    break;
                case EventType.Receive:
                    if (superParanoidMode) Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage(): Channel {0}, Data Length: {1}", ENetEvent.ChannelID, ENetEvent.Packet.Length);
                    transportEvent = TransportEvent.Data;


                    break;
                case EventType.None:
                    return false;
                default:
                    // Fail safe.
                    if (superParanoidMode) Debug.Log("Ignorance rUDP Transport ClientGetNextMessage(): Failsafe triggered");
                    break;
            }


            // Feed the data to the output...
            data = new byte[ENetEvent.Packet.Length];
            ENetEvent.Packet.CopyTo(data);
            */

            // Dispose of the packet.
            ENetEvent.Packet.Dispose();

            // We're done here. Bugger off.
            client.Flush();
            return true;
        }

        /// <summary>
        /// Send client data to the server.
        /// </summary>
        /// <param name="channelId">The channel ID.</param>
        /// <param name="data">The data array.</param>
        /// <returns></returns>
        public bool ClientSend(int channelId, byte[] data)
        {
            // TODO: Is this flush really needed?
            client.Flush();

            // Mailing Pigeons. Gotta love the birds.
            // Very useful in the wartime, as long as the enemy team didn't
            // shoot them down. At least you got a free dinner. Who doesn't
            // want a delicious pigeon pie?
            // 
            // DEVELOPER DISCLAIMER: What the fuck am i even going on about?
            Packet mailingPigeon = default(Packet);
            mailingPigeon.Create(data, PacketFlags.Reliable);

            return clientPeer.Send((byte)channelId, ref mailingPigeon);
        }

        // -- END CLIENT FUNCTIONS -- //

        // -- START SERVER FUNCTIONS -- //        
        public bool GetConnectionInfo(int connectionId, out string address)
        {
            address = "(invalid)";

            if (knownPeerDictionary.ContainsKey(connectionId))
            {
                address = knownPeerDictionary[connectionId].IP;
                return true;
            }

            return false;
        }

        public bool ServerActive()
        {
            return hackyServerActive;
        }

        public bool ServerDisconnect(int connectionId)
        {
            // TODO: Probably revise this?
            if (knownPeerDictionary.ContainsKey(connectionId))
            {
                knownPeerDictionary[connectionId].Disconnect(0);
                return true;
            }

            return false;
        }

        public bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data)
        {
            connectionId = -1;
            transportEvent = TransportEvent.Disconnected;
            data = null;

            if (!server.IsSet)
            {
                Debug.Log("Not ready yet...");
                return false;
            }

            // Create a placeholder variable for the incoming ENetEvent that will be emitted by ENet Wrapper.
            Event ENetEvent;

            // Get the next message...
            server.Service(0, out ENetEvent);

            // What type is this?
            // FIXME WARNING: Peer.ID is unsigned int, but converting to Int32 causes it to crap itself with a OverFlowException. Why does ENet-C# use unsigned ints?
            switch (ENetEvent.Type)
            {
                case EventType.Connect:
                    Debug.LogFormat("Ignorance rUDP Transport ServerGetNextMessage() says something connected to the server, with peer ID {0}, IP {1}", ENetEvent.Peer.ID, ENetEvent.Peer.IP);
                    connectionId = Convert.ToInt32(ENetEvent.Peer.ID);

                    knownPeerDictionary.Add(connectionId, ENetEvent.Peer);

                    transportEvent = TransportEvent.Connected;
                    break;
                case EventType.Disconnect:
                    Debug.LogFormat("Ignorance rUDP Transport ServerGetNextMessage() says something disconnected from the server, with peer ID {0}, IP {1}", ENetEvent.Peer.ID, ENetEvent.Peer.IP);
                    connectionId = Convert.ToInt32(ENetEvent.Peer.ID);

                    knownPeerDictionary.Remove(connectionId);

                    transportEvent = TransportEvent.Disconnected;
                    break;
                case EventType.Timeout:
                    Debug.LogFormat("Ignorance rUDP Transport ServerGetNextMessage() says it encountered a timeout, with peer ID {0}, IP {1}", ENetEvent.Peer.ID, ENetEvent.Peer.IP);
                    connectionId = Convert.ToInt32(ENetEvent.Peer.ID);
                    transportEvent = TransportEvent.Disconnected;
                    break;

                case EventType.Receive:
                    transportEvent = TransportEvent.Data;
                    Debug.LogWarningFormat("Ignorance rUDP Transport ServerGetNextMessage(): data! channel {0}, data length: {1}", ENetEvent.ChannelID, ENetEvent.Packet.Length);

                    // Feed the data to the output...
                    connectionId = Convert.ToInt32(ENetEvent.Peer.ID);
                    data = new byte[ENetEvent.Packet.Length];
                    ENetEvent.Packet.CopyTo(data);
                    break;

                case EventType.None:
                    Debug.LogFormat("Ignorance rUDP Transport ServerGetNextMessage(): Nothing yet");
                    return false;
            }

            // Dispose of the packet.
            ENetEvent.Packet.Dispose();

            server.Flush();

            // We're done here. Bugger off.
            return true;
        }

        // TODO.
        public bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            // TODO: Is this needed?
            server.Flush();

            // Another mailing pigeon
            Packet mailingPigeon = default(Packet);

            // see https://github.com/nxrighthere/ENet-CSharp/issues/21
            if (knownPeerDictionary.ContainsKey(connectionId))
            {
                Peer target = knownPeerDictionary[connectionId];
                return target.Send((byte)channelId, ref mailingPigeon);
            }
            else
            {
                return false;
            }
            //return serverPeer.Send((byte)channelId, ref mailingPigeon);
        }

        public void ServerStart(string address, int port, int maxConnections)
        {
            server = new Host();
            knownPeerDictionary = new Dictionary<int, Peer>();

            if (superParanoidMode) Debug.LogFormat("Ignorance rUDP Transport ServerStart(): {0}, {1}, {2}", address, port, maxConnections);
            // Why a bloody ushort?!
            serverAddress = new Address() { Port = (ushort)port };
            serverAddress.SetHost(address);

            server.Create(serverAddress, maxConnections);
            hackyServerActive = true;
        }

        public void ServerStartWebsockets(string address, int port, int maxConnections)
        {
            // Websockets? Nani?
            throw new NotImplementedException();
        }

        public void ServerStop()
        {
            if (superParanoidMode) Debug.LogFormat("Ignorance rUDP Transport ServerStop()");

            foreach (KeyValuePair<int, Peer> entry in knownPeerDictionary)
            {
                // TODO: WTF?
                entry.Value.Disconnect(0);
                server.Flush();
            }

            // Don't forget to flush after you've finished in the bathroom.
            server.Flush();
            server.Dispose();

            hackyServerActive = false;
        }

        // -- SHUTDOWN FUNCTIONS -- //
        public void Shutdown()
        {
            Debug.Log("The Ignorance rUDP Transport is going down for shutdown NOW!");

            if (client.IsSet)
            {
                if (superParanoidMode) Debug.Log("Sending the client process to the dumpster fire...");
                client.Flush();
                client.Dispose();
            }

            if (server.IsSet)
            {
                hackyServerActive = false;

                if (superParanoidMode) Debug.Log("Sending the server process to the dumpster fire...");
                server.Flush();
                server.Dispose();
            }

            if (superParanoidMode) Debug.Log("Closing the lid on the dumpster fire...");
            Library.Deinitialize();
            if (superParanoidMode) Debug.Log("Ignorance rUDP Transport shutdown complete.");
        }

        // -- PARANOID MODE FUNCTIONS //
        public void EnableParanoidLogging(bool enable)
        {
            superParanoidMode = enable;
        }
    }

}
