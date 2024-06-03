using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class CIPMessageRouterRequest
    {
        public byte RequestService;
        public byte RequestPathSize;
        List<CIPSegment> requestPath;
        public byte[] RequestData;

        public List<CIPSegment> RequestPath
        {
            get { return requestPath; }
        }

        public int Size {
            get { return 2 + RequestPath.CIPGetByteCount() + RequestData?.Length ?? 0; }
        }

        public CIPMessageRouterRequest()
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

            byte[] buffer = new byte[2 + pathBytes + requestDataLength];
            using (var ms = new MemoryStream(buffer, true))
            using (var writer = new BinaryWriter(ms)) {
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

        public static CIPMessageRouterRequest FromByteArray(byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer, false))
            using (var reader = new BinaryReader(ms)) {
                var request = new CIPMessageRouterRequest();
                request.RequestService = reader.ReadByte();
                request.RequestPathSize = reader.ReadByte();
                byte[] segmentBuffer = reader.ReadBytes(request.RequestPathSize);
                int bytesRead = 0;
                while (bytesRead < request.RequestPathSize) {
                    CIPSegment segment = reader.CIPReadSegment();
                    request.RequestPath.Add(segment);
                    bytesRead += segment.GetLength();
                }
                request.RequestData = reader.ReadBytes(buffer.Length - (2 + segmentBuffer.Length));
                return request;
            }
        }
    }
}
