using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public abstract class CIPSegment
    {
        byte segmentTypeValue;

        public byte SegmentType
        {
            get { return segmentTypeValue; }
            protected set { segmentTypeValue = value; }
        }

        public abstract int GetLength();
        public abstract byte[] ToByteArray();
    }

    public static class CIPSegmentExtensions
    {
        /// <summary>
        /// Returns the length of path segments in BYTES
        /// </summary>
        /// <param name="segments"></param>
        /// <returns></returns>
        public static int CIPGetByteCount(this IEnumerable<CIPSegment> segments)
        {
            int totalLength = 0;
            foreach (var segment in segments) {
                totalLength += segment.GetLength();
            }
            return totalLength;
        }

        /// <summary>
        /// Returns the length of path segments in WORDS
        /// </summary>
        /// <param name="segments"></param>
        /// <returns></returns>
        public static int CIPGetWordCount(this IEnumerable<CIPSegment> segments)
        {
            return segments.CIPGetByteCount() / 2;
        }

        public static CIPSegment CIPReadSegment(this BinaryReader reader)
        {
            byte segmentType = reader.ReadByte();
            if ((segmentType <= 15) ||
                (segmentType == CIP.Segment_LogicalClass8Bit) ||
                (segmentType == CIP.Segment_LogicalInstance8Bit) ||
                (segmentType == CIP.Segment_LogicalElement8Bit) ||
                (segmentType == CIP.Segment_LogicalAttribute8Bit)) {
                byte value = reader.ReadByte();
                return new CIPSegment8Bits(segmentType, value);
            } else if ((segmentType == CIP.Segment_LogicalClass16Bit) ||
                (segmentType == CIP.Segment_LogicalInstance16Bit) ||
                (segmentType == CIP.Segment_LogicalElement16Bit) ||
                (segmentType == CIP.Segment_LogicalAttribute16Bit)) {
                ushort value = reader.ReadUInt16();
                return new CIPSegment16Bits(segmentType, value);
            } else if (segmentType == CIP.Segment_LogicalElement32Bit) {
                uint value = reader.ReadUInt32();
                return new CIPSegment32Bits(segmentType, value);
            } else if (segmentType == CIP.Segment_SymbolicANSIExtended) {
                int length = reader.ReadByte();
                string value = Encoding.ASCII.GetString(reader.ReadBytes(length));
                if ((length % 2) > 0) {
                    reader.ReadByte();
                }
                return new CIPSegmentANSIExtended(value);
            } else {
                throw new CIPProtocolViolationException("Unexpected segment encoding.");
            }
        }
    }

    public class CIPSegment8Bits : CIPSegment
    {
        public byte Value;

        public CIPSegment8Bits(byte segmentType, byte value = 0)
        {
            if ((segmentType <= 15) || 
                (segmentType == CIP.Segment_LogicalClass8Bit) ||
                (segmentType == CIP.Segment_LogicalInstance8Bit) ||
                (segmentType == CIP.Segment_LogicalElement8Bit) ||
                (segmentType == CIP.Segment_LogicalAttribute8Bit)) {
                base.SegmentType = segmentType;
                this.Value = value;
            } else {
                throw new CIPProtocolViolationException("Must be 8-bit segment mask");
            }
        }

        public override int GetLength()
        {
            return 2;
        }

        public override byte[] ToByteArray()
        {
            ushort usvalue = Value;
            ushort ussegmentMask = SegmentType;
            return BitConverter.GetBytes((ushort)((usvalue << 8) | ussegmentMask));
        }
    }

    public class CIPSegment16Bits : CIPSegment
    {
        public ushort Value;

        public CIPSegment16Bits(byte segmentType, ushort value = 0)
        {
            if ((segmentType == CIP.Segment_LogicalClass16Bit) ||
                (segmentType == CIP.Segment_LogicalInstance16Bit) ||
                (segmentType == CIP.Segment_LogicalElement16Bit) ||
                (segmentType == CIP.Segment_LogicalAttribute16Bit)) {
                base.SegmentType = segmentType;
                this.Value = value;
            } else {
                throw new CIPProtocolViolationException("Must be 16-bit segment mask");
            }
        }

        public override int GetLength()
        {
            return 4;
        }

        public override byte[] ToByteArray()
        {
            uint ussegmentMask = SegmentType;
            uint uivalue = Value;
            return BitConverter.GetBytes((uivalue << 16) | ussegmentMask);
        }
    }

    public class CIPSegment32Bits : CIPSegment
    {
        public uint Value;

        public CIPSegment32Bits(byte segmentType, uint value = 0)
        {
            if (segmentType == CIP.Segment_LogicalElement32Bit) {
                base.SegmentType = segmentType;
                this.Value = value;
            } else {
                throw new CIPProtocolViolationException("Must be 32-bit segment mask");
            }
        }

        public override int GetLength()
        {
            return 6;
        }

        public override byte[] ToByteArray()
        {
            byte[] valueBuffer = BitConverter.GetBytes(Value);
            byte[] buffer = new byte[6];
            buffer[0] = SegmentType;
            buffer[1] = 0;
            buffer[2] = valueBuffer[0];
            buffer[3] = valueBuffer[1];
            buffer[4] = valueBuffer[2];
            buffer[5] = valueBuffer[3];
            return buffer;
        }
    }

    public class CIPSegmentANSIExtended : CIPSegment
    {
        public string Value;

        public CIPSegmentANSIExtended(string value = null)
        {
            base.SegmentType = CIP.Segment_SymbolicANSIExtended;
            this.Value = value;
        }

        public override int GetLength()
        {
            int valueLength = Value?.Length ?? 0;
            return 2 + valueLength + (valueLength % 2);
        }

        public override byte[] ToByteArray()
        {
            if(string.IsNullOrEmpty(Value)) {
                throw new CIPProtocolViolationException("Value cannot be empty.");
            }
            byte[] valueBuffer = Encoding.ASCII.GetBytes(Value);
            byte[] buffer = new byte[2 + valueBuffer.Length + (valueBuffer.Length % 2)];
            buffer[0] = SegmentType;
            buffer[1] = (byte)valueBuffer.Length;
            Buffer.BlockCopy(valueBuffer, 0, buffer, 2, valueBuffer.Length);
            return buffer;
        }
    }
}
