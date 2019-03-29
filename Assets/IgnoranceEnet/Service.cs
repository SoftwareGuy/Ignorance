using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ignorance
{
    public class Service : Enet.Native.IServiceSubscriber
    {
        public Service()
        {
        }

        public bool LogEnabled { get; set; }

        public bool Setup(int maxClients)
        {
            if (!Enet.Native.ServiceSetup(maxClients)) return false;
            return true;
        }

        public bool Cleanup()
        {
            if (!Enet.Native.ServiceCleanup()) return false;
            return true;
        }

        public bool Start(string ipstr, int port)
        {
            if (!Enet.Native.ServiceStart(ipstr, port)) return false;
            return true;
        }

        public bool Stop()
        {
            if (!Enet.Native.ServiceStop()) return false;
            return true;
        }

        public bool Update()
        {
            return Enet.Native.ServiceUpdate(this);
        }

        public void OnServiceStart()
        {
            if (LogEnabled) Debug.Log("service started");
        }

        public void OnServiceStop()
        {
            if (LogEnabled) Debug.Log("service stopped");
        }

        public void OnServiceClientOpen(in Enet.Native.EventClientOpen evt)
        {
            //if (LogEnabled) Debug.LogFormat("client id #{0} connected from: {1}:{2}", evt.client_id, evt.ip, evt.port);
        }

        public void OnServiceClientClose(in Enet.Native.EventClientClose evt)
        {
            //if (LogEnabled) Debug.LogFormat("client id #{0} disconnected", evt.client_id);
        }

        public void OnServiceClientRead(in Enet.Native.EventClientRead evt)
        {
            //if (LogEnabled) Debug.LogFormat("client id #{0} recv: {1} bytes", evt.client_id, evt.buffer.Length);
            if (!Enet.Native.ServiceSend(evt.client_id, evt.buffer, 0, Enet.Native.PacketFlags.Reliable))
            {
                Debug.LogErrorFormat("error sending {0} bytes to client id: {1}", evt.buffer.Length, evt.client_id);
            }
        }

        public void OnServiceError(in Enet.Native.EventError evt)
        {
            if (LogEnabled) Debug.LogErrorFormat("client id #{0} error code: {1} -- {2}", evt.client_id, evt.error_code, evt.error_string);
        }
    }
}
