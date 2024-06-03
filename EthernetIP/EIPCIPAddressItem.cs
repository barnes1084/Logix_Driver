using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class EIPCIPAddressItem
    {
        const ushort TypeIDConst = 0x00;
        const ushort LengthConst = 0;

        public ushort TypeID;
        public ushort Length;

        public CPFItem ToCPFItem()
        {
            if (TypeID != TypeIDConst) {
                throw new EIPProtocolViolationException($"TypeID must be 0x{TypeIDConst.ToString("X4")}.");
            }
            if (Length != LengthConst) {
                throw new EIPProtocolViolationException($"Length must be {LengthConst}");
            }
            return new CPFItem(TypeIDConst, null);
        }

        public static EIPCIPAddressItem FromCPFItem(CPFItem item)
        {
            if (item.TypeID != TypeIDConst) {
                throw new EIPProtocolViolationException($"item.TypeID must be 0x{TypeIDConst.ToString("X4")}.");
            }
            if (item.Length != LengthConst) {
                throw new EIPProtocolViolationException($"item.Length must be {LengthConst}");
            }
            return new EIPCIPAddressItem();
        }

        static EIPCIPAddressItem emptyValue;

        public static EIPCIPAddressItem Empty
        {
            get { return emptyValue; }
        }

        static EIPCIPAddressItem()
        {
            emptyValue = new EIPCIPAddressItem();
        }
    }
}
