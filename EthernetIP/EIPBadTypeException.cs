using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    [Serializable]
    public class EIPBadTypeException : Exception
    {
        ushort expectedTypeValue;
        ushort actualTypeValue;

        public ushort ExpectedType
        {
            get { return expectedTypeValue; }
        }

        public ushort ActualType
        {
            get { return actualTypeValue; }
        }

        public EIPBadTypeException(ushort expectedType, ushort actualType)
            : base($"Tag type of '{expectedType.ToString("X2")}' expected.  Type of '{actualType.ToString("X2")}' returned.")
        {
            expectedTypeValue = expectedType;
            actualTypeValue = actualType;
        }
    }
}
