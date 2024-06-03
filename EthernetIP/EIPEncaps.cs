using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Logix
{
    [StructLayout(LayoutKind.Explicit, Pack = 2, CharSet = CharSet.Ansi, Size = MarshalSize)]
    public struct EIPEncaps
    {
        public const int MarshalSize = 24;

        [FieldOffset(0)]
        public ushort Command;
        [FieldOffset(2)]
        public ushort Length;
        [FieldOffset(4)]
        public uint SessionHandle;
        [FieldOffset(8)]
        public uint Status;
        [FieldOffset(12)]
        public CIPEightBytes SenderContext;
        [FieldOffset(20)]
        public uint Options;

        public static EIPEncaps CreateNew(ushort command, ushort length, uint sessionHandle, uint status, string senderContext, uint options)
        {
            var packet = new EIPEncaps()
            {
                Command = command,
                Length = length,
                SessionHandle = sessionHandle,
                Status = status,
                Options = options
            };
            packet.SenderContext.Value = senderContext;
            return packet;
        }

        public byte[] ToByteArray()
        {
            return StructPack.Pack<EIPEncaps>(this);
        }

        public void ToStream(Stream stream)
        {
            StructPack.ToStream<EIPEncaps>(this, stream);
        }

        public static EIPEncaps FromByteArray(byte[] buffer)
        {
            return FromByteArray(buffer, 0);
        }

        public static EIPEncaps FromByteArray(byte[] buffer, int offset)
        {
            return StructPack.Unpack<EIPEncaps>(buffer, offset);
        }

        public static EIPEncaps FromStream(Stream stream)
        {
            return StructPack.FromStream<EIPEncaps>(stream);
        }
    }
}
