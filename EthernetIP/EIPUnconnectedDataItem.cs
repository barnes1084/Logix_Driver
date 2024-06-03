using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class EIPUnconnectedDataItem
    {
        public const ushort TypeIDConst = 0xB2;

        public ushort TypeID;
        public ushort Length;
        public byte[] Data;

        public EIPUnconnectedDataItem()
        {
            TypeID = TypeIDConst;
        }

        public EIPUnconnectedDataItem(byte[] data)
        {
            TypeID = TypeIDConst;
            Length = (ushort)(data?.Length ?? 0);
            Data = data;
        }

        public CPFItem ToCPFItem()
        {
            if (TypeID != TypeIDConst) {
                throw new EIPProtocolViolationException($"TypeID must be 0x{TypeIDConst.ToString("X4")}.");
            }
            if (Length != (Data?.Length ?? 0)) {
                throw new EIPProtocolViolationException($"Length must equal Data.Length.");
            }
            return new CPFItem(TypeIDConst, Data);
        }

        public static EIPUnconnectedDataItem FromCPFItem(CPFItem item)
        {
            if (item.TypeID != TypeIDConst) {
                throw new EIPProtocolViolationException($"item.TypeID must be 0x{TypeIDConst.ToString("X4")}.");
            }
            if (item.Length != (item.Data?.Length ?? 0)) {
                throw new EIPProtocolViolationException($"item.Length must equal item.Data.Length.");
            }

            var unconnectedDataItem = new EIPUnconnectedDataItem()
            {
                TypeID = item.TypeID,
                Length = item.Length,
                Data = item.Data
            };
            return unconnectedDataItem;
        }
    }
}
