using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class EIPPacket
    {
        public EIPEncaps Encaps;
        public byte[] Data;
        public CIPSockaddrInfo DgramContext;

        public EIPPacket()
        {
            Encaps = new EIPEncaps();
        }

        public EIPPacket(ushort command, uint sessionHandle, string senderContext, byte[] data = null)
        {
            Encaps = new EIPEncaps();
            Encaps.Command = command;
            Encaps.Length = (ushort)(data?.Length ?? 0);
            Encaps.SessionHandle = sessionHandle;
            Encaps.SenderContext.Value = senderContext;
            Data = data;
        }
    }
}
