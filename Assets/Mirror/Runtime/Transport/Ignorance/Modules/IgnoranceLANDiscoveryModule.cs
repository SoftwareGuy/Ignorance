// Ignorance 1.3.x
// A Unity LLAPI Replacement Transport for Mirror Networking
// https://github.com/SoftwareGuy/Ignorance
// -----------------
// LAN Discovery Module
// Written by c6burn, coburn64
// -----------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using System.Threading;

namespace Mirror
{
    public class IgnoranceLANDiscoveryModule : MonoBehaviour
    {
        // Obsolete. You should really look at MirrorNetworkDiscovery instead.
        // https://github.com/in0finite/MirrorNetworkDiscovery
        // This will be removed in a later version of Ignorance.

        // Use this as a hook to add servers to the list, etc.
        // The string parameter will be the server's IP address.
        public Action<string> OnServerDiscovered;
        public bool DebugMode = false;

        [SerializeField] protected bool AutomaticDiscovery = true;
        [SerializeField] protected int DiscoveryIntervalMilliseconds = 5000;     // 5 seconds.
        [SerializeField] protected int LanDiscoveryPort = 7778;

        protected Ignorance coreModule;
        protected volatile bool ceaseOperation = false;
        protected volatile bool clientCeaseOperation = false;

        protected Thread serverThread;
        protected Thread clientThread;

        protected List<string> KnownServers = new List<string>();

        // Do not touch the following.
        byte[] lanDiscoveryRequestData = System.Text.Encoding.ASCII.GetBytes("IgnoranceLANDiscoveryRequest");
        byte[] lanDiscoveryResponseData = System.Text.Encoding.ASCII.GetBytes("IgnoranceLANDiscoveryResponse");

        public void Awake()
        {
            Debug.LogWarning("This module is obsolete and will be removed in a later version of Ignorance. Read the source comments for more information.");

            coreModule = GetComponent<Ignorance>();
            if (!coreModule)
            {
                // Can't continue without our core module.
                Debug.LogError("Ignorance LAN Discovery Module requires a Ignorance Transport script on the gameObject as the one you have this script on. I can't find it.");
                enabled = false;
                return;
            }

            coreModule.OnIgnoranceServerStartup += OnIgnoranceServerStart;
            coreModule.OnIgnoranceServerShutdown += OnIgnoranceServerShutdown;

            coreModule.OnIgnoranceClientStartup += OnIgnoranceClientStart;
            coreModule.OnIgnoranceClientShutdown += OnIgnoranceClientShutdown;

            Debug.LogWarning("This LAN Discovery module is beta-quality. Use with caution. If you encounter problems, please file a bug report!");
        }

        public void Start()
        {
            // TODO: Come up with a better "don't start if we're a server" if check here.
            if (AutomaticDiscovery && !NetworkManager.isHeadless)
            {              
                StartLANDiscoveryClient();
            }
        }

        /// <summary>
        /// Calling this starts the LAN Discovery Client.
        /// </summary>
        public void StartLANDiscoveryClient()
        {
            clientCeaseOperation = false;
            clientThread = new Thread(LANDiscoveryClientThread);
            clientThread.Start();
        }

        /// <summary>
        /// Calling this stops the LAN Discovery Client.
        /// </summary>
        public void StopLANDiscoveryClient()
        {
            clientCeaseOperation = true;
            if (!clientThread.IsAlive) return;
            clientThread.Join();
        }

        /// <summary>
        /// Called when the core module is starting the client.
        /// </summary>
        private void OnIgnoranceClientStart()
        {
            ;
        }

        /// <summary>
        /// Called when the core module is stopping the client.
        /// </summary>
        private void OnIgnoranceClientShutdown()
        {
            ;
        }

        /// <summary>
        /// Called when the core module is starting the server.
        /// </summary>
        public void OnIgnoranceServerStart()
        {
            ceaseOperation = false;

            serverThread = new Thread(LANDiscoveryServerThread);
            serverThread.Start();
        }

        /// <summary>
        /// Called when the core module is shutting down the server.
        /// </summary>
        public void OnIgnoranceServerShutdown()
        {
            ceaseOperation = true;
            if (!serverThread.IsAlive) return;
            serverThread.Join();
        }

        /// <summary>
        /// The script is being disposed.
        /// </summary>
        public void OnDestroy()
        {
            coreModule.OnIgnoranceServerStartup -= OnIgnoranceServerStart;
            coreModule.OnIgnoranceServerShutdown -= OnIgnoranceServerShutdown;

            coreModule.OnIgnoranceClientStartup -= OnIgnoranceClientStart;
            coreModule.OnIgnoranceClientShutdown -= OnIgnoranceClientShutdown;

            StopLANDiscoveryClient();
        }

        private void LANDiscoveryServerThread()
        {
            Debug.Log("Ignorance LAN Discovery: Server thread has started.");
            UdpClient lanDiscoveryServer = new UdpClient(LanDiscoveryPort);
            lanDiscoveryServer.Client.Blocking = false;

            bool needSleep;
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Any, LanDiscoveryPort);

            while (!ceaseOperation)
            {
                needSleep = true;
                try
                {
                    byte[] ClientRequestData = lanDiscoveryServer.Receive(ref clientEndpoint);

                    if (ClientRequestData == null)
                    {
                        // Nothing received. Carry on.
                    }
                    else if (ClientRequestData.Length != lanDiscoveryRequestData.Length)
                    {
                        // Not the right data length?
                        Debug.LogWarning("Ignorance LAN Discovery: Client request packet length mismatch. Possible UDP attack.");
                    }
                    else if (lanDiscoveryRequestData.SequenceEqual(ClientRequestData))
                    {
                        needSleep = false;
                        if (DebugMode) print($"Ignorance LAN Discovery: Potential client found at {clientEndpoint.Address}... Advertising the server to them.");
                        lanDiscoveryServer.Send(lanDiscoveryResponseData, lanDiscoveryResponseData.Length, clientEndpoint);
                    }
                    else
                    {
                        Debug.LogWarning($"Ignorance LAN Discovery: Wrong UDP Data from {clientEndpoint.Address}");
                    }
                }
                catch
                {
                    // TODO: Ensure that we catch all errors apart from the non-blocking operation could not be completed immediately exception
                    // if(sockEx.SocketErrorCode != SocketError.)
                    // Debug.LogError($"Ignorance LAN Discovery: An exception occurred. {e.ToString()}");
                }

                if (!needSleep) continue;
                Thread.Sleep(20);   // 20ms sleep for the server thread should be plenty.
            }

            lanDiscoveryServer.Close();
            Debug.Log("Ignorance LAN Discovery: Server thread has stopped.");
        }

        private void LANDiscoveryClientThread()
        {
            print("Ignorance LAN Discovery: Client thread has started.");

            UdpClient discoveryClient = new UdpClient();
            discoveryClient.EnableBroadcast = true;
            discoveryClient.Client.Blocking = false;

            IPEndPoint serverEp = new IPEndPoint(IPAddress.Any, LanDiscoveryPort);
            IPEndPoint bcastEP = new IPEndPoint(IPAddress.Broadcast, LanDiscoveryPort);

            bool needSleep;

            // Start the stopwatch.
            bool firsty = true;
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

            while (!clientCeaseOperation)
            {
                needSleep = true;

                try
                {
                    if (!firsty)
                    {
                        if (sw.ElapsedMilliseconds > DiscoveryIntervalMilliseconds)
                        {
                            if (DebugMode) print($"Ignorance LAN Discovery: (Re-)Discovering servers...");
                            sw.Restart();
                            discoveryClient.Send(lanDiscoveryRequestData, lanDiscoveryRequestData.Length, bcastEP);
                        }
                    } else
                    {
                        if (DebugMode) print($"Ignorance LAN Discovery: Start discovering servers...");
                        discoveryClient.Send(lanDiscoveryRequestData, lanDiscoveryRequestData.Length, bcastEP);
                        sw.Start();

                        // We've sent out the first packet.
                        firsty = false;
                    }
                        
                    // Our incoming response.
                    byte[] incomingResponse = discoveryClient.Receive(ref serverEp);
                    if (incomingResponse == null)
                    {
                        // Nothing's happening here lads.
                        ;
                    }
                    else if (incomingResponse.Length != lanDiscoveryResponseData.Length)
                    {
                        // Wrong length.
                        Debug.LogWarning("Ignorance LAN Discovery: Server response packet length mismatch. Possible MITM or UDP attack.");
                    }
                    else if (incomingResponse.SequenceEqual(lanDiscoveryResponseData))
                    {
                        // It's a hit.
                        needSleep = false;

                        if (!KnownServers.Contains(serverEp.Address.ToString()))
                        {
                            if (DebugMode) print($"Ignorance LAN Discovery: I have found a new server at {serverEp.Address.ToString()}");
                            KnownServers.Add(serverEp.Address.ToString());
                            OnServerDiscovered?.Invoke(serverEp.Address.ToString());
                        } else
                        {
                            if (DebugMode) print($"Ignorance LAN Discovery: We already know the server at {serverEp.Address.ToString()}; skipping.");
                        }
                    }
                    else
                    {
                        Debug.LogError($"Ignorance LAN Discovery: Bad data from {serverEp.Address.ToString()}");
                    }
                }
                catch
                {
                    // TODO: Ensure that we catch all errors apart from the non-blocking operation could not be completed immediately exception
                    // Debug.LogError($"Ignorance LAN Discovery: An exception occurred. {e.ToString()}");
                }

                if (!needSleep) continue;
                Thread.Sleep(DiscoveryIntervalMilliseconds);
            }

            discoveryClient.Close();

            print("Ignorance LAN Discovery: Client thread has stopped.");
        }
    }
}
