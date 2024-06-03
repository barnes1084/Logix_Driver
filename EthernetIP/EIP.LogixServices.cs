using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public static partial class EIP
    {
        public const byte Service_ReadTag = 0x4C;
        public const byte Service_ReadTagFragmented = 0x52;
        public const byte Service_WriteTag = 0x4D;
        public const byte Service_WriteTagFragmented = 0x53;
        public const byte Service_ReadModifyWriteTag = 0x4E;
        public const byte Service_MultipleServicePacket = 0x0A;


    }
}
