using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Logix
{
    public class LogixTcpClient : IDisposable
    {
        const int TransactionTimeout = 30000;

        const int State_Connecting = 1;
        const int State_Connected = 2;
        const int State_Faulted = 3;

        const int Connection_Size = 504;

        volatile int disposed;
        volatile int state;
        LogixTcpSocket tcpSock;
        ManualResetEvent establishingSessionRE;
        IPEndPoint targetEP;
        int port;
        int slot;
        uint eipSessionHandle;
        CIPForwardOpenRequest fopen_Accepted;
        uint cipConnectionID;
        int connectedServiceSequenceID;
        object connectedServiceSequenceIDLock;
        CIPIdentity identity;
        List<EIPService> services;
        Exception faultException;

        Dictionary<string, ushort> structHandlesDict;

        public bool IsConnecting {
            get { return state == State_Connecting; }
        }

        public bool IsConnected {
            get { return state == State_Connected; }
        }

        public bool IsFaulted {
            get { return state == State_Faulted; }
        }

        public Exception FaultException {
            get { return faultException; }
        }

        public LogixTcpClient(IPEndPoint targetEP, int port = 1, int slot = 0)
        {
            if ((port < 0) || (port > 255)) {
                throw new ArgumentException("Must be between 0-255", "port");
            }
            else if ((slot < 0) || (slot > 255)) {
                throw new ArgumentException("Must be between 0-255", "slot");
            }

            this.disposed = 0;
            this.targetEP = targetEP;
            this.port = port;
            this.slot = slot;
            this.establishingSessionRE = new ManualResetEvent(false);
            this.services = new List<EIPService>();
            this.connectedServiceSequenceIDLock = new object();
            this.connectedServiceSequenceID = GetConnectedServiceSequenceIDSeed();
            this.structHandlesDict = new Dictionary<string, ushort>(StringComparer.InvariantCultureIgnoreCase);
            tcpSock = new LogixTcpSocket(targetEP);
            Connect();
            
        }

        ~LogixTcpClient()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (0 == Interlocked.CompareExchange(ref disposed, 1, 0)) {
                if ((state == State_Connected) && (disposing)) {
                    try {
                        UnRegisterSession();
                    }
                    catch (Exception) {
                    }
                }
                tcpSock?.Close();
                tcpSock = null;
            }
        }

        public void Close()
        {
            Dispose();
        }

        bool TrySetToConnected() => TrySetTo(State_Connected, State_Connecting);

        bool TrySetToFaulted()
        {
            return (TrySetTo(State_Faulted, State_Connecting) || TrySetTo(State_Faulted, State_Connected));
        }

        bool TrySetTo(int value, int requiredPrev)
        {
            return requiredPrev == Interlocked.CompareExchange(ref state, value, requiredPrev);
        }

        void Fault(Exception ex)
        {
            if (TrySetToFaulted()) {
                faultException = ex;
            }
        }

        int GetConnectedServiceSequenceIDSeed()
        {
            return Process.GetCurrentProcess().Id % 32768;
        }

        ushort GetNextConnectedServiceSequenceID()
        {
            lock (connectedServiceSequenceIDLock) {
                connectedServiceSequenceID++;
                if (connectedServiceSequenceID == ushort.MaxValue) {
                    connectedServiceSequenceID = GetConnectedServiceSequenceIDSeed();
                }
                return (ushort)connectedServiceSequenceID;
            }
        }

        void Connect()
        {
            state = State_Connecting;
            ThreadPool.QueueUserWorkItem((o) => {
                try {
                    DetermineCIPIdentity();                     // Updates the Global Tag:    CIPIdentity identity;
                    DetermineEncapsulationServiceClasses();     // Updates the Global Tag:    List<EIPService> services;
                    RegisterSession();                          // Updates the Global Tag:    uint eipSessionHandle;
                    ConnectToMessageRouter();                   // Updates the Global Tag:    CIPForwardOpenRequest fopen_Accepted;  &   uint cipConnectionID;
                    TrySetToConnected();    // Provides a thread-safe way to set the state of an object to a new value, ensuring that only one thread can update the state at a time.
                }
                catch (Exception ex) {
                    Fault(ex);
                }
                establishingSessionRE.Set();
            }, null);
        }

        bool WaitForConnection(int millisecondsTimeout)
        {
            if (IsConnected) {
                return true;
            }
            else {
                establishingSessionRE.WaitOne(millisecondsTimeout);
                return IsConnected;
            }
        }

        public void RegisterSession()
        {
            Stopwatch transactionTimer = Stopwatch.StartNew();
            var packet = EIPPacketFactory.CreateRegisterSession(null);


            tcpSock.Send(packet, (int)(TransactionTimeout - transactionTimer.ElapsedMilliseconds));
            packet = tcpSock.Receive((int)(TransactionTimeout - transactionTimer.ElapsedMilliseconds));


            if (packet.Encaps.Command == EIP.Command_RegisterSession) {
                eipSessionHandle = packet.Encaps.SessionHandle;
            }
            else {
                throw new EIPProtocolViolationException("Unexpected reply packet");
            }
        }

        void UnRegisterSession()
        {
            try {
                Stopwatch transactionTimer = Stopwatch.StartNew();
                if (WaitForConnection(TransactionTimeout)) {
                    var packet = EIPPacketFactory.CreateUnRegisterSession(eipSessionHandle, null);


                    tcpSock.Send(packet, (int)(TransactionTimeout - transactionTimer.ElapsedMilliseconds));
                }
            }
            catch (Exception ex) {
                Fault(ex);
            }
        }

        void SendUnitData(IEnumerable<CPFItem> items, string senderContext = null, ushort command = EIP.Command_SendUnitData, int millisecondsTimeout = TransactionTimeout)
        {
            byte[] buffer = new byte[CPF.GetBytesRequired(items) + 6];
            using (var ms = new MemoryStream(buffer, true))
            using (var writer = new BinaryWriter(ms)) {
                writer.Write((uint)0);
                writer.Write((ushort)0);
            }
            CPF.WriteItems(items, buffer, 6);
            var packet = EIPPacketFactory.CreateSendData(command, eipSessionHandle, senderContext, buffer);


            tcpSock.Send(packet, millisecondsTimeout);
        }

        IEnumerable<CPFItem> ReceiveUnitData(ushort command = EIP.Command_SendUnitData, int millisecondsTimeout = TransactionTimeout)
        {
            var packet = tcpSock.Receive(command, millisecondsTimeout);


            return CPF.ReadItems(packet.Data, 6, packet.Data.Length - 6);
        }

        IEnumerable<CPFItem> SendReceiveUnitData(IEnumerable<CPFItem> items, string senderContext = null, ushort command = EIP.Command_SendUnitData, int millisecondsTimeout = TransactionTimeout)
        {
            Stopwatch sw = Stopwatch.StartNew();
            SendUnitData(items, senderContext, command, millisecondsTimeout);
            return ReceiveUnitData(command, (int)(millisecondsTimeout - sw.ElapsedMilliseconds));
        }

        IEnumerable<CPFItem> SendRRData(IEnumerable<CPFItem> items, string senderContext = null, int millisecondsTimeout = TransactionTimeout) =>
            SendReceiveUnitData(items, senderContext, EIP.Command_SendRRData, millisecondsTimeout);


        public void DetermineCIPIdentity()
        {
            const int Timeout = 1000;
            Stopwatch timeoutTimer = Stopwatch.StartNew();
            var request = EIPPacketFactory.CreateListIdentity();

            if (request == null)
            {
                throw new Exception("Request is null!");
            }

            tcpSock.Send(request, Timeout);
            var response = tcpSock.Receive(EIP.Command_ListIdentity,
                                           (int)(Timeout - timeoutTimer.ElapsedMilliseconds),
                                           false);

            if (response == null || response.Data == null)
            {
                throw new Exception("Response or Response.Data is null!");
            }

            var items = CPF.ReadItems(response.Data, 0, response.Data.Length);

            if (items == null)
            {
                throw new Exception("Items is null!");
            }

            CPFItem identityItem = items.Where(x => x.TypeID == 0x0C).SingleOrDefault();

            identity = (identityItem != null) ? CIPIdentity.FromCPFItem(identityItem) : null;
        }



        public void DetermineEncapsulationServiceClasses()
        {
            const int Timeout = 1000;
            Stopwatch timeoutTimer = Stopwatch.StartNew();
            var request = EIPPacketFactory.CreateListServices();

            

            tcpSock.Send(request, Timeout);
            var response = tcpSock.Receive(EIP.Command_ListServices, (int)(Timeout - timeoutTimer.ElapsedMilliseconds), false);


            List<CPFItem> serviceItems = CPF.ReadItems(response.Data, 0, response.Data.Length).ToList();
            foreach (var serviceItem in serviceItems) {
                services.Add(EIPService.FromCPFItem(serviceItem));
            }
        }

        void DisconnectFromMessageRouter()
        {

            CIPMessageRouterRequest fcloseRequest = new CIPMessageRouterRequest();
            fcloseRequest.RequestService = CIPConnectionManager.Instance_Service_Forward_Close;
            fcloseRequest.RequestPath.Add(new CIPSegment8Bits(CIP.Segment_LogicalClass8Bit, CIP.Class_ConnectionManager));
            fcloseRequest.RequestPath.Add(new CIPSegment8Bits(CIP.Segment_LogicalInstance8Bit, CIPConnectionManager.Instance_OpenRequest));
            fcloseRequest.RequestPathSize = (byte)fcloseRequest.RequestPath.CIPGetWordCount();

            CIPForwardCloseRequest fclose = new CIPForwardCloseRequest();
            fclose.PriorityTime_tick.TickTime = 10;
            fclose.Timeout_ticks = 5;
            fclose.ConnectionSerialNumber = fopen_Accepted?.ConnectionSerialNumber ?? 0;
            fclose.OVendorID = 0xBADD;
            fclose.OSerialNumber = fopen_Accepted?.OSerialNumber ?? 0;
            fclose.ConnectionPath.Add(new CIPSegment8Bits((byte)port, (byte)slot));
            fclose.ConnectionPath.Add(new CIPSegment8Bits(CIP.Segment_LogicalClass8Bit, CIP.Class_MessageRouter));
            fclose.ConnectionPath.Add(new CIPSegment8Bits(CIP.Segment_LogicalInstance8Bit, 1));
            fclose.ConnectionPathSize = (byte)fclose.ConnectionPath.CIPGetWordCount();
            fcloseRequest.RequestData = fclose.ToByteArray();

            var fcloseDataItem = new EIPUnconnectedDataItem(fcloseRequest.ToByteArray());
            //var fcloseResponses = SendRRData(new CPFItem[] { EIPCIPAddressItem.Empty.ToCPFItem(), fcloseDataItem.ToCPFItem() }).ToList();

            // Create an array of CPFItem objects
            CPFItem[] cpfItems = new CPFItem[2];
            cpfItems[0] = EIPCIPAddressItem.Empty.ToCPFItem();
            cpfItems[1] = fcloseDataItem.ToCPFItem();

            // Send the CPFItem objects to the server and receive the response as a List<byte>
            IEnumerable<CPFItem> responseBytes = SendRRData(cpfItems);
            List<CPFItem> fcloseResponses = responseBytes.ToList();



            fcloseDataItem = EIPUnconnectedDataItem.FromCPFItem(fcloseResponses[1]);
            var mrrResponse = CIPMessageRouterResponse.FromByteArray(fcloseDataItem.Data);
            var fcloseResponse = mrrResponse.ToCIPForwardCloseResponse();
            if ((fcloseResponse.Success) || (mrrResponse.GeneralStatus == 0x01)) { // 'Sucess' or 'Connection did not exist'.
                fopen_Accepted = null;
                cipConnectionID = 0;
            }
            else {
                throw new CIPUnexpectedResponseException($"Forward_Close failed with GeneralStatus = {mrrResponse.GeneralStatus.ToString("X2")}");
            }
        }

        public void ConnectToMessageRouter()
        {
            DisconnectFromMessageRouter();

            CIPMessageRouterRequest fopenRequest = new CIPMessageRouterRequest();
            fopenRequest.RequestService = CIPConnectionManager.Instance_Service_Forward_Open;
            fopenRequest.RequestPath.Add(new CIPSegment8Bits(CIP.Segment_LogicalClass8Bit, CIP.Class_ConnectionManager));
            fopenRequest.RequestPath.Add(new CIPSegment8Bits(CIP.Segment_LogicalInstance8Bit, CIPConnectionManager.Instance_OpenRequest));
            fopenRequest.RequestPathSize = (byte)fopenRequest.RequestPath.CIPGetWordCount();

            Random rnd = new Random((int)(Stopwatch.GetTimestamp() % int.MaxValue));

            CIPForwardOpenRequest fopen = new CIPForwardOpenRequest();
            fopen.PriorityTime_tick.TickTime = 10;
            fopen.Timeout_ticks = 5;
            fopen.OtoTNetworkConnectionID = 0;
            fopen.TtoONetworkConnectionID = (uint)rnd.Next();
            fopen.ConnectionSerialNumber = (ushort)(rnd.Next() % ushort.MaxValue);
            fopen.OVendorID = 0xBADD;
            fopen.OSerialNumber = (uint)rnd.Next();
            fopen.ConnectionTimeoutMultiplier = 1;
            fopen.OtoTRpi = 2000000;
            fopen.OtoTNetworkConnectionParameters.RedundantOwner = false;
            fopen.OtoTNetworkConnectionParameters.ConnectionType = CIPConnectionManager.Instance_Service_Parameter_NetworkConnectionParameters.ConnectionTypes.PointToPoint;
            fopen.OtoTNetworkConnectionParameters.Priority = CIPConnectionManager.Instance_Service_Parameter_NetworkConnectionParameters.Priorities.Low;
            fopen.OtoTNetworkConnectionParameters.VariableSize = true;
            fopen.OtoTNetworkConnectionParameters.ConnectionSize = Connection_Size;
            fopen.TtoORpi = 2000000;
            fopen.TtoONetworkConnectionParameters.Value = fopen.OtoTNetworkConnectionParameters.Value;
            fopen.TransportTypeTrigger.IsServer = true;
            fopen.TransportTypeTrigger.ProductionTrigger = CIPConnection.Instance_Attribute_TransportClass_trigger.ProductionTriggers.ApplicationObject;
            fopen.TransportTypeTrigger.TransportClass = CIPConnection.Instance_Attribute_TransportClass_trigger.TransportClasses.Class3;
            fopen.ConnectionPath.Add(new CIPSegment8Bits((byte)port, (byte)slot));
            fopen.ConnectionPath.Add(new CIPSegment8Bits(CIP.Segment_LogicalClass8Bit, CIP.Class_MessageRouter));
            fopen.ConnectionPath.Add(new CIPSegment8Bits(CIP.Segment_LogicalInstance8Bit, 1));
            fopen.ConnectionPathSize = (byte)fopen.ConnectionPath.CIPGetWordCount();
            fopenRequest.RequestData = fopen.ToByteArray();

            var fopenDataItem = new EIPUnconnectedDataItem(fopenRequest.ToByteArray());
            var fopenResponses = SendRRData(new CPFItem[] { EIPCIPAddressItem.Empty.ToCPFItem(), fopenDataItem.ToCPFItem() }).ToList();
            fopenDataItem = EIPUnconnectedDataItem.FromCPFItem(fopenResponses[1]);
            var mrrResponse = CIPMessageRouterResponse.FromByteArray(fopenDataItem.Data);
            var fopenResponse = mrrResponse.ToCIPForwardOpenResponse();
            if (fopenResponse.Success) {
                CIPForwardOpenResponseOK okResponse = (CIPForwardOpenResponseOK)fopenResponse;
                fopen_Accepted = fopen;
                cipConnectionID = okResponse.OtoTConnectionID;
            }
            else {
                throw new CIPUnexpectedResponseException($"Forward_Open failed with GeneralStatus = {mrrResponse.GeneralStatus.ToString("X2")}");
            }
        }

        void CycleCIPConnection()
        {
            DisconnectFromMessageRouter();
            ConnectToMessageRouter();
        }

        public CIPIdentity GetDeviceIdentity()
        {
            if (!WaitForConnection(TransactionTimeout)) {
                throw new LogixNotConnectedException(targetEP, port, slot);
            }
            return identity;
        }

        public IEnumerable<EIPService> GetDeviceServices()
        {
            if (!WaitForConnection(TransactionTimeout)) {
                throw new LogixNotConnectedException(targetEP, port, slot);
            }
            return services;
        }

        CIPConnectedServiceResponse ReadTag(string tagName, ushort numberOfElements = 1)
        {
            if (!WaitForConnection(TransactionTimeout))
            {
                throw new LogixNotConnectedException(targetEP, port, slot);
            }

            CIPConnectedServiceRequest rtagRequest = new CIPConnectedServiceRequest();
            rtagRequest.SequenceNumber = GetNextConnectedServiceSequenceID();
            rtagRequest.RequestService = EIP.Service_ReadTag;
            rtagRequest.RequestPath.AddRange(tagName.CIPParseSegments());
            rtagRequest.RequestPathSize = (byte)rtagRequest.RequestPath.CIPGetWordCount();
            rtagRequest.RequestData = BitConverter.GetBytes(numberOfElements);

            var rtagDataItem = new EIPConnectedDataItem(rtagRequest.ToByteArray());
            var rtagAddrItem = new EIPConnectedAddressItem(cipConnectionID);
            List<CPFItem> rtagResponses = null;
            try 
            {
                rtagResponses = SendReceiveUnitData(new CPFItem[] { rtagAddrItem.ToCPFItem(), rtagDataItem.ToCPFItem() }).ToList();
            }
            catch (Exception ex) {
                Fault(ex);
                throw ex;
            }

            var rtagResponseDataItem = EIPConnectedDataItem.FromCPFItem(rtagResponses[1]);
            var rtagResponse = CIPConnectedServiceResponse.FromByteArray(rtagResponseDataItem.Data);
            return rtagResponse;
        }

        List<CIPConnectedServiceResponse> ReadTagFragmented(string tagName, ushort numberOfElements)
        {
            if (!WaitForConnection(TransactionTimeout)) {
                throw new LogixNotConnectedException(targetEP, port, slot);
            }

            CIPConnectedServiceRequest rtagRequest = new CIPConnectedServiceRequest();
            rtagRequest.RequestService = EIP.Service_ReadTagFragmented;
            rtagRequest.RequestPath.AddRange(tagName.CIPParseSegments());
            rtagRequest.RequestPathSize = (byte)rtagRequest.RequestPath.CIPGetWordCount();
            byte[] requestData = new byte[6];
            rtagRequest.RequestData = requestData;
            byte[] numberOfElementBytes = BitConverter.GetBytes(numberOfElements);
            rtagRequest.RequestData[0] = numberOfElementBytes[0];
            rtagRequest.RequestData[1] = numberOfElementBytes[1];

            int offset = 0;
            List<CIPConnectedServiceResponse> rtagDataResponses = new List<CIPConnectedServiceResponse>();
            while (true) {
                rtagRequest.SequenceNumber = GetNextConnectedServiceSequenceID();

                byte[] offsetBytes = BitConverter.GetBytes(offset);
                rtagRequest.RequestData[2] = offsetBytes[0];
                rtagRequest.RequestData[3] = offsetBytes[1];
                rtagRequest.RequestData[4] = offsetBytes[2];
                rtagRequest.RequestData[5] = offsetBytes[3];

                var rtagDataItem = new EIPConnectedDataItem(rtagRequest.ToByteArray());
                var rtagAddrItem = new EIPConnectedAddressItem(cipConnectionID);
                List<CPFItem> rtagResponses = null;
                try {
                    rtagResponses = SendReceiveUnitData(new CPFItem[] { rtagAddrItem.ToCPFItem(), rtagDataItem.ToCPFItem() }).ToList();
                }
                catch (Exception ex) {
                    Fault(ex);
                    throw ex;
                }
                var rtagResponseDataItem = EIPConnectedDataItem.FromCPFItem(rtagResponses[1]);
                var rtagResponse = CIPConnectedServiceResponse.FromByteArray(rtagResponseDataItem.Data);
                if (0x06 == rtagResponse.GeneralStatus) { // Reply Data Too Large
                    rtagDataResponses.Add(rtagResponse);
                    ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                    if (EIP.DataType_Struct == dataType) {
                        offset += (rtagResponse.ReplyData.Length - 4);
                    }
                    else {
                        offset += (rtagResponse.ReplyData.Length - 2);
                    }
                }
                else {
                    rtagDataResponses.Add(rtagResponse);
                    break;
                }
            }

            return rtagDataResponses;
        }

        public List<CIPMessageRouterResponse> ReadTags(List<string> tagNames)
        {
            List<CIPMessageRouterResponse> retVal = new List<CIPMessageRouterResponse>();
            if (!WaitForConnection(TransactionTimeout)) {
                throw new LogixNotConnectedException();
            }

            Queue<CIPMessageRouterRequest> tagRequests = new Queue<CIPMessageRouterRequest>();
            foreach (string tag in tagNames) {
                CIPMessageRouterRequest request = new CIPMessageRouterRequest();
                request.RequestService = EIP.Service_ReadTag;
                request.RequestPath.AddRange(tag.CIPParseSegments());
                request.RequestPathSize = (byte)request.RequestPath.CIPGetWordCount();
                request.RequestData = BitConverter.GetBytes((ushort)1);
                tagRequests.Enqueue(request);
            }

            while (tagRequests.Count > 0) {
                CIPConnectedServiceRequest rtagRequest = new CIPConnectedServiceRequest();
                rtagRequest.SequenceNumber = GetNextConnectedServiceSequenceID();
                rtagRequest.RequestService = EIP.Service_MultipleServicePacket;
                rtagRequest.RequestPath.Add(new CIPSegment8Bits(CIP.Segment_LogicalClass8Bit, 0x02));
                rtagRequest.RequestPath.Add(new CIPSegment8Bits(CIP.Segment_LogicalInstance8Bit, 0x01));
                rtagRequest.RequestPathSize = (byte)rtagRequest.RequestPath.CIPGetWordCount();

                int msrpSize = 8 + 2; //8 byte header, 2 bytes of overhead in CIPMultipleServiceRequestPacket
                CIPMultipleServiceRequestPacket msrp = new CIPMultipleServiceRequestPacket();
                while (tagRequests.Count > 0) {
                    var tagRequest = tagRequests.Peek();
                    if (msrpSize + 2 + tagRequest.Size < Connection_Size) { //Size of this request will be 2 bytes for offset + size of actual request.
                        msrp.Add(tagRequests.Dequeue());
                        msrpSize += tagRequest.Size + 2;
                    }
                    else {
                        break;
                    }
                }
                rtagRequest.RequestData = msrp.ToByteArray();

                var rtagDataItem = new EIPConnectedDataItem(rtagRequest.ToByteArray());
                var rtagAddrItem = new EIPConnectedAddressItem(cipConnectionID);
                List<CPFItem> rtagResponses = null;
                try {
                    rtagResponses = SendReceiveUnitData(new CPFItem[] { rtagAddrItem.ToCPFItem(), rtagDataItem.ToCPFItem() }).ToList();
                }
                catch (Exception ex) {
                    Fault(ex);
                    throw ex;
                }

                var rtagResponseDataItem = EIPConnectedDataItem.FromCPFItem(rtagResponses[1]);
                var rtagResponse = CIPConnectedServiceResponse.FromByteArray(rtagResponseDataItem.Data);
                if (rtagResponse.GeneralStatus == 0x00) {
                    CIPMultipleServiceResponsePacket p = CIPMultipleServiceResponsePacket.FromByteArray(rtagResponse.ReplyData);
                    retVal.AddRange(p);
                }
                else if (rtagResponse.GeneralStatus == 0x1E) { //An embedded service resulted in an error. This should mean that the MSR was OK, just one of the embedded requests failed. We are going to try to process (though with a try/catch). We should be able to.
                    try {
                        CIPMultipleServiceResponsePacket p = CIPMultipleServiceResponsePacket.FromByteArray(rtagResponse.ReplyData);
                        retVal.AddRange(p);
                    }
                    catch(Exception) {
                        throw new EIPLogixServiceException(rtagResponse);
                    }
                }
                else {
                    throw new EIPLogixServiceException(rtagResponse);
                }
            }
            return retVal;
        }

        public List<object> ReadTags(List<LogixReadTagRequest> tags)
        {
            List<object> retVal = new List<object>();

            var responses = ReadTags(tags.Select(x => x.TagName).ToList());

            if (responses.Count() != tags.Count()) {
                throw new LogixClientException($"Number of ReadTags responses ({responses.Count()}) does not equal number of tags requested ({tags.Count()}).");
            }

            for (int i = 0; i < tags.Count(); i++) {
                LogixReadTagRequest rTagRequest = tags[i];
                CIPMessageRouterResponse rtagResponse = responses[i];

                if (0x00 == rtagResponse.GeneralStatus) {
                    ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                    switch (rTagRequest.DataType) {
                        case Logix.TagTypes.Bool:
                            if (EIP.DataType_BOOL_Mask == (dataType & EIP.DataType_BOOL_Mask)) {
                                retVal.Add(rtagResponse.ReplyData[2] > 0);
                            }
                            else {
                                throw new EIPBadTypeException(EIP.DataType_BOOL_Mask, dataType);
                            }
                            break;
                        case Logix.TagTypes.UInt8:
                            if (EIP.DataType_SINT == dataType) {
                                retVal.Add((byte)rtagResponse.ReplyData[2]);
                            }
                            else {
                                throw new EIPBadTypeException(EIP.DataType_SINT, dataType);
                            }
                            break;
                        case Logix.TagTypes.Int8:
                            if (EIP.DataType_SINT == dataType) {
                                retVal.Add((sbyte)rtagResponse.ReplyData[2]);
                            }
                            else {
                                throw new EIPBadTypeException(EIP.DataType_SINT, dataType);
                            }
                            break;
                        case Logix.TagTypes.UInt16:
                            if (EIP.DataType_INT == dataType) {
                                retVal.Add(BitConverter.ToUInt16(rtagResponse.ReplyData, 2));
                            }
                            else {
                                throw new EIPBadTypeException(EIP.DataType_INT, dataType);
                            }
                            break;
                        case Logix.TagTypes.Int16:
                            if (EIP.DataType_INT == dataType) {
                                retVal.Add((Int16)BitConverter.ToUInt16(rtagResponse.ReplyData, 2));
                            }
                            else {
                                throw new EIPBadTypeException(EIP.DataType_INT, dataType);
                            }
                            break;
                        case Logix.TagTypes.UInt32:
                            if (EIP.DataType_DWORD == dataType) {
                                retVal.Add(BitConverter.ToUInt32(rtagResponse.ReplyData, 2));
                            }
                            else {
                                throw new EIPBadTypeException(EIP.DataType_DWORD, dataType);
                            }
                            break;
                        case Logix.TagTypes.Int32:
                            if (EIP.DataType_DINT == dataType) {
                                retVal.Add((Int32)BitConverter.ToUInt32(rtagResponse.ReplyData, 2));
                            }
                            else {
                                throw new EIPBadTypeException(EIP.DataType_DWORD, dataType);
                            }
                            break;
                        case Logix.TagTypes.UInt64:
                            if (EIP.DataType_LINT == dataType) {
                                retVal.Add(BitConverter.ToUInt64(rtagResponse.ReplyData, 2));
                            }
                            else {
                                throw new EIPBadTypeException(EIP.DataType_LINT, dataType);
                            }
                            break;
                        case Logix.TagTypes.Int64:
                            if (EIP.DataType_LINT == dataType) {
                                retVal.Add((Int64)BitConverter.ToUInt64(rtagResponse.ReplyData, 2));
                            }
                            else {
                                throw new EIPBadTypeException(EIP.DataType_LINT, dataType);
                            }
                            break;
                        case Logix.TagTypes.Single:
                            if (EIP.DataType_REAL == dataType) {
                                retVal.Add(BitConverter.ToSingle(rtagResponse.ReplyData, 2));
                            }
                            else {
                                throw new EIPBadTypeException(EIP.DataType_LINT, dataType);
                            }
                            break;
                        case Logix.TagTypes.Double:
                            if (EIP.DataType_LINT == dataType) {
                                retVal.Add(BitConverter.Int64BitsToDouble((Int64)BitConverter.ToUInt64(rtagResponse.ReplyData, 2)));
                            }
                            else {
                                throw new EIPBadTypeException(EIP.DataType_LINT, dataType);
                            }
                            break;
                        case Logix.TagTypes.String:
                            if (EIP.DataType_Struct == dataType) {
                                int LEN = BitConverter.ToInt32(rtagResponse.ReplyData, 4);
                                string value = Encoding.ASCII.GetString(rtagResponse.ReplyData, 8, LEN);
                                retVal.Add(value);
                            }
                            else {
                                throw new EIPBadTypeException(EIP.DataType_Struct, dataType);
                            }
                            break;
                    }
                }
                else if (0x05 == rtagResponse.GeneralStatus) {  //Path Destination Unknown
                    retVal.Add(null); //This is going to happen whenever a tag doesn't exist. Let's just return null instead of breaking the entire channel.
                }
                else {
                    retVal.Add(null); //This is going to happen whenever a tag doesn't exist. Let's just return null instead of breaking the entire channel.
                    //throw new EIPLogixRouterException(rtagResponse);
                }
            }

            return retVal;
        }

        public bool ReadBool(string tagName)
        {
            var rtagResponse = ReadTag(tagName);
            if (0x00 == rtagResponse.GeneralStatus) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (EIP.DataType_BOOL_Mask == (dataType & EIP.DataType_BOOL_Mask)) {
                    return rtagResponse.ReplyData[2] > 0;
                }
                else {
                    throw new EIPBadTypeException(EIP.DataType_BOOL_Mask, dataType);
                }
            }
            else {
                throw new EIPLogixServiceException(rtagResponse);
            }
        }

        /// <summary>
        /// Reads an array of BOOL values.  Returns the array as raw memory (Packed bytes).
        /// </summary>
        /// <param name="tagName">Name of the ControlLogix Tag</param>
        /// <param name="arraySize">Number of BOOL elements in the array.</param>
        /// <returns></returns>
        public byte[] ReadBools_RAW(string tagName, int arraySize)
        {
            int controlLogixSize = 0;
            if (0 != (arraySize % 32)) {
                int paddingBools = 32 - (arraySize % 32);
                controlLogixSize = (arraySize + paddingBools) / 32;
            }
            else {
                controlLogixSize = arraySize / 32;
            }

            var rtagResponses = ReadTagFragmented(tagName, (ushort)controlLogixSize);
            foreach (var rtagResponse in rtagResponses) {
                if ((0x00 != rtagResponse.GeneralStatus) && (0x06 != rtagResponse.GeneralStatus)) {
                    throw new EIPLogixServiceException(rtagResponse);
                }
            }

            byte[] values = new byte[controlLogixSize * 4];
            int valuesIndex = 0;
            foreach (var rtagResponse in rtagResponses) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (EIP.DataType_BOOL_Mask == (dataType & EIP.DataType_BOOL_Mask)) {
                    int byteCount = rtagResponse.ReplyData.Length - 2;
                    Buffer.BlockCopy(rtagResponse.ReplyData, 2, values, valuesIndex, byteCount);
                    valuesIndex += byteCount;
                }
                else {
                    throw new EIPBadTypeException(EIP.DataType_BOOL_Mask, dataType);
                }
            }
            return values;
        }

        public bool[] ReadBools(string tagName, int arraySize)
        {
            byte[] values = ReadBools_RAW(tagName, arraySize);
            bool[] boolValues = new bool[values.Length * 8];
            for (int i = 0; i < values.Length; i++) {
                int boolsIndex = (8 * i);
                boolValues[boolsIndex] = (0x01 == (0x01 & values[i]));
                boolValues[boolsIndex + 1] = (0x02 == (0x02 & values[i]));
                boolValues[boolsIndex + 2] = (0x04 == (0x04 & values[i]));
                boolValues[boolsIndex + 3] = (0x08 == (0x08 & values[i]));
                boolValues[boolsIndex + 4] = (0x10 == (0x10 & values[i]));
                boolValues[boolsIndex + 5] = (0x20 == (0x20 & values[i]));
                boolValues[boolsIndex + 6] = (0x40 == (0x40 & values[i]));
            }
            return boolValues;
        }

        public byte[] ReadUInt8s(string tagName, int arraySize)
        {
            var rtagResponses = ReadTagFragmented(tagName, (ushort)arraySize);
            foreach (var rtagResponse in rtagResponses) {
                if ((0x00 != rtagResponse.GeneralStatus) && (0x06 != rtagResponse.GeneralStatus)) {
                    throw new EIPLogixServiceException(rtagResponse);
                }
            }

            byte[] values = new byte[arraySize];
            int valuesIndex = 0;
            foreach (var rtagResponse in rtagResponses) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (EIP.DataType_SINT == dataType) {
                    int byteCount = rtagResponse.ReplyData.Length - 2;
                    Buffer.BlockCopy(rtagResponse.ReplyData, 2, values, valuesIndex, byteCount);
                    valuesIndex += byteCount;
                }
                else {
                    throw new EIPBadTypeException(EIP.DataType_SINT, dataType);
                }
            }
            return values;
        }

        public sbyte[] ReadInt8s(string tagName, int arraySize)
        {
            byte[] cpuBuffer = ReadUInt8s(tagName, arraySize);

            sbyte[] values = new sbyte[cpuBuffer.Length];
            Buffer.BlockCopy(cpuBuffer, 0, values, 0, cpuBuffer.Length);
            return values;
        }

        public byte ReadUInt8(string tagName)
        {
            var rtagResponse = ReadTag(tagName);
            if (0x00 == rtagResponse.GeneralStatus) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (EIP.DataType_SINT == dataType) {
                    return rtagResponse.ReplyData[2];
                }
                else {
                    throw new EIPBadTypeException(EIP.DataType_SINT, dataType);
                }
            }
            else {
                throw new EIPLogixServiceException(rtagResponse);
            }
        }

        public sbyte ReadInt8(string tagName) =>
            unchecked((sbyte)ReadUInt8(tagName));

        public ushort ReadUInt16(string tagName)
        {
            var rtagResponse = ReadTag(tagName);
            if (0x00 == rtagResponse.GeneralStatus) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (EIP.DataType_INT == dataType) {
                    return BitConverter.ToUInt16(rtagResponse.ReplyData, 2);
                }
                else {
                    throw new EIPBadTypeException(EIP.DataType_INT, dataType);
                }
            }
            else {
                throw new EIPLogixServiceException(rtagResponse);
            }
        }

        public short ReadInt16(string tagName) =>
            (short)ReadUInt16(tagName);

        public byte[] ReadUInt16s_RAW(string tagName, int arraySize)
        {
            var rtagResponses = ReadTagFragmented(tagName, (ushort)arraySize);
            foreach (var rtagResponse in rtagResponses) {
                if ((0x00 != rtagResponse.GeneralStatus) && (0x06 != rtagResponse.GeneralStatus)) {
                    throw new EIPLogixServiceException(rtagResponse);
                }
            }

            byte[] cpuBuffer = new byte[arraySize * 2];
            int cpuBufferIndex = 0;
            foreach (var rtagResponse in rtagResponses) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (EIP.DataType_INT == dataType) {
                    int byteCount = rtagResponse.ReplyData.Length - 2;
                    Buffer.BlockCopy(rtagResponse.ReplyData, 2, cpuBuffer, cpuBufferIndex, byteCount);
                    cpuBufferIndex += byteCount;
                }
                else {
                    throw new EIPBadTypeException(EIP.DataType_INT, dataType);
                }
            }
            return cpuBuffer;
        }

        public ushort[] ReadUInt16s(string tagName, int arraySize)
        {
            var cpuBuffer = ReadUInt16s_RAW(tagName, arraySize);

            ushort[] values = new ushort[cpuBuffer.Length / 2];
            Buffer.BlockCopy(cpuBuffer, 0, values, 0, cpuBuffer.Length);
            return values;
        }

        public short[] ReadInt16s(string tagName, int arraySize)
        {
            var cpuBuffer = ReadUInt16s_RAW(tagName, arraySize);

            short[] values = new short[cpuBuffer.Length / 2];
            Buffer.BlockCopy(cpuBuffer, 0, values, 0, cpuBuffer.Length);
            return values;
        }

        uint ReadUInt32(string tagName, ushort expectedType)
        {
            var rtagResponse = ReadTag(tagName);
            if (0x00 == rtagResponse.GeneralStatus) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (expectedType == dataType) {
                    return BitConverter.ToUInt32(rtagResponse.ReplyData, 2);
                }
                else {
                    throw new EIPBadTypeException(expectedType, dataType);
                }
            }
            else {
                throw new EIPLogixServiceException(rtagResponse);
            }
        }

        byte[] ReadUInt32s_RAW(string tagName, int arraySize, ushort expectedType)
        {
            var rtagResponses = ReadTagFragmented(tagName, (ushort)arraySize);
            foreach (var rtagResponse in rtagResponses) {
                if ((0x00 != rtagResponse.GeneralStatus) && (0x06 != rtagResponse.GeneralStatus)) {
                    throw new EIPLogixServiceException(rtagResponse);
                }
            }

            byte[] cpuBuffer = new byte[arraySize * 4];
            int cpuBufferIndex = 0;
            foreach (var rtagResponse in rtagResponses) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (expectedType == dataType) {
                    int byteCount = rtagResponse.ReplyData.Length - 2;
                    Buffer.BlockCopy(rtagResponse.ReplyData, 2, cpuBuffer, cpuBufferIndex, byteCount);
                    cpuBufferIndex += byteCount;
                }
                else {
                    throw new EIPBadTypeException(expectedType, dataType);
                }
            }
            return cpuBuffer;
        }

        public uint[] ReadUInt32s(string tagName, int arraySize)
        {
            var cpuBuffer = ReadUInt32s_RAW(tagName, arraySize, EIP.DataType_DWORD);

            uint[] values = new uint[cpuBuffer.Length / 4];
            Buffer.BlockCopy(cpuBuffer, 0, values, 0, cpuBuffer.Length);
            return values;
        }

        public uint ReadUInt32(string tagName) =>
            ReadUInt32(tagName, EIP.DataType_DWORD);

        public int ReadInt32(string tagName) =>
            (int)ReadUInt32(tagName, EIP.DataType_DINT);

        public byte[] ReadUInt32s_RAW(string tagName, int arraySize) =>
            ReadUInt32s_RAW(tagName, arraySize, EIP.DataType_DWORD);

        public byte[] ReadInt32s_RAW(string tagName, int arraySize) =>
            ReadUInt32s_RAW(tagName, arraySize, EIP.DataType_DINT);

        public int[] ReadInt32s(string tagName, int arraySize)
        {
            var cpuBuffer = ReadUInt32s_RAW(tagName, arraySize, EIP.DataType_DINT);

            int[] values = new int[cpuBuffer.Length / 4];
            Buffer.BlockCopy(cpuBuffer, 0, values, 0, cpuBuffer.Length);
            return values;
        }

        public float ReadSingle(string tagName)
        {
            var rtagResponse = ReadTag(tagName);
            if (0x00 == rtagResponse.GeneralStatus) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (EIP.DataType_REAL == dataType) {
                    return BitConverter.ToSingle(rtagResponse.ReplyData, 2);
                }
                else {
                    throw new EIPBadTypeException(EIP.DataType_REAL, dataType);
                }
            }
            else {
                throw new EIPLogixServiceException(rtagResponse);
            }
        }

        public byte[] ReadSingles_RAW(string tagName, int arraySize) =>
            ReadUInt32s_RAW(tagName, arraySize, EIP.DataType_REAL);

        public float[] ReadSingles(string tagName, int arraySize)
        {
            var cpuBuffer = ReadUInt32s_RAW(tagName, arraySize, EIP.DataType_REAL);

            float[] values = new float[cpuBuffer.Length / 4];
            Buffer.BlockCopy(cpuBuffer, 0, values, 0, cpuBuffer.Length);
            return values;
        }

        public ulong ReadUInt64(string tagName)
        {
            var rtagResponse = ReadTag(tagName);
            if (0x00 == rtagResponse.GeneralStatus) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (EIP.DataType_LINT == dataType) {
                    return BitConverter.ToUInt64(rtagResponse.ReplyData, 2);
                }
                else {
                    throw new EIPBadTypeException(EIP.DataType_LINT, dataType);
                }
            }
            else {
                throw new EIPLogixServiceException(rtagResponse);
            }
        }

        public long ReadInt64(string tagName) =>
            (long)ReadUInt64(tagName);

        public byte[] ReadUInt64s_RAW(string tagName, int arraySize)
        {
            var rtagResponses = ReadTagFragmented(tagName, (ushort)arraySize);
            foreach (var rtagResponse in rtagResponses) {
                if ((0x00 != rtagResponse.GeneralStatus) && (0x06 != rtagResponse.GeneralStatus)) {
                    throw new EIPLogixServiceException(rtagResponse);
                }
            }

            byte[] cpuBuffer = new byte[arraySize * 8];
            int cpuBufferIndex = 0;
            foreach (var rtagResponse in rtagResponses) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (EIP.DataType_LINT == dataType) {
                    int byteCount = rtagResponse.ReplyData.Length - 2;
                    Buffer.BlockCopy(rtagResponse.ReplyData, 2, cpuBuffer, cpuBufferIndex, byteCount);
                    cpuBufferIndex += byteCount;
                }
                else {
                    throw new EIPBadTypeException(EIP.DataType_LINT, dataType);
                }
            }
            return cpuBuffer;
        }

        public ulong[] ReadUInt64s(string tagName, int arraySize)
        {
            var cpuBuffer = ReadUInt16s_RAW(tagName, arraySize);

            ulong[] values = new ulong[cpuBuffer.Length / 8];
            Buffer.BlockCopy(cpuBuffer, 0, values, 0, cpuBuffer.Length);
            return values;
        }

        public long[] ReadInt64s(string tagName, int arraySize)
        {
            var cpuBuffer = ReadUInt16s_RAW(tagName, arraySize);

            long[] values = new long[cpuBuffer.Length / 8];
            Buffer.BlockCopy(cpuBuffer, 0, values, 0, cpuBuffer.Length);
            return values;
        }

        public double ReadDouble(string tagName)
        {
            long bits = ReadInt64(tagName);
            return BitConverter.Int64BitsToDouble(bits);
        }

        public double[] ReadDoubles(string tagName, int arraySize)
        {
            var cpuBuffer = ReadUInt16s_RAW(tagName, arraySize);

            double[] values = new double[cpuBuffer.Length / 8];
            Buffer.BlockCopy(cpuBuffer, 0, values, 0, cpuBuffer.Length);
            return values;
        }

        public string ReadString(string tagName)
        {
            var rtagResponse = ReadTag(tagName);
            if (0x00 == rtagResponse.GeneralStatus) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (EIP.DataType_Struct == dataType) {
                    int LEN = BitConverter.ToInt32(rtagResponse.ReplyData, 4);
                    string value = Encoding.ASCII.GetString(rtagResponse.ReplyData, 8, LEN);
                    return value;
                }
                else {
                    throw new EIPBadTypeException(EIP.DataType_Struct, dataType);
                }
            }
            else {
                throw new EIPLogixServiceException(rtagResponse);
            }
        }

        public byte[] ReadStructs(string tagName, int arraySize)
        {
            var rtagResponses = ReadTagFragmented(tagName, (ushort)arraySize);
            foreach (var rtagResponse in rtagResponses) {
                if ((0x00 != rtagResponse.GeneralStatus) && (0x06 != rtagResponse.GeneralStatus)) {
                    throw new EIPLogixServiceException(rtagResponse);
                }
            }

            int totalBytes = 0;
            foreach (var rtagResponse in rtagResponses) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (EIP.DataType_Struct == dataType) {
                    totalBytes += (rtagResponse.ReplyData.Length - 4);
                }
                else {
                    throw new EIPBadTypeException(EIP.DataType_Struct, dataType);
                }
            }

            byte[] cpuBuffer = new byte[totalBytes];
            int cpuBufferIndex = 0;
            foreach (var rtagResponse in rtagResponses) {
                int byteCount = rtagResponse.ReplyData.Length - 4;
                Buffer.BlockCopy(rtagResponse.ReplyData, 4, cpuBuffer, cpuBufferIndex, byteCount);
                cpuBufferIndex += byteCount;
            }

            return cpuBuffer;
        }

        public byte[] ReadStruct(string tagName) =>
            ReadStructs(tagName, 1);

        public ushort QueryStructureHandle(string tagName)
        {
            var rtagResponses = ReadTagFragmented(tagName, 1);
            var rtagResponse = rtagResponses?.LastOrDefault();
            if (0x00 == rtagResponse.GeneralStatus) {
                ushort dataType = BitConverter.ToUInt16(rtagResponse.ReplyData, 0);
                if (EIP.DataType_Struct == dataType) {
                    return BitConverter.ToUInt16(rtagResponse.ReplyData, 2);
                }
                else {
                    throw new EIPBadTypeException(EIP.DataType_Struct, dataType);
                }
            }
            else {
                throw new EIPLogixServiceException(rtagResponse);
            }
        }

        CIPConnectedServiceResponse WriteTag(string tagName, Action<CIPConnectedServiceRequest> fillRequest)
        {
            if (!WaitForConnection(TransactionTimeout)) {
                throw new LogixNotConnectedException(targetEP, port, slot);
            }

            CIPConnectedServiceRequest wtagRequest = new CIPConnectedServiceRequest();
            wtagRequest.SequenceNumber = GetNextConnectedServiceSequenceID();
            wtagRequest.RequestService = EIP.Service_WriteTag;
            wtagRequest.RequestPath.AddRange(tagName.CIPParseSegments());
            wtagRequest.RequestPathSize = (byte)wtagRequest.RequestPath.CIPGetWordCount();

            fillRequest(wtagRequest);

            var wtagDataItem = new EIPConnectedDataItem(wtagRequest.ToByteArray());
            var wtagAddrItem = new EIPConnectedAddressItem(cipConnectionID);
            List<CPFItem> wtagResponses = null;
            try {
                wtagResponses = SendReceiveUnitData(new CPFItem[] { wtagAddrItem.ToCPFItem(), wtagDataItem.ToCPFItem() }).ToList();
            }
            catch (Exception ex) {
                Fault(ex);
                throw ex;
            }
            var wtagResponseDataItem = EIPConnectedDataItem.FromCPFItem(wtagResponses[1]);
            var wtagResponse = CIPConnectedServiceResponse.FromByteArray(wtagResponseDataItem.Data);
            return wtagResponse;
        }

        CIPConnectedServiceResponse WriteTagFragmented(string tagName, Func<CIPConnectedServiceRequest, int, bool> fnFillRequest) =>
            WriteTagFragmented(tagName, fnFillRequest, true);

        CIPConnectedServiceResponse WriteTagFragmented(string tagName, Func<CIPConnectedServiceRequest, int, bool> fnFillRequest, bool throwOnFail)
        {
            if (!WaitForConnection(TransactionTimeout)) {
                throw new LogixNotConnectedException(targetEP, port, slot);
            }

            CIPConnectedServiceRequest wtagRequest = new CIPConnectedServiceRequest();
            wtagRequest.RequestService = EIP.Service_WriteTagFragmented;
            wtagRequest.RequestPath.AddRange(tagName.CIPParseSegments());
            wtagRequest.RequestPathSize = (byte)wtagRequest.RequestPath.CIPGetWordCount();

            CIPConnectedServiceResponse wtagResponse = null;
            while (true) {
                wtagRequest.SequenceNumber = GetNextConnectedServiceSequenceID();
                const int CIPConnectedServiceRequestHeaderBytes = 18;
                int maxDataBytes = Connection_Size - CIPConnectedServiceRequestHeaderBytes - (wtagRequest.RequestPathSize * 2);
                if (!fnFillRequest(wtagRequest, maxDataBytes)) {
                    break;
                }
                var wtagDataItem = new EIPConnectedDataItem(wtagRequest.ToByteArray());
                var wtagAddrItem = new EIPConnectedAddressItem(cipConnectionID);
                List<CPFItem> wtagResponses = null;
                try {
                    wtagResponses = SendReceiveUnitData(new CPFItem[] { wtagAddrItem.ToCPFItem(), wtagDataItem.ToCPFItem() }).ToList();
                }
                catch (Exception ex) {
                    Fault(ex);
                    throw ex;
                }
                var wtagResponseDataItem = EIPConnectedDataItem.FromCPFItem(wtagResponses[1]);
                wtagResponse = CIPConnectedServiceResponse.FromByteArray(wtagResponseDataItem.Data);
                if (wtagResponse.GeneralStatus != 0x00) {
                    if (throwOnFail) {
                        throw new EIPLogixServiceException(wtagResponse);
                    }
                    else {
                        break;
                    }
                }
            }
            return wtagResponse;
        }

        public void WriteBool(string tagName, bool value)
        {
            var wtagResponse = WriteTag(tagName, (wtagRequest) => {
                ushort dataType = EIP.DataType_BOOL(0);
                byte[] valueBuffer = new byte[1] { (byte)((value == true) ? 0xFF : 0x00) };
                byte[] requestData = new byte[4 + valueBuffer.Length];
                using (var ms = new MemoryStream(requestData, true))
                using (var writer = new BinaryWriter(ms)) {
                    writer.Write(dataType);
                    writer.Write((ushort)1);
                    writer.Write(valueBuffer);
                }
                wtagRequest.RequestData = requestData;
            });
            if (wtagResponse.GeneralStatus != 0x00) {
                throw new EIPLogixServiceException(wtagResponse);
            }
        }

        public void WriteUInt8(string tagName, byte value)
        {
            var wtagResponse = WriteTag(tagName, (wtagRequest) => {
                ushort dataType = EIP.DataType_SINT;
                byte[] valueBuffer = new byte[1] { value };
                byte[] requestData = new byte[4 + valueBuffer.Length];
                using (var ms = new MemoryStream(requestData, true))
                using (var writer = new BinaryWriter(ms)) {
                    writer.Write(dataType);
                    writer.Write((ushort)1);
                    writer.Write(valueBuffer);
                }
                wtagRequest.RequestData = requestData;
            });
            if (wtagResponse.GeneralStatus != 0x00) {
                throw new EIPLogixServiceException(wtagResponse);
            }
        }

        public void WriteInt8(string tagName, sbyte value) =>
            WriteUInt8(tagName, (byte)value);

        void WriteUInt8s_RAW(string tagName, byte[] buffer, ushort sourceDataType)
        {
            ushort dataType = sourceDataType;

            int elementSize = 0;
            if (dataType == EIP.DataType_SINT) {
                elementSize = 1;
            }
            else if (dataType == EIP.DataType_INT) {
                elementSize = 2;
            }
            else if ((dataType == EIP.DataType_DWORD) || (dataType == EIP.DataType_DINT) || (dataType == EIP.DataType_REAL)) {
                elementSize = 4;
            }
            else if (dataType == EIP.DataType_LINT) {
                elementSize = 8;
            }
            else {
                throw new NotSupportedException("Use a different function for this data type");
            }

            if (0 != (buffer.Length % elementSize)) {
                throw new ArgumentException($"Array size must be evenly divisible by {elementSize}", "values");
            }

            int arraySize = buffer.Length / elementSize;
            int bufferOffset = 0;
            int bytesRemaining = buffer.Length;
            byte[] subBuffer = null;

            WriteTagFragmented(tagName, (wtagRequest, maxDataBytes) => {

                int appliedMaxDataBytes = maxDataBytes;
                if (0 != (maxDataBytes % elementSize)) {
                    appliedMaxDataBytes = maxDataBytes - (maxDataBytes % elementSize);
                }

                if (bufferOffset == buffer.Length) {
                    return false;
                }
                int frameBytes = Math.Min(appliedMaxDataBytes, bytesRemaining);
                if (frameBytes != (subBuffer?.Length ?? 0)) {
                    subBuffer = new byte[frameBytes];
                }
                Buffer.BlockCopy(buffer, bufferOffset, subBuffer, 0, frameBytes);
                byte[] requestData = new byte[8 + subBuffer.Length];
                using (var ms = new MemoryStream(requestData, true))
                using (var writer = new BinaryWriter(ms)) {
                    writer.Write(dataType);
                    writer.Write((ushort)arraySize);
                    writer.Write(bufferOffset);
                    writer.Write(subBuffer);
                }
                wtagRequest.RequestData = requestData;
                bufferOffset += frameBytes;
                bytesRemaining = bytesRemaining - frameBytes;
                return true;
            });
        }

        public void WriteUInt8s(string tagName, byte[] values) =>
            WriteUInt8s_RAW(tagName, values, EIP.DataType_SINT);

        public void WriteInt8s(string tagName, sbyte[] values)
        {
            byte[] valuesBuffer = new byte[values.Length];
            Buffer.BlockCopy(values, 0, valuesBuffer, 0, values.Length);
            WriteUInt8s(tagName, valuesBuffer);
        }

        public void WriteUInt16(string tagName, ushort value)
        {
            var wtagResponse = WriteTag(tagName, (wtagRequest) => {
                ushort dataType = EIP.DataType_INT;
                byte[] valueBuffer = BitConverter.GetBytes(value);
                byte[] requestData = new byte[4 + valueBuffer.Length];
                using (var ms = new MemoryStream(requestData, true))
                using (var writer = new BinaryWriter(ms)) {
                    writer.Write(dataType);
                    writer.Write((ushort)1);
                    writer.Write(valueBuffer);
                }
                wtagRequest.RequestData = requestData;
            });
            if (wtagResponse.GeneralStatus != 0x00) {
                throw new EIPLogixServiceException(wtagResponse);
            }
        }

        public void WriteInt16(string tagName, short value) =>
            WriteUInt16(tagName, (ushort)value);

        public void WriteUInt16s(string tagName, ushort[] values)
        {
            byte[] buffer = new byte[values.Length * Marshal.SizeOf<ushort>()];
            Buffer.BlockCopy(values, 0, buffer, 0, buffer.Length);
            WriteUInt8s_RAW(tagName, buffer, EIP.DataType_INT);
        }

        public void WriteInt16s(string tagName, short[] values)
        {
            byte[] buffer = new byte[values.Length * Marshal.SizeOf<short>()];
            Buffer.BlockCopy(values, 0, buffer, 0, buffer.Length);
            WriteUInt8s_RAW(tagName, buffer, EIP.DataType_INT);
        }

        void WriteUInt32(string tagName, uint value, ushort dataType)
        {
            var wtagResponse = WriteTag(tagName, (wtagRequest) => {
                byte[] valueBuffer = BitConverter.GetBytes(value);
                byte[] requestData = new byte[4 + valueBuffer.Length];
                using (var ms = new MemoryStream(requestData, true))
                using (var writer = new BinaryWriter(ms)) {
                    writer.Write(dataType);
                    writer.Write((ushort)1);
                    writer.Write(valueBuffer);
                }
                wtagRequest.RequestData = requestData;
            });
            if (wtagResponse.GeneralStatus != 0x00) {
                throw new EIPLogixServiceException(wtagResponse);
            }
        }

        public void WriteUInt32(string tagName, uint value) =>
            WriteUInt32(tagName, value, EIP.DataType_DWORD);

        public void WriteInt32(string tagName, int value) =>
            WriteUInt32(tagName, (uint)value, EIP.DataType_DINT);

        public void WriteUInt32s(string tagName, uint[] values)
        {
            byte[] buffer = new byte[values.Length * Marshal.SizeOf<uint>()];
            Buffer.BlockCopy(values, 0, buffer, 0, buffer.Length);
            WriteUInt8s_RAW(tagName, buffer, EIP.DataType_DWORD);
        }

        public void WriteInt32s(string tagName, int[] values)
        {
            byte[] buffer = new byte[values.Length * Marshal.SizeOf<int>()];
            Buffer.BlockCopy(values, 0, buffer, 0, buffer.Length);
            WriteUInt8s_RAW(tagName, buffer, EIP.DataType_DINT);
        }

        public void WriteSingle(string tagName, float value)
        {
            var wtagResponse = WriteTag(tagName, (wtagRequest) => {
                ushort dataType = EIP.DataType_REAL;
                byte[] valueBuffer = BitConverter.GetBytes(value);
                byte[] requestData = new byte[4 + valueBuffer.Length];
                using (var ms = new MemoryStream(requestData, true))
                using (var writer = new BinaryWriter(ms)) {
                    writer.Write(dataType);
                    writer.Write((ushort)1);
                    writer.Write(valueBuffer);
                }
                wtagRequest.RequestData = requestData;
            });
            if (wtagResponse.GeneralStatus != 0x00) {
                throw new EIPLogixServiceException(wtagResponse);
            }
        }

        public void WriteSingles(string tagName, float[] values)
        {
            byte[] buffer = new byte[values.Length * Marshal.SizeOf<float>()];
            Buffer.BlockCopy(values, 0, buffer, 0, buffer.Length);
            WriteUInt8s_RAW(tagName, buffer, EIP.DataType_REAL);
        }

        public void WriteUInt64(string tagName, ulong value)
        {
            var wtagResponse = WriteTag(tagName, (wtagRequest) => {
                ushort dataType = EIP.DataType_LINT;
                byte[] valueBuffer = BitConverter.GetBytes(value);
                byte[] requestData = new byte[4 + valueBuffer.Length];
                using (var ms = new MemoryStream(requestData, true))
                using (var writer = new BinaryWriter(ms)) {
                    writer.Write(dataType);
                    writer.Write((ushort)1);
                    writer.Write(valueBuffer);
                }
                wtagRequest.RequestData = requestData;
            });
            if (wtagResponse.GeneralStatus != 0x00) {
                throw new EIPLogixServiceException(wtagResponse);
            }
        }

        public void WriteInt64(string tagName, long value) =>
            WriteUInt64(tagName, (ulong)value);

        public void WriteUInt64s(string tagName, ulong[] values)
        {
            byte[] buffer = new byte[values.Length * Marshal.SizeOf<ulong>()];
            Buffer.BlockCopy(values, 0, buffer, 0, buffer.Length);
            WriteUInt8s_RAW(tagName, buffer, EIP.DataType_LINT);
        }

        public void WriteInt64s(string tagName, long[] values)
        {
            byte[] buffer = new byte[values.Length * Marshal.SizeOf<long>()];
            Buffer.BlockCopy(values, 0, buffer, 0, buffer.Length);
            WriteUInt8s_RAW(tagName, buffer, EIP.DataType_LINT);
        }

        public void WriteDouble(string tagName, double value) =>
            WriteInt64(tagName, BitConverter.DoubleToInt64Bits(value));

        public void WriteDoubles(string tagName, double[] values)
        {
            byte[] buffer = new byte[values.Length * Marshal.SizeOf<double>()];
            Buffer.BlockCopy(values, 0, buffer, 0, buffer.Length);
            WriteUInt8s_RAW(tagName, buffer, EIP.DataType_LINT);
        }

        //CIPConnectedServiceResponse WriteStruct_Internal(ushort structHandle, string tagName, byte[] buffer)
        //{
        //    var wtagResponse = WriteTag(tagName, (wtagRequest) => {
        //        ushort dataType = EIP.DataType_Struct;
        //        byte[] valueBuffer = buffer;
        //        byte[] requestData = new byte[6 + valueBuffer.Length];
        //        using (var ms = new MemoryStream(requestData, true))
        //        using (var writer = new BinaryWriter(ms)) {
        //            writer.Write(dataType);
        //            writer.Write(structHandle);
        //            writer.Write((ushort)1);
        //            writer.Write(valueBuffer);
        //        }
        //        wtagRequest.RequestData = requestData;
        //    });
        //    return wtagResponse;
        //}

        CIPConnectedServiceResponse WriteStruct_Internal(ushort structHandle, string tagName, byte[] buffer)
        {
            int bufferOffset = 0;
            int bytesRemaining = buffer.Length;
            byte[] subBuffer = null;

            CIPConnectedServiceResponse wtagResponse = null;
            wtagResponse = WriteTagFragmented(tagName, (wtagRequest, maxDataBytes) => {
                ushort dataType = EIP.DataType_Struct;
                if (bufferOffset == buffer.Length) {
                    return false;
                }
                int frameBytes = Math.Min(maxDataBytes, bytesRemaining);
                if (frameBytes != (subBuffer?.Length ?? 0)) {
                    subBuffer = new byte[frameBytes];
                }
                Buffer.BlockCopy(buffer, bufferOffset, subBuffer, 0, frameBytes);
                byte[] requestData = new byte[10 + subBuffer.Length];
                using (var ms = new MemoryStream(requestData, true))
                using (var writer = new BinaryWriter(ms)) {
                    writer.Write(dataType);
                    writer.Write(structHandle);
                    writer.Write((ushort)1);
                    writer.Write(bufferOffset);
                    writer.Write(subBuffer);
                }
                wtagRequest.RequestData = requestData;
                bufferOffset += frameBytes;
                bytesRemaining = bytesRemaining - frameBytes;
                return true;
            }, false);
            return wtagResponse;
        }

        public void WriteStruct(ushort structHandle, string tagName, byte[] buffer)
        {
            var wtagResponse = WriteStruct_Internal(structHandle, tagName, buffer);
            if (wtagResponse.GeneralStatus != 0x00) {
                throw new EIPLogixServiceException(wtagResponse);
            }
        }

        public void WriteStruct(string tagName, byte[] buffer)
        {
            bool tryAgain = false;
            do {
                ushort structHandle = 0;
                if (!structHandlesDict.TryGetValue(tagName, out structHandle)) {
                    structHandle = QueryStructureHandle(tagName);
                    structHandlesDict.Add(tagName, structHandle);
                }
                var wtagResponse = WriteStruct_Internal(structHandle, tagName, buffer);
                if (0x00 == wtagResponse.GeneralStatus) {
                    return;
                }
                else if (0xFF == wtagResponse.GeneralStatus) {
                    if (wtagResponse.ExtendedStatusSize > 0) {
                        if (0x2107 == wtagResponse.ExtendedStatus[0]) {
                            structHandle = QueryStructureHandle(tagName);
                            if (structHandle != structHandlesDict[tagName]) {
                                structHandlesDict[tagName] = structHandle;
                                tryAgain = true;
                            }
                            else {
                                throw new EIPLogixServiceException(wtagResponse);
                            }
                        }
                        else {
                            throw new EIPLogixServiceException(wtagResponse);
                        }
                    }
                    else {
                        throw new EIPLogixServiceException(wtagResponse);
                    }
                }
                else {
                    throw new EIPLogixServiceException(wtagResponse);
                }
            } while (tryAgain);
        }

        public void WriteStructs(string tagName, IEnumerable<byte[]> buffers)
        {
            int structSize = 0;
            if (0 == buffers.Count()) {
                throw new ArgumentException("Must contain at least one element");
            }
            else {
                foreach (var buffer in buffers) {
                    if (0 == structSize) {
                        structSize = buffer.Length;
                    }
                    else {
                        if (structSize != buffer.Length) {
                            throw new ArgumentException("All structs must be the same size", "buffers");
                        }
                    }
                }
            }

            int i = 0;
            foreach (var buffer in buffers) {
                WriteStruct($"{tagName}[{i}]", buffer);
                i++;
            }
        }

        public void WriteString(string tagName, string value, int maxLength)
        {
            byte[] buffer = LogixString.ToByteArray(maxLength, value);
            WriteStruct(tagName, buffer);
        }

        public void KeepAlive()
        {
            if (!WaitForConnection(TransactionTimeout)) {
                throw new LogixNotConnectedException(targetEP, port, slot);
            }
            try {
                CycleCIPConnection();
            }
            catch (Exception ex) {
                Fault(ex);
                throw ex;
            }
        }


        void ToBufferArray(EIPPacket packet, string msgname)
        {
            byte[] buf = new byte[EIPEncaps.MarshalSize + packet.Encaps.Length];
            using (var ms = new MemoryStream(buf))
            using (BinaryWriter wr = new BinaryWriter(ms))
            {
                wr.Write(packet.Encaps.ToByteArray());
                if (null != packet.Data)
                {
                    wr.Write(packet.Data);
                }
            }
            for (int i = 0; i < buf.Length; i++)
            {
                Console.WriteLine($"{msgname} Byte[{i}] = {buf[i]}");
            }
        }

    }
}
