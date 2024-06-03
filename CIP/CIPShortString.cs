using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class CIPShortString
    {
        public byte Length;
        public string Value;

        public override string ToString()
        {
            return $"<{Length}>{Value}";
        }

        public byte[] ToByteArray()
        {
            if (null == Value) {
                if (0 != Length) {
                    throw new CIPProtocolViolationException("Header.Length is greater than zero while Data is null.");
                }
                return new byte[] { Length };
            } else {
                if (Length != Value.Length) {
                    throw new CIPProtocolViolationException("Value of Header.Length does not match Data array length.");
                }
                byte[] buffer = new byte[Value.Length + 1];
                buffer[0] = Length;
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(Value), 0, buffer, 1, Length);
                return buffer;
            }
        }

        public static CIPShortString FromByteArray(byte[] buffer)
        {
            CIPShortString value = new CIPShortString();
            value.Length = buffer[0];
            if (value.Length > 0) {
                if (buffer.Length < value.Length) {
                    throw new CIPProtocolViolationException("Not enough data in buffer.");
                }
                value.Value = Encoding.ASCII.GetString(buffer, 1, value.Length);
            }
            return value;
        }
    }
}
