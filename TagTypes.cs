using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public enum TagTypes
    {
        Bool = 0,
        UInt8 = 1,
        Int8 = 2,
        UInt16 = 3,
        Int16 = 4,
        UInt32 = 5,
        Int32 = 6,
        UInt64 = 7,
        Int64 = 8,
        Single = 9,
        Double = 11,
        String = 12,
        UInt8Array = 13,
        UInt16Array = 14,
        Char = 15,
        CharArray = 16,
        Struct = 20,

        Custom = 1000,
    }
}
