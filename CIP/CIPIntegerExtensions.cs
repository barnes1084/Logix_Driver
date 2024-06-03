using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Logix
{
    public static class CIPIntegerExtensions
    {
        [StructLayout(LayoutKind.Explicit, Pack = 2, Size = 2)]
        struct LgxCIPWordStruct
        {
            [FieldOffset(0)]
            public ushort UInt16;
            [FieldOffset(0)]
            public short Int16;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 4)]
        struct LgxCIPDWordStruct
        {
            [FieldOffset(0)]
            public uint UInt32;
            [FieldOffset(0)]
            public int Int32;
        }

        public static bool CIPIsFlagSet(this byte value, int bitNo)
        {
            if ((bitNo < 0) || (bitNo > 7)) {
                throw new IndexOutOfRangeException();
            }
            int mask = (1 << bitNo);
            return mask == (value & mask);
        }

        public static void CIPSetFlag(this byte value, int bitNo)
        {
            if ((bitNo < 0) || (bitNo > 7)) {
                throw new IndexOutOfRangeException();
            }
            value = (byte)(value | (1 << bitNo));
        }

        public static void CIPResetFlag(this byte value, int bitNo)
        {
            if ((bitNo < 0) || (bitNo > 7)) {
                throw new IndexOutOfRangeException();
            }
            value = (byte)(value & (~(1 << bitNo)));
        }

        public static void CIPSetOrResetFlag(this byte value, int bitNo, bool newValue)
        {
            if(newValue) {
                value.CIPSetFlag(bitNo);
            } else {
                value.CIPResetFlag(bitNo);
            }
        }

        public static bool CIPIsFlagSet(this ushort value, int bitNo)
        {
            if ((bitNo < 0) || (bitNo > 15)) {
                throw new IndexOutOfRangeException();
            }
            int mask = (1 << bitNo);
            return mask == (value & mask);
        }

        public static void CIPSetFlag(this ushort value, int bitNo)
        {
            if ((bitNo < 0) || (bitNo > 15)) {
                throw new IndexOutOfRangeException();
            }
            value = (ushort)(value | (1 << bitNo));
        }

        public static void CIPResetFlag(this ushort value, int bitNo)
        {
            if ((bitNo < 0) || (bitNo > 15)) {
                throw new IndexOutOfRangeException();
            }
            value = (ushort)(value & (~(1 << bitNo)));
        }

        public static void CIPSetOrResetFlag(this ushort value, int bitNo, bool newValue)
        {
            if (newValue) {
                value.CIPSetFlag(bitNo);
            } else {
                value.CIPResetFlag(bitNo);
            }
        }

        public static bool CIPFlagIsSet(this short value, int bitNo)
        {
            LgxCIPWordStruct ws = new LgxCIPWordStruct() { Int16 = value };
            return ws.UInt16.CIPIsFlagSet(bitNo);
        }

        public static void CIPSetFlag(this short value, int bitNo)
        {
            LgxCIPWordStruct ws = new LgxCIPWordStruct() { Int16 = value };
            ws.UInt16.CIPSetFlag(bitNo);
            value = ws.Int16;
        }

        public static void CIPResetFlag(this short value, int bitNo)
        {
            LgxCIPWordStruct ws = new LgxCIPWordStruct() { Int16 = value };
            ws.UInt16.CIPResetFlag(bitNo);
            value = ws.Int16;
        }

        public static void CIPSetOrResetFlag(this short value, int bitNo, bool newValue)
        {
            if (newValue) {
                value.CIPSetFlag(bitNo);
            } else {
                value.CIPResetFlag(bitNo);
            }
        }
    }
}
