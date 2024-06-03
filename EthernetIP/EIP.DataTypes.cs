using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public static partial class EIP
    {
        public const ushort DataType_BOOL_Mask = 0x00C1;
        public static ushort DataType_BOOL(int bitNo)
        {
            if ((bitNo < 0) || (bitNo > 7)) {
                throw new ArgumentException("Must be between 0-7.", "bitNo");
            }
            return (ushort)((bitNo << 8) | DataType_BOOL_Mask);
        }

        public const ushort DataType_SINT = 0x00C2;
        public const ushort DataType_INT = 0x00C3;
        public const ushort DataType_DINT = 0x00C4;
        public const ushort DataType_REAL = 0x00CA;
        public const ushort DataType_DWORD = 0x00D3;
        public const ushort DataType_LINT = 0x00C5;
        public const ushort DataType_Struct = 0x02A0;

        public const byte BOOL_False = 0x00;
        public const byte BOOL_True = 0xFF;
    }
}
