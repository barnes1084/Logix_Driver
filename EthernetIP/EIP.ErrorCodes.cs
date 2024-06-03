using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public static partial class EIP
    {
        public const ushort ErrorCode_Success = 0x0000;
        public const ushort ErrorCode_UnsupportedCommand = 0x0001;
        public const ushort ErrorCode_InsufficientMemory = 0x0002;
        public const ushort ErrorCode_InvalidData = 0x0003;
        public const ushort ErrorCode_InvalidSessionHandle = 0x0064;
        public const ushort ErrorCode_InvalidLength = 0x0065;
        public const ushort ErrorCode_UnsupportedProtocolRevision = 0x0069;

        public static string GetErrorText(uint errorCode)
        {
            switch (errorCode) {
                case ErrorCode_Success:
                    return "Success";
                case ErrorCode_UnsupportedCommand:
                    return "Unsupported command";
                case ErrorCode_InsufficientMemory:
                    return "Insufficient memory";
                case ErrorCode_InvalidData:
                    return "Invalid data";
                case ErrorCode_InvalidSessionHandle:
                    return "Invalid session handle";
                case ErrorCode_InvalidLength:
                    return "Invalid length";
                case ErrorCode_UnsupportedProtocolRevision:
                    return "Unsupported protocol revision";
                default:
                    throw new ArgumentException("Unrecognized value", "errorCode");
            }

        }
    }
}
