using HsmsTester.Hsms.Struct;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HsmsTester.Hsms.Define
{
    public record class HsmsMsgDefine
    {
        public string Name { get; set; } = string.Empty;

        public List<HsmsMsgPair> Pairs { get; set; } = new List<HsmsMsgPair>();

        public bool IsSelected { get; set; } = false;

        // 이전 형식(msg : 메시지 목록) 호환용. 불러올 때 Primary 바로 뒤의 같은 S, F + 1 메시지를 Secondary로 묶는다. 저장 시에는 쓰지 않음
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<HsmsJson>? Msg
        {
            get => null;
            set
            {
                if (value is not null) Pairs = ToPairs(value);
            }
        }

        // Primary, Secondary 전체 (받은 메시지의 정의를 찾을 때 사용)
        [JsonIgnore]
        public IEnumerable<HsmsJson> AllMsgs => Pairs.SelectMany(p => p.Secondary is null ? new[] { p.Primary } : new[] { p.Primary, p.Secondary });

        private static List<HsmsMsgPair> ToPairs(List<HsmsJson> msgs)
        {
            var pairs = new List<HsmsMsgPair>();
            for (int i = 0; i < msgs.Count; i++)
            {
                var pair = new HsmsMsgPair { Name = msgs[i].SubName, Primary = msgs[i] };
                bool hasSecondary = i + 1 < msgs.Count && msgs[i].Function % 2 == 1
                    && msgs[i + 1].Stream == msgs[i].Stream && msgs[i + 1].Function == msgs[i].Function + 1;
                if (hasSecondary == true)
                {
                    pair.Secondary = msgs[++i];
                }
                pairs.Add(pair);
            }
            return pairs;
        }
    }

    // Primary(요청)와 Secondary(응답) 메시지 쌍 (SMD의 PairName 단위)
    public record class HsmsMsgPair
    {
        public string Name { get; set; } = string.Empty;
        public HsmsJson Primary { get; set; } = new HsmsJson();
        public HsmsJson? Secondary { get; set; } = null;       // null이면 응답 없는 메시지
    }
}
