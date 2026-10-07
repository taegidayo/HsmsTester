using HsmsTester.API;
using HsmsTester.Hsms.Define;
using HsmsTester.Hsms.Struct;
using Org.BouncyCastle.Asn1.Mozilla;
using Org.BouncyCastle.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HsmsTester.Manager
{
    partial class HsmsManager
    {
        internal void SendControlMessage(eSessionSType sessionType, uint systemByte = 0)
        {
            // 원래는 각각 구현해야하나 테스트 툴로 사용하기에는 사용안해도 될 항목들에 대해선 구현하지 않고 공통항목으로만 구현한다.
            HsmsHeaderMessage header = new HsmsHeaderMessage();

            header.Length = (uint)(header.HEADER_LENGTH - 4);    // Control Message는 Header(10byte)만 존재
            header.Stype = (byte)sessionType;
            header.HeaderByte2 = 0;
            header.HeaderByte3 = 0;
            header.DeviceID = Config.DeviceID;
            header.SystemByte = systemByte != 0 ? systemByte : NextControlSystemByte();     // 응답은 받은 SystemByte, 요청은 새 SystemByte
            var bytes = header.GetFullHeaderByte();
            EnequeueSendMsg(header, bytes);
            LogBroadcaster.Instance.Write($"Control Message 전송 : [{sessionType}], \n\t\t\t데이터 : {byteArrToStr(bytes)}");

        }

        // 받은 메시지에 대한 자동 응답. IsAutoResponse 응답을 먼저 보내고, 이어서 HsmsConditionalDefines 응답을 보낸다.
        // 같은 S/F 메시지는 한 번만 보낸다. (먼저 보낸 IsAutoResponse 응답이 우선)
        private void SendAutoResponses(HsmsJson recv)
        {
            var sent = new HashSet<(int Stream, int Function)>();

            // IsAutoResponse : 받은 메시지와 일치한 정의 쌍의 Secondary로 응답 (ex. S6F11 → 같은 쌍의 S6F12)
            if (recv.IsAutoResponse == true && recv.Wbit == true)
            {
                if (recv.Stream == 6 && recv.Function == 11 && T3TimeoutCheckFlag == true)
                {
                }
                else
                {
                    var pair = HsmsMsgDefines.SelectMany(d => d.Pairs)
                        .FirstOrDefault(p => ReferenceEquals(p.Primary, recv.Define) == true);
                    if (pair?.Secondary is not null)
                    {
                        SendReply(recv, HsmsConditionalDefine.DeepCopy(pair.Secondary), sent);     // 정의 원본의 Header가 바뀌지 않도록 복사본 사용
                    }
                }
            }

            foreach (var rule in HsmsConditionalDefines.Where(r => r.IsMatch(recv) == true))
            {
                SendReply(recv, rule.BuildReply(recv), sent);
            }
        }

        private void SendReply(HsmsJson recv, HsmsJson reply, HashSet<(int Stream, int Function)> sent)
        {
            if (sent.Add((reply.Stream, reply.Function)) == false) return;

            // 받은 메시지의 2차 메시지(같은 S, F + 1)는 받은 SystemByte를 그대로 사용, 그 외는 새 SystemByte
            bool isSecondary = reply.Stream == recv.Stream && reply.Function == recv.Function + 1;
            reply.SetHeader(isSecondary == true ? recv.Header.SystemByte : NextDataSystemByte());
            EnequeueSendMsg(reply.Header, reply.ToHsmsBytes());
            LogBroadcaster.Instance.Write($"Data Message 전송 : [{reply.Header.StreamFunction}] {reply.FullName}\n{msgToLog(reply)}");
        }

        // 로그용 메시지 문자열 (SML 형태, 레벨별 Tab 들여쓰기)
        private static string msgToLog(HsmsJson json) => $"S{json.Stream}F{json.Function}{(json.Wbit == true ? " W" : string.Empty)}\n{json.ToSmlString()}";

        // Selected 조건이 있는 HsmsConditionalDefines 전송. Select 완료(IsSelected false → true) 시 호출
        private void SendSelectedMessages()
        {
            var sent = new HashSet<(int Stream, int Function)>();
            var none = new HsmsJson();      // 받은 메시지가 없으므로 Copy/Repeat 등 받은 데이터를 쓰는 Action은 적용되지 않음
            foreach (var rule in HsmsConditionalDefines.Where(r => r.Enabled == true && r.IsSelectedTrigger == true))
            {
                SendReply(none, rule.BuildReply(none), sent);
            }
        }

        private string byteArrToStr(byte[] bytes)
        {

            return string.Join(" ", bytes.Select(b => b.ToString("X2")));
        }

    }
}
