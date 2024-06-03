using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    [Serializable]
    public class CIPUnexpectedResponseException : Exception
    {
        public CIPUnexpectedResponseException()
            : base()
        {
        }

        public CIPUnexpectedResponseException(string message)
            : base(message)
        {
        }
    }
}
