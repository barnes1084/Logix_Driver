using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Logix
{
    public static class CIPConnectionManager
    {
        public const byte Instance_OpenRequest = 0x01;
        public const byte Instance_OpenFormatRejected = 0x02;
        public const byte Instance_OpenResourceRejected = 0x03;
        public const byte Instance_OpenOtherRejected = 0x04;
        public const byte Instance_CloseRequest = 0x05;
        public const byte Instance_CloseFormatRequest = 0x06;
        public const byte Instance_CloseOtherRequest = 0x07;
        public const byte Instance_ConnectionTimeout = 0x08;

        public const byte Instance_Service_Forward_Close = 0x4E;
        public const byte Instance_Service_Unconnected_Send = 0x52;
        public const byte Instance_Service_Forward_Open = 0x54;
        public const byte Instance_Service_Get_Connection_Data = 0x56;
        public const byte Instance_Service_Search_Connection_Data = 0x57;
        public const byte Instance_Service_Get_Connection_Owner = 0x5A;
        public const byte Instance_Service_Large_Forward_Open = 0x5B;

        [StructLayout(LayoutKind.Explicit, Pack = 2, Size = 2)]
        public struct Instance_Service_Parameter_NetworkConnectionParameters
        {
            public enum ConnectionTypes : ushort
            {
                Null = 0,
                Multicast = 1,
                PointToPoint = 2,
                Reserved = 3
            }

            public enum Priorities
            {
                Low = 0,
                High = 1,
                Scheduled = 2,
                Urgent = 3
            }

            const ushort RedundantOwner_Mask = 0x8000;
            const ushort ConnectionType_Mask = 0x6000;
            const ushort Priority_Mask = 0x0C00;
            const ushort Variable_Mask = 0x0200;
            const ushort ConnectionSize_Mask = 0x01FF;

            [FieldOffset(0)]
            public ushort Value;

            public ConnectionTypes ConnectionType
            {
                get { return (ConnectionTypes)((Value & ConnectionType_Mask) >> 13); }
                set { Value = (ushort)((Value & (~ConnectionType_Mask)) | (((int)value) << 13)); }
            }

            public bool RedundantOwner
            {
                get { return Convert.ToBoolean((Value & RedundantOwner_Mask) >> 15); }
                set { Value = (ushort)((Value & (~RedundantOwner_Mask)) | ((Convert.ToInt32(value) << 15))); }
            }

            public Priorities Priority
            {
                get { return (Priorities)((Value & Priority_Mask) >> 10); }
                set { Value = (ushort)((Value & (~Priority_Mask)) | (((int)value) << 10)); }
            }

            public bool VariableSize
            {
                get { return Convert.ToBoolean((Value & Variable_Mask) >> 9); }
                set { Value = (ushort)((Value & (~Variable_Mask)) | ((Convert.ToInt32(value) << 9))); }
            }

            public int ConnectionSize
            {
                get { return Value & ConnectionSize_Mask; }
                set
                {
                    if ((value < 0) && (value > 511)) {
                        throw new ArgumentException("Must be between 0-511", "value");
                    }
                    Value = (ushort)((Value & (~ConnectionSize_Mask)) | value);
                }
            }
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1)]
        public struct Instance_Service_Parameter_PriorityTime_tick
        {
            [FieldOffset(0)]
            public byte Value;

            public bool Priority
            {
                get { return Value.CIPIsFlagSet(4); }
                set { this.Value.CIPSetOrResetFlag(4, value); }
            }

            /// <summary>
            /// Actual value is 2^TickTime.  So, 2 would be 4 milliseconds, 10 would be 1024 milliseconds, etc.
            /// </summary>
            public int TickTime
            {
                get { return (Value & 0x0F); }

                set
                {
                    if ((value > 15) || (value < 0)) {
                        throw new ArgumentException("Must be between 0-15.", "value");
                    }
                    this.Value = (byte)((this.Value & 0xF0) | (value & 0x0F));
                }
            }
        }
    }
}
