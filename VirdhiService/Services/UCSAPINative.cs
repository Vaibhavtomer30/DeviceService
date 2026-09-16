using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeviceServices.Services
{
    public class UCSAPINative
    {
        private const string DllName = "UCSAPI40.dll"; // adjust if different

        [StructLayout(LayoutKind.Sequential)]
        public struct UCSAPI_VERSION
        {
            public byte Major;
            public byte Minor;
            public ushort Build;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct UCSAPI_TERMINAL_INFO
        {
            public uint TerminalID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] TerminalIP;
            public byte TerminalStatus;
            public byte DoorStatus;
            public byte CoverStatus;
            public byte LockStatus;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] ExtSignal;
            public UCSAPI_VERSION Firmware;
            public UCSAPI_VERSION Protocol;
            public UCSAPI_VERSION CardReader;
            public ushort ModelNo;
            public byte TerminalType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] MacAddr;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int UCSAPI_GetTerminalInfo(
            uint TerminalID,
            out UCSAPI_TERMINAL_INFO pInfo
        );
    }
}
