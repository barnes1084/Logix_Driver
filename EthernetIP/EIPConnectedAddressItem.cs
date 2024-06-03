using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class EIPConnectedAddressItem
    {
        public const int MarshalSize = 8;

        public const ushort TypeIDConst = 0xA1;
        public const ushort LengthConst = 4;

        public ushort TypeID;
        public ushort Length;
        public uint ConnectionIdentifier;

        public EIPConnectedAddressItem()
        {
            TypeID = TypeIDConst;
            Length = LengthConst;
        }

        public EIPConnectedAddressItem(uint connectionID)
        {
            TypeID = TypeIDConst;
            Length = LengthConst;
            ConnectionIdentifier = connectionID;
        }

        public CPFItem ToCPFItem()
        {
            if (TypeID != TypeIDConst) {
                throw new EIPProtocolViolationException($"TypeID must be 0x{TypeIDConst.ToString("X4")}.");
            }
            if (Length != LengthConst) {
                throw new EIPProtocolViolationException($"Length must be {LengthConst}");
            }
            return new CPFItem(TypeIDConst, BitConverter.GetBytes(ConnectionIdentifier));
        }

        public static EIPConnectedAddressItem FromCPFItem(CPFItem item)
        {
            if (item.TypeID != TypeIDConst) {
                throw new EIPProtocolViolationException($"item.TypeID must be 0x{TypeIDConst.ToString("X4")}.");
            }
            if (item.Length != LengthConst) {
                throw new EIPProtocolViolationException($"item.Length must be {LengthConst}.");
            }

            var connectedAddrItem = new EIPConnectedAddressItem()
            {
                TypeID = item.TypeID,
                Length = item.Length,
                ConnectionIdentifier = BitConverter.ToUInt32(item.Data, 0)
            };
            return connectedAddrItem;
        }
    }
}
