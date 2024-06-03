using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class CIPConnectedServiceResponse
    {
        public ushort SequenceNumber;
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
            byte[] buffer = new byte[6 + (2 * actualExtendedStatusSize) + (ReplyData?.Length ?? 0)];
            using (var ms = new MemoryStream(buffer))
            using (var writer = new BinaryWriter(ms)) {
                writer.Write(SequenceNumber);
                writer.Write(ReplyService);
                writer.Write(Reserved);
                writer.Write(GeneralStatus);
                writer.Write(ExtendedStatusSize);
                if (ExtendedStatusSize > 0) {
                    foreach (ushort extStatus in ExtendedStatus) {
                        writer.Write(extStatus);
                    }
                }
                if (null != ReplyData) {
                    writer.Write(ReplyData);
                }
            }
            return buffer;
        }

        public static CIPConnectedServiceResponse FromByteArray(byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer, false))
            using (var reader = new BinaryReader(ms)) {
                var response = new CIPConnectedServiceResponse();
                response.SequenceNumber = reader.ReadUInt16();
                response.ReplyService = reader.ReadByte();
                response.Reserved = reader.ReadByte();
                response.GeneralStatus = reader.ReadByte();
                response.ExtendedStatusSize = reader.ReadByte();
                if (response.ExtendedStatusSize > 0) {
                    response.ExtendedStatus = new ushort[response.ExtendedStatusSize];
                    for (int i = 0; i < response.ExtendedStatusSize; i++) {
                        response.ExtendedStatus[i] = reader.ReadUInt16();
                    }
                }
                int bytesRemaining = buffer.Length - (6 + (2 * response.ExtendedStatusSize));
                if (bytesRemaining > 0) {
                    response.ReplyData = reader.ReadBytes(bytesRemaining);
                }
                return response;
            }
        }
    }
}
