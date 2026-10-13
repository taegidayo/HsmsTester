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
        // 받은 메시지에 대한 자동 응답
        //  1. 조건 응답(HsmsConditionalDefines) 중 받은 메시지의 응답(같은 S, F + 1)이 있으면 그걸 보내고, 자동 응답(IsAutoResponse)은 보내지 않음
        //  2. 없으면 IsAutoResponse로 같은 쌍의 Secondary 응답 (ex. S6F11 → 같은 쌍의 S6F12)
        //     S6F11은 같은 쌍에 S6F12 정의가 없어도 기본 S6F12(ACKC6 = 0)로 응답
        //  3. 이어서 나머지 조건 응답(새 메시지) 전송
        // 응답(Secondary)은 받은 메시지의 SystemByte를 그대로 사용 (SendReply). 같은 S/F 메시지는 한 번만 보낸다.
        private void SendAutoResponses(HsmsJson recv)
        {
            var sent = new HashSet<(int Stream, int Function)>();
            bool IsSecondaryOf(HsmsJson m) => m.Stream == recv.Stream && m.Function == recv.Function + 1;

            // T3 Timeout 확인용 : S6F11에는 응답하지 않음 (자동 응답, 조건 응답 모두)
            bool noReply = recv.Stream == 6 && recv.Function == 11 && T3TimeoutCheckFlag == true;

            var conditionalReplies = ActiveConditionalDefines().Where(r => r.IsMatch(recv) == true).Select(r => r.BuildReply(recv)).ToList();
            var conditionalSecondary = conditionalReplies.FirstOrDefault(IsSecondaryOf);
    
            if (noReply == false)
            {
                if (conditionalSecondary is not null)
                {
                    SendReply(recv, conditionalSecondary, sent);
                }
                else if (recv.IsAutoResponse == true && recv.Wbit == true)
                {
                    var pairs = HsmsMsgDefines.Where(d => d.IsSelected == true).SelectMany(d => d.Pairs).ToList();
                    var secondary = pairs.FirstOrDefault(p => ReferenceEquals(p.Primary, recv.Define) == true)?.Secondary
                        // 일치한 쌍에 응답이 없으면 같은 S/F Primary를 가진 다른 쌍의 응답 사용 (ex. Undefined에서 구조만 추가한 S6F11)
                        ?? pairs.FirstOrDefault(p => p.Primary.Stream == recv.Stream && p.Primary.Function == recv.Function && p.Secondary is not null)?.Secondary;
                    if (secondary is not null)
                    {
                        SendReply(recv, HsmsConditionalDefine.DeepCopy(secondary), sent);     // 정의 원본의 Header가 바뀌지 않도록 복사본 사용
                    }
                    else if (recv.Stream == 6 && recv.Function == 11)
                    {
                        SendReply(recv, DefaultS6F12(), sent);      // S6F12 정의가 없으면 기본값(ACKC6 = 0)으로 응답
                    }
                }
            }

            foreach (var reply in conditionalReplies.Where(r => IsSecondaryOf(r) == false))
            {
                SendReply(recv, reply, sent);
            }
        }

        // S6F12 기본 응답 : <B[1] ACKC6 0> (0 = Accepted)
        private static HsmsJson DefaultS6F12() => new HsmsJson
        {
            Stream = 6, Function = 12, Wbit = false, Name = "Event Report Ack",
            Body = new HsmsField { Name = "ACKC6", Type = eHsmsDataType.BINARY, Count = 1, Value = (byte)0 },
        };

        private void SendReply(HsmsJson recv, HsmsJson reply, HashSet<(int Stream, int Function)> sent)
        {
            if (sent.Add((reply.Stream, reply.Function)) == false) return;

            // 받은 메시지의 2차 메시지(같은 S, F + 1)는 받은 SystemByte를 그대로 사용, 그 외는 새 SystemByte
            bool isSecondary = reply.Stream == recv.Stream && reply.Function == recv.Function + 1;
            reply.SetHeader(isSecondary == true ? recv.Header.SystemByte : NextDataSystemByte());
            EnqueueDataMsg(reply);
        }

        // 새 Data Message 전송 (새 SystemByte). API sendMsg, WPF Send 등 직접 보내는 메시지
        internal void SendMsg(HsmsJson msg)
        {
            msg.SetHeader();
            EnqueueDataMsg(msg);
        }

        // Header가 만들어진 Data Message를 전송 큐에 넣고 로그를 남긴다 (모든 Data Message 전송은 여기를 거침)
        private void EnqueueDataMsg(HsmsJson msg)
        {
            EnequeueSendMsg(msg.Header, msg.ToHsmsBytes());
            LogBroadcaster.Instance.Write($"Data Message 전송 : [{msg.Header.StreamFunction}] {msg.FullName}\n{msgToLog(msg)}");
        }

        // 로그용 메시지 문자열 (SML 형태, 레벨별 Tab 들여쓰기)
        private static string msgToLog(HsmsJson json) => $"S{json.Stream}F{json.Function}{(json.Wbit == true ? " W" : string.Empty)}\n{json.ToSmlString()}";

        // Selected 조건이 있는 HsmsConditionalDefines 전송. Select 완료(IsSelected false → true) 시 호출
        private void SendSelectedMessages()
        {
            var sent = new HashSet<(int Stream, int Function)>();
            var none = new HsmsJson();      // 받은 메시지가 없으므로 Copy/Repeat 등 받은 데이터를 쓰는 Action은 적용되지 않음
            foreach (var rule in ActiveConditionalDefines().Where(r => r.IsSelectedTrigger == true))
            {
                SendReply(none, rule.BuildReply(none), sent);
            }
        }

        // 주기 전송 : Selected 상태인 동안 IntervalMs마다 Reply 전송. Selected가 되면 한 주기 뒤부터 시작
        // ponytail: 50ms 간격으로 확인하므로 오차 최대 50ms, 더 정밀해야 하면 규칙별 타이머로
        private void _periodicThreadDowork(CancellationToken token)
        {
            var nextSend = new Dictionary<HsmsConditionalDefine, long>(ReferenceEqualityComparer.Instance);
            while (token.WaitHandle.WaitOne(50) == false)
            {
                try
                {
                    if (IsSelected == false || IsConnected() == false)
                    {
                        nextSend.Clear();
                        continue;
                    }

                    long now = Environment.TickCount64;
                    var rules = ActiveConditionalDefines().Where(r => r.IsPeriodic == true).ToList();
                    foreach (var rule in rules)
                    {
                        if (nextSend.TryGetValue(rule, out var due) == false)
                        {
                            nextSend[rule] = now + rule.EffectiveIntervalMs;
                            continue;
                        }
                        if (now < due) continue;

                        var none = new HsmsJson();      // 받은 메시지가 없으므로 Copy/Repeat 등 받은 데이터를 쓰는 Action은 적용되지 않음
                        SendReply(none, rule.BuildReply(none), new HashSet<(int Stream, int Function)>());
                        nextSend[rule] = now + rule.EffectiveIntervalMs;
                    }

                    // 삭제/비활성화되었거나 정의가 교체된 규칙 정리
                    foreach (var removed in nextSend.Keys.Where(k => rules.Contains(k) == false).ToList())
                        nextSend.Remove(removed);
                }
                catch (Exception ex)
                {
                    LogBroadcaster.Instance.Write($"주기 전송 오류 : {ex.Message}");
                }
            }
        }

        private string byteArrToStr(byte[] bytes)
        {

            return string.Join(" ", bytes.Select(b => b.ToString("X2")));
        }

    }
}
