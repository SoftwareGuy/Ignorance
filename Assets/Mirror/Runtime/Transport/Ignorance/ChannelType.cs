using System;
using ENet;

namespace Mirror.Ignorance
{
    [Serializable]
    public enum ChannelType
    {
        Reliable,
        Unreliable,
        UnreliableFragmented,
        UnreliableSequenced,
    }

    public static class ChannelTypeExtensions
    {
        public static PacketFlags ToENetPacketFlag(this ChannelType type)
        {
            switch (type)
            {
                case ChannelType.Reliable:
                    return PacketFlags.Reliable;
                case ChannelType.Unreliable:
                    return PacketFlags.Unsequenced;
                case ChannelType.UnreliableFragmented:
                    return PacketFlags.UnreliableFragment;
                case ChannelType.UnreliableSequenced:
                    return PacketFlags.None;
                default:
                    return PacketFlags.Unsequenced;
            }
        }
    }
}
