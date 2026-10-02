using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Hsms.Struct
{
    internal class HsmsDataMessageObject : IHsmsModel
    {
        private HsmsHeaderMessage _msgHeader = new HsmsHeaderMessage();      // Data Message Header

        public HsmsObjectVariable Data = null;

        public HsmsHeaderMessage Header { get { return _msgHeader; } private set { _msgHeader = value; } }

        internal HsmsDataMessageObject()
        {
        }

        internal bool ParsingHsmsDataMessage(byte[] msg, HsmsHeaderMessage header)
        {
            Header = header;
            Stream = header.Stream;
            Function = header.Function;

            if (Header.Length >= 10)
            {

                byte[] dataMsg = new byte[msg.Length - header.HEADER_LENGTH];
                Array.Copy(msg, header.HEADER_LENGTH, dataMsg, 0, dataMsg.Length);
                var parsingData = ParsingHsmsObjectVariable(ref dataMsg);

                if ((Stream == 1 && Function == 1) == false && parsingData == null) return false;

                Data = parsingData;
            }
            return true;
        }

        public bool SetValue(string name, object value)
        {
            name = name.Trim();
            return Data.SetValue(name, value);
        }

        public T GetValue<T>(string name)
        {
            try
            {
                var rtnType = typeof(T);
                var data = Data.FindByName(name.Trim());
                if (data == null)
                {
                    throw new Exception($"{name} is Not Exist");
                }

                return (T)(object)data.Value;
            }

            catch (Exception ex)
            {
                throw ex;
            }
        }

        public List<HsmsObjectVariable> GetRepeatValue(string name)
        {
            var data = Data.FindByName(name);

            if (data == null) return null;

            else
            {
                return (List<HsmsObjectVariable>)data.Value;
            }

        }

        ///// <summary>
        ///// List N
        ///// </summary>
        ///// <param name="name"></param>
        ///// <returns></returns>
        //public HsmsObjectVariable GetRepeatHsmsVariable_Empty(string name)
        //{
        //    var msg = HsmsManager.Instance._reader.List.FirstOrDefault(t => t.Stream == this.Stream && t.Function == this.Function && t.FullName == this.FullName);

        //    var value = msg.Item.SubClassList().FirstOrDefault(t => t.Name == name);

        //    if (value?.SubItem.Count == 1)
        //    {
        //        return value.SubItem.First();
        //    }
        //    else
        //    {
        //        return null;
        //    }

        //}

        internal bool SetHeaderData(HsmsJson json)
        {
            this.Stream = json.Stream;
            this.Function = json.Function;
            this.Name = json.Name;
            this.SubName = json.SubName;
            this.FullName = json.FullName;
            Header.FullName = json.FullName;
            Header.Stream = (byte)json.Stream;
            Header.Function = (byte)json.Function;
            Header.HeaderByte2 = (byte)json.Stream;
            Header.HeaderByte3 = (byte)json.Function;
            return true;
        }

        //internal bool SetVariableNames(HsmsItem msg)
        //{
        //    return Data.SetVariableNames(msg);
        //}

        // 전달받은 메시지를 변수형태로 변경시킨다.
        internal HsmsObjectVariable ParsingHsmsObjectVariable(ref byte[] msg)
        {
            if (msg == null || msg.Length < 0) return null;
            try
            {
                int nowIndex = 0;
                int type = msg[nowIndex] >> 2;
                int length = msg[nowIndex] & 0x03; // 하위 2비트 사용

                nowIndex++;

                byte[] tempByte = new byte[length];
                for (int index = 0; index < length; index++)
                {
                    tempByte[index] = msg[nowIndex++];
                }
                byte[] padded = new byte[4];
                Array.Copy(tempByte, 0, padded, 4 - tempByte.Length, tempByte.Length);

                if (BitConverter.IsLittleEndian == true)
                {
                    Array.Reverse(padded);
                }

                int dataLength = BitConverter.ToInt32(padded, 0);

                switch (type)
                {
                    case (int)eHsmsDataType.LIST:
                        {
                            List<HsmsObjectVariable> value = new List<HsmsObjectVariable>();
                            byte[] temp = new byte[msg.Length - nowIndex];
                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            for (int index = 0; index < dataLength; index++)
                            {
                                var subData = ParsingHsmsObjectVariable(ref temp);
                                if (subData == null)
                                {
                                    return null;
                                }
                                value.Add(subData);
                            }

                            msg = temp;

                            return new HsmsObjectVariable(value);
                        }
                    case (int)eHsmsDataType.BINARY:
                        {
                            byte[] value = new byte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            return new HsmsObjectVariable(value, true);
                        }
                    case (int)eHsmsDataType.BOOL:
                        {
                            byte[] value = new byte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);
                            bool[] data = value.Select(b => b != 0x00).ToArray();

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            return new HsmsObjectVariable(data);
                        }
                    case (int)eHsmsDataType.ASCII:
                        {
                            byte[] value = new byte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            string data = System.Text.Encoding.ASCII.GetString(value);
                            return new HsmsObjectVariable(data);
                        }
                    case (int)eHsmsDataType.INT1:
                        {
                            sbyte[] value = new sbyte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);
                            sbyte[] data = new sbyte[dataLength];
                            for (int index = 0; index < dataLength; index++)
                            {
                                data[index] = unchecked((sbyte)value[index]);
                            }

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            return new HsmsObjectVariable(data);
                        }
                    case (int)eHsmsDataType.INT2:
                        {
                            byte[] value = new byte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);
                            short[] data = new short[dataLength / 2];
                            for (int index = 0; index < dataLength; index++)
                            {
                                data[index] = (short)((value[index * 2] << 8) | value[index * 2 + 1]);
                            }

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            return new HsmsObjectVariable(data);
                        }
                    case (int)eHsmsDataType.INT4:
                        {
                            byte[] value = new byte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);
                            int[] data = new int[dataLength / 4];
                            for (int index = 0; index < dataLength; index++)
                            {
                                int offset = index * 4;
                                data[index] = (int)((value[offset] << 24) | (value[offset + 1] << 16) | (value[offset + 2] << 8) | (value[offset + 3]));
                            }

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            return new HsmsObjectVariable(data);
                        }

                    case (int)eHsmsDataType.INT8:
                        {
                            byte[] value = new byte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);
                            long[] data = new long[dataLength / 8];
                            for (int index = 0; index < dataLength; index++)
                            {
                                int offset = index * 8;
                                data[index] = (long)((value[offset] << 56) | (value[offset + 1] << 48) | (value[offset + 2] << 40) | (value[offset + 3] << 32) | (value[offset + 4] << 24) | (value[offset + 5] << 16) | (value[offset + 6] << 8) | (value[offset + 7]));
                            }

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            return new HsmsObjectVariable(data);
                        }
                    case (int)eHsmsDataType.UINT1:
                        {
                            byte[] value = new byte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            return new HsmsObjectVariable(value);
                        }
                    case (int)eHsmsDataType.UINT2:
                        {
                            byte[] value = new byte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);
                            ushort[] data = new ushort[dataLength / 2];
                            for (int index = 0; index < dataLength / 2; index++)
                            {
                                data[index] = (ushort)((value[index * 2] << 8) | value[index * 2 + 1]);
                            }

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            return new HsmsObjectVariable(data);
                        }
                    case (int)eHsmsDataType.UINT4:
                        {
                            byte[] value = new byte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);
                            uint[] data = new uint[dataLength / 4];
                            for (int index = 0; index < dataLength / 4; index++)
                            {
                                int offset = index * 4;
                                data[index] = (uint)((value[offset] << 24) | (value[offset + 1] << 16) | (value[offset + 2] << 8) | (value[offset + 3]));
                            }

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            return new HsmsObjectVariable(data);
                        }
                    case (int)eHsmsDataType.UINT8:
                        {
                            byte[] value = new byte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);
                            ulong[] data = new ulong[dataLength / 8];
                            for (int index = 0; index < dataLength / 8; index++)
                            {
                                int offset = index * 8;
                                data[index] = ((ulong)value[offset] << 56) | ((ulong)value[offset + 1] << 48) | ((ulong)value[offset + 2] << 40) | ((ulong)value[offset + 3] << 32) | ((ulong)value[offset + 4] << 24) | ((ulong)value[offset + 5] << 16) | ((ulong)value[offset + 6] << 8) | ((ulong)value[offset + 7]);
                            }

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            return new HsmsObjectVariable(data);
                        }
                    case (int)eHsmsDataType.FLOAT4:
                        {
                            byte[] value = new byte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);
                            float[] data = new float[dataLength / 4];
                            for (int index = 0; index < dataLength / 4; index++)
                            {
                                byte[] reversed = new byte[4];
                                reversed[0] = value[index * 4 + 3];
                                reversed[1] = value[index * 4 + 2];
                                reversed[2] = value[index * 4 + 1];
                                reversed[3] = value[index * 4];

                                data[index] = BitConverter.ToSingle(reversed, 0);
                            }

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            return new HsmsObjectVariable(data);
                        }
                    case (int)eHsmsDataType.FLOAT8:
                        {
                            byte[] value = new byte[dataLength];
                            Array.Copy(msg, nowIndex, value, 0, dataLength);
                            double[] data = new double[dataLength / 8];
                            for (int index = 0; index < dataLength / 8; index++)
                            {
                                byte[] reversed = new byte[8];

                                reversed[0] = value[index * 8 + 7];
                                reversed[1] = value[index * 8 + 6];
                                reversed[2] = value[index * 8 + 5];
                                reversed[3] = value[index * 8 + 4];
                                reversed[4] = value[index * 8 + 3];
                                reversed[5] = value[index * 8 + 2];
                                reversed[6] = value[index * 8 + 1];
                                reversed[7] = value[index * 8];

                                data[index] = BitConverter.ToDouble(reversed, 0);
                            }

                            nowIndex += dataLength;
                            byte[] temp = new byte[msg.Length - nowIndex];

                            Array.Copy(msg, nowIndex, temp, 0, msg.Length - nowIndex);

                            msg = temp;

                            return new HsmsObjectVariable(data);
                        }
                }
            }
            catch (Exception ex)
            {

                return null;

            }

            return null;
        }

    }

}
