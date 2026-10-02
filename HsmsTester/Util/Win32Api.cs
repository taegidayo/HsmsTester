using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Util
{
    public static class Win32Api
    {
        #region Windows DateTime

        private const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
        private const int FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;

        /// <summary>
        /// 에러메시지에 대해 출력하고자 하는 언어 타입
        /// </summary>
        public enum LanguageType
        {
            /// <summary>
            /// 시스템에서 설정한 기본 언어값
            /// </summary>
            DEFAULT = 0,
            KOREAN = 0x0412,
            ENGLISH = 0x0409,
            /// <summary>
            /// 간체자(중국본토)
            /// </summary>
            SIMPLIFIED_CHINESE = 0x0804,
            /// <summary>
            /// 번체자(대만/홍콩)
            /// </summary>
            TRADITIONAL_CHINESE = 0x0404,
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int FormatMessage(int flags, IntPtr source, int messageId, int languageId, StringBuilder buffer, int size, IntPtr arguments);

        [StructLayout(LayoutKind.Sequential)]
        private struct stTime
        {
            internal short Year;
            internal short Month;
            internal short DayOfWeek;
            internal short Day;
            internal short Hour;
            internal short Minute;
            internal short Second;
            internal short Millisecond;
        }

        #endregion

        #region Windows Get TickCount

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();                           // Use C++ Method

        public static ulong TickCount64 => GetTickCount64();

        // CompareTick에 비교하고자 하는 현재 TickCount를 입력 하여 현재 TickCount와의 차이를 리턴 [ ms ]
        public static ulong GetDuration_ms(ulong compareTick)
        {
            ulong ulCurrnet = GetTickCount64();
            ulong ulDiff = ulCurrnet - compareTick;

            return ulDiff;
        }

        // CompareTick에 비교하고자 하는 현재 TickCount를 입력 하여 현재 TickCount와의 차이를 리턴 [ sec ]
        public static ulong GetDuration_sec(ulong compareTick)
        {
            ulong ulCurrnet = GetTickCount64();
            ulong ulDiff = (ulCurrnet - compareTick) / 1000;

            return ulDiff;
        }

        // GetTickCount64() 로 부터 얻은 시간을 Date Time으로 변경한다. 
        public static DateTime ToDateTime(this ulong tickCount)
        {
            // "dd.hh:mm:ss" 형식으로 반환
            //TimeSpan tspan = TimeSpan.FromMilliseconds(tickCount);
            //string strTime = string.Format("{0:%d}.{0:hh}:{0:mm}:{0:ss}:{0:fff}", tspan);

            return DateTime.MinValue.AddMilliseconds(tickCount);
        }
        #endregion
    }
}
