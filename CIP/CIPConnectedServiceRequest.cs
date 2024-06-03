using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class CIPConnectedServiceRequest
    {
        public ushort SequenceNumber;
        public byte RequestService;
        public byte RequestPathSize;
        List<CIPSegment> requestPath;
        public byte[] RequestData;

        public List<CIPSegment> RequestPath
        {
            get { return requestPath; }
        }

        public CIPConnectedServiceRequest()
        {
            requestPath = new List<CIPSegment>();
        }

        public byte[] ToByteArray()
        {
            int pathBytes = RequestPath.CIPGetByteCount();
            int pathWords = requestPath.CIPGetWordCount();
            int requestDataLength = RequestData?.Length ?? 0;
            if (RequestPathSize != pathWords) {
                throw new CIPProtocolViolationException("RequestPathSize is incorrect.");
            }

            byte[] buffer = new byte[4 + pathBytes + requestDataLength];
            using (var ms = new MemoryStream(buffer, true))
            using (var writer = new BinaryWriter(ms)) {
                writer.Write(SequenceNumber);
                writer.Write(RequestService);
                writer.Write(RequestPathSize);
                foreach (var segment in RequestPath) {
                    writer.Write(segment.ToByteArray());
                }
                if (requestDataLength > 0) {
                    writer.Write(RequestData);
                }
            }
            return buffer;
        }

        public static CIPConnectedServiceRequest FromByteArray(byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer, false))
            using (var reader = new BinaryReader(ms)) {
                var request = new CIPConnectedServiceRequest();
                request.SequenceNumber = reader.ReadUInt16();
                request.RequestService = reader.ReadByte();
                request.RequestPathSize = reader.ReadByte();
                byte[] segmentBuffer = reader.ReadBytes(request.RequestPathSize);
                int bytesRead = 0;
                while (bytesRead < request.RequestPathSize) {
                    CIPSegment segment = reader.CIPReadSegment();
                    request.RequestPath.Add(segment);
                    bytesRead += segment.GetLength();
                }
                request.RequestData = reader.ReadBytes(buffer.Length - (4 + segmentBuffer.Length));
                return request;
            }
        }
    }
}
