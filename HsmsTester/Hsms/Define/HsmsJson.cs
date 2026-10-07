using System;
using HsmsTester.Manager;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HsmsTester.Hsms.Struct
{
    // Hsms 메시지를 Json으로 받을 때 사용하는 타입

    public record class HsmsJson
    {
        // Header는 메시지를 주고받을 때 사용하는 것으로 굳이 Json형태로 저장하지 않는다.
        [JsonIgnore]
        public HsmsHeaderMessage Header { get; set; } = new HsmsHeaderMessage();

        public int Stream { get; set; }     // S
        public int Function { get; set; }   // F
        public bool Wbit { get; set; }      // W
        public string Name { get; set; } = string.Empty;
        public string SubName { get; set; } = string.Empty;
        public string FullName => $"{Name}-{SubName}";
        public bool IsAutoResponse { get; set; } // 자동응답이 가능한 메시지인지 확인하기 위한 변수, S6F11에서 true라면 자동으로 S6F12에 대해 응답을 보낸다.
        public HsmsField? Body { get; set; } = null;

        // ApplyDefine()에서 일치한 메시지 정의 원본 (자동 응답 시 같은 쌍의 Secondary를 찾기 위해 사용)
        [JsonIgnore]
        public HsmsJson? Define { get; private set; } = null;    // 실제 Body 데이터, null이면 Header만 전송 (ex. S1F1)

        // 메시지 내용(S, F, W)을 바탕으로 Data Message용 Header 작성. Length는 ToHsmsBytes()에서 채운다.
        public void SetHeader(uint systemByte = 0)
        {
            Header.DeviceID = HsmsManager.Instance.Config.DeviceID;
            Header.Stream = (byte)Stream;
            Header.Function = (byte)Function;
            Header.wBit = Wbit;
            Header.HeaderByte2 = (byte)((Stream & 0x7F) | (Wbit == true ? 0x80 : 0x00));
            Header.HeaderByte3 = (byte)Function;
            Header.Ptype = 0;
            Header.Stype = 0;       // 0 : Data Message
            Header.SystemByte = systemByte != 0 ? systemByte : HsmsManager.Instance.NextDataSystemByte();
            Header.FullName = FullName;
        }

        // Header + Body를 HSMS로 전송할 byte[]로 변환 (Length 4byte + Header 10byte + Body)
        public byte[] ToHsmsBytes()
        {
            var body = new List<byte>();
            if (Body is not null) EncodeItem(Body, body);

            Header.Length = (uint)(Header.HEADER_LENGTH - 4 + body.Count);
            return [.. Header.GetFullHeaderByte(), .. body];
        }

        // 받은 HSMS byte[] (Length 4byte + Header 10byte + Body)로 현재 데이터를 세팅. 형식이 잘못되면 false (이때 현재 데이터는 변경 안 됨)
        // Name, SubName, IsAutoResponse, 항목 Name은 구조가 일치하는 메시지 정의에서 가져온다. defines가 null이면 HsmsManager의 정의 사용
        public bool FromHsmsBytes(byte[] msg, IEnumerable<HsmsJson>? defines = null)
        {
            var header = new HsmsHeaderMessage();
            if (msg.Length < header.HEADER_LENGTH) return false;
            header.ParsingRecvToHeader(msg);

            int offset = header.HEADER_LENGTH;
            HsmsField? body;
            try
            {
                body = offset < msg.Length ? DecodeItem(msg, ref offset) : null;
            }
            catch (InvalidDataException)
            {
                return false;
            }

            Header = header;
            Stream = header.Stream;
            Function = header.Function;
            Wbit = header.wBit;
            Body = body;
            Name = string.Empty;            // 일치하는 정의가 없으면 빈 값
            SubName = string.Empty;
            IsAutoResponse = false;
            Define = null;
            ApplyDefine(defines ?? HsmsManager.Instance.HsmsMsgDefines.Where(t=>t.IsSelected == true).SelectMany(d => d.AllMsgs));
            return true;
        }

        private static HsmsField DecodeItem(byte[] msg, ref int offset)
        {
            CheckRange(msg, offset, 1);
            var type = (eHsmsDataType)(msg[offset] >> 2);
            int lengthBytes = msg[offset] & 0x03;
            offset++;

            CheckRange(msg, offset, lengthBytes);
            int length = 0;
            for (int i = 0; i < lengthBytes; i++)
                length = (length << 8) | msg[offset++];

            var field = new HsmsField { Type = type, Count = length };

            if (field.IsList == true)
            {
                for (int i = 0; i < length; i++)
                    field.Fields.Add(DecodeItem(msg, ref offset));      // LIST는 Length = 하위 항목 수
                return field;
            }

            CheckRange(msg, offset, length);
            var data = msg.AsSpan(offset, length);
            offset += length;

            if (type is eHsmsDataType.ASCII or eHsmsDataType.JIS8)
            {
                field.Value = Encoding.ASCII.GetString(data);
                return field;
            }

            int size = type switch
            {
                eHsmsDataType.BINARY or eHsmsDataType.BOOL or eHsmsDataType.INT1 or eHsmsDataType.UINT1 => 1,
                eHsmsDataType.INT2 or eHsmsDataType.UINT2 => 2,
                eHsmsDataType.INT4 or eHsmsDataType.UINT4 or eHsmsDataType.FLOAT4 => 4,
                eHsmsDataType.INT8 or eHsmsDataType.UINT8 or eHsmsDataType.FLOAT8 => 8,
                _ => throw new InvalidDataException($"알 수 없는 Format Code : 0x{(int)type:X2}"),
            };
            if (length % size != 0) throw new InvalidDataException($"{type} 길이({length})가 {size}byte 단위가 아님");

            var values = new List<object>();
            for (int i = 0; i < length; i += size)
            {
                var item = data.Slice(i, size);
                values.Add(type switch
                {
                    eHsmsDataType.BOOL => item[0] != 0,
                    eHsmsDataType.BINARY or eHsmsDataType.UINT1 => item[0],
                    eHsmsDataType.UINT2 => BinaryPrimitives.ReadUInt16BigEndian(item),
                    eHsmsDataType.UINT4 => BinaryPrimitives.ReadUInt32BigEndian(item),
                    eHsmsDataType.UINT8 => BinaryPrimitives.ReadUInt64BigEndian(item),
                    eHsmsDataType.INT1 => (sbyte)item[0],
                    eHsmsDataType.INT2 => BinaryPrimitives.ReadInt16BigEndian(item),
                    eHsmsDataType.INT4 => BinaryPrimitives.ReadInt32BigEndian(item),
                    eHsmsDataType.INT8 => BinaryPrimitives.ReadInt64BigEndian(item),
                    eHsmsDataType.FLOAT4 => BinaryPrimitives.ReadSingleBigEndian(item),
                    _ => BinaryPrimitives.ReadDoubleBigEndian(item),
                });
            }

            // 값이 1개면 단일 값, 여러 개면 배열 (ToHsmsBytes와 같은 형태)
            field.Count = values.Count;
            field.Value = values.Count == 1 ? values[0] : values;
            return field;
        }

        // 받은 메시지와 S/F, Body 구조가 일치하는 정의를 찾아 Name을 채운다.
        // 구조가 일치하는 정의가 여러 개면 (ex. CEID만 다른 S6F11) 정의에 적힌 값과 같은 항목이 가장 많은 정의를 선택, 동점이면 먼저 정의된 것
        public bool ApplyDefine(IEnumerable<HsmsJson> defines)
        {
            var best = defines
                .Where(d => d.Stream == Stream && d.Function == Function && IsSameStructure(d.Body, Body) == true)
                .MaxBy(d => ValueMatchScore(d.Body, Body));
            if (best is null) return false;

            Define = best;
            Name = best.Name;
            SubName = best.SubName;
            IsAutoResponse = best.IsAutoResponse;
            ApplyNames(best.Body, Body);
            return true;
        }

        private static bool IsSameStructure(HsmsField? define, HsmsField? recv)
        {
            if (define is null || recv is null) return define is null && recv is null;
            if (define.Type != recv.Type) return false;
            if (define.IsList == false) return true;

            // 고정 List는 항목 수가 같아야 함, List N은 항목 수 무관
            if (define.Count != 0 && define.Fields.Count != recv.Fields.Count) return false;
            return Pairs(define, recv).All(p => IsSameStructure(p.Define, p.Recv) == true);
        }

        private static int ValueMatchScore(HsmsField? define, HsmsField? recv)
        {
            if (define is null || recv is null) return 0;
            if (define.IsList == true) return Pairs(define, recv).Sum(p => ValueMatchScore(p.Define, p.Recv));

            var expected = ToTextList(define.Value);
            bool hasValue = expected.Any(t => t != string.Empty);
            return hasValue == true && expected.SequenceEqual(ToTextList(recv.Value)) == true ? 1 : 0;
        }

        private static void ApplyNames(HsmsField? define, HsmsField? recv)
        {
            if (define is null || recv is null) return;
            recv.Name = define.Name;
            foreach (var p in Pairs(define, recv))
                ApplyNames(p.Define, p.Recv);
        }

        // 정의와 받은 데이터의 하위 항목 짝짓기. List N(Count == 0)은 정의의 첫 항목을 틀로 받은 항목 전부와 짝지음
        private static IEnumerable<(HsmsField Define, HsmsField Recv)> Pairs(HsmsField define, HsmsField recv)
        {
            if (define.Count != 0) return define.Fields.Zip(recv.Fields);

            var template = define.Fields.FirstOrDefault();
            return template is null ? [] : recv.Fields.Select(f => (template, f));
        }

        private static void CheckRange(byte[] msg, int offset, int length)
        {
            if (offset + length > msg.Length)
                throw new InvalidDataException($"데이터 길이 부족 : offset {offset}, 필요 {length}byte, 전체 {msg.Length}byte");
        }

        private static void EncodeItem(HsmsField field, List<byte> output)
        {
            if (field.IsList == true)
            {
                WriteItemHeader(field.Type, field.Fields.Count, output);     // LIST는 Length = 하위 항목 수
                foreach (var item in field.Fields)
                    EncodeItem(item, output);
                return;
            }

            var data = EncodeValue(field);
            WriteItemHeader(field.Type, data.Length, output);
            output.AddRange(data);
        }

        // Format Byte (Type 6bit + Length Byte 수 2bit) + Length (1~3byte, Big Endian)
        private static void WriteItemHeader(eHsmsDataType type, int length, List<byte> output)
        {
            int lengthBytes = length <= 0xFF ? 1 : length <= 0xFFFF ? 2 : 3;
            output.Add((byte)(((int)type << 2) | lengthBytes));
            for (int i = lengthBytes - 1; i >= 0; i--)
                output.Add((byte)(length >> (8 * i)));
        }

        // Value는 단일 값 또는 배열 모두 가능 (ex. 1, "1", [1, 2, 3])
        private static byte[] EncodeValue(HsmsField field)
        {
            if (field.Type is eHsmsDataType.ASCII or eHsmsDataType.JIS8)
            {
                // ponytail: JIS8도 ASCII로 인코딩, 일본어 필요하면 Shift_JIS(CodePages) 사용
                return Encoding.ASCII.GetBytes(ToText(field.Value));
            }

            var values = ToTextList(field.Value);
            int size = field.Type switch
            {
                eHsmsDataType.BINARY or eHsmsDataType.BOOL or eHsmsDataType.INT1 or eHsmsDataType.UINT1 => 1,
                eHsmsDataType.INT2 or eHsmsDataType.UINT2 => 2,
                eHsmsDataType.INT4 or eHsmsDataType.UINT4 or eHsmsDataType.FLOAT4 => 4,
                _ => 8,
            };

            var result = new byte[values.Count * size];
            for (int i = 0; i < values.Count; i++)
            {
                var span = result.AsSpan(i * size, size);
                var text = values[i];
                switch (field.Type)
                {
                    case eHsmsDataType.BOOL:
                        span[0] = (byte)(text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                        break;
                    case eHsmsDataType.FLOAT4:
                        BinaryPrimitives.WriteSingleBigEndian(span, float.Parse(text, CultureInfo.InvariantCulture));
                        break;
                    case eHsmsDataType.FLOAT8:
                        BinaryPrimitives.WriteDoubleBigEndian(span, double.Parse(text, CultureInfo.InvariantCulture));
                        break;
                    case eHsmsDataType.BINARY or eHsmsDataType.UINT1 or eHsmsDataType.UINT2 or eHsmsDataType.UINT4 or eHsmsDataType.UINT8:
                        WriteBigEndian(span, ulong.Parse(text, CultureInfo.InvariantCulture));
                        break;
                    default:    // INT1 ~ INT8
                        WriteBigEndian(span, (ulong)long.Parse(text, CultureInfo.InvariantCulture));
                        break;
                }
            }
            return result;
        }

        private static void WriteBigEndian(Span<byte> span, ulong value)
        {
            for (int i = span.Length - 1; i >= 0; i--)
            {
                span[i] = (byte)value;
                value >>= 8;
            }
        }

        // 로그용 SML 형태 문자열. 하위 항목은 레벨마다 Tab 한 칸씩 들여쓴다
        // ex) <L[2]
        //         <A[4] MDLN "HOST">
        //         <U4[2] SVID 1 2>
        //     >
        public string ToSmlString()
        {
            if (Body is null) return "(Header Only)";

            var sb = new StringBuilder();
            AppendSml(Body, 0, sb);
            return sb.ToString().TrimEnd();
        }

        private static readonly Dictionary<eHsmsDataType, string> SmlTypeNames = new()
        {
            [eHsmsDataType.LIST] = "L", [eHsmsDataType.BINARY] = "B", [eHsmsDataType.BOOL] = "BOOLEAN",
            [eHsmsDataType.ASCII] = "A", [eHsmsDataType.JIS8] = "J",
            [eHsmsDataType.INT1] = "I1", [eHsmsDataType.INT2] = "I2", [eHsmsDataType.INT4] = "I4", [eHsmsDataType.INT8] = "I8",
            [eHsmsDataType.UINT1] = "U1", [eHsmsDataType.UINT2] = "U2", [eHsmsDataType.UINT4] = "U4", [eHsmsDataType.UINT8] = "U8",
            [eHsmsDataType.FLOAT4] = "F4", [eHsmsDataType.FLOAT8] = "F8",
        };

        private static void AppendSml(HsmsField field, int depth, StringBuilder sb)
        {
            var indent = new string('\t', depth);
            var type = SmlTypeNames.TryGetValue(field.Type, out var t) == true ? t : field.Type.ToString();
            var name = field.Name == string.Empty ? string.Empty : $" {field.Name}";

            if (field.IsList == true)
            {
                if (field.Fields.Count == 0)
                {
                    sb.Append(indent).Append($"<{type}[0]{name}>").Append('\n');
                    return;
                }

                sb.Append(indent).Append($"<{type}[{field.Fields.Count}]{name}").Append('\n');
                foreach (var item in field.Fields)
                    AppendSml(item, depth + 1, sb);
                sb.Append(indent).Append('>').Append('\n');
                return;
            }

            string value;
            int count;
            if (field.Type is eHsmsDataType.ASCII or eHsmsDataType.JIS8)
            {
                var text = ToText(field.Value);
                value = $" \"{text}\"";
                count = text.Length;
            }
            else
            {
                var values = ToTextList(field.Value);
                value = values.Count == 0 ? string.Empty : " " + string.Join(" ", values);
                count = values.Count;
            }
            sb.Append(indent).Append($"<{type}[{count}]{name}{value}>").Append('\n');
        }

        private static List<string> ToTextList(object? value)
        {
            if (value is null) return [];
            if (value is JsonElement { ValueKind: JsonValueKind.Array } array)
                return array.EnumerateArray().Select(e => ToText(e)).ToList();
            if (value is not string && value is IEnumerable list)
                return list.Cast<object>().Select(ToText).ToList();
            return [ToText(value)];
        }

        private static string ToText(object? value) => value switch
        {
            null => string.Empty,
            JsonElement { ValueKind: JsonValueKind.String } e => e.GetString() ?? string.Empty,
            JsonElement { ValueKind: JsonValueKind.Null } => string.Empty,
            JsonElement e => e.GetRawText(),
            bool b => b == true ? "true" : "false",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }

    public record class HsmsField
    {
        public string Name { get; set; } = string.Empty;          // 필드 이름
        public eHsmsDataType Type { get; set; }            // ascii, uint2, list 등
        public int Count { get; set; }           // Count==0 : List N , else Count

        public List<HsmsField> Fields { get; set; } = new List<HsmsField>(); // list일 때 하위 항목

        public bool IsList => Type == eHsmsDataType.LIST;

        public object Value { get; set; } = null;
    }
}
