using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Logix
{
    public class CIPConnection
    {
        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 1)]
        public struct Instance_Attribute_TransportClass_trigger
        {
            public enum ProductionTriggers
            {
                Cyclic = 0,
                ChangeOfState = 1,
                ApplicationObject = 2
            }

            public enum TransportClasses
            {
                Class0 = 0,
                Class1 = 1,
                Class2 = 2,
                Class3 = 3
            }

            const byte IsServer_Mask = 0x80;
            const byte ProductionTrigger_Mask = 0x70;
            const byte TransportClass_Mask = 0x0F;

            [FieldOffset(0)]
            public byte Value;

            public bool IsServer
            {
                get { return Convert.ToBoolean((Value & IsServer_Mask) >> 7); }
                set { Value = (byte)((Value & (~IsServer_Mask)) | ((Convert.ToInt32(value) << 7))); }
            }

            public ProductionTriggers ProductionTrigger
            {
                get { return (ProductionTriggers)((Value & ProductionTrigger_Mask) >> 4); }
                set { Value = (byte)((Value & (~ProductionTrigger_Mask)) | (((int)value) << 4)); }
            }

            public TransportClasses TransportClass
            {
                get { return (TransportClasses)(Value & TransportClass_Mask); }
                set { Value = (byte)((Value & (~TransportClass_Mask)) | (int)value); }
            }
        }
    }
}
