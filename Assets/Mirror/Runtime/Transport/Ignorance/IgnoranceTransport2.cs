using System;
using ENet;
using Mirror;
using Mirror.Ignorance;
using UnityEngine;

namespace Mirror
{
    public class IgnoranceTransport2 : Transport, ISegmentTransport
    {
        public ushort Port = 7778;

        public IgnoranceTransport2()
        {

        }

        #region Client World
        public override void ClientConnect(string address)
        {
            throw new NotImplementedException();
        }

        public override bool ClientConnected()
        {
            throw new NotImplementedException();
        }

        public override void ClientDisconnect()
        {
            throw new NotImplementedException();
        }
        
        public bool ClientSend(int channelId, ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        public override bool ClientSend(int channelId, byte[] data)
        {
            throw new NotImplementedException();
        }
        #endregion
        
        #region Server World
        public override bool ServerActive()
        {
            return ServerShowerhead.IsServerActive();
        }

        public override bool ServerDisconnect(int connectionId)
        {
            throw new NotImplementedException();
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            throw new NotImplementedException();
        }

        public bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        public override bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override void ServerStart()
        {
            ServerShowerhead.OnServerConnected.AddListener(ServerConnected);
            ServerShowerhead.OnServerDisconnected.AddListener(ServerDisconnected);
            ServerShowerhead.OnServerDataReceived.AddListener(ServerDataReceived);
            ServerShowerhead.OnServerError.AddListener(ServerErrored);

            ServerShowerhead.StartInternal(Port);
        }

        public override void ServerStop()
        {
            ServerShowerhead.Stop();
        }

        public override void Shutdown()
        {
            throw new NotImplementedException();
        }
        #endregion

        private void ProcessQueuedServerPackets()
        {
            if (ServerShowerhead.Incoming.Count > 0)
            {
                while (ServerShowerhead.Incoming.Count > 0)
                {
                    QueuedPacket packet;

                    if (ServerShowerhead.Incoming.TryDequeue(out packet))
                    {
                        byte[] databuff = new byte[packet.contents.Length];
                        packet.contents.CopyTo(databuff);

                        OnServerDataReceived.Invoke(packet.connectionId, databuff);
                    }
                }
            }
        }

        public override int GetMaxPacketSize(int channelId = 0)
        {
            return (int)Library.maxPacketSize;  // 33,554,432 bytes.
        }

        private void ServerConnected(int connectionId)
        {
            OnServerConnected.Invoke(connectionId);
        }

        private void ServerDisconnected(int connectionId)
        {
            OnServerDisconnected.Invoke(connectionId);
        }

        private void ServerDataReceived(int arg0, byte[] arg1)
        {
            OnServerDataReceived.Invoke(arg0, arg1);
        }

        private void ServerErrored(int arg0, Exception arg1)
        {
            OnServerError.Invoke(arg0, arg1);
        }
    }
}
