using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class CIPIdentity
    {
        const ushort TypeIDConst = 0x0C;
        const int BaseMarshalSize = 33;

        public ushort TypeID;
        public ushort Length;
        public ushort ProtocolVersion;
        public CIPSockaddrInfo SocketAddress;
        public ushort VendorID;
        public ushort DeviceType;
        public ushort ProductCode;
        public CIPDeviceRevision Revision;
        public ushort Status;
        public uint SerialNumber;
        public CIPShortString ProductName;
        public byte State;

        public CPFItem ToCPFItem()
        {
            if (TypeID != TypeIDConst) {
                throw new EIPProtocolViolationException($"TypeID must be 0x{TypeIDConst.ToString("X4")}.");
            }
            if (Length < BaseMarshalSize) {
                throw new EIPProtocolViolationException($"Length must be at least {BaseMarshalSize}");
            }

            byte[] buffer = new byte[BaseMarshalSize + ProductName.Length];
            using (var ms = new MemoryStream(buffer, true))
            using (var writer = new BinaryWriter(ms)) {
                writer.Write(ProtocolVersion);
                writer.Write(SocketAddress.ToByteArray());
                writer.Write(VendorID);
                writer.Write(DeviceType);
                writer.Write(ProductCode);
                writer.Write(Revision.WordValue);
                writer.Write(Status);
                writer.Write(SerialNumber);
                writer.Write(ProductName.ToByteArray());
                writer.Write(State);
            }
            return new CPFItem(TypeIDConst, buffer);
        }

        public static CIPIdentity FromCPFItem(CPFItem item)
        {
            if (item.TypeID != TypeIDConst) {
                throw new EIPProtocolViolationException($"item.TypeID must be 0x{TypeIDConst.ToString("X4")}.");
            }
            if (item.Length < BaseMarshalSize) {
                throw new EIPProtocolViolationException($"item.Length must be at least {BaseMarshalSize}");
            }

            using (var ms = new MemoryStream(item.Data))
            using (var reader = new BinaryReader(ms)) {
                CIPIdentity identity = new CIPIdentity();
                identity.TypeID = item.TypeID;
                identity.Length = item.Length;
                identity.ProtocolVersion = reader.ReadUInt16();
                identity.SocketAddress = CIPSockaddrInfo.FromByteArray(reader.ReadBytes(CIPSockaddrInfo.MarshalSize));
                identity.VendorID = reader.ReadUInt16();
                identity.DeviceType = reader.ReadUInt16();
                identity.ProductCode = reader.ReadUInt16();
                identity.Revision.WordValue = reader.ReadUInt16();
                identity.Status = reader.ReadUInt16();
                identity.SerialNumber = reader.ReadUInt32();
                identity.ProductName = CIPShortString.FromByteArray(reader.ReadBytes(item.Length - BaseMarshalSize));
                identity.State = reader.ReadByte();
                return identity;
            }
        }
    }
}
