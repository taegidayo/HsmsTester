using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Hsms.Struct
{
    public class HsmsHeaderMessage
    {
        public readonly int HEADER_LENGTH = 14;

        public uint Length = 0;

        public short DeviceID = 0;

        /// <summary>
        /// ControlMessage : 0 or Status
        /// Data Message : Stream
        /// </summary>
        public byte HeaderByte2 = 0;

        /// <summary>
        /// ControlMessage : 0 or Status
        /// Data Message : Function
        /// </summary>
        public byte HeaderByte3 = 0;

        public byte Stream = 0;
        public byte Function = 0;

        public string StreamFunction => string.Format("S{0}F{1}", this.Stream, this.Function);

        public byte Ptype = 0;

        /// <summary>
        /// 
        /// </summary>
        public byte Stype = 0;

        public uint SystemByte = 0;

        /// <summary>
        /// DataMessage에서 응답을 요구하는 메시지인 경우 true가 된다.
        /// </summary>
        public bool wBit = false;

        public string FullName = string.Empty;

        // 받은 메시지에서 앞의 데이터를 헤더정보로 변환한다.
        public void ParsingRecvToHeader(byte[] recvMsg)
        {
            if (BitConverter.IsLittleEndian == false)
            {
                // 리틀 엔디언 시스템이면 그대로 사용
                Length = BitConverter.ToUInt32(recvMsg, 0);
            }
            else
            {
                // 빅 엔디언 → 리틀 엔디언으로 변환
                byte[] temp = new byte[4];
                Array.Copy(recvMsg, 0, temp, 0, 4);
                Array.Reverse(temp);
                Length = BitConverter.ToUInt32(temp, 0);
            }

            if (BitConverter.IsLittleEndian == false)
            {
                // 리틀 엔디언이면 그대로 사용
                DeviceID = BitConverter.ToInt16(recvMsg, 4);
            }
            else
            {
                // 빅 엔디언 → 리틀 엔디언으로 변환
                byte[] temp = new byte[2];
                Array.Copy(recvMsg, 4, temp, 0, 2);
                Array.Reverse(temp);
                DeviceID = BitConverter.ToInt16(temp, 0);
            }

            HeaderByte2 = recvMsg[6];
            HeaderByte3 = recvMsg[7];

            Ptype = recvMsg[8];
            Stype = recvMsg[9];

            if (Stype == 0)
            {
                Stream = (byte)(HeaderByte2 & 0x7F);
                Function = HeaderByte3;
                wBit = (HeaderByte2 & 0x80) != 0; // HeaderByte2 에서 첫번째 비트는 wBit로, 1인경우 응답이 필요하다.
            }

            if (BitConverter.IsLittleEndian == false)
            {
                // 리틀 엔디언이면 그대로 사용
                SystemByte = (uint)BitConverter.ToInt32(recvMsg, 10);
            }
            else
            {
                // 빅 엔디언 → 리틀 엔디언으로 변환
                byte[] temp = new byte[4];
                Array.Copy(recvMsg, 10, temp, 0, 4);
                Array.Reverse(temp);
                SystemByte = (uint)BitConverter.ToInt32(temp, 0);
            }

            byte[] sysByte = BitConverter.GetBytes(SystemByte);
        }

        public byte[] GetFullHeaderByte()
        {
            byte[] header = new byte[14];

            byte[] length = BitConverter.GetBytes(Length);

            // 리틀 엔디언 시스템이면, 그대로 사용 (필요시 Reverse)
            if (BitConverter.IsLittleEndian == false)
            {
                Array.Copy(length, 0, header, 0, 4);
            }
            else
            {
                // 엔디언 순서 반대로 해서 복사
                Array.Reverse(length);
                Array.Copy(length, 0, header, 0, 4);
            }

            byte[] sessionID = BitConverter.GetBytes(DeviceID);

            // 리틀 엔디언 시스템이면, 그대로 사용 (필요시 Reverse)
            if (BitConverter.IsLittleEndian == false)
            {
                Array.Copy(sessionID, 0, header, 4, 2);
            }
            else
            {
                // 엔디언 순서 반대로 해서 복사
                Array.Reverse(sessionID);
                Array.Copy(sessionID, 0, header, 4, 2);
            }

            header[6] = HeaderByte2;
            header[7] = HeaderByte3;
            header[8] = Ptype;
            header[9] = Stype;

            byte[] sysByte = BitConverter.GetBytes(SystemByte);

            // 리틀 엔디언 시스템이면, 그대로 사용 (필요시 Reverse)
            if (BitConverter.IsLittleEndian == false)
            {
                Array.Copy(sysByte, 0, header, 10, 4);
            }
            else
            {
                // 엔디언 순서 반대로 해서 복사
                Array.Reverse(sysByte);
                Array.Copy(sysByte, 0, header, 10, 4);
            }

            return header;
        }

        /// <summary>
        /// S9계열 메시지를 보내기 위해 사용하는 함수, 앞의 Length를 제거하고 보낸다.
        /// </summary>
        /// <returns></returns>
        public byte[] GetOnlyHeaderByte()
        {
            byte[] header = new byte[10];

            byte[] sessionID = BitConverter.GetBytes(DeviceID);

            // 리틀 엔디언 시스템이면, 그대로 사용 (필요시 Reverse)
            if (BitConverter.IsLittleEndian == false)
            {
                Array.Copy(sessionID, 0, header, 0, 2);
            }
            else
            {
                // 엔디언 순서 반대로 해서 복사
                Array.Reverse(sessionID);
                Array.Copy(sessionID, 0, header, 0, 2);
            }

            header[2] = HeaderByte2;
            header[3] = HeaderByte3;
            header[4] = Ptype;
            header[5] = Stype;

            byte[] sysByte = BitConverter.GetBytes(SystemByte);

            // 리틀 엔디언 시스템이면, 그대로 사용 (필요시 Reverse)
            if (BitConverter.IsLittleEndian == false)
            {
                Array.Copy(sysByte, 0, header, 6, 4);
            }
            else
            {
                // 엔디언 순서 반대로 해서 복사
                Array.Reverse(sysByte);
                Array.Copy(sysByte, 0, header, 6, 4);
            }

            return header;
        }

    }
}
