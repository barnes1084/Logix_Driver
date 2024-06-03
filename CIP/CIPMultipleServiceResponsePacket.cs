using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class CIPMultipleServiceResponsePacket : List<CIPMessageRouterResponse>
    {
        public static CIPMultipleServiceResponsePacket FromByteArray(byte[] bytes)
        {
            CIPMultipleServiceResponsePacket response = new CIPMultipleServiceResponsePacket();
            int numReplies = BitConverter.ToUInt16(bytes, 0);
            ushort[] offsets = new ushort[numReplies];
            for (int i = 0; i < numReplies; i++) {
                offsets[i] = BitConverter.ToUInt16(bytes, 2 + (i * 2));
                response.Add(CIPMessageRouterResponse.FromByteArray(bytes, offsets[i]));
            }
            return response;
        }
    }
}