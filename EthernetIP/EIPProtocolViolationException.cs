using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    [Serializable]
    public class EIPProtocolViolationException : Exception
    {
        public EIPProtocolViolationException()
            : base()
        {
        }

        public EIPProtocolViolationException(uint errorCode)
            : base(EIP.GetErrorText(errorCode))
        {
        }

        public EIPProtocolViolationException(string message)
            : base(message)
        {
        }
    }
}
