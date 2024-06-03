using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class EIPSequencedAddressItem
    {
        public const ushort TypeIDConst = 0x8002;
        public const ushort LengthConst = 8;

        public ushort TypeID;
        public ushort Length;
        public uint ConnectionIdentifier;
        public uint SequenceNumber;

        public CPFItem ToCPFItem()
        {
            if (TypeID != TypeIDConst) {
                throw new EIPProtocolViolationException($"TypeID must be 0x{TypeIDConst.ToString("X4")}.");
            }
            if (Length != LengthConst) {
                throw new EIPProtocolViolationException($"Length must be {LengthConst}");
            }

            byte[] buffer = new byte[LengthConst];
            using (var ms = new MemoryStream(buffer, true))
            using (var writer = new BinaryWriter(ms)) {
                writer.Write(ConnectionIdentifier);
                writer.Write(SequenceNumber);
            }

            return new CPFItem(TypeIDConst, buffer);
        }

        public static EIPSequencedAddressItem FromCPFItem(CPFItem item)
        {
            if (item.TypeID != TypeIDConst) {
                throw new EIPProtocolViolationException($"item.TypeID must be 0x{TypeIDConst.ToString("X4")}.");
            }
            if (item.Length != LengthConst) {
                throw new EIPProtocolViolationException($"item.Length must be {LengthConst}.");
            }

            var sequencedAddrItem = new EIPSequencedAddressItem()
            {
                TypeID = item.TypeID,
                Length = item.Length,
                ConnectionIdentifier = BitConverter.ToUInt32(item.Data, 0),
                SequenceNumber = BitConverter.ToUInt32(item.Data, 4)
            };
            return sequencedAddrItem;
        }
    }
}
