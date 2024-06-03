using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class CIPMessageRouterResponse
    {
        public byte ReplyService;
        public byte Reserved;
        public byte GeneralStatus;
        public byte ExtendedStatusSize;
        public ushort[] ExtendedStatus;
        public byte[] ReplyData;

        public byte[] ToByteArray()
        {
            int actualExtendedStatusSize = ExtendedStatus?.Length ?? 0;
            if (actualExtendedStatusSize != ExtendedStatusSize) {
                throw new CIPProtocolViolationException("ExtendedStatusSize must be equal to ExtendedStatus.Length");
            }
            byte[] buffer = new byte[4 + (2 * actualExtendedStatusSize) + (ReplyData?.Length ?? 0)];
            using (var ms = new MemoryStream(buffer))
            using (var writer = new BinaryWriter(ms)) {
                writer.Write(ReplyService);
                writer.Write(Reserved);
                writer.Write(GeneralStatus);
                writer.Write(ExtendedStatusSize);
                if (ExtendedStatusSize > 0) {
                    foreach (ushort extStatus in ExtendedStatus) {
                        writer.Write(extStatus);
                    }
                }
                if(null != ReplyData) {
                    writer.Write(ReplyData);
                }   
            }
            return buffer;
        }

        public static CIPMessageRouterResponse FromByteArray(byte[] buffer) =>
            FromByteArray(buffer, 0);

        public static CIPMessageRouterResponse FromByteArray(byte[] buffer, int startIndex)
        {
            using (var ms = new MemoryStream(buffer, startIndex, buffer.Length - startIndex, false))
            using (var reader = new BinaryReader(ms)) {
                var response = new CIPMessageRouterResponse();
                response.ReplyService = reader.ReadByte();
                response.Reserved = reader.ReadByte();
                response.GeneralStatus = reader.ReadByte();
                response.ExtendedStatusSize = reader.ReadByte();
                if(response.ExtendedStatusSize > 0) {
                    response.ExtendedStatus = new ushort[response.ExtendedStatusSize];
                    for (int i = 0; i < response.ExtendedStatusSize; i++) {
                        response.ExtendedStatus[i] = reader.ReadUInt16();
                    }
                }
                int bytesRemaining = buffer.Length - (4 + (2 * response.ExtendedStatusSize));
                if(bytesRemaining > 0) {
                    response.ReplyData = reader.ReadBytes(bytesRemaining);
                }
                return response;
            }
        }
    }
}
