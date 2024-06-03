using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Logix
{
    [StructLayout(LayoutKind.Explicit, Pack = 2, CharSet = CharSet.Ansi, Size = MarshalSize)]
    public struct CIPSockaddrInfo
    {
        public const int MarshalSize = 16;

        [FieldOffset(0)]
        public short sin_family;
        [FieldOffset(2)]
        public ushort sin_port;
        [FieldOffset(4)]
        public uint sin_addr;
        [FieldOffset(8)]
        public CIPEightBytes sin_zero;

        public override string ToString()
        {
            return ToIPEndPoint().ToString();
        }

        public byte[] ToByteArray()
        {
            byte[] buffer = new byte[MarshalSize];
            using (var ms = new MemoryStream(buffer))
            using (var writer = new BinaryWriter(ms)) {
                writer.Write(IPAddress.HostToNetworkOrder(sin_family));
                writer.Write(IPAddress.HostToNetworkOrder(sin_port));
                writer.Write(sin_addr);
                writer.Write(sin_zero.GetByte(7));
                writer.Write(sin_zero.GetByte(6));
                writer.Write(sin_zero.GetByte(5));
                writer.Write(sin_zero.GetByte(4));
                writer.Write(sin_zero.GetByte(3));
                writer.Write(sin_zero.GetByte(2));
                writer.Write(sin_zero.GetByte(1));
                writer.Write(sin_zero.GetByte(0));
            }
            return buffer;
        }

        public IPEndPoint ToIPEndPoint()
        {
            return new IPEndPoint(new IPAddress(sin_addr), sin_port);
        }

        public static CIPSockaddrInfo FromByteArray(byte[] buffer, int offset = 0)
        {
            using (var ms = new MemoryStream(buffer, offset, MarshalSize))
            using (var reader = new BinaryReader(ms)) {
                CIPSockaddrInfo sockaddrInfo = new CIPSockaddrInfo();
                sockaddrInfo.sin_family = IPAddress.NetworkToHostOrder(reader.ReadInt16());
                sockaddrInfo.sin_port = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                sockaddrInfo.sin_addr = (uint)reader.ReadInt32();
                sockaddrInfo.sin_zero.SetByte(7, reader.ReadByte());
                sockaddrInfo.sin_zero.SetByte(6, reader.ReadByte());
                sockaddrInfo.sin_zero.SetByte(5, reader.ReadByte());
                sockaddrInfo.sin_zero.SetByte(4, reader.ReadByte());
                sockaddrInfo.sin_zero.SetByte(3, reader.ReadByte());
                sockaddrInfo.sin_zero.SetByte(2, reader.ReadByte());
                sockaddrInfo.sin_zero.SetByte(1, reader.ReadByte());
                sockaddrInfo.sin_zero.SetByte(0, reader.ReadByte());
                return sockaddrInfo;
            }
        }

        public static CIPSockaddrInfo FromIPEndPoint(IPEndPoint endPoint)
        {
            if (null == endPoint) {
                return Zero;
            }

            byte[] addressBytes = endPoint.Address.GetAddressBytes();
            if (4 != addressBytes.Length) {
                throw new ArgumentException("Must be IPv4.");
            }

            CIPSockaddrInfo sockaddrInfo = new CIPSockaddrInfo();
            sockaddrInfo.sin_addr = BitConverter.ToUInt32(addressBytes, 0);
            sockaddrInfo.sin_family = (short)endPoint.AddressFamily;
            sockaddrInfo.sin_port = (ushort)endPoint.Port;
            return sockaddrInfo;
        }

        public static readonly CIPSockaddrInfo Zero = new CIPSockaddrInfo() { sin_addr = 0, sin_family = 0, sin_port = 0, sin_zero = new CIPEightBytes() { ValueUL = 0UL } };
    }
}
