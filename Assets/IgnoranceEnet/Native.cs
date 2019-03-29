using System.Runtime.InteropServices;
using System;
using System.IO;
using System.Diagnostics;
using System.Security;
using UnityEngine;


namespace Ignorance.Enet
{

    public static unsafe class Native
    {
        // tn_service_state_t: service state
        public enum ServiceState
        {
            TN_SERVICE_NEW,
            TN_SERVICE_STARTING,
            TN_SERVICE_STARTED,
            TN_SERVICE_STOPPING,
            TN_SERVICE_STOPPED,
            TN_SERVICE_ERROR,
            TN_SERVICE_INVALID,
        };

        public enum PacketFlags
        {
            None,
            Reliable,
            Unordered,
        };

        #region event data structures
        // tn_event_type_t: event type
        internal enum EventType
        {
            TN_EVENT_NONE,
            TN_EVENT_START,
            TN_EVENT_STOP,
            TN_EVENT_IOERROR,
            TN_EVENT_CLIENT_OPEN,
            TN_EVENT_CLIENT_CLOSE,
            TN_EVENT_CLIENT_READ,
        };

        // tn_event_base_t: base event
        [StructLayout(LayoutKind.Explicit, Size = 256, CharSet = CharSet.Ansi)]
        internal readonly ref struct tn_event_base_t
        {
            [FieldOffset(0)] public readonly UInt32 id;
            [FieldOffset(4)] public readonly UInt32 type;
        };

        // tn_event_error_t: error event
        [StructLayout(LayoutKind.Explicit, Size = 256, CharSet = CharSet.Ansi)]
        public readonly ref struct tn_event_error_t
        {
            [FieldOffset(0)] public readonly UInt32 id;
            [FieldOffset(4)] public readonly UInt32 type;
            [FieldOffset(8)] public readonly UInt64 client_id;
            [FieldOffset(24)] public readonly UInt32 error_code;
            //[FieldOffset(28)] public readonly byte *error_string;
        };
        public readonly ref struct EventError
        {
            public readonly int client_id;
            public readonly UInt32 error_code;
            public readonly string error_string;

            public unsafe EventError(in tn_event_error_t* evt)
            {
                client_id = (int)evt->client_id;
                error_code = evt->error_code;
                error_string = Marshal.PtrToStringAnsi(IntPtr.Add((IntPtr)evt, 28));
            }
        };

        // tn_event_client_open_t: client connected
        [StructLayout(LayoutKind.Explicit, Size = 256, CharSet = CharSet.Ansi)]
        public readonly ref struct tn_event_client_open_t
        {
            [FieldOffset(0)] public readonly UInt32 id;
            [FieldOffset(4)] public readonly UInt32 type;
            [FieldOffset(8)] public readonly UInt64 client_id;
            //[FieldOffset(16)] public fixed byte host_local[64];
            //[FieldOffset(80)] public fixed byte host_remote[64];
            [FieldOffset(144)] public readonly UInt16 port_local;
            [FieldOffset(146)] public readonly UInt16 port_remote;
        };
        public readonly ref struct EventClientOpen
        {
            public readonly int client_id;
            public readonly string ip;
            public readonly UInt16 port;

            public unsafe EventClientOpen(in tn_event_client_open_t* evt)
            {
                client_id = (int)evt->client_id;
                ip = Marshal.PtrToStringAnsi(IntPtr.Add((IntPtr)evt, 16));
                port = evt->port_remote;
            }
        };

        // tn_event_client_close_t: client disconnected
        [StructLayout(LayoutKind.Explicit, Size = 256, CharSet = CharSet.Ansi)]
        public readonly ref struct tn_event_client_close_t
        {
            [FieldOffset(0)] public readonly UInt32 id;
            [FieldOffset(4)] public readonly UInt32 type;
            [FieldOffset(8)] public readonly UInt64 client_id;
        };
        public readonly ref struct EventClientClose
        {
            public readonly int client_id;

            public unsafe EventClientClose(in tn_event_client_close_t* evt)
            {
                client_id = (int)evt->client_id;
            }
        };

        // tn_event_client_read_t: client recv buffer
        [StructLayout(LayoutKind.Explicit, Size = 256, CharSet = CharSet.Ansi)]
        public readonly ref struct tn_event_client_read_t
        {
            [FieldOffset(0)] public readonly UInt32 id;
            [FieldOffset(4)] public readonly UInt32 type;
            [FieldOffset(8)] public readonly UInt64 client_id;
            [FieldOffset(16)] public readonly byte* buffer;
            [FieldOffset(24)] public readonly UInt64 len;
        };
        public readonly ref struct EventClientRead
        {
            public readonly int client_id;
            public readonly ReadOnlySpan<byte> buffer;

            public unsafe EventClientRead(in tn_event_client_read_t* evt)
            {
                client_id = (int)evt->client_id;
                buffer = new ReadOnlySpan<byte>(evt->buffer, (int)evt->len);
            }
        };
        #endregion

        #region native API
        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_setup(UInt32 max_clients);

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_cleanup();

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_start([MarshalAs(UnmanagedType.LPStr)] string ipstr, UInt16 port);

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_stop();

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_events_acquire(tn_event_base_t*** out_evt_ptr_arr, UInt64* out_evt_ptr_count);

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_events_release();

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_send(UInt64 client_id, byte* buffer, UInt64 length, byte channel, PacketFlags flags);
        #endregion

        #region public API
        internal interface IServiceSubscriber
        {
            void OnServiceStart();
            void OnServiceStop();
            void OnServiceClientOpen(in EventClientOpen evt);
            void OnServiceClientClose(in EventClientClose evt);
            void OnServiceClientRead(in EventClientRead evt);
            void OnServiceError(in EventError evt);
        };

        public static bool ServiceSetup(int maxClients)
        {
            if (service_setup((uint)maxClients) != 0) return false;
            return true;
        }

        public static bool ServiceCleanup()
        {
            if (service_cleanup() != 0) return false;
            return true;
        }

        public static bool ServiceStart(string ipstr, int port)
        {
            if (service_start(ipstr, (UInt16)port) != 0) return false;
            return true;
        }

        public static bool ServiceStop()
        {
            if (service_stop() != 0) return false;
            return true;
        }

        public static bool ServiceSend(int client_id, in byte[] buffer, int length, int channel, PacketFlags flags)
        {
            bool rv = true;
            fixed (byte* ptr = buffer)
            {
                if (service_send((UInt64)client_id, ptr, (UInt64)length, (byte)channel, flags) != 0) rv = false;
            }
            return rv;
        }

        public static bool ServiceSend(int client_id, in ReadOnlySpan<byte> buffer, int channel, PacketFlags flags)
        {
            bool rv = true;
            fixed (byte* ptr = buffer)
            {
                if (service_send((UInt64)client_id, ptr, (UInt64)buffer.Length, (byte)channel, flags) != 0) rv = false;
            }
            return rv;
        }

        internal static bool ServiceUpdate(IServiceSubscriber subscriber)
        {
            tn_event_base_t** evtPtr;
            UInt64 evtCount = 0;

            if (service_events_acquire(&evtPtr, &evtCount) != 0) return false;

            for (UInt64 e = 0; e < evtCount; e++)
            {
                switch ((EventType)evtPtr[e]->type)
                {
                    case EventType.TN_EVENT_IOERROR:
                        subscriber.OnServiceError(new EventError((tn_event_error_t*)evtPtr[e]));
                        break;
                    case EventType.TN_EVENT_START:
                        subscriber.OnServiceStart();
                        break;
                    case EventType.TN_EVENT_STOP:
                        subscriber.OnServiceStop();
                        break;
                    case EventType.TN_EVENT_CLIENT_OPEN:
                        subscriber.OnServiceClientOpen(new EventClientOpen((tn_event_client_open_t*)evtPtr[e]));
                        break;
                    case EventType.TN_EVENT_CLIENT_CLOSE:
                        subscriber.OnServiceClientClose(new EventClientClose((tn_event_client_close_t*)evtPtr[e]));
                        break;
                    case EventType.TN_EVENT_CLIENT_READ:
                        subscriber.OnServiceClientRead(new EventClientRead((tn_event_client_read_t*)evtPtr[e]));
                        break;
                }
            }

            if (service_events_release() != 0)
            {
                UnityEngine.Debug.LogError("Failed to release native events");
                return false;
            }

            return true;
        }
        #endregion
    }
}