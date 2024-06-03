using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public interface ICIPForwardOpenResponse
    {
        bool Success { get; }
    }

    public class CIPForwardOpenResponseOK : ICIPForwardOpenResponse
    {
        public bool Success { get { return true; } }

        public uint OtoTConnectionID;
        public uint TtoOConnectionID;
        public ushort ConnectionSerialNumber;
        public ushort OVendorID;
        public uint OSerialNumber;
        public uint OtoTApi;
        public uint TtoOApi;
        public byte ApplicationReplySize;
        public byte Reserved;
        public byte[] ApplicationReply;

        public static CIPForwardOpenResponseOK FromByteArray(byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer))
            using (var reader = new BinaryReader(ms)) {
                CIPForwardOpenResponseOK response = new CIPForwardOpenResponseOK();
                response.OtoTConnectionID = reader.ReadUInt32();
                response.TtoOConnectionID = reader.ReadUInt32();
                response.ConnectionSerialNumber = reader.ReadUInt16();
                response.OVendorID = reader.ReadUInt16();
                response.OSerialNumber = reader.ReadUInt32();
                response.OtoTApi = reader.ReadUInt32();
                response.TtoOApi = reader.ReadUInt32();
                response.ApplicationReplySize = reader.ReadByte();
                response.Reserved = reader.ReadByte();
                response.ApplicationReply = reader.ReadBytes(response.ApplicationReplySize * 2);
                return response;
            }
        }
    }

    public class CIPForwardOpenResponseFailed : ICIPForwardOpenResponse
    {
        public bool Success { get { return false; } }

        public ushort ConnectionSerialNumber;
        public ushort OVendorID;
        public uint OSerialNumber;
        public byte RemainingPathSize;
        public byte Reserved;

        public static CIPForwardOpenResponseFailed FromByteArray(byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer))
            using (var reader = new BinaryReader(ms)) {
                CIPForwardOpenResponseFailed response = new CIPForwardOpenResponseFailed();
                response.ConnectionSerialNumber = reader.ReadUInt16();
                response.OVendorID = reader.ReadUInt16();
                response.OSerialNumber = reader.ReadUInt32();
                response.RemainingPathSize = reader.ReadByte();
                response.Reserved = reader.ReadByte();
                return response;
            }
        }
    }

    public static class CIPForwardOpenResponse
    {
        public static ICIPForwardOpenResponse ToCIPForwardOpenResponse(this CIPMessageRouterResponse response)
        {
            if (response.GeneralStatus == 0x00) {
                return CIPForwardOpenResponseOK.FromByteArray(response.ReplyData);
            } else {
                return CIPForwardOpenResponseFailed.FromByteArray(response.ReplyData);
            }
        }
    }
}
