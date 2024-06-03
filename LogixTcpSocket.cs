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
    public class LogixTcpSocket : IDisposable
    {
        const int DefaultIOTimeout = 6000;

        const int State_Connecting = 1;
        const int State_Connected = 2;
        const int State_Faulted = 3;

        [StructLayout(LayoutKind.Explicit, Size = MarshalSize)]
        struct TcpKeepAlive
        {
            const int MarshalSize = 12;

            [FieldOffset(0)]
            public uint OnOff;
            [FieldOffset(4)]
            public uint KeepAliveTime;
            [FieldOffset(8)]
            public uint KeepAliveInterval;
        }

        volatile int disposed;
        volatile int state;
        Socket sock;
        ManualResetEvent connectCompleteRE;
        IPEndPoint remoteEP;
        Exception faultException;

        public bool Connecting
        {
            get { return state == State_Connecting; }
        }

        public bool Connected
        {
            get { return state == State_Connected && sock != null; }
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

        public LogixTcpSocket(IPEndPoint remoteEP)
        {
            this.remoteEP = remoteEP;
            disposed = 0;
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                Blocking = false,
                NoDelay = true,
            };
            TcpKeepAlive keepAlive = new TcpKeepAlive()
            {
                OnOff = 1,
                KeepAliveTime = 7200000,
                KeepAliveInterval = 1000
            };
            sock.IOControl(IOControlCode.KeepAliveValues, StructPack.Pack<TcpKeepAlive>(keepAlive), null);
            connectCompleteRE = new ManualResetEvent(false);
            ConnectAsync();
        }

        ~LogixTcpSocket()
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
                    Fault(new ObjectDisposedException("LogixTcpConnection"));
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

        void ConnectAsync()
        {
            state = State_Connecting;
            try {
                var ar = sock.BeginConnect(remoteEP, sock_EndConnect, sock);
                if (ar.CompletedSynchronously) {
                    sock.EndConnect(ar);
                    CompleteConnection();
                }
            } catch (Exception ex) {
                FailConnection(ex);
            }
        }

        void FailConnection(Exception ex)
        {
            Fault(ex);
            connectCompleteRE.Set();
        }

        void CompleteConnection()
        {
            Interlocked.CompareExchange(ref state, State_Connected, State_Connecting);
            connectCompleteRE.Set();
        }

        void sock_EndConnect(IAsyncResult ar)
        {
            if (ar.CompletedSynchronously) {
                return;
            }
            try {
                Socket callbackSock = (Socket)ar.AsyncState;
                callbackSock.EndConnect(ar);
                CompleteConnection();
            } catch (Exception ex) {
                FailConnection(ex);
            }
        }

        public bool WaitForConnection(int millisecondsTimeout)
        {
            if (Connected) {
                return true;
            }
            connectCompleteRE.WaitOne(millisecondsTimeout);
            return Connected;
        }

        public bool Poll(int microSeconds, SelectMode mode)
        {
            return sock?.Poll(microSeconds, mode) ?? false;
        }

        public void Send(EIPPacket packet, int millisecondsTimeout = DefaultIOTimeout)
        {
            try
            {
                if (null == packet) {
                    throw new ArgumentNullException("packet");
                } else if (packet.Encaps.Length != (packet.Data?.Length ?? 0)) {
                    throw new EIPProtocolViolationException("Encaps.Length does not match Data.Length");
                }
                if (sock == null)
                {
                    throw new InvalidOperationException("Socket is not initialized.");
                }

                Stopwatch timeoutTimer = Stopwatch.StartNew();
                if (WaitForConnection(millisecondsTimeout)) {
                    byte[] buffer = new byte[EIPEncaps.MarshalSize + packet.Encaps.Length];
                    using (var ms = new MemoryStream(buffer))
                    using (BinaryWriter writer = new BinaryWriter(ms)) {
                        writer.Write(packet.Encaps.ToByteArray());
                        if (null != packet.Data) {
                            writer.Write(packet.Data);
                        }
                    }
                    int index = 0;
                    while (index < buffer.Length) {
                        int bytes = sock.Send(buffer, index, buffer.Length - index, SocketFlags.None);
                        index += bytes;
                        if (timeoutTimer.ElapsedMilliseconds >= millisecondsTimeout) {
                            throw new LogixSocketException("Send timed out.");
                        } else if (bytes == 0) {
                            Thread.Sleep(1);
                        }
                    }
                } else {
                    throw new LogixSocketException("No connection");
                }
            } catch (Exception ex) {
                Fault(ex);
                throw;
            }
        }

        //******REMOVE IF THIS CODE RUNS WELL FOR A LONG PERIOD OF TIME*******
        //public EIPPacket Receive(int millisecondsTimeout = DefaultIOTimeout, bool throwOnTimeout = true)
        //{
        //    try {
        //        Stopwatch timeoutTimer = Stopwatch.StartNew();
        //        if (WaitForConnection(millisecondsTimeout)) {

        //            byte[] buffer = new byte[EIPEncaps.MarshalSize];
        //            int index = 0;
        //            while (index < buffer.Length) {
        //                if (sock.Poll((int)(millisecondsTimeout - timeoutTimer.ElapsedMilliseconds), SelectMode.SelectRead)) {
        //                    int bytes = sock.Receive(buffer, index, buffer.Length, SocketFlags.None);
        //                    if (bytes == 0) {
        //                        throw new LogixSocketException("Connection Reset", new SocketException((int)SocketError.ConnectionReset));
        //                    }
        //                    index += bytes;
        //                }
        //                if (timeoutTimer.ElapsedMilliseconds >= millisecondsTimeout) {
        //                    if (throwOnTimeout) {
        //                        throw new LogixSocketException("Receive timed out.");
        //                    } else {
        //                        return null;
        //                    }
        //                }
        //            }

        //            EIPEncaps encaps = EIPEncaps.FromByteArray(buffer);
        //            if(encaps.Length > 0) {
        //                buffer = new byte[encaps.Length];
        //                index = 0;
        //                while (index < buffer.Length) {
        //                    if (sock.Poll((int)(millisecondsTimeout - timeoutTimer.ElapsedMilliseconds), SelectMode.SelectRead)) {
        //                        int bytes = sock.Receive(buffer, index, buffer.Length, SocketFlags.None);
        //                        if (bytes == 0) {
        //                            throw new LogixSocketException("Connection Reset", new SocketException((int)SocketError.ConnectionReset));
        //                        }
        //                        index += bytes;
        //                    }
        //                    if (timeoutTimer.ElapsedMilliseconds >= millisecondsTimeout) {
        //                        if (throwOnTimeout) {
        //                            throw new LogixSocketException("Receive timed out.");
        //                        } else {
        //                            return null;
        //                        }
        //                    }
        //                }
        //            }

        //            var packet = new EIPPacket();
        //            packet.Encaps = encaps;
        //            packet.Data = buffer;
        //            return packet;
        //        } else {
        //            throw new LogixSocketException("No connection");
        //        }
        //    } catch (Exception ex) {
        //        Fault(ex);
        //        throw;
        //    }
        //}

        public EIPPacket Receive(int millisecondsTimeout = DefaultIOTimeout, bool throwOnTimeout = true)
        {
            try
            {
                // Check if the socket is initialized
                if (sock == null)
                {
                    throw new LogixSocketException("Socket is not initialized.");
                }

                Stopwatch timeoutTimer = Stopwatch.StartNew();

                if (WaitForConnection(millisecondsTimeout))
                {
                    byte[] buffer = new byte[EIPEncaps.MarshalSize];

                    // Check if buffer is initialized
                    if (buffer == null)
                    {
                        throw new Exception("Buffer is null!");
                    }

                    int index = 0;
                    while (index < buffer.Length)
                    {
                        if (sock.Poll((int)(millisecondsTimeout - timeoutTimer.ElapsedMilliseconds), SelectMode.SelectRead))
                        {
                            int bytes = sock.Receive(buffer, index, buffer.Length, SocketFlags.None);
                            if (bytes == 0)
                            {
                                throw new LogixSocketException("Connection Reset", new SocketException((int)SocketError.ConnectionReset));
                            }
                            index += bytes;
                        }
                        if (timeoutTimer.ElapsedMilliseconds >= millisecondsTimeout)
                        {
                            if (throwOnTimeout)
                            {
                                throw new LogixSocketException("Receive timed out.");
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }

                    EIPEncaps encaps = EIPEncaps.FromByteArray(buffer);
                    if (encaps.Length > 0)
                    {
                        buffer = new byte[encaps.Length];
                        index = 0;
                        while (index < buffer.Length)
                        {
                            if (sock.Poll((int)(millisecondsTimeout - timeoutTimer.ElapsedMilliseconds), SelectMode.SelectRead))
                            {
                                int bytes = sock.Receive(buffer, index, buffer.Length, SocketFlags.None);
                                if (bytes == 0)
                                {
                                    throw new LogixSocketException("Connection Reset", new SocketException((int)SocketError.ConnectionReset));
                                }
                                index += bytes;
                            }
                            if (timeoutTimer.ElapsedMilliseconds >= millisecondsTimeout)
                            {
                                if (throwOnTimeout)
                                {
                                    throw new LogixSocketException("Receive timed out.");
                                }
                                else
                                {
                                    return null;
                                }
                            }
                        }
                    }

                    var packet = new EIPPacket();
                    packet.Encaps = encaps;
                    packet.Data = buffer;
                    return packet;
                }
                else
                {
                    throw new LogixSocketException("No connection");
                }
            }
            catch (Exception ex)
            {
                Fault(ex);
                throw;
            }
        }



        public EIPPacket Receive(ushort commandFilter, int millisecondsTimeout = DefaultIOTimeout, bool throwOnTimeout = true)
        {
            Stopwatch timeoutTimer = Stopwatch.StartNew();
            EIPPacket packet = null;
            while (null != (packet = Receive((int)(millisecondsTimeout - timeoutTimer.ElapsedMilliseconds), false))) {
                if (commandFilter > 0) {
                    if (packet.Encaps.Command == commandFilter) {
                        return packet;
                    }
                } else {
                    return packet;
                }
            }
            if (throwOnTimeout) {
                throw new LogixSocketException("Receive timed out.");
            } else {
                return null;
            }
        }

        public IEnumerable<EIPPacket> ReceiveMany(int millisecondsTimeout = DefaultIOTimeout)
        {
            Stopwatch timeoutTimer = Stopwatch.StartNew();
            List<EIPPacket> packets = new List<EIPPacket>();
            EIPPacket packet = null;
            while (null != (packet = Receive((int)(millisecondsTimeout - timeoutTimer.ElapsedMilliseconds), false))) {
                packets.Add(packet);
            }
            return packets;
        }
    }
}