using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Logix
{
    public static class StringExtensions
    {
        public static List<CIPSegment> CIPParseSegments(this string tagName)
        {
            List<CIPSegment> requestPath = new List<CIPSegment>();
            foreach (string part in tagName.Split('.')) {
                var match = Regex.Match(part, @"(?<symbol>[0-9a-zA-Z_:]+)(\[(?<index>[0-9]+)\])?");
                string symbol = match.Groups["symbol"]?.Value;
                string index = match.Groups["index"]?.Value;
                requestPath.Add(new CIPSegmentANSIExtended(symbol));
                if (!string.IsNullOrEmpty(index)) {
                    uint symbolIndex = Convert.ToUInt32(index);
                    if (symbolIndex <= 0xFF) {
                        requestPath.Add(new CIPSegment8Bits(CIP.Segment_LogicalElement8Bit, (byte)symbolIndex));
                    } else if (symbolIndex <= 0xFFFF) {
                        requestPath.Add(new CIPSegment16Bits(CIP.Segment_LogicalElement16Bit, (ushort)symbolIndex));
                    } else if (symbolIndex <= 0xFFFFFFFF) {
                        requestPath.Add(new CIPSegment32Bits(CIP.Segment_LogicalElement32Bit, (uint)symbolIndex));
                    } else {
                    }
                }
            }
            return requestPath;
        }
    }
}
