using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    [Serializable]
    public class LogixNotConnectedException : Exception
    {
        public LogixNotConnectedException()
            : base()
        {
        }

        public LogixNotConnectedException(string message)
            : base(message)
        {
        }

        public LogixNotConnectedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public LogixNotConnectedException(Exception innerException)
            : base(innerException.Message, innerException)
        {
        }

        public LogixNotConnectedException(IPEndPoint targetEP, int port, int slot)
            : base($"<{targetEP.ToString()}>.{port},{slot} Not Connected")
        {
        }
    }
}
