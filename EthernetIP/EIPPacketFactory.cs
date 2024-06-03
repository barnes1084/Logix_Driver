using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public static class EIPPacketFactory
    {
        public static EIPPacket CreateNOP()
        {
            var packet = new EIPPacket(EIP.Command_NOP, 0, null);
            return packet;
        }

        public static EIPPacket CreateNOP(uint sessionHandle, string senderContext)
        {
            var packet = new EIPPacket(EIP.Command_NOP, sessionHandle, senderContext);
            return packet;
        }

        public static EIPPacket CreateRegisterSession(string senderContext, ushort protocolVersion = 1)
        {
            var packet = new EIPPacket(EIP.Command_RegisterSession, 0, senderContext);
            packet.Encaps.Length = 4;
            packet.Data = new byte[4];
            using (var ms = new MemoryStream(packet.Data))
            using (var writer = new BinaryWriter(ms)) {
                writer.Write((ushort)1);
                writer.Write((ushort)0);
            }
            return packet;
        }

        public static EIPPacket CreateUnRegisterSession(uint sessionHandle, string senderContext, ushort protocolVersion = 1)
        {
            var packet = new EIPPacket(EIP.Command_UnRegisterSession, sessionHandle, senderContext);
            return packet;
        }

        public static EIPPacket CreateListIdentity()
        {
            var packet = new EIPPacket(EIP.Command_ListIdentity, 0, null);
            return packet;
        }

        public static EIPPacket CreateListInterfaces()
        {
            var packet = new EIPPacket(EIP.Command_ListInterfaces, 0, null);
            return packet;
        }

        public static EIPPacket CreateListServices()
        {
            var packet = new EIPPacket(EIP.Command_ListServices, 0, null);
            return packet;
        }

        public static EIPPacket CreateSendData(ushort command, uint sessionHandle, string senderContext, byte[] buffer)
        {
            if ((command != EIP.Command_SendRRData) && (command != EIP.Command_SendUnitData)) {
                throw new ArgumentException("Must be Command_SendRRData or Command_SendUnitData", "command");
            }
            var packet = new EIPPacket(command, sessionHandle, senderContext, buffer);
            return packet;
        }
    }
}
