using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public static class LogixString
    {
        public static byte[] ToByteArray(int maxLength, string value)
        {
            int padBytes = 4 - (maxLength % 4);
            if (4 == padBytes) {
                padBytes = 0;
            }
            int len = Math.Min(maxLength, value?.Length ?? 0);
            string data = (value ?? string.Empty).PadRight(maxLength, '\0').Substring(0, maxLength);
            byte[] dataBytes = Encoding.ASCII.GetBytes(data);
            byte[] buffer = new byte[4 + maxLength + padBytes];
            using (var ms = new MemoryStream(buffer, true))
            using (var writer = new BinaryWriter(ms)) {
                writer.Write(len);
                writer.Write(dataBytes);
            }
            return buffer;
        }

        public static string FromByteArray(byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer))
            using (var reader = new BinaryReader(ms)) {
                int len = reader.ReadInt32();
                byte[] dataBytes = reader.ReadBytes(len);
                string data = Encoding.ASCII.GetString(dataBytes);
                return data;
            }
        }
    }
}
