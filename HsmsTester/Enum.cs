using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester
{
    public enum HsmsInitializeResult
    {
        Success = 0,
        /// <summary>
        /// Config 파일 경로가 설정되지 않음
        /// </summary>
        ConfigPathInvalid = 1,
        /// <summary>
        /// 해당 경로에 파일이 존재하지 않음.
        /// </summary>
        ConfigFileNotExist = 2,
        /// <summary>
        /// 허가되지 않은 파일 형식 사용
        /// </summary>
        NotAllowedFileType = 3,
        /// <summary>
        /// 파일은 있으나 데이터가 없음
        /// </summary>
        DataNotExist = 4,

        /// <summary>
        /// XML형식에서 벗어난 데이터 존재
        /// </summary>
        UnknownData = 5,
        /// <summary>
        /// 지정되지 않은 Config 타입
        /// </summary>
        MismatchConfigArgment = 6,
        /// <summary>
        /// SML파일 경로가 설정되지 않음
        /// </summary>
        SMLPathInvalid = 7,
        /// <summary>
        /// SML 파일이 존재하지 않음.
        /// </summary>
        SMLFileNotExist = 8,

        AssemblyIsEmpty = 9,

        /// <summary>
        /// 프로그램 오류
        /// </summary>
        Exception = 99,
    }

    public enum eSessionSType : byte
    {
        DataMessage = 0x00,
        SelectRequest = 0x01,
        SelectResponse = 0x02,
        DeselectRequest = 0x03,
        DeselectResponse = 0x04,
        LinktestRequest = 0x05,
        LinktestResponse = 0x06,
        RejectRequest = 0x07,
        SeparateRequest = 0x09,
    }

    /// <summary>
    /// Hsms 통신 연결 유형을 정의
    /// </summary>
    public enum SECS_STATE
    {
        UNKNOWN = 0,
        NOT_CONNECTED = 101,
        NOT_SELECTED = 102,
        SELECTED = 103,
        ALMXC_T3 = 203,
        ALMXC_START = 215,
        ALMXC_PROTECTION = 216,
        ALMXC_INVALID_MSG = 217,
        ALMXC_UNKNOWN_DEV_ID = 221,
        ALMXC_UNKNOWN_STREAM = 222,
        ALMXC_UNKNOWN_FUNC = 223,
    }

    public enum eHsmsDataType
    {
        LIST = 0x00,

        BINARY = 0x08,
        BOOL = 0x09,

        ASCII = 0x10,
        JIS8 = 0x11,

        INT1 = 0x19,
        INT2 = 0x1A,
        INT4 = 0x1C,
        INT8 = 0x18,

        UINT1 = 0x29,
        UINT2 = 0x2A,
        UINT4 = 0x2C,
        UINT8 = 0x28,

        FLOAT4 = 0x24,
        FLOAT8 = 0x20,
    }

    public enum S9Message
    {
        SUCCESS = 0,

        S9F1_UnrecognizedDeviceID = 1,
        S9F3_UnrecognizedStreamType = 3,
        S9F5_UnrecognizedFunctionType = 5,
        S9F7_IllegalData = 7,
        S9F9_TransactionTimerTimeout = 9,
        S9F11_DataTooLong = 11,
    }
}
