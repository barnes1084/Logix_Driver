using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public static partial class CIP
    {
        //public const byte SegmentFlag_Bits8 = (not 8) && (not 9);
        public const int SegmentFlag_Bits16 = 0;
        public const int SegmentFlag_Bits32 = 1;

        //public const byte SegmentFlag_IsClass = (not 10) && (not 11) && (not 12);
        public const int SegmentFlag_IsInstance = 2;
        public const int SegmentFlag_IsElement = 3;
        public const int SegmentFlag_IsAttribute = 4;

        public const int SegmentFlag_TypeLogical = 5;
        public const int SegmentFlag_TypeSymbolic = 7;

        public const byte Segment_LogicalClass8Bit = (1 << CIP.SegmentFlag_TypeLogical);
        public const byte Segment_LogicalClass16Bit = (1 << CIP.SegmentFlag_TypeLogical) | (1 << CIP.SegmentFlag_Bits16);
        public const byte Segment_LogicalInstance8Bit = (1 << CIP.SegmentFlag_TypeLogical) | (1 << CIP.SegmentFlag_IsInstance);
        public const byte Segment_LogicalInstance16Bit = (1 << CIP.SegmentFlag_TypeLogical) | (1 << CIP.SegmentFlag_IsInstance) | (1 << CIP.SegmentFlag_Bits16);
        public const byte Segment_LogicalElement8Bit = (1 << CIP.SegmentFlag_TypeLogical) | (1 << CIP.SegmentFlag_IsElement);
        public const byte Segment_LogicalElement16Bit = (1 << CIP.SegmentFlag_TypeLogical) | (1 << CIP.SegmentFlag_IsElement) | (1 << CIP.SegmentFlag_Bits16);
        public const byte Segment_LogicalElement32Bit = (1 << CIP.SegmentFlag_TypeLogical) | (1 << CIP.SegmentFlag_IsElement) | (1 << CIP.SegmentFlag_Bits32);
        public const byte Segment_LogicalAttribute8Bit = (1 << CIP.SegmentFlag_TypeLogical) | (1 << CIP.SegmentFlag_IsAttribute);
        public const byte Segment_LogicalAttribute16Bit = (1 << CIP.SegmentFlag_TypeLogical) | (1 << CIP.SegmentFlag_IsAttribute) | (1 << CIP.SegmentFlag_Bits16);
        public const byte Segment_SymbolicANSIExtended = (1 << CIP.SegmentFlag_TypeSymbolic) | (1 << CIP.SegmentFlag_IsAttribute) | (1 << CIP.SegmentFlag_Bits16);
    }
}
