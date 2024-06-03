using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class CPFItem
    {
        public ushort TypeID;
        public ushort Length;
        public byte[] Data;

        public CPFItem()
        {
        }

        public CPFItem(ushort typeID, ushort length)
        {
            TypeID = typeID;
            Length = length;
            if (length > 0) {
                Data = new byte[length];
            }
        }

        public CPFItem(ushort typeID, byte[] data)
        {
            TypeID = typeID;
            Length = (ushort)(data?.Length ?? 0);
            Data = data;
        }
    }
}
