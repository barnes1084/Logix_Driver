using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Logix
{
    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Ansi, Pack = 1, Size = 2)]
    public struct CIPDeviceRevision
    {
        [FieldOffset(0)]
        public ushort WordValue;
        [FieldOffset(0)]
        public byte LowByte;
        [FieldOffset(1)]
        public byte HighByte;

        public byte GetByte(int byteNo)
        {
            if (0 == byteNo) {
                return LowByte;
            } else if (1 == byteNo) {
                return HighByte;
            } else {
                throw new IndexOutOfRangeException();
            }
        }

        public void SetByte(int byteNo, byte value)
        {
            if (0 == byteNo) {
                LowByte = value;
            } else if (1 == byteNo) {
                HighByte = value;
            } else {
                throw new IndexOutOfRangeException();
            }
        }
    }
}
