using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    [Serializable]
    public class CIPProtocolViolationException : Exception
    {
        public CIPProtocolViolationException()
            : base()
        {
        }

        public CIPProtocolViolationException(string message)
            : base(message)
        {
        }
    }
}
