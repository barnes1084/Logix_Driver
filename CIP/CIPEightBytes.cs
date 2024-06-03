using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Logix
{
    [StructLayout(LayoutKind.Explicit, Pack = 2, Size = 8)]
    public struct CIPEightBytes
    {
        [FieldOffset(0)]
        public ulong ValueUL;

        public string Value
        {
            get
            {
                return Encoding.ASCII.GetString(BitConverter.GetBytes(ValueUL)).TrimEnd('\0');
            }
            set
            {
                string safeValue = (value ?? "").PadRight(8, '\0').Substring(0, 8);
                this.ValueUL = BitConverter.ToUInt64(Encoding.ASCII.GetBytes(safeValue), 0);
            }
        }

        public byte GetByte(int byteNo)
        {
            if ((byteNo < 0) || (byteNo > 7)) {
                throw new IndexOutOfRangeException();
            }
            return (byte)((ValueUL >> (byteNo * 8)) & 0xFFUL);
        }

        public void SetByte(int byteNo, byte value)
        {
            if ((byteNo < 0) || (byteNo > 7)) {
                throw new IndexOutOfRangeException();
            }
            int shiftBits = (byteNo * 8);
            this.ValueUL = (((ulong)value) << shiftBits) | (this.ValueUL & ~(0xFFUL << shiftBits));
        }
    }
}
