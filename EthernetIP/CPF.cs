using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public static class CPF
    {
        public static CPFItem[] ReadItems(byte[] data, int offset, int count)
        {
            using (var ms = new MemoryStream(data, offset, count, false))
            using (var reader = new BinaryReader(ms)) {
                ushort itemCount = reader.ReadUInt16();
                CPFItem[] items = new CPFItem[itemCount];
                for (int i = 0; i < itemCount; i++) {
                    ushort typeID = reader.ReadUInt16();
                    ushort length = reader.ReadUInt16();
                    byte[] itemData = reader.ReadBytes(length);
                    var item = new CPFItem(typeID, itemData);
                    items[i] = item;
                }
                return items;
            }
        }

        public static void WriteItems(IEnumerable<CPFItem> items, byte[] buffer, int offset)
        {
            int totalLength = GetBytesRequired(items);
            using (var ms = new MemoryStream(buffer, offset, totalLength, true))
            using (var writer = new BinaryWriter(ms)) {
                writer.Write((ushort)items.Count());
                foreach (var item in items) {
                    if (item.Length != (item.Data?.Length ?? 0)) {
                        throw new EIPProtocolViolationException("Length and Data.Length must match.");
                    }
                    writer.Write(item.TypeID);
                    writer.Write(item.Length);
                    if (item.Length > 0) {
                        writer.Write(item.Data);
                    }
                }
            }
        }

        public static int GetBytesRequired(IEnumerable<CPFItem> items)
        {
            int totalLength = 2;
            foreach (var item in items) {
                totalLength += (4 + item.Length);
            }
            return totalLength;
        }
    }
}
