using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    [Serializable]
    public class LogixSocketException : Exception
    {
        public LogixSocketException()
            : base()
        {
        }

        public LogixSocketException(string message)
            : base(message)
        {
        }

        public LogixSocketException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public LogixSocketException(Exception innerException)
            : base(innerException.Message, innerException)
        {
        }
    }
}
