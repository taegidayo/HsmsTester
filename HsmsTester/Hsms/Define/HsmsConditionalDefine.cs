using HsmsTester.Hsms.Struct;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HsmsTester.Hsms.Define
{
    // 특정 메시지를 받았을 때, 자동으로 응답되게 하기 위한 조건 설정
    // Path 규칙 : Body 기준 Fields 인덱스를 '/'로 연결. "" = Body 자신, "1/0" = Body.Fields[1].Fields[0]
    public record class HsmsConditionalDefine
    {
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;

        // 트리거 : 이 메시지를 받으면
        public int Stream { get; set; }
        public int Function { get; set; }
        public List<HsmsCondition> Conditions { get; set; } = [];       // 전부 만족(AND)하면 발동

        // 응답 : Reply를 복사한 뒤 Actions를 순서대로 적용해서 전송
        public HsmsJson Reply { get; set; } = new HsmsJson();
        public List<HsmsReplyAction> Actions { get; set; } = [];

        public bool IsMatch(HsmsJson received)
        {
            if (Enabled == false || received.Stream != Stream || received.Function != Function) return false;

            return Conditions.All(c => c.Check(received.Body));
        }

        public HsmsJson BuildReply(HsmsJson received)
        {
            var reply = DeepCopy(Reply);
            foreach (var action in Actions)
                action.Apply(received.Body, reply.Body);
            return reply;
        }

        internal static T DeepCopy<T>(T src) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(src))!;

        internal static HsmsField? Find(HsmsField? root, string path)
        {
            if (root is null) return null;
            var node = root;
            foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part, out var idx) == false || idx < 0 || idx >= node.Fields.Count) return null;
                node = node.Fields[idx];
            }
            return node;
        }

        internal static string ValueText(HsmsField field) => field.Value?.ToString() ?? string.Empty;
    }

    public record class HsmsCondition
    {
        public string Path { get; set; } = string.Empty;    // 받은 메시지 안의 위치
        public ConditionalType Op { get; set; }
        public string Value { get; set; } = string.Empty;   // 둘 다 숫자면 숫자 비교, 아니면 문자열 비교

        public bool Check(HsmsField? receivedBody)
        {
            var node = HsmsConditionalDefine.Find(receivedBody, Path);
            if (node is null) return false;

            var actual = HsmsConditionalDefine.ValueText(node);
            if (double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) == true &&
                double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var b) == true)
            {
                return Op switch
                {
                    ConditionalType.Equal => a == b,
                    ConditionalType.NotEqual => a != b,
                    ConditionalType.Bigger => a > b,
                    ConditionalType.Smaller => a < b,
                    _ => false,
                };
            }

            return Op switch
            {
                ConditionalType.Equal => actual == Value,
                ConditionalType.NotEqual => actual != Value,
                _ => false,     // 문자열은 대소 비교 안 함
            };
        }
    }

    public record class HsmsReplyAction
    {
        public ReplyActionType Type { get; set; }
        public string Target { get; set; } = string.Empty;  // Reply 안의 위치
        public string Source { get; set; } = string.Empty;  // 받은 메시지 안의 위치 (Copy, Repeat)
        public string Value { get; set; } = string.Empty;   // 고정값 (Set)
        public List<HsmsReplyAction> ItemActions { get; set; } = [];    // Repeat 전용 : 각 항목 기준 상대경로로 적용

        public void Apply(HsmsField? sourceRoot, HsmsField? targetRoot)
        {
            var target = HsmsConditionalDefine.Find(targetRoot, Target);
            if (target is null) return;

            // ponytail: 값은 문자열/원본 객체 그대로 넣음, 전송 시 Type에 맞게 변환 필요하면 여기서 변환
            switch (Type)
            {
                case ReplyActionType.Set:
                    SetValue(target, Value);
                    break;

                case ReplyActionType.Copy:
                    var src = HsmsConditionalDefine.Find(sourceRoot, Source);
                    if (src is null) return;
                    if (src.IsList == true && target.IsList == true)
                    {
                        target.Fields = HsmsConditionalDefine.DeepCopy(src.Fields);
                        target.Count = target.Fields.Count;
                    }
                    else SetValue(target, src.Value);
                    break;

                case ReplyActionType.Repeat:
                    Repeat(sourceRoot, target);
                    break;
            }
        }

        // Target 리스트의 Fields[0]을 틀로 N개 복제. N = Source가 리스트면 항목 수, 아니면 Source 값
        private void Repeat(HsmsField? sourceRoot, HsmsField target)
        {
            var src = HsmsConditionalDefine.Find(sourceRoot, Source);
            var template = target.Fields.FirstOrDefault();
            if (src is null || template is null) return;

            int count;
            if (src.IsList == true) count = src.Fields.Count;
            else if (int.TryParse(HsmsConditionalDefine.ValueText(src), out count) == false || count < 0) return;

            var items = new List<HsmsField>(count);
            for (int i = 0; i < count; i++)
            {
                var item = HsmsConditionalDefine.DeepCopy(template);
                var itemSource = src.IsList == true ? src.Fields[i] : sourceRoot;
                foreach (var action in ItemActions)
                    action.Apply(itemSource, item);
                items.Add(item);
            }

            target.Fields = items;
            target.Count = count;
        }

        private static void SetValue(HsmsField target, object? value)
        {
            target.Value = value!;
            if (target.Type is eHsmsDataType.ASCII or eHsmsDataType.JIS8)
                target.Count = HsmsConditionalDefine.ValueText(target).Length;
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConditionalType
    {
        Equal,
        NotEqual,
        Bigger,
        Smaller,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ReplyActionType
    {
        Set,
        Copy,
        Repeat,
    }
}
