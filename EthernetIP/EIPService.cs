using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class EIPService
    {
        const int MarshalSize = 24;
        const int LengthOf_NameOfService = 16;

        public ushort TypeID;
        public ushort Length;
        public ushort ProtocolVersion;
        public ushort Capability;
        public string NameOfService;

        public bool SupportsCIPonTCP
        {
            get { return Capability.CIPIsFlagSet(5); }
            set { Capability.CIPSetOrResetFlag(5, value); }
        }

        public bool SupportsCIPonUDP
        {
            get { return Capability.CIPIsFlagSet(8); }
            set { Capability.CIPSetOrResetFlag(8, value); }
        }

        public CPFItem ToCPFItem()
        {
            var item = new CPFItem(TypeID, Length);
            using (var ms = new MemoryStream(item.Data, true))
            using (var writer = new BinaryWriter(ms)) {
                writer.Write(ProtocolVersion);
                writer.Write(Capability);
                writer.Write(Encoding.ASCII.GetBytes(NameOfService.PadRight(LengthOf_NameOfService, '\0').Substring(0, LengthOf_NameOfService)));
            }
            return item;
        }

        public static EIPService FromCPFItem(CPFItem item)
        {
            using (var ms = new MemoryStream(item.Data))
            using (var reader = new BinaryReader(ms)) {
                EIPService response = new EIPService();
                response.TypeID = item.TypeID;
                response.Length = item.Length;
                response.ProtocolVersion = reader.ReadUInt16();
                response.Capability = reader.ReadUInt16();
                response.NameOfService = Encoding.ASCII.GetString(reader.ReadBytes(LengthOf_NameOfService)).TrimEnd(new char[] { '\0', ' ' });
                return response;
            }
        }
    }
}
