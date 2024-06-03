using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public static partial class EIP
    {
        public const ushort Command_NOP = 0x0000;
        public const ushort Command_ListServices = 0x0004;
        public const ushort Command_ListIdentity = 0x0063;
        public const ushort Command_ListInterfaces = 0x0064;
        public const ushort Command_RegisterSession = 0x0065;
        public const ushort Command_UnRegisterSession = 0x0066;
        public const ushort Command_SendRRData = 0x006F;
        public const ushort Command_SendUnitData = 0x0070;
        public const ushort Command_IndicateStatus = 0x0072;
        public const ushort Command_Cancel = 0x0073;
    }
}
