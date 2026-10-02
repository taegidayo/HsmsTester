using HsmsTester.Util;
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
            ConfigPath = configPath;

            _deviceID = IniFileHandler.GetIniFileData(ConfigPath, "Base", "DeviceID", (short)1);
            _host = IniFileHandler.GetIniFileData(ConfigPath, "Base", "Host", false);
            _smlPath = IniFileHandler.GetIniFileData(ConfigPath, "Base", "SML", string.Empty);
            _ipAddress = IniFileHandler.GetIniFileData(ConfigPath, "Base", "IP", "127.0.0.1");
            _port = IniFileHandler.GetIniFileData(ConfigPath, "Base", "Port", 8000);
            _active = IniFileHandler.GetIniFileData(ConfigPath, "Base", "Active", true);
            _useMsgClass = IniFileHandler.GetIniFileData(ConfigPath, "Base", "UseMsgClass", false);
            _t3 = IniFileHandler.GetIniFileData(ConfigPath, "Timeout", "T3", 45);
            _t5 = IniFileHandler.GetIniFileData(ConfigPath, "Timeout", "T5", 10);
            _t6 = IniFileHandler.GetIniFileData(ConfigPath, "Timeout", "T6", 5);
            _t7 = IniFileHandler.GetIniFileData(ConfigPath, "Timeout", "T7", 10);
            _t8 = IniFileHandler.GetIniFileData(ConfigPath, "Timeout", "T8", 5);
            _logUse = IniFileHandler.GetIniFileData(ConfigPath, "LOG", "LogUse", true);
            _loggingEvent = IniFileHandler.GetIniFileData(ConfigPath, "LOG", "LoggingEvent", true);
            _loggingAlarm = IniFileHandler.GetIniFileData(ConfigPath, "LOG", "LoggingAlarm", true);
            _loggingHour = IniFileHandler.GetIniFileData(ConfigPath, "LOG", "LoggingHour", true);
            _logPath = IniFileHandler.GetIniFileData(ConfigPath, "LOG", "LoggingAlarm", $"{Path.GetDirectoryName(ConfigPath)}/LOG");
            _keepDays = IniFileHandler.GetIniFileData(ConfigPath, "LOG", "KeepDays", 30);
            _hsmsConfigType = HsmsConfigType.CFG;
        }  

        public string ConfigPath { get; private set; }
        private HsmsConfigType _hsmsConfigType = HsmsConfigType.JSON;


        // 기존의 Xcom과는 다르 게, Config를 라이브러리 내에서 자주 호출되기 때문에, 성능향상을 위해 GetIniFileData는 한번만 호출되도록 한다..
        private short _deviceID;
        private bool _host;
        private string _smlPath;
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "Base", "DeviceID", value) == true)
                            {
                                _deviceID = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "Base", "Host", value) == true)
                            {
                                _host = value;
                            }
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

        public string SMLPath
        {
            get => _smlPath;
            set
            {
                switch (_hsmsConfigType)
                {
                    case HsmsConfigType.CFG:
                        {
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "Base", "SML", value) == true)
                            {
                                _smlPath = value;
                            }
                        }
                        break;
                    case HsmsConfigType.JSON:
                        {
                            _smlPath = value;
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "Base", "IP", value) == true)
                            {
                                _ipAddress = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "Base", "Port", value) == true)
                            {
                                _port = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "Base", "Active", value) == true)
                            {
                                _active = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "Base", "UseMsgClass", value) == true)
                            {
                                _useMsgClass = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "Timeout", "T3", value) == true)
                            {
                                _t3 = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "Timeout", "T5", value) == true)
                            {
                                _t5 = value;
                            }
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
                if (IniFileHandler.WriteIniFileData(ConfigPath, "Timeout", "T6", value) == true)
                {
                    _t6 = value;
                }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "Timeout", "T7", value) == true)
                            {
                                _t7 = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "Timeout", "T8", value) == true)
                            {
                                _t8 = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "LOG", "LogUse", value) == true)
                            {
                                _logUse = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "LOG", "LoggingEvent", value) == true)
                            {
                                _loggingEvent = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "LOG", "LoggingAlarm", value))
                            {
                                _loggingAlarm = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "LOG", "LoggingHour", value) == true)
                            {
                                _loggingHour = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "LOG", "LoggingAlarm", value) == true)
                            {
                                _logPath = value;
                            }
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
                            if (IniFileHandler.WriteIniFileData(ConfigPath, "LOG", "KeepDays", value) == true)
                            {
                                _keepDays = value;
                            }
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
            SMLPath = $"{Path.GetDirectoryName(ConfigPath)}\\hsms.sml";
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
            LogPath = $"{Path.GetDirectoryName(ConfigPath)}\\LOG";
            KeepDays = 30;
        }
    }
}
