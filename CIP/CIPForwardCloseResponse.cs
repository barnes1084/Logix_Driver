using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public interface ICIPForwardCloseResponse
    {
        bool Success { get; }
    }

    public class CIPForwardCloseResponseOK : ICIPForwardCloseResponse
    {
        public bool Success { get { return true; } }

        public ushort ConnectionSerialNumber;
        public ushort OVendorID;
        public uint OSerialNumber;
        public byte ApplicationReplySize;
        public byte Reserved;
        public byte[] ApplicationReply;

        public static CIPForwardCloseResponseOK FromByteArray(byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer))
            using (var reader = new BinaryReader(ms)) {
                CIPForwardCloseResponseOK response = new CIPForwardCloseResponseOK();
                response.ConnectionSerialNumber = reader.ReadUInt16();
                response.OVendorID = reader.ReadUInt16();
                response.OSerialNumber = reader.ReadUInt32();
                response.ApplicationReplySize = reader.ReadByte();
                response.Reserved = reader.ReadByte();
                response.ApplicationReply = reader.ReadBytes(response.ApplicationReplySize * 2);
                return response;
            }
        }
    }

    public class CIPForwardCloseResponseFailed : ICIPForwardCloseResponse
    {
        public bool Success { get { return false; } }

        public ushort ConnectionSerialNumber;
        public ushort OVendorID;
        public uint OSerialNumber;
        public byte RemainingPathSize;
        public byte Reserved;

        public static CIPForwardCloseResponseFailed FromByteArray(byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer))
            using (var reader = new BinaryReader(ms)) {
                CIPForwardCloseResponseFailed response = new CIPForwardCloseResponseFailed();
                response.ConnectionSerialNumber = reader.ReadUInt16();
                response.OVendorID = reader.ReadUInt16();
                response.OSerialNumber = reader.ReadUInt32();
                response.RemainingPathSize = reader.ReadByte();
                response.Reserved = reader.ReadByte();
                return response;
            }
        }
    }

    public static class CIPForwardCloseResponse
    {
        public static ICIPForwardCloseResponse ToCIPForwardCloseResponse(this CIPMessageRouterResponse response)
        {
            if (response.GeneralStatus == 0x00) {
                return CIPForwardCloseResponseOK.FromByteArray(response.ReplyData);
            } else {
                return CIPForwardCloseResponseFailed.FromByteArray(response.ReplyData);
            }
        }
    }
}
