using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Logix
{
    public class LogixUdpSocket
    {
        const int DefaultIOTimeout = 6000;

        const int State_Binding = 1;
        const int State_Bound = 2;
        const int State_Faulted = 3;

        volatile int disposed;
        volatile int state;
        Socket sock;
        Exception faultException;
        ManualResetEvent bindingRE;
        IPEndPoint boundEP;

        public bool Binding
        {
            get { return state == State_Binding; }
        }

        public bool Bound
        {
            get { return state == State_Bound; }
        }

        public bool Faulted
        {
            get { return state == State_Faulted; }
        }

        public bool DataAvailable
        {
            get { return (sock?.Available ?? 0) > 0; }
        }

        public Exception FaultException
        {
            get { return faultException; }
        }

        public LogixUdpSocket(IPEndPoint localEP)
        {
            disposed = 0;
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sock.EnableBroadcast = true;
            sock.Blocking = false;

            bindingRE = new ManualResetEvent(false);
            state = State_Binding;
            BindAsync(localEP).ContinueWith((t) => {
                if (t.IsCompleted) {
                    Interlocked.Exchange(ref state, State_Bound);
                } else if (t.IsFaulted) {
                    Fault(t.Exception);
                }
                bindingRE.Set();
            });
        }

        ~LogixUdpSocket()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (0 == Interlocked.CompareExchange(ref disposed, 1, 0)) {
                sock.Close();
                sock = null;
                if (!Faulted) {
                    Fault(new ObjectDisposedException("LgxUdpSocket"));
                }
            }
        }

        public void Close()
        {
            Dispose();
        }

        void Fault(Exception ex)
        {
            faultException = ex;
            Interlocked.Exchange(ref state, State_Faulted);
        }

        Task BindAsync(IPEndPoint localEP)
        {
            return Task.Run(() => { Bind(localEP); });
        }

        void Bind(IPEndPoint localEP)
        {
            var addressBytes = localEP.Address.GetAddressBytes();
            if (4 == addressBytes.Length) {
                sock.Bind(localEP);
                boundEP = localEP;
            } else {
                throw new ArgumentException("Must be IPv4", "localEP");
            }
        }

        public bool WaitForBinding(int millisecondsTimeout)
        {
            if (Bound) {
                return true;
            }
            bindingRE.WaitOne(millisecondsTimeout);
            return Bound;
        }

        public bool Poll(int microSeconds, SelectMode mode)
        {
            return sock?.Poll(microSeconds, mode) ?? false;
        }

        public void SendTo(EIPPacket packet, IPEndPoint remoteEP, int millisecondsTimeout = DefaultIOTimeout)
        {
            if (null == packet) {
                throw new ArgumentNullException("packet");
            } else if (packet.Encaps.Length != (packet.Data?.Length ?? 0)) {
                throw new EIPProtocolViolationException("Encaps.Length does not match Data.Length");
            }

            try {
                Stopwatch timeoutTimer = Stopwatch.StartNew();
                if (WaitForBinding(millisecondsTimeout)) {
                    byte[] buffer = new byte[EIPEncaps.MarshalSize + packet.Encaps.Length];
                    using (var ms = new MemoryStream(buffer))
                    using (BinaryWriter writer = new BinaryWriter(ms)) {
                        writer.Write(packet.Encaps.ToByteArray());
                        if (null != packet.Data) {
                            writer.Write(packet.Data);
                        }
                    }
                    int bytes = 0;
                    while (0 == bytes) {
                        if (sock.Poll(((int)(millisecondsTimeout - timeoutTimer.ElapsedMilliseconds)) * 100, SelectMode.SelectWrite)) {
                            bytes = sock.SendTo(buffer, 0, buffer.Length, SocketFlags.None, remoteEP);
                        }
                        if (timeoutTimer.ElapsedMilliseconds >= millisecondsTimeout) {
                            throw new LogixSocketException("Send timed out.");
                        }
                    }
                } else {
                    throw new LogixSocketException("No binding");
                }
            } catch (Exception ex) {
                Fault(ex);
                throw;
            }
        }

        public EIPPacket ReceiveFrom(IPEndPoint remoteEP, int millisecondsTimeout = DefaultIOTimeout, bool throwOnTimeout = true, byte[] externalBuffer = null)
        {
            try {
                Stopwatch timeoutTimer = Stopwatch.StartNew();
                if (WaitForBinding(millisecondsTimeout)) {
                    byte[] buffer = externalBuffer;
                    if (null == buffer) {
                        buffer = new byte[EIP.NetworkUdpMaxPacketSize];
                    }
                    for (;;) {
                        if (sock.Poll(((int)(millisecondsTimeout - timeoutTimer.ElapsedMilliseconds)) * 100, SelectMode.SelectRead)) {
                            EndPoint packetEP = remoteEP;
                            int bytes = sock.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref packetEP);
                            if (bytes > EIPEncaps.MarshalSize) {
                                EIPEncaps encaps = EIPEncaps.FromByteArray(buffer);
                                byte[] data = null;
                                if (encaps.Length > 0) {
                                    if ((bytes - EIPEncaps.MarshalSize) == encaps.Length) {
                                        data = new byte[encaps.Length];
                                        Buffer.BlockCopy(buffer, EIPEncaps.MarshalSize, data, 0, encaps.Length);
                                    } else {
                                        throw new EIPProtocolViolationException("Poorly formed EIP datagram");
                                    }
                                }
                                var packet = new EIPPacket();
                                packet.Encaps = encaps;
                                packet.Data = data;
                                packet.DgramContext = CIPSockaddrInfo.FromIPEndPoint((IPEndPoint)packetEP);
                                return packet;
                            } else {
                                throw new EIPProtocolViolationException("Poorly formed EIP datagram");
                            }
                        }
                        if (timeoutTimer.ElapsedMilliseconds >= millisecondsTimeout) {
                            if (throwOnTimeout) {
                                throw new LogixSocketException("ReceiveFrom timed out.");
                            } else {
                                return null;
                            }
                        }
                    }
                } else {
                    throw new LogixSocketException("No binding");
                }
            } catch (Exception ex) {
                Fault(ex);
                throw;
            }
        }

        public IEnumerable<EIPPacket> ReceiveManyFrom(IPEndPoint remoteEP, int millisecondsTimeout = DefaultIOTimeout)
        {
            Stopwatch timeoutTimer = Stopwatch.StartNew();
            byte[] buffer = new byte[EIP.NetworkUdpMaxPacketSize];
            List<EIPPacket> packets = new List<EIPPacket>();
            EIPPacket packet = null;
            while (null != (packet = ReceiveFrom(remoteEP, (int)(millisecondsTimeout - timeoutTimer.ElapsedMilliseconds), false, buffer))) {
                packets.Add(packet);
            }
            return packets;
        }
    }
}
