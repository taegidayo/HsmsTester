using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Util
{
    public class IniFileHandler
    {

        [DllImport("kernel32.dll")]

        private static extern int WritePrivateProfileString(string section, string key, string val, string filepath);

        [DllImport("kernel32.dll")]

        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder reVal, int size, string filepath);


        private static readonly int _iniReadingSize = 65535;

        public static bool WriteIniFileData(string filePath, string section, string key, object val)
        {
            try
            {
                var res = WritePrivateProfileString(section, key, val.ToString(), filePath);
            }
            catch (Exception ex)
            {

                return false;
            }

            return true;
        }


        public static string GetIniFileData(string filePath, string section, string key, string defaultVal)
        {
            StringBuilder result = new StringBuilder(_iniReadingSize);
            try
            {
                int len = GetPrivateProfileString(section, key, defaultVal, result, result.Capacity, filePath);
            }
            catch (Exception e)
            {
                return defaultVal;
            }

            return result.ToString();
        }

        public static int GetIniFileData(string filePath, string section, string key, int defaultVal)
        {
            StringBuilder result = new StringBuilder(_iniReadingSize);
            try
            {
                int len = GetPrivateProfileString(section, key, defaultVal.ToString(), result, result.Capacity, filePath);
            }
            catch (Exception e)
            {
                return defaultVal;
            }

            if (int.TryParse(result.ToString(), out int value) == true)
            {
                return value;
            }

            return defaultVal;
        }

        public static short GetIniFileData(string filePath, string section, string key, short defaultVal)
        {
            StringBuilder result = new StringBuilder(_iniReadingSize);
            try
            {
                int len = GetPrivateProfileString(section, key, defaultVal.ToString(), result, result.Capacity, filePath);
            }
            catch (Exception e)
            {
                return defaultVal;
            }

            if (short.TryParse(result.ToString(), out short value) == true)
            {
                return value;
            }

            return defaultVal;
        }


        public static bool GetIniFileData(string filePath, string section, string key, bool defaultVal)
        {
            StringBuilder result = new StringBuilder(_iniReadingSize);
            try
            {
                int len = GetPrivateProfileString(section, key, defaultVal.ToString(), result, result.Capacity, filePath);
            }
            catch (Exception e)
            {
                return defaultVal;
            }

            if (bool.TryParse(result.ToString(), out bool value) == true)
            {
                return value;
            }

            return defaultVal;
        }


    }
}
