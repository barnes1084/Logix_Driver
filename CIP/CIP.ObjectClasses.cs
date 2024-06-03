using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public static partial class CIP
    {
        public const byte Class_Identity = 0x01;
        public const byte Class_MessageRouter = 0x02;
        public const byte Class_DeviceNet = 0x03;
        public const byte Class_Assembly = 0x04;
        public const byte Class_Connection = 0x05;
        public const byte Class_ConnectionManager = 0x06;
        public const byte Class_Register = 0x07;
        public const byte Class_DiscreteInputPoint = 0x08;
        public const byte Class_DiscreteOutputPoint = 0x09;
        public const byte Class_AnalogInputPoint = 0x0A;
        public const byte Class_AnalogOutputPoint = 0x0B;
        public const byte Class_PresenceSensing = 0x0E;
        public const byte Class_Parameter = 0x0F;
        public const byte Class_ParameterGroup = 0x10;
        public const byte Class_Group = 0x12;
        public const byte Class_DiscreteInputGroup = 0x1D;
        public const byte Class_DiscreteOutputGroup = 0x1E;
        public const byte Class_DiscreteGroup = 0x1F;
        public const byte Class_AnalogInputGroup = 0x20;
        public const byte Class_AnalogOutputGroup = 0x21;
        public const byte Class_AnalogGroup = 0x22;
        public const byte Class_PositionSensor = 0x23;
        public const byte Class_PositionControllerSupervisor = 0x24;
        public const byte Class_PositionController = 0x25;
        public const byte Class_BlockSequencer = 0x26;
        public const byte Class_CommandBlock = 0x27;
        public const byte Class_MotorData = 0x28;
        public const byte Class_ControlSupervisor = 0x29;
        public const byte Class_ACDCDrive = 0x2A;
        public const byte Class_AcknowledgeHandler = 0x2B;
        public const byte Class_Overload = 0x2C;
        public const byte Class_Softstart = 0x2D;
        public const byte Class_Selection = 0x2E;
        public const byte Class_SDeviceSupervisor = 0x30;
        public const byte Class_SAnalogSensor = 0x31;
        public const byte Class_SAnalogActuator = 0x32;
        public const byte Class_SSingleStageController = 0x33;
        public const byte Class_SGasCalibration = 0x34;
        public const byte Class_TripPoint = 0x35;
        //public const byte Class_DriveData = 0x0;
        public const byte Class_File = 0x37;
        public const byte Class_SPartialPressure = 0x38;
        public const byte Class_SafetySupervisor = 0x39;
        public const byte Class_SafetyValidator = 0x3A;
        public const byte Class_SafetyDiscreteOutputPoint = 0x3B;
        public const byte Class_SafetyDiscreteOutputGroup = 0x3C;
        public const byte Class_SafetyDiscreteInputPoint = 0x3D;
        public const byte Class_SafetyDiscreteInputGroup = 0x3E;
        public const byte Class_SafetyDualChannelOutput = 0x3F;
        public const byte Class_SSensorCalibration = 0x40;
        public const byte Class_EventLog = 0x41;
        public const byte Class_MotionAxis = 0x42;
        public const byte Class_TimeSync = 0x43;
        public const byte Class_Modbus = 0x44;
        public const byte Class_ControlNet = 0xF0;
        public const byte Class_ControlNetKepper = 0xF1;
        public const byte Class_ControlNetScheduling = 0xF2;
        public const byte Class_ConnectionConfiguration = 0xF3;
        public const byte Class_Port = 0xF4;
        public const byte Class_TCPIPInterface = 0xF5;
        public const byte Class_EthernetLink = 0xF6;
        public const byte Class_CompoNetLink = 0xF7;
        public const byte Class_CompoNetRepeater = 0xF8;
    }
}
