using System;
using ENet;
using Unity.Collections;
using Unity.Jobs;

namespace Mirror.Ignorance
{
    public struct ServerPumpJob
        : IJob
    {
        private readonly IntPtr _enetHost;

        private NativeArray<ENetEvent> _events;
        private NativeArray<int> _counterOut;
        private int _count;

        public ServerPumpJob(IntPtr enetHost, NativeArray<ENetEvent> output, NativeArray<int> count)
        {
            _enetHost = enetHost;
            _events = output;
            _count = 0;
            _counterOut = count;
        }

        public void Execute()
        {
            try
            {
                //First read events we didn't manage to read last time (possible buffer exhaustion?)
                CheckEvents();

                //Check if we've filled the buffer, early exit if so
                if (_count == _events.Length)
                    return;

                //Pump enet and store the single event from it
                if (Native.enet_host_service(_enetHost, out var evt, 0) <= 0)
                    return;

                _events[_count++] = evt;

                //Read the rest of the events
                CheckEvents();
            }
            finally
            {
                _counterOut[0] = _count;
            }
        }

        private void CheckEvents()
        {
            while (Native.enet_host_check_events(_enetHost, out var nativeEvent) > 0 && _count < _events.Length)
                _events[_count++] = nativeEvent;
        }
    }
}
