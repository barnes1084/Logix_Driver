using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class CIPMultipleServiceRequestPacket : List<CIPMessageRouterRequest>
    {
        public byte[] ToByteArray()
        {
            List<byte[]> requests = new List<byte[]>();
            foreach (var request in this) {
                requests.Add(request.ToByteArray());
            }

            byte[] buffer = new byte[2 + (2 * requests.Count) + requests.Sum(x => x.Length)];
            buffer[0] = (byte)requests.Count;
            buffer[1] = (byte)(requests.Count >> 8);

            int offset = 2 + (2 * requests.Count);
            for (int i = 0; i<requests.Count; i++){
                byte[] byteRequest = requests[i];

                buffer[2 + (i * 2)] = (byte)offset;
                buffer[2 + (i * 2) + 1] = (byte)(offset >> 8);

                Buffer.BlockCopy(byteRequest, 0, buffer, offset, byteRequest.Length);

                offset += byteRequest.Length;
            }
            return buffer;
        }
    }
}