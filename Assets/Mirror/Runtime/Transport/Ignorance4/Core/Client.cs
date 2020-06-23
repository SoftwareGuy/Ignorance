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
using System.Collections.Concurrent;
using System.Threading;
using ENet;

namespace OiranStudio.Ignorance4
{
    /// <summary>
    /// This spawns a Ignorance Client, which is used by a client instance to pump
    /// the network in it's own thread.
    /// </summary>    
    public class Client
    {
        // Where is the client is going to connect to?
        public string Address = string.Empty;
        // Which port should the client connect to?
        public ushort Port = 7777;
        // Maximum Packet Size. Packets above this get dropped.
        public int MaximumPacketSize = 1200;
        // How big a packet buffer do we want?
        // This is by default, a 1MB buffer. That means worse case scenario,
        // 1MB buffer at server tick rate of 60, means ~60 Megabit/s of
        // traffic before you will get a buffer overrun. On a gigabit connection,
        // you might want to set this to a bigger buffer.
        public int MaximumBufferSize = 1048576;
        // Number of channels. Maximum of 255.
        public byte ChannelCount = 2;
        // Polling waiting time. This is how long native waits in milliseconds until it
        // passes back control to the C# world. At low peer counts, you may find Enet
        // doing hardly anything. But when you have high peer counts, ENet will
        // be shuttling things back and forth as much as possible. This is a
        // trade off between CPU usage and performance. Setting it to 1 is maximum
        // performance, at the cost of CPU time. 3 - 5 seems to be the sweet spot.
        public uint WaitingTime = 3;

        // Is this client active?
        public bool IsClientActive { get { return ClientActive; } }

        // Allows one to capture what Ignorance is saying.
        // The bool param means it's debug oriented; ie. "notice me senpai".
        public Action<bool, string> EmittedLogString;

        // Queues that we use to push data into and pull data out of.
        public static ConcurrentQueue<Definitions.IgnoranceDataPacket> IncomingDataPackets = new ConcurrentQueue<Definitions.IgnoranceDataPacket>();
        private static ConcurrentQueue<Definitions.IgnoranceDataPacket> OutgoingDataPackets = new ConcurrentQueue<Definitions.IgnoranceDataPacket>();

        // Operations Thread
        protected Thread clientThread;
        protected volatile static bool StayHome = false;
        protected volatile static bool ClientActive = false;

        /// <summary>
        /// Call this to start the client instance and bootstrap the Enet Thread.
        /// Just make sure you populate all public fields before hand!
        /// </summary>
        public void StartClient()
        {
            ClientActive = false;
            StayHome = false;
            EmittedLogString?.Invoke(false, "An Ignorance client instance is now starting up.");

            // Drain anything in the queues; so we don't get random crap.
            // It is better to just do this than call 'new' as we'll have a memory allocation.
            while (IncomingDataPackets.TryDequeue(out _)) ;
            while (OutgoingDataPackets.TryDequeue(out _)) ;

            // Attempt to start the thread.
            try
            {
                EmittedLogString?.Invoke(false, "Starting thread");
                clientThread = new Thread(() => WorkerThread(Address, Port, ChannelCount, MaximumPacketSize, MaximumBufferSize, WaitingTime));
                clientThread.Start();
            }
            catch (Exception ex)
            {
                EmittedLogString?.Invoke(false, $"I caught an exception. Mission failed, we'll get them next time.\n{ex}");
            }
        }

        /// <summary>
        /// Call this to stop the client instance and hopefully clean up.
        /// </summary>
        public void StopClient()
        {
            if (!clientThread.IsAlive) return;

            EmittedLogString?.Invoke(false, "Stopping client instance.");
            StayHome = true;

            if(clientThread.IsAlive)
            {
                clientThread.Join();
            }

            ClientActive = false;
            EmittedLogString?.Invoke(false, "Stopped client instance.");            
        }


        public void PlaceIntoQueue(int channel, ArraySegment<byte> payload)
        {
            Definitions.IgnoranceDataPacket outgoingPacket = new Definitions.IgnoranceDataPacket
            {
                ChannelId = (byte)channel,
                IsOutgoing = true,
                Payload = payload
            };

            OutgoingDataPackets.Enqueue(outgoingPacket);
        }

        #region Thread World
        protected void WorkerThread(string connectionAddress, ushort port, byte numChannels, int maxPacket, int maxBuffer, uint nativeWaitTime)
        {
            // Allocate the buffer size.
            byte[] clientWorkBuffer = new byte[maxBuffer];
            int bufferCurrentPosition = 0;
            // uint bufferNextPosition = 0;

            bool clientPollingDone = false;

            // Enet Internals
            Peer enetPeer;
            Address enetAddress = new Address();
            Event enetEvent;

            EmittedLogString?.Invoke(true, "Worker thread has arrived");

            using (Host clientHost = new Host())
            {
                try
                {
                    // Create the client host object.
                    clientHost.Create(null, 1, numChannels, 0, 0, (int)maxPacket);
                    // Set the connection target (client -> host:port)
                    enetAddress.SetHost(connectionAddress);
                    enetAddress.Port = port;

                    // Set a flag to know we're active
                    ClientActive = true;
                }
                catch (Exception e)
                {
                    EmittedLogString?.Invoke(false, $"Client has encountered a fatal exception. You might want to report this as a bug on the GitHub.\n" +
                        "Dev Tip: Using a Debug enet library makes a log file called 'enet_log.txt' in the root of your game. On mobile devices, debug info will be logged to your devices' logs.");
                    EmittedLogString?.Invoke(false, $"The exception returned was: {e}");
                    return;
                }

                EmittedLogString?.Invoke(true, $"Connection attempt to {connectionAddress} started. Go go go!");

                enetPeer = clientHost.Connect(enetAddress, numChannels);

                // START: Pump Loops
                while (!StayHome)
                {
                    // For this iteration, reset...
                    clientPollingDone = false;

                    // While we haven't done client polls, let's keep doing that
                    while (!clientPollingDone)
                    {
                        if (clientHost.CheckEvents(out enetEvent) <= 0)
                        {
                            if (clientHost.Service((int)nativeWaitTime, out enetEvent) <= 0) break;
                            clientPollingDone = true;
                        }

                        // What event happened?
                        switch (enetEvent.Type)
                        {
                            // Nothing happened this time. Take a nap.
                            case ENet.EventType.None:
                            default:
                                break;

                            // Client connected.
                            case ENet.EventType.Connect:
                                EmittedLogString?.Invoke(true, $"Connection established to {connectionAddress}:{port}!");
                                IncomingDataPackets.Enqueue(new Definitions.IgnoranceDataPacket()
                                {
                                    Type = Definitions.PacketEventType.Connect,
                                    ChannelId = enetEvent.ChannelID
                                });
                                break;

                            case ENet.EventType.Receive:
                                EmittedLogString?.Invoke(true, $"Connection receiving data: channel {enetEvent.ChannelID}, {enetEvent.Packet.Length} byte");
                                if (enetEvent.Packet.IsSet)
                                {
                                    // Make sure we're not trying to process a packet too big.
                                    if (enetEvent.Packet.Length <= maxPacket)
                                    {
                                        // Is our buffer too small to fit this?
                                        // If so, we dispose of the packet.
                                        if (enetEvent.Packet.Length > clientWorkBuffer.Length)
                                        {
                                            EmittedLogString?.Invoke(false, "WARNING: The client work buffer is too small to contain this packet and will be dropped. Increase the Maximum Thread Work Buffer Size.");
                                            enetEvent.Packet.Dispose();
                                            return;
                                        }
                                        else
                                        {
                                            // Is this packet going to overrun our buffer?
                                            // If so, reset it back to position 0.
                                            if ((bufferCurrentPosition + enetEvent.Packet.Length) >= clientWorkBuffer.Length)
                                            {
                                                EmittedLogString?.Invoke(true, "Rewinding to start of buffer! If you see this too often, chances are the buffer is too small. Increase the Maximum Thread Work Buffer size.");
                                                bufferCurrentPosition = 0;
                                            }
                                        }

                                        // Good, now let's process this.
                                        // Cache that packet.
                                        EmittedLogString?.Invoke(true, $"Buffer write position: {bufferCurrentPosition}"); // DEBUG

                                        enetEvent.Packet.CopyTo(clientWorkBuffer, (int)bufferCurrentPosition);

                                        Definitions.IgnoranceDataPacket ignoranceDataPacket = new Definitions.IgnoranceDataPacket()
                                        {
                                            Type = Definitions.PacketEventType.Data,
                                            ChannelId = enetEvent.ChannelID,
                                            Payload = new ArraySegment<byte>(clientWorkBuffer, bufferCurrentPosition, enetEvent.Packet.Length)
                                        };

                                        bufferCurrentPosition += enetEvent.Packet.Length;
                                        // EmittedLogString?.Invoke(true, $"Buffer next write position: {bufferCurrentPosition}"); // DEBUG

                                        // All good - now put it into the queue to shuttle back over to mirror world
                                        enetEvent.Packet.Dispose();                                        
                                        IncomingDataPackets.Enqueue(ignoranceDataPacket);
                                    }
                                    else
                                    {
                                        EmittedLogString?.Invoke(true, $"Dropping a packet because it was too big for our buffer. {enetEvent.Packet.Length} vs {maxPacket} bytes");
                                        enetEvent.Packet.Dispose();
                                    }
                                }
                                else
                                {
                                    EmittedLogString?.Invoke(true, "Connection got a packet that wasn't properly set up. This shouldn't happen.");
                                }
                                break;

                            // Disconnect and Timeout are both similar things.
                            // In Mirror, we can treat them as both.
                            case ENet.EventType.Disconnect:
                            case ENet.EventType.Timeout:
                                EmittedLogString?.Invoke(true, $"We have been disconnected or our connection to {connectionAddress}:{port} timed out.");
                                IncomingDataPackets.Enqueue(new Definitions.IgnoranceDataPacket()
                                {
                                    Type = Definitions.PacketEventType.Disconnect,
                                    ChannelId = enetEvent.ChannelID
                                });
                                break;
                        }
                    }

                    // Sending outgoing packets...
                    while (OutgoingDataPackets.TryDequeue(out Definitions.IgnoranceDataPacket thePacket))
                    {
                        ENet.Packet outPacket = default;
                        outPacket.Create(thePacket.Payload.Array, thePacket.Payload.Offset, thePacket.Payload.Count + thePacket.Payload.Offset, thePacket.Flags);

                        int returnCode = enetPeer.SendAndReturnStatusCode(thePacket.ChannelId, ref outPacket);
                        if (returnCode != 0) EmittedLogString?.Invoke(true, $"ERROR: Could not send {outPacket.Length} bytes to server on channel {thePacket.ChannelId}, error code {returnCode}");
                    }
                }
                // END: Pump Loops

                enetPeer.DisconnectNow(0);
                clientHost.Flush();
            }

            EmittedLogString?.Invoke(true, "Worker thread has departed");
            ClientActive = false;
        }
        #endregion
    }

}
