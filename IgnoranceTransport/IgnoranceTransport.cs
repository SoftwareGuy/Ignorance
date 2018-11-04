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
        // -- SERVER WORLD VARIABLES -- //
        Host server;
        Address serverAddress;

        private Dictionary<int, Peer> knownPeersServerDictionary;
        private int fakeConnectionCounter = 1;  // Start at 1 because 0 seems to be localClient ?

        // -- CLIENT WORLD VARIABLES -- //
        Host client;
        // Testing
        // Address clientAddress;
        Peer clientPeer;

        private bool superParanoidMode = true;
        // private bool hackyServerActive = false;
        // private int clientConnectionId = -1;

        public IgnoranceTransport()
        {
            Library.Initialize();

            Debug.Log("This is the Ignorance rUDP Transport reporting in for duty.");
            Debug.Log("Please note that this highly experimental and may cause your game to spontanously combust into a violent dumpster fire.");
        }

        // -- SERVER WORLD FUNCTIONS -- //
        /// <summary>
        /// Is the client connected currently?
        /// </summary>
        /// <returns>True if connected, False if not.</returns>
        public bool ClientConnected()
        {
            if (superParanoidMode) Debug.Log("Ignorance rUDP Transport: ClientConnected() called");
            return clientPeer.IsSet && clientPeer.State == PeerState.Connected;
            // return clientPeer.IsSet;
        }

        public bool GetConnectionInfo(int connectionId, out string address)
        {
            address = "(invalid)";

            if (knownPeersServerDictionary.ContainsKey(connectionId))
            {
                address = knownPeersServerDictionary[connectionId].IP;
                return true;
            }

            return false;
        }

        public bool ServerActive()
        {
            return server.IsSet;
        }

        public bool ServerDisconnect(int connectionId)
        {
            // TODO: Probably revise this?
            if (knownPeersServerDictionary.ContainsKey(connectionId))
            {
                knownPeersServerDictionary[connectionId].Disconnect(0);
                return true;
            }

            return false;
        }

        public bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data)
        {
            Event incomingEvent;
            connectionId = -1;
            transportEvent = TransportEvent.Disconnected;
            data = null;

            if (!server.IsSet)
            {
                Debug.Log("Server is not ready yet.");
                return false;
            }

            // Get the next message...
            server.Service(0, out incomingEvent);

            // What type is this?
            // FIXME WARNING: Peer.ID is unsigned int, but converting to Int32 causes it to crap itself with a OverFlowException. Why does ENet-C# use unsigned ints?
            switch (incomingEvent.Type)
            {
                case EventType.Connect:
                    Debug.LogFormat("Ignorance rUDP Transport ServerGetNextMessage() says something connected to the server, with peer ID {0}, IP {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP);

                    // Peer ID from ENet Wrapper will be a unsigned Int32. Since Mirror uses a signed int, we need to do a hacky work around.
                    // This sucks, but honestly if done right it should work as intended. Here's hoping my magic doesn't shaft me at my own game.

                    // Give the new connection a fake connection ID, but also cache the Peer.
                    connectionId = fakeConnectionCounter;

                    // The peer object will allow us to do stuff with it later.
                    knownPeersServerDictionary.Add(connectionId, incomingEvent.Peer);
                    Debug.LogFormat("Ignorance rUDP Transport ServerGetNextMessage() setup fake ConnectionID. connID {0} belongs to peer ID {1}, IP {2}", fakeConnectionCounter, incomingEvent.Peer.ID, incomingEvent.Peer.IP);

                    // Increment the fake connection counter by one.
                    fakeConnectionCounter += 1;

                    // Report back saying we got a connection event.
                    transportEvent = TransportEvent.Connected;

                    break;
                case EventType.Disconnect:
                    Debug.LogFormat("Ignorance rUDP Transport ServerGetNextMessage() says something disconnected from the server, with peer ID {0}, IP {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP);

                    // Peer ID from ENet Wrapper will be a unsigned Int32. Since Mirror uses a signed int, we need to do a hacky work around.
                    // Since our dictionary stores fake connection IDs, we need to go through and find the real Peer ID.
                    // knownPeersServerDictionary.Remove(connectionId);
                    foreach (KeyValuePair<int, Peer> entry in knownPeersServerDictionary)
                    {
                        if (entry.Value.ID == incomingEvent.Peer.ID)
                        {
                            connectionId = entry.Key;

                            Debug.LogFormat("Evicting peer ID {0} (fake connID {1})", incomingEvent.Peer.ID, entry.Key);
                            knownPeersServerDictionary.Remove(entry.Key);
                            // No need to keep going through the list. Halt.
                            break;
                        }
                    }

                    // Report back.
                    transportEvent = TransportEvent.Disconnected;
                    break;

                case EventType.Timeout:
                    Debug.LogFormat("Ignorance rUDP Transport ServerGetNextMessage() says it encountered a timeout, with peer ID {0}, IP {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP);
                    foreach (KeyValuePair<int, Peer> entry in knownPeersServerDictionary)
                    {
                        if (entry.Value.ID == incomingEvent.Peer.ID)
                        {
                            connectionId = entry.Key;

                            // No need to keep going through the list. Halt.
                            break;
                        }
                    }

                    transportEvent = TransportEvent.Disconnected;
                    break;

                case EventType.Receive:
                    transportEvent = TransportEvent.Data;
                    Debug.LogWarningFormat("Ignorance rUDP Transport ServerGetNextMessage(): data! channel {0}, data length: {1}", incomingEvent.ChannelID, incomingEvent.Packet.Length);

                    foreach (KeyValuePair<int, Peer> entry in knownPeersServerDictionary)
                    {
                        if (entry.Value.ID == incomingEvent.Peer.ID)
                        {
                            // Debug.LogFormat("PeerID {0} maps to connection ID {1}", incomingEvent.Peer.ID, entry.Key);
                            connectionId = entry.Key;

                            // No need to keep going through the list. Halt.
                            break;
                        }
                    }

                    // Try to be safe, but at the moment this just causes an access violation.
                    byte[] newDataPacketContents = new byte[incomingEvent.Packet.Length];
                    incomingEvent.Packet.CopyTo(newDataPacketContents);                     // <- This will crap the bed with an Access Violation.
                    Debug.LogFormat("Incoming data: {0}", BitConverter.ToString(newDataPacketContents));

                    data = newDataPacketContents;
                    break;

                case EventType.None:
                    // Debug.LogFormat("Ignorance rUDP Transport ServerGetNextMessage(): Nothing yet");
                    return false;
            }

            // Dispose of the packet.
            incomingEvent.Packet.Dispose();

            server.Flush();

            // We're done here. Bugger off.
            return true;
        }

        public bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            // TODO: Is this needed?
            server.Flush();

            // Another mailing pigeon
            Packet mailingPigeon = default(Packet);

            // see https://github.com/nxrighthere/ENet-CSharp/issues/21
            if (knownPeersServerDictionary.ContainsKey(connectionId))
            {
                Peer target = knownPeersServerDictionary[connectionId];
                Debug.LogFormat("ServerSend fakeConnID {0} channelId {1} data {2}", connectionId, channelId, BitConverter.ToString(data));

                bool wasSuccessful = target.Send((byte)channelId, ref mailingPigeon);

                Debug.LogFormat("ServerSend was successful? {0}", wasSuccessful);
                return wasSuccessful;
            }
            else
            {
                Debug.LogFormat("ServerSend failure fakeConnID {0} channelId {1} data {2}", connectionId, channelId, BitConverter.ToString(data));
                return false;
            }

            //return serverPeer.Send((byte)channelId, ref mailingPigeon);
        }

        public void ServerStart(string address, int port, int maxConnections)
        {
            server = new Host();
            serverAddress = new Address();
            knownPeersServerDictionary = new Dictionary<int, Peer>();

            if (superParanoidMode) Debug.LogFormat("Ignorance rUDP Transport ServerStart(): {0}, {1}, {2}", address, port, maxConnections);
            serverAddress.SetHost(address);
            serverAddress.Port = Convert.ToUInt16(port);

            server.Create(serverAddress, maxConnections);
        }

        public void ServerStartWebsockets(string address, int port, int maxConnections)
        {
            // Websockets? Nani?
            throw new NotImplementedException();
        }

        public void ServerStop()
        {
            if (superParanoidMode) Debug.LogFormat("Ignorance rUDP Transport ServerStop()");

            foreach (KeyValuePair<int, Peer> entry in knownPeersServerDictionary)
            {
                // TODO: WTF?
                entry.Value.Disconnect(0);
                server.Flush();
            }

            // Don't forget to flush after you've finished in the bathroom.
            server.Flush();
            server.Dispose();

            // hackyServerActive = false;
        }

        // -- CLIENT WORLD FUNCTIONS -- //
        /// <summary>
        /// Connects the client to a server.
        /// </summary>
        /// <param name="address">The connection address.</param>
        /// <param name="port">The connection port.</param>
        public void ClientConnect(string address, int port)
        {
            client = new Host();

            if (superParanoidMode) Debug.LogFormat("Ignorance rUDP Transport: ClientConnect({0}, {1})", address, port);

            Address clientAddress = new Address();

            // Set hostname and port to connect to.
            clientAddress.SetHost(address);
            clientAddress.Port = Convert.ToUInt16(port);

            // Create the client.
            client.Create();

            // Connect the client to the server.
            clientPeer = client.Connect(clientAddress);

            // Debugging only
            Debug.LogWarning("clientpeer isSet? " + clientPeer.IsSet);
        }

        /// <summary>
        /// Disconnect the client.
        /// </summary>
        public void ClientDisconnect()
        {
            if (superParanoidMode) Debug.Log("Ignorance rUDP Transport: ClientDisconnect()");

            // TODO: I dunno what to put here! nx has something about Reasons for the disconnection??
            if (clientPeer.IsSet)
            {
                clientPeer.DisconnectNow(0);
            }
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
            // Setup some basic things            
            Event incomingEvent;
            transportEvent = TransportEvent.Disconnected;
            data = null;

            // Safety check: if the client isn't created, then we shouldn't do anything. ENet might be warming up.
            if (!client.IsSet)
            {
                Debug.LogWarning("Client is not ready yet.");
                return false;
            }

            // Get the next message...
            client.Service(0, out incomingEvent);

            // Debugging only
            Debug.Log("ClientGetNextMessage event: " + incomingEvent.Type);

            // What type is this?
            // FIXME WARNING: Peer.ID is unsigned int, but converting to Int32 causes it to crap itself with a OverFlowException. Why does ENet-C# use unsigned ints?
            switch (incomingEvent.Type)
            {
                case EventType.Connect:
                    Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage() connect; real ENET peerID {0}, address {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP);
                    // clientConnectionId = 0;
                    transportEvent = TransportEvent.Connected;
                    break;
                case EventType.Disconnect:
                    Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage() disconnect; peerID {0}, address {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP);
                    transportEvent = TransportEvent.Disconnected;
                    // clientConnectionId = -1;
                    break;
                case EventType.Timeout:
                    Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage() timeout; peerID {0}, address {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP);
                    transportEvent = TransportEvent.Disconnected;
                    break;

                case EventType.Receive:
                    transportEvent = TransportEvent.Data;
                    Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage() data; channel {0}, length: {1}", incomingEvent.ChannelID, incomingEvent.Packet.Length);

                    // Feed the data to the output...
                    data = new byte[incomingEvent.Packet.Length];
                    incomingEvent.Packet.CopyTo(data);
                    break;

                case EventType.None:
                    // Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage(): Nothing yet");
                    return false;
            }

            // Dispose of the packet.
            incomingEvent.Packet.Dispose();

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
            if (!client.IsSet)
            {
                Debug.Log("ClientSend(): Client is not ready yet.");
                return false;
            }

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

            Debug.LogFormat("ClientSend channelId {0} data {1}", channelId, BitConverter.ToString(data));

            bool wasSuccessful = clientPeer.Send((byte)channelId, ref mailingPigeon);
            Debug.LogFormat("ClientSend was successful? {0}", wasSuccessful);

            return wasSuccessful;
        }

        // -- END CLIENT FUNCTIONS -- //

        // -- SHUTDOWN FUNCTIONS -- //
        public void Shutdown()
        {
            Debug.Log("The Ignorance rUDP Transport is going down for shutdown NOW!");

            if (client != null && client.IsSet)
            {
                if (superParanoidMode) Debug.Log("Sending the client process to the dumpster fire...");
                client.Flush();
                client.Dispose();
            }

            if (server != null && server.IsSet)
            {
                // hackyServerActive = false;

                if (superParanoidMode) Debug.Log("Sending the server process to the dumpster fire...");
                server.Flush();
                server.Dispose();
            }

            Library.Deinitialize();

            if (superParanoidMode) Debug.Log("Ignorance rUDP Transport shutdown complete.");
        }

        // -- PARANOID MODE FUNCTIONS //
        public void EnableParanoidLogging(bool enable)
        {
            superParanoidMode = enable;
        }

        // -- Hacky workarounds -- //
    }

}

