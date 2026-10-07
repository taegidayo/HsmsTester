using HsmsTester;
using HsmsTester.Hsms.Define;
using HsmsTester.Hsms.Struct;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HsmsProtocol.Views
{
    // SML(XCom), SMD(XML) 파일을 HsmsMsgDefine(쌍 구조)으로 변환 (웹 secsFile.js와 동일한 규칙)
    internal static class SecsFileParser
    {
        private static readonly Dictionary<string, eHsmsDataType> SmlTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["BOOLEAN"] = eHsmsDataType.BOOL, ["BOOL"] = eHsmsDataType.BOOL,
            ["U1"] = eHsmsDataType.UINT1, ["U2"] = eHsmsDataType.UINT2, ["U4"] = eHsmsDataType.UINT4, ["U8"] = eHsmsDataType.UINT8,
            ["I1"] = eHsmsDataType.INT1, ["I2"] = eHsmsDataType.INT2, ["I4"] = eHsmsDataType.INT4, ["I8"] = eHsmsDataType.INT8,
            ["F4"] = eHsmsDataType.FLOAT4, ["F8"] = eHsmsDataType.FLOAT8,
            ["L"] = eHsmsDataType.LIST, ["A"] = eHsmsDataType.ASCII, ["B"] = eHsmsDataType.BINARY, ["J"] = eHsmsDataType.JIS8,
        };

        private static eHsmsDataType ToType(string raw) =>
            SmlTypes.TryGetValue(raw, out var t) == true ? t
            : Enum.TryParse<eHsmsDataType>(raw, true, out var e) == true ? e
            : throw new FormatException($"알 수 없는 타입 : {raw}");

        // 값이 없는 정의 항목은 그대로 보내면 인코딩이 실패하므로 Count만큼 기본값 채움
        private static object DefaultValue(eHsmsDataType type, int count)
        {
            if (type is eHsmsDataType.ASCII or eHsmsDataType.JIS8) return string.Empty;
            var values = Enumerable.Repeat(type == eHsmsDataType.BOOL ? "false" : "0", count).ToList();
            return values.Count == 1 ? values[0] : values;
        }

        // 메시지 목록을 Primary / Secondary 쌍으로 묶는다. Primary(홀수 F) 바로 뒤의 같은 S, F + 1 메시지가 Secondary
        private static List<HsmsMsgPair> ToPairs(List<(HsmsJson Msg, string PairName)> items)
        {
            var pairs = new List<HsmsMsgPair>();
            for (int i = 0; i < items.Count; i++)
            {
                var primary = items[i].Msg;
                var pair = new HsmsMsgPair { Name = items[i].PairName, Primary = primary };
                bool hasSecondary = primary.Function % 2 == 1 && i + 1 < items.Count
                    && items[i + 1].Msg.Stream == primary.Stream && items[i + 1].Msg.Function == primary.Function + 1;
                if (hasSecondary == true)
                {
                    pair.Secondary = items[++i].Msg;
                }
                pairs.Add(pair);
            }
            return pairs;
        }

        #region SMD (XML)
        // <S6F12><Header><Stream>6</Stream>...<Wait>False</Wait><AutoReply>False</AutoReply></Header>
        //   <DataItem><B Count="1" Fixed="True" ItemName="ACKC6">0</B></DataItem></S6F12>
        public static HsmsMsgDefine ParseSmd(string text, string groupName)
        {
            var root = XDocument.Parse(text).Root ?? throw new FormatException("XML 형식 오류");
            var items = new List<(HsmsJson, string)>();

            foreach (var el in root.Elements())
            {
                var header = el.Element("Header");
                string Get(string tag) => header?.Element(tag)?.Value.Trim() ?? string.Empty;

                var pairName = header?.Element("Description")?.Attribute("PairName")?.Value ?? string.Empty;
                var msgName = Get("MessageName") is { Length: > 0 } n ? n : el.Name.LocalName;
                var item = el.Element("DataItem")?.Elements().FirstOrDefault();

                var msg = new HsmsJson
                {
                    Stream = int.Parse(Get("Stream")),
                    Function = int.Parse(Get("Function")),
                    Wbit = Get("Wait").Equals("true", StringComparison.OrdinalIgnoreCase),
                    Name = msgName,
                    SubName = pairName != string.Empty && pairName != msgName ? pairName : string.Empty,
                    IsAutoResponse = Get("AutoReply").Equals("true", StringComparison.OrdinalIgnoreCase),
                    Body = item is null ? null : SmdField(item),
                };
                items.Add((msg, pairName != string.Empty ? pairName : msgName));
            }
            return new HsmsMsgDefine { Name = groupName, Pairs = ToPairs(items) };
        }

        private static HsmsField SmdField(XElement el)
        {
            var type = ToType(el.Name.LocalName);
            var name = el.Attribute("ItemName")?.Value ?? string.Empty;
            int.TryParse(el.Attribute("Count")?.Value, out var count);

            if (type == eHsmsDataType.LIST)
            {
                bool isFixed = el.Attribute("Fixed")?.Value != "False";
                return new HsmsField { Type = type, Name = name, Count = isFixed == true ? count : 0, Fields = el.Elements().Select(SmdField).ToList() };
            }

            var text = el.Value;
            var value = text.Trim() != string.Empty || type == eHsmsDataType.ASCII ? SplitValue(type, text) : DefaultValue(type, Math.Max(count, 1));
            return new HsmsField { Type = type, Name = name, Count = count, Value = value };
        }

        #endregion

        #region SML (XCom)
        // <S1F6 S FormattedStateSendAck - PortState      P: Primary(W-bit), S: Secondary, N: No reply
        //   <LIST n Ports                                n: 가변 List
        //     <UINT1 1 SFCD >
        //   >
        // >
        public static HsmsMsgDefine ParseSml(string text, string groupName)
        {
            var tokens = Regex.Matches(text, "<|>|\"[^\"]*\"|'[^']*'|[^\\s<>]+").Select(m => m.Value).ToList();
            int i = 0;
            var items = new List<(HsmsJson, string)>();

            // '<' 다음부터 짝이 맞는 '>' 까지 읽어 단어들과 하위 블록을 돌려준다
            (List<string> Words, List<object> Children) ReadBlock()
            {
                var words = new List<string>();
                var children = new List<object>();
                while (i < tokens.Count)
                {
                    var t = tokens[i++];
                    if (t == ">") break;
                    if (t == "<") children.Add(ReadBlock());
                    else words.Add(t.Trim('"', '\''));
                }
                return (words, children);
            }

            HsmsField ToField(object blockObj)
            {
                var (words, children) = ((List<string>, List<object>))blockObj;
                var type = ToType(words.ElementAtOrDefault(0) ?? string.Empty);
                var n = words.ElementAtOrDefault(1) ?? string.Empty;
                var name = words.ElementAtOrDefault(2) ?? string.Empty;
                bool isNumber = int.TryParse(n, out var count);

                if (type == eHsmsDataType.LIST)
                {
                    return new HsmsField { Type = type, Name = name, Count = isNumber == true ? count : 0, Fields = children.Select(ToField).ToList() };
                }
                if (isNumber == false) count = 1;
                var value = words.Count > 3 ? SplitValue(type, string.Join(" ", words.Skip(3))) : DefaultValue(type, count);
                return new HsmsField { Type = type, Name = name, Count = count, Value = value };
            }

            while (i < tokens.Count)
            {
                if (tokens[i++] != "<") continue;
                var (words, children) = ReadBlock();
                var m = Regex.Match(words.ElementAtOrDefault(0) ?? string.Empty, @"^S(\d+)F(\d+)$", RegexOptions.IgnoreCase);
                if (m.Success == false) continue;   // <XCom 2.4> 등

                var dir = (words.ElementAtOrDefault(1) ?? string.Empty).ToUpperInvariant();
                var names = string.Join(" ", words.Skip(2)).Split(" - ");
                var msg = new HsmsJson
                {
                    Stream = int.Parse(m.Groups[1].Value),
                    Function = int.Parse(m.Groups[2].Value),
                    Wbit = dir == "P",
                    Name = names[0].Trim(),
                    SubName = string.Join(" - ", names.Skip(1)).Trim(),
                    Body = children.Count > 0 ? ToField(children[0]) : null,
                };
                items.Add((msg, msg.Name));
            }
            return new HsmsMsgDefine { Name = groupName, Pairs = ToPairs(items) };
        }

        #endregion

        private static object SplitValue(eHsmsDataType type, string text)
        {
            if (type is eHsmsDataType.ASCII or eHsmsDataType.JIS8) return text;
            var values = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
            return values.Count == 1 ? values[0] : values;
        }
    }
}
