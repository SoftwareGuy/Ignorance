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

        // Do not touch the following.
        byte[] lanDiscoveryRequestData = System.Text.Encoding.ASCII.GetBytes("IgnoranceLANDiscoveryRequest");
        byte[] lanDiscoveryResponseData = System.Text.Encoding.ASCII.GetBytes("IgnoranceLANDiscoveryResponse");

        public void Awake()
        {
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
        }

        public void Start()
        {
            if(AutomaticDiscovery)
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called when the core module is stopping the client.
        /// </summary>
        private void OnIgnoranceClientShutdown()
        {
            throw new NotImplementedException();
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
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Any, 0);

            while (!ceaseOperation)
            {
                needSleep = true;
                try
                {
                    byte[] ClientRequestData = lanDiscoveryServer.Receive(ref clientEndpoint);

                    if(ClientRequestData == null)
                    {
                        // Nothing received. Carry on.
                    } else if(ClientRequestData.Length != lanDiscoveryRequestData.Length)
                    {
                        Debug.LogWarning("Ignorance LAN Discovery: Client request packet length mismatch. Possible UDP attack.");
                    } else if(lanDiscoveryRequestData.SequenceEqual(ClientRequestData))
                    {
                        needSleep = false;
                        if(DebugMode) print($"Ignorance LAN Discovery: Potential client found at {clientEndpoint.Address}... Advertising the server to them.");                       
                        lanDiscoveryServer.Send(lanDiscoveryResponseData, lanDiscoveryResponseData.Length, clientEndpoint);
                    } else
                    {
                        Debug.LogWarning($"Ignorance LAN Discovery: Wrong UDP Data from {clientEndpoint.Address}");
                    }
                } catch(SocketException sockEx)
                {
                    // TODO: Ensure that we catch all errors apart from the non-blocking operation could not be completed immediately exception
                    // Debug.LogError($"Ignorance LAN Discovery: An exception occurred. {e.ToString()}");
                }

                if (!needSleep) continue;
                Thread.Sleep(20);   // 20ms sleep for the server thread should be plenty.
            }

            lanDiscoveryServer.Close();
            Debug.Log("Ignorance LAN Discovery: Server thread has stopped.");
        }

        private void LANDiscoveryClientThread ()
        {
            print("Ignorance LAN Discovery: Client thread has started.");

            UdpClient discoveryClient = new UdpClient();
            discoveryClient.EnableBroadcast = true;
            discoveryClient.Client.Blocking = false;

            IPEndPoint serverEp = new IPEndPoint(IPAddress.Any, 0);
            IPEndPoint bcastEP = new IPEndPoint(IPAddress.Broadcast, LanDiscoveryPort);

            bool needSleep = false;
            while (!clientCeaseOperation)
            {
                needSleep = true;

                try
                {
                    discoveryClient.Send(lanDiscoveryRequestData, lanDiscoveryRequestData.Length, bcastEP);
                    if(DebugMode) print($"Ignorance LAN Discovery: Discovering servers...");
                    
                    byte[] incomingResponse = discoveryClient.Receive(ref serverEp);
                    if (incomingResponse == null)
                    {
                        // Nothing's happening here lads.
                        ;
                    } else if (incomingResponse.Length != lanDiscoveryResponseData.Length)
                    {
                        // Wrong length.
                        Debug.LogWarning("Ignorance LAN Discovery: Server response packet length mismatch. Possible UDP attack.");

                    } else if (incomingResponse.SequenceEqual(lanDiscoveryResponseData))
                    {
                        // It's a hit.
                        needSleep = false;

                        if (DebugMode) print($"Ignorance LAN Discovery: I have found a server at {serverEp.Address.ToString()}");
                        OnServerDiscovered?.Invoke(serverEp.Address.ToString());
                    } else
                    {
                        Debug.LogError($"Ignorance LAN Discovery: Bad data from {serverEp.Address.ToString()}");
                    }
                }
                catch (SocketException sockEx)
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
