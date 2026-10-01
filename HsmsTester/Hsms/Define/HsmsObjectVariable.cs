using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Hsms.Struct
{
    //// 유동적으로 사용되는 메시지 구조
    public class HsmsObjectVariable
    {
        public string Name { get; private set; }

        public eHsmsDataType Type { get; private set; }

        public int Length { get; private set; }

        public object Value { get; private set; }

        public List<HsmsObjectVariable> SubItems = null;

        public bool IsRepeat { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetValue(string name, object value)
        {
            if (this.Name == name)
            {
                switch (Type)
                {
                    case eHsmsDataType.LIST:
                        {
                            // 리스트는 SetValue로 설정할 수 없다.
                            return false;
                        }
                    case eHsmsDataType.BINARY:
                    case eHsmsDataType.UINT1:
                        {
                            if (value is sbyte)
                            {
                                Value = value;
                                return true;
                            }
                            else if (value is sbyte[] list)
                            {
                                Value = value;
                                return true;
                            }
                        }
                        break;
                    case eHsmsDataType.BOOL:
                        {
                            if (value is bool || value is bool[])
                            {
                                Value = value;
                                return true;
                            }
                        }
                        break;
                    case eHsmsDataType.ASCII:
                        {
                            if (value is string str)
                            {
                                str = str.Length > Length ? str.Substring(0, Length) : str;
                                Value = str;
                                return true;
                            }
                        }
                        break;
                    case eHsmsDataType.UINT2:
                        {
                            if (value is ushort || value is ushort[])
                            {
                                Value = value;
                                return true;
                            }
                        }
                        break;
                    case eHsmsDataType.UINT4:
                        {
                            if (value is uint || value is uint[])
                            {
                                Value = value;
                                return true;
                            }
                        }
                        break;
                    case eHsmsDataType.UINT8:
                        {
                            if (value is ulong || value is ulong[])
                            {
                                Value = value;
                                return true;
                            }
                        }
                        break;
                    case eHsmsDataType.INT1:
                        {
                            if (value is byte || value is byte[])
                            {
                                Value = value;
                                return true;
                            }
                        }
                        break;
                    case eHsmsDataType.INT2:
                        {
                            if (value is short || value is short[])
                            {
                                Value = value;
                                return true;
                            }
                        }
                        break;
                    case eHsmsDataType.INT4:
                        {
                            if (value is int || value is int[])
                            {
                                Value = value;
                                return true;
                            }
                        }
                        break;
                    case eHsmsDataType.INT8:
                        {
                            if (value is long || value is long[])
                            {
                                Value = value;
                                return true;
                            }
                        }
                        break;
                    case eHsmsDataType.FLOAT4:
                        {
                            if (value is float || value is float[])
                            {
                                Value = value;
                                return true;
                            }
                        }
                        break;
                    case eHsmsDataType.FLOAT8:
                        {
                            if (value is double || value is double[])
                            {
                                Value = value;
                                return true;
                            }
                        }
                        break;

                }
                return false;
            }
            else if (this.Type == eHsmsDataType.LIST)
            {
                if (Value is List<HsmsObjectVariable> list)
                {
                    foreach (var variable in list)
                    {
                        if (variable.SetValue(name, value) == true)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public T GetValue<T>(string name)
        {
            try
            {
                var rtnType = typeof(T);
                var data = FindByName(name.Trim());
                if (data == null)
                {
                    throw new Exception($"{name} is Not Exist");
                }

                return (T)Convert.ChangeType(data.Value, typeof(T));
            }

            catch (Exception ex)
            {
                throw ex;
            }
        }

        #region 데이터 생성자

        private HsmsObjectVariable()
        {

        }

        /// <summary>
        /// HSMS BINARY || U1 메시지 
        /// </summary>
        /// <param name="isBinary">바이너리일경우 True로 사용</param>
        public HsmsObjectVariable(byte[] value, bool isBinary = false)
        {
            Type = isBinary == true ? eHsmsDataType.BINARY : eHsmsDataType.UINT1;
            Length = value.Length;
            Value = value;
        }

        /// <summary>
        /// HSMS BOOL 메시지
        /// </summary>
        public HsmsObjectVariable(params bool[] value)
        {
            Type = eHsmsDataType.BOOL;
            Length = value.Length;
            //길이가 한개일 경우 List로 만들지 않고 단일 변수로 만들기 위함.
            if (Length == 1)
            {
                Value = value.First();
            }
            else
            {
                Value = value;
            }
        }

        /// <summary>
        /// HSMS ASCII 메시지
        /// </summary>
        public HsmsObjectVariable(string value)
        {
            Type = eHsmsDataType.ASCII;
            Length = value.Length;
            //길이가 한개일 경우 List로 만들지 않고 단일 변수로 만들기 위함.
            if (Length == 1)
            {
                Value = value.First();
            }
            else
            {
                Value = value;
            }
        }

        /// <summary>
        /// HSMS I1 메시지
        /// </summary>
        public HsmsObjectVariable(params sbyte[] value)
        {
            Type = eHsmsDataType.INT1;
            Length = value.Length;
            //길이가 한개일 경우 List로 만들지 않고 단일 변수로 만들기 위함.
            if (Length == 1)
            {
                Value = value.First();
            }
            else
            {
                Value = value;
            }
        }

        /// <summary>
        /// HSMS I2메시지
        /// </summary>
        public HsmsObjectVariable(params short[] value)
        {
            Type = eHsmsDataType.INT2;
            Length = value.Length;
            //길이가 한개일 경우 List로 만들지 않고 단일 변수로 만들기 위함.
            if (Length == 1)
            {
                Value = value.First();
            }
            else
            {
                Value = value;
            }
        }

        /// <summary>
        /// HSMS I4 메시지
        /// </summary>
        public HsmsObjectVariable(params int[] value)
        {
            Type = eHsmsDataType.INT4;
            Length = value.Length;
            //길이가 한개일 경우 List로 만들지 않고 단일 변수로 만들기 위함.
            if (Length == 1)
            {
                Value = value.First();
            }
            else
            {
                Value = value;
            }
        }

        /// <summary>
        /// HSMS I8 메시지
        /// </summary>
        public HsmsObjectVariable(params long[] value)
        {
            Type = eHsmsDataType.INT8;
            Length = value.Length;
            //길이가 한개일 경우 List로 만들지 않고 단일 변수로 만들기 위함.
            if (Length == 1)
            {
                Value = value.First();
            }
            else
            {
                Value = value;
            }
        }

        /// <summary>
        /// HSMS U2 메시지
        /// </summary>
        public HsmsObjectVariable(params ushort[] value)
        {
            Type = eHsmsDataType.UINT2;
            Length = value.Length;
            //길이가 한개일 경우 List로 만들지 않고 단일 변수로 만들기 위함.
            if (Length == 1)
            {
                Value = value.First();
            }
            else
            {
                Value = value;
            }
        }

        /// <summary>
        /// HSMS U4 메시지
        /// </summary>
        public HsmsObjectVariable(params uint[] value)
        {
            Type = eHsmsDataType.UINT4;
            Length = value.Length;
            //길이가 한개일 경우 List로 만들지 않고 단일 변수로 만들기 위함.
            if (Length == 1)
            {
                Value = value.First();
            }
            else
            {
                Value = value;
            }
        }

        /// <summary>
        /// HSMS U8 메시지
        /// </summary>
        public HsmsObjectVariable(params ulong[] value)
        {
            Type = eHsmsDataType.UINT8;
            Length = value.Length;
            //길이가 한개일 경우 List로 만들지 않고 단일 변수로 만들기 위함.
            if (Length == 1)
            {
                Value = value.First();
            }
            else
            {
                Value = value;
            }
        }

        /// <summary>
        /// HSMS F4 메시지
        /// </summary>
        public HsmsObjectVariable(params float[] value)
        {
            Type = eHsmsDataType.FLOAT4;
            Length = value.Length;
            //길이가 한개일 경우 List로 만들지 않고 단일 변수로 만들기 위함.
            if (Length == 1)
            {
                Value = value.First();
            }
            else
            {
                Value = value;
            }
        }

        /// <summary>
        /// HSMS F8 메시지
        /// </summary>
        public HsmsObjectVariable(params double[] value)
        {
            Type = eHsmsDataType.LIST;
            Length = value.Length;
            //길이가 한개일 경우 List로 만들지 않고 단일 변수로 만들기 위함.
            if (Length == 1)
            {
                Value = value.First();
            }
            else
            {
                Value = value;
            }
        }

        /// <summary>
        /// HSMS LIST 메시지
        /// </summary>
        public HsmsObjectVariable(List<HsmsObjectVariable> value, string name = "")
        {
            Type = eHsmsDataType.LIST;
            Name = name;
            Length = value.Count;
            if (value.Count == 0) IsRepeat = true;
            Value = value;
            SubItems = value;

        }

        /// <summary>
        /// HSMS LIST (N) 메시지
        /// </summary>
        public HsmsObjectVariable(eHsmsDataType type, int length, string name)
        {
            Type = type;
            Length = length;
            Name = name;
            switch (type)
            {
                case eHsmsDataType.LIST:
                    {
                        Value = new List<HsmsObjectVariable>();
                        IsRepeat = true;
                    }
                    break;
                case eHsmsDataType.BINARY:
                case eHsmsDataType.UINT1:
                    {
                        Value = new byte[length];
                    }
                    break;
                case eHsmsDataType.BOOL:
                    {
                        if (length == 1)
                        {
                            Value = false;
                        }
                        else
                        {
                            Value = new bool[length];
                        }
                    }
                    break;
                case eHsmsDataType.ASCII:
                    {
                        Value = string.Empty;
                    }
                    break;
                case eHsmsDataType.INT1:
                    {
                        if (length == 1)
                        {
                            Value = (sbyte)0;
                        }
                        else
                        {
                            Value = new byte[length];
                        }
                    }
                    break;
                case eHsmsDataType.INT2:
                    {
                        if (length == 1)
                        {
                            Value = (short)0;
                        }
                        else
                        {
                            Value = new short[length];
                        }
                    }
                    break;
                case eHsmsDataType.INT4:
                    {
                        if (length == 1)
                        {
                            Value = (int)0;
                        }
                        else
                        {
                            Value = new int[length];
                        }
                    }
                    break;
                case eHsmsDataType.INT8:
                    {
                        if (length == 1)
                        {
                            Value = (long)0;
                        }
                        else
                        {
                            Value = new long[length];
                        }
                    }
                    break;
                case eHsmsDataType.UINT2:
                    {
                        if (length == 1)
                        {
                            Value = (ushort)0;
                        }
                        else
                        {
                            Value = new ushort[length];
                        }
                    }
                    break;
                case eHsmsDataType.UINT4:
                    {
                        if (length == 1)
                        {
                            Value = (uint)0;
                        }
                        else
                        {
                            Value = new uint[length];
                        }
                    }
                    break;
                case eHsmsDataType.UINT8:
                    {
                        if (length == 1)
                        {
                            Value = (ulong)0;
                        }
                        else
                        {
                            Value = new ulong[length];
                        }
                    }
                    break;
                case eHsmsDataType.FLOAT4:
                    {
                        if (length == 1)
                        {
                            Value = (float)0;
                        }
                        else
                        {
                            Value = new float[length];
                        }
                    }
                    break;
                case eHsmsDataType.FLOAT8:
                    {
                        if (length == 1)
                        {
                            Value = (double)0;
                        }
                        else
                        {
                            Value = new double[length];
                        }
                    }
                    break;
            }

        }

        #endregion 데이터 생성자

        //internal bool SetVariableNames(HsmsItem msg)
        //{
        //    try
        //    {
        //        if (this.Type != msg.Type) return false;

        //        Name = msg.Name;

        //        if (this.Type == eHsmsDataType.LIST)
        //        {
        //            if (Value is List<HsmsObjectVariable> listValue)
        //            {
        //                for (int index = 0; index < Length; index++)
        //                {

        //                    if (msg.SubItem.Count == 1)
        //                    {
        //                        if (listValue[index].SetVariableNames(msg.SubItem[0]) == false)
        //                        {
        //                            return false;
        //                        }
        //                    }
        //                    else
        //                    {
        //                        if (listValue[index].SetVariableNames(msg.SubItem[index]) == false)
        //                        {
        //                            return false;
        //                        }
        //                    }

        //                }
        //            }
        //        }

        //        return true;
        //    }
        //    catch (Exception e)
        //    {
        //        return false;
        //    }
        //}

        internal HsmsObjectVariable FindByName(string name)
        {
            // 현재 객체의 이름이 일치하면 반환
            if (this.Name.ToUpper() == name.ToUpper())
                return this;

            if (SubItems != null)
            {
                // 하위 SubItem에서 재귀적으로 탐색
                foreach (var sub in SubItems)
                {
                    var result = sub.FindByName(name);
                    if (result != null)
                        return result;
                }
            }

            // 없으면 null 반환
            return null;
        }

        //public bool AppendData(string name, HsmsObjectVariable variable)
        //{
        //    if (this.Type != eHsmsDataType.LIST) return false;

        //    lock (this)
        //    {
        //        var subItem = FindByName(name);

        //        if (subItem.Value is List<HsmsObjectVariable> val)
        //        {
        //            val.Add(variable);
        //            return true;
        //        }

        //        return false;

        //    }
        //}

        public static implicit operator HsmsObjectVariable(HsmsField field)
        {
            HsmsObjectVariable variable = new HsmsObjectVariable();

            variable.Name = field.Name;
            variable.Type = field.Type;
            variable.Length = field.Count;
            if (field.Type == eHsmsDataType.LIST)
            {
                if (variable.Length == 0) variable.IsRepeat = true;
                var variableList = new List<HsmsObjectVariable>();
                foreach (var item in field.Fields)
                {
                    variableList.Add(item);
                }
                variable.Value = variableList;
            }

            return variable;
        }

    }
}
