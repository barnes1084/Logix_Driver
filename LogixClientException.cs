using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    [Serializable]
    public class LogixClientException : Exception
    {
        public LogixClientException()
            : base()
        {
        }

        public LogixClientException(string message)
            : base(message)
        {
        }

        public LogixClientException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public LogixClientException(Exception innerException)
            : base(innerException.Message, innerException)
        {
        }
    }
}
