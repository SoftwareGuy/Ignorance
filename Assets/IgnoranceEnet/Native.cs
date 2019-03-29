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

        public const int SizeOfUInt8 = 1;
        public const int SizeOfUInt16 = 2;
        public const int SizeOfUInt32 = 4;
        public const int SizeOfUInt64 = 8;
        public const int SizeOfBaseEvent = 128;

        // tn_event_base_t: base event
        [StructLayout(LayoutKind.Explicit, Size = SizeOfBaseEvent, CharSet = CharSet.Ansi)]
        internal readonly ref struct tn_event_base_t
        {
            [FieldOffset(0)] public readonly UInt32 id;
            [FieldOffset(4)] public readonly UInt32 type;
        };

        // tn_event_error_t: error event
        [StructLayout(LayoutKind.Explicit, Size = SizeOfBaseEvent, CharSet = CharSet.Ansi)]
        public readonly ref struct tn_event_error_t
        {
            [FieldOffset(0)] public readonly UInt32 id;
            [FieldOffset(4)] public readonly UInt32 type;
            [FieldOffset(8)] public readonly UInt64 client_id;
            [FieldOffset(24)] public readonly UInt32 error_code;
            //[FieldOffset(28)] public fixed byte error_string[];
        };
        public readonly ref struct EventError
        {
            public readonly int client_id;
            public readonly UInt32 error_code;
            public readonly string error_string;

            public unsafe EventError(in tn_event_error_t *evt)
            {
                client_id = (int)evt->client_id;
                error_code = evt->error_code;
                error_string = Marshal.PtrToStringAnsi(IntPtr.Add((IntPtr)evt, SizeOfUInt32 + SizeOfUInt32 + SizeOfUInt64 + SizeOfUInt32));
            }
        };

        // tn_event_client_open_t: client connected
        [StructLayout(LayoutKind.Explicit, Size = SizeOfBaseEvent, CharSet = CharSet.Ansi)]
        public readonly ref struct tn_event_client_open_t
        {
            [FieldOffset(0)] public readonly UInt32 id;
            [FieldOffset(4)] public readonly UInt32 type;
            [FieldOffset(8)] public readonly UInt64 client_id;
            //[FieldOffset(80)] public fixed byte host[64];
            [FieldOffset(146)] public readonly UInt16 port;
        };
        public readonly ref struct EventClientOpen
        {
            public readonly int client_id;
            public readonly string ip;
            public readonly UInt16 port;

            public unsafe EventClientOpen(in tn_event_client_open_t *evt)
            {
                client_id = (int)evt->client_id;
                ip = Marshal.PtrToStringAnsi(IntPtr.Add((IntPtr)evt, SizeOfUInt32 + SizeOfUInt32 + SizeOfUInt64));
                port = evt->port;
            }
        };

        // tn_event_client_close_t: client disconnected
        [StructLayout(LayoutKind.Explicit, Size = SizeOfBaseEvent, CharSet = CharSet.Ansi)]
        public readonly ref struct tn_event_client_close_t
        {
            [FieldOffset(0)] public readonly UInt32 id;
            [FieldOffset(4)] public readonly UInt32 type;
            [FieldOffset(8)] public readonly UInt64 client_id;
        };
        public readonly ref struct EventClientClose
        {
            public readonly int client_id;

            public unsafe EventClientClose(in tn_event_client_close_t *evt)
            {
                client_id = (int)evt->client_id;
            }
        };

        // tn_event_client_read_t: client recv buffer
        [StructLayout(LayoutKind.Explicit, Size = SizeOfBaseEvent, CharSet = CharSet.Ansi)]
        public readonly ref struct tn_event_client_read_t
        {
            [FieldOffset(0)] public readonly UInt32 id;
            [FieldOffset(4)] public readonly UInt32 type;
            [FieldOffset(8)] public readonly UInt64 client_id;
            [FieldOffset(16)] public readonly byte *buffer;
            [FieldOffset(24)] public readonly UInt64 len;
        };
        public readonly ref struct EventClientRecv
        {
            public readonly int client_id;
            public readonly Span<byte> buffer;

            public unsafe EventClientRecv(in tn_event_client_read_t *evt)
            {
                client_id = (int)evt->client_id;
                buffer = new Span<byte>(evt->buffer, (int)evt->len);
            }
        };
        #endregion

        #region Native Client API
        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_client_setup();

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_client_cleanup();

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_client_start([MarshalAs(UnmanagedType.LPStr)] string ipstr, UInt16 port);

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_client_stop();

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_client_events_acquire(tn_event_base_t ***out_evt_ptr_arr, UInt64 *out_evt_ptr_count);

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_client_events_release();

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_client_send(byte *buffer, UInt64 length, byte channel, PacketFlags flags);
        #endregion

        #region Managed Client API
        public interface IEnetClientSubscriber
        {
            void OnEnetClientStart();
            void OnEnetClientStop();
            void OnEnetClientOpen(in EventClientOpen evt);
            void OnEnetClientClose(in EventClientClose evt);
            void OnEnetClientRecv(in EventClientRecv evt);
            void OnEnetClientError(in EventError evt);
        };

        public static bool ClientSetup()
        {
            if (service_client_setup() != 0) return false;
            return true;
        }

        public static bool ClientCleanup()
        {
            if (service_client_cleanup() != 0) return false;
            return true;
        }

        public static bool ClientStart(string ipstr, int port)
        {
            if (service_client_start(ipstr, (UInt16)port) != 0) return false;
            return true;
        }

        public static bool ClientStop()
        {
            if (service_client_stop() != 0) return false;
            return true;
        }

        public static bool ClientSend(in byte[] buffer, int length, int channel, PacketFlags flags)
        {
            bool rv = true;
            fixed (byte *ptr = buffer)
            {
                if (service_client_send(ptr, (UInt64)length, (byte)channel, flags) != 0) rv = false;
            }
            return rv;
        }

        public static bool ClientSend(in Span<byte> buffer, int channel, PacketFlags flags)
        {
            bool rv = true;
            fixed (byte *ptr = buffer)
            {
                if (service_client_send(ptr, (UInt64)buffer.Length, (byte)channel, flags) != 0) rv = false;
            }
            return rv;
        }

        public static bool ClientUpdate(IEnetClientSubscriber subscriber)
        {
            tn_event_base_t **evtPtr;
            UInt64 evtCount = 0;

            if (service_client_events_acquire(&evtPtr, &evtCount) != 0) return false;

            for (UInt64 e = 0; e < evtCount; e++)
            {
                switch ((EventType)evtPtr[e]->type)
                {
                case EventType.TN_EVENT_IOERROR:
                    subscriber.OnEnetClientError(new EventError((tn_event_error_t *)evtPtr[e]));
                    break;
                case EventType.TN_EVENT_START:
                    subscriber.OnEnetClientStart();
                    break;
                case EventType.TN_EVENT_STOP:
                    subscriber.OnEnetClientStop();
                    break;
                case EventType.TN_EVENT_CLIENT_OPEN:
                    subscriber.OnEnetClientOpen(new EventClientOpen((tn_event_client_open_t *)evtPtr[e]));
                    break;
                case EventType.TN_EVENT_CLIENT_CLOSE:
                    subscriber.OnEnetClientClose(new EventClientClose((tn_event_client_close_t *)evtPtr[e]));
                    break;
                case EventType.TN_EVENT_CLIENT_READ:
                    subscriber.OnEnetClientRecv(new EventClientRecv((tn_event_client_read_t *)evtPtr[e]));
                    break;
                }
            }

            if (service_client_events_release() != 0)
            {
                UnityEngine.Debug.LogError("Failed to release native events");
                return false;
            }

            return true;
        }
        #endregion


        #region Native Service API
        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_server_setup(UInt32 max_clients);

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_server_cleanup();

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_server_start([MarshalAs(UnmanagedType.LPStr)] string ipstr, UInt16 port);

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_server_stop();

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_server_events_acquire(tn_event_base_t*** out_evt_ptr_arr, UInt64* out_evt_ptr_count);

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_server_events_release();

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_server_send(UInt64 client_id, byte* buffer, UInt64 length, byte channel, PacketFlags flags);

        [DllImport("IgnoranceEnet", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl), SuppressUnmanagedCodeSecurity]
        private static extern int service_server_close(UInt64 client_id);
        #endregion

        #region Managed Service API
        public interface IEnetServiceSubscriber
        {
            void OnEnetServiceStart();
            void OnEnetServiceStop();
            void OnEnetServiceOpen(in EventClientOpen evt);
            void OnEnetServiceClose(in EventClientClose evt);
            void OnEnetServiceRecv(in EventClientRecv evt);
            void OnEnetServiceError(in EventError evt);
        };

        public static bool ServiceSetup(int maxClients)
        {
            if (service_server_setup((uint)maxClients) != 0) return false;
            return true;
        }

        public static bool ServiceCleanup()
        {
            if (service_server_cleanup() != 0) return false;
            return true;
        }

        public static bool ServiceStart(string ipstr, int port)
        {
            if (service_server_start(ipstr, (UInt16)port) != 0) return false;
            return true;
        }

        public static bool ServiceStop()
        {
            if (service_server_stop() != 0) return false;
            return true;
        }

        public static bool ServiceSend(int client_id, in byte[] buffer, int length, int channel, PacketFlags flags)
        {
            bool rv = true;
            fixed (byte* ptr = buffer)
            {
                if (service_server_send((UInt64)client_id, ptr, (UInt64)length, (byte)channel, flags) != 0) rv = false;
            }
            return rv;
        }

        public static bool ServiceSend(int client_id, in Span<byte> buffer, int channel, PacketFlags flags)
        {
            bool rv = true;
            fixed (byte* ptr = buffer)
            {
                if (service_server_send((UInt64)client_id, ptr, (UInt64)buffer.Length, (byte)channel, flags) != 0) rv = false;
            }
            return rv;
        }

        public static bool ServiceClose(int client_id)
        {
            if (service_server_close((UInt64)client_id) != 0) return false;
            return true;
        }

        public static bool ServiceUpdate(IEnetServiceSubscriber subscriber)
        {
            tn_event_base_t** evtPtr;
            UInt64 evtCount = 0;

            if (service_server_events_acquire(&evtPtr, &evtCount) != 0) return false;

            for (UInt64 e = 0; e < evtCount; e++)
            {
                switch ((EventType)evtPtr[e]->type)
                {
                case EventType.TN_EVENT_IOERROR:
                    subscriber.OnEnetServiceError(new EventError((tn_event_error_t*)evtPtr[e]));
                    break;
                case EventType.TN_EVENT_START:
                    subscriber.OnEnetServiceStart();
                    break;
                case EventType.TN_EVENT_STOP:
                    subscriber.OnEnetServiceStop();
                    break;
                case EventType.TN_EVENT_CLIENT_OPEN:
                    subscriber.OnEnetServiceOpen(new EventClientOpen((tn_event_client_open_t*)evtPtr[e]));
                    break;
                case EventType.TN_EVENT_CLIENT_CLOSE:
                    subscriber.OnEnetServiceClose(new EventClientClose((tn_event_client_close_t*)evtPtr[e]));
                    break;
                case EventType.TN_EVENT_CLIENT_READ:
                    subscriber.OnEnetServiceRecv(new EventClientRecv((tn_event_client_read_t*)evtPtr[e]));
                    break;
                }
            }

            if (service_server_events_release() != 0)
            {
                UnityEngine.Debug.LogError("Failed to release native events on server");
                return false;
            }

            return true;
        }
        #endregion
    }
}