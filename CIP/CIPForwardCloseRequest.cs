using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class CIPForwardCloseRequest
    {
        const int MarshalSizeBase = 12;

        List<CIPSegment> connectionPath;

        public CIPConnectionManager.Instance_Service_Parameter_PriorityTime_tick PriorityTime_tick;
        /// <summary>
        /// Timeout tick multiplier
        /// <para>
        /// Actual Timeout value becomes TimeoutTicks x (2 ^ PriorityTickTime.TickTime).
        /// </para>
        /// <para>
        /// Example: If PriorityTickTime.TickTime is 10 and TimeoutTicks is 5, Actual Timeout value is 5120 milliseconds (~5 seconds)
        /// </para>
        /// </summary>
        public byte Timeout_ticks;
        public ushort ConnectionSerialNumber;
        public ushort OVendorID;
        public uint OSerialNumber;
        public byte ConnectionPathSize;
        public byte Reserved0;
        public List<CIPSegment> ConnectionPath
        {
            get { return connectionPath; }
        }

        public CIPForwardCloseRequest()
        {
            connectionPath = new List<CIPSegment>();
        }

        public byte[] ToByteArray()
        {
            int pathBytes = ConnectionPath.CIPGetByteCount();
            int pathWords = ConnectionPath.CIPGetWordCount();
            if (ConnectionPathSize != pathWords) {
                throw new CIPProtocolViolationException("ConnectionPathSize is incorrect.");
            }
            byte[] buffer = new byte[MarshalSizeBase + pathBytes];
            using (var ms = new MemoryStream(buffer, true))
            using (var writer = new BinaryWriter(ms)) {
                writer.Write(PriorityTime_tick.Value);
                writer.Write(Timeout_ticks);
                writer.Write(ConnectionSerialNumber);
                writer.Write(OVendorID);
                writer.Write(OSerialNumber);
                writer.Write(ConnectionPathSize);
                writer.Write(Reserved0);
                foreach (var epath in ConnectionPath) {
                    writer.Write(epath.ToByteArray());
                }
            }
            return buffer;
        }
    }
}
