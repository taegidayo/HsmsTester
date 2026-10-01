using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Hsms.Define
{
    public class HsmsConfiguration
    {
        private enum HsmsConfigType
        {
            CFG,
            JSON
        }

        // 파일을 사용하는 경우(Client만 가능)
        internal HsmsConfiguration(string configPath)
        {
            _hsmsConfigType = HsmsConfigType.CFG;
        }

        private HsmsConfigType _hsmsConfigType = HsmsConfigType.JSON;

        // 기존의 Xcom과는 다르 게, Config를 라이브러리 내에서 자주 호출되기 때문에, 성능향상을 위해 GetIniFileData는 한번만 호출되도록 한다..
        private short _deviceID;
        private bool _host;
        private string _ipAddress;
        private int _port;
        private bool _active;
        private bool _useMsgClass;
        private int _t3;
        private int _t5;
        private int _t6;
        private int _t7;
        private int _t8;
        private bool _logUse;
        private bool _loggingEvent;
        private bool _loggingAlarm;
        private bool _loggingHour;
        private string _logPath;
        private int _keepDays;

        public short DeviceID
        {
            get => _deviceID;
            set
            {

                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _deviceID = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _deviceID = value;
                        }
                        break;
                }
            }
        }

        public bool Host
        {
            get => _host;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _host = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _host = value;
                        }
                        break;
                }
            }
        }

        public string IPAddress
        {
            get => _ipAddress;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _ipAddress = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _ipAddress = value;
                        }
                        break;
                }
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _port = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _port = value;
                        }
                        break;
                }
            }
        }

        public bool Active
        {
            get => _active;
            set
            {

                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _active = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _active = value;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 클래스를 사용하여, 해당 클래스에 메시지를 할당할 것인지, HsmsDataMessageObject를 사용해서 따로 클래스 생성 없이 사용할 것인지에 대한 플래그
        /// </summary>
        public bool UseMsgClass
        {
            get => _useMsgClass;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _useMsgClass = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _useMsgClass = value;
                        }
                        break;
                }
            }
        }

        #region Timeout
        /// <summary>
        /// Primary message를 보낸 후, Response message(Secondary message)가 도착할 때까지 기다리는 시간.
        /// </summary>
        public int T3
        {
            get => _t3;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _t3 = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _t3 = value;
                        }
                        break;
                }
            }
        }
        /// <summary>
        /// LinkTest Request(S9F1) 메시지를 전송한 후, LinkTest Response(S9F3)를 기다리는 시간.
        /// </summary>
        public int T5
        {
            get => _t5;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _t5 = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _t5 = value;
                        }
                        break;
                }
            }
        }
        /// <summary>
        /// Select.req 메시지를 보낸 후, Select.rsp 응답을 기다리는 시간.
        /// </summary>
        public int T6
        {
            get => _t6;
            set
            {
                _t6 = value;
            }
        }
        /// <summary>
        /// 연결이 끊긴 후, 다시 연결을 시도하기 전까지의 대기 시간.
        /// </summary>
        public int T7
        {
            get => _t7;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _t7 = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _t7 = value;
                        }
                        break;
                }
            }
        }
        /// <summary>
        /// TCP 연결 이후, HSMS-level 연결 (Select.req/rsp 완료 포함)을 완료하는 데 걸리는 최대 시간.
        /// </summary>
        public int T8
        {
            get => _t8;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _t8 = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _t8 = value;
                        }
                        break;
                }
            }
        }

        #endregion

        #region Logging

        public bool LogUse
        {
            get => _logUse;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _logUse = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _logUse = value;
                        }
                        break;
                }
            }
        }

        public bool LoggingEvent
        {
            get => _loggingEvent;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _loggingEvent = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _loggingEvent = value;
                        }
                        break;
                }
            }
        }

        public bool LoggingAlarm
        {
            get => _loggingAlarm;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _loggingAlarm = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _loggingAlarm = value;
                        }
                        break;
                }
            }
        }

        public bool LoggingHour
        {
            get => _loggingHour;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _loggingHour = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _loggingHour = value;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 기본값: Cfg파일 위치
        /// </summary>
        public string LogPath
        {
            get => _logPath;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _logPath = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _logPath = value;
                        }
                        break;
                }
            }
        }

        public int KeepDays
        {
            get => _keepDays;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            _keepDays = value;
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _keepDays = value;
                        }
                        break;
                }
            }
        }
        #endregion

        public void SetDefaultConfig()
        {
            DeviceID = 1;
            Host = false;
            IPAddress = "127.0.0.1";
            Port = 8000;
            Active = false;
            UseMsgClass = false;
            T3 = 45;
            T5 = 10;
            T6 = 5;
            T7 = 10;
            T8 = 5;
            LogUse = true;
            LoggingEvent = true;
            LoggingAlarm = true;
            LoggingHour = true;
            KeepDays = 30;
        }
    }
}

