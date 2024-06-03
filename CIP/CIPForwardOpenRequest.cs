using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Logix
{
    public class CIPForwardOpenRequest
    {
        const int MarshalSizeBase = 36;

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
        public uint OtoTNetworkConnectionID;
        public uint TtoONetworkConnectionID;
        public ushort ConnectionSerialNumber;
        public ushort OVendorID;
        public uint OSerialNumber;
        public byte ConnectionTimeoutMultiplier;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
        /// <summary>
        /// Requested time between packets in microseconds.
        /// </summary>
        public uint OtoTRpi;
        public CIPConnectionManager.Instance_Service_Parameter_NetworkConnectionParameters OtoTNetworkConnectionParameters;
        /// <summary>
        /// Requested time between packets in microseconds.
        /// </summary>
        public uint TtoORpi;
        public CIPConnectionManager.Instance_Service_Parameter_NetworkConnectionParameters TtoONetworkConnectionParameters;
        public CIPConnection.Instance_Attribute_TransportClass_trigger TransportTypeTrigger;
        public byte ConnectionPathSize;
        public List<CIPSegment> ConnectionPath
        {
            get { return connectionPath; }
        }

        public CIPForwardOpenRequest()
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
                writer.Write(OtoTNetworkConnectionID);
                writer.Write(TtoONetworkConnectionID);
                writer.Write(ConnectionSerialNumber);
                writer.Write(OVendorID);
                writer.Write(OSerialNumber);
                writer.Write(ConnectionTimeoutMultiplier);
                writer.Write(Reserved0);
                writer.Write(Reserved1);
                writer.Write(Reserved2);
                writer.Write(OtoTRpi);
                writer.Write(OtoTNetworkConnectionParameters.Value);
                writer.Write(TtoORpi);
                writer.Write(TtoONetworkConnectionParameters.Value);
                writer.Write(TransportTypeTrigger.Value);
                writer.Write(ConnectionPathSize);
                foreach (var epath in ConnectionPath) {
                    writer.Write(epath.ToByteArray());
                }
            }
            return buffer;
        }
    }
}
