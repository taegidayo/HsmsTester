using HsmsTester.Hsms.Define;
using HsmsTester.Hsms.Struct;
using Org.BouncyCastle.Asn1.Mozilla;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            EnequeueSendMsg(header, header.GetFullHeaderByte());

        }

        // 받은 메시지에 대한 자동 응답. IsAutoResponse 응답을 먼저 보내고, 이어서 HsmsConditionalDefines 응답을 보낸다.
        // 같은 S/F 메시지는 한 번만 보낸다. (먼저 보낸 IsAutoResponse 응답이 우선)
        private void SendAutoResponses(HsmsJson recv)
        {
            var sent = new HashSet<(int Stream, int Function)>();

            // IsAutoResponse : 같은 Stream, Function + 1 정의로 응답 (ex. S6F11 → S6F12)
            if (recv.IsAutoResponse == true && recv.Wbit == true)
            {
                if (recv.Stream == 6 && recv.Function == 11 && T3TimeoutCheckFlag == true)
                {
                }
                else
                {
                    var define = HsmsMsgDefines.SelectMany(d => d.Msg)
                        .FirstOrDefault(m => m.Stream == recv.Stream && m.Function == recv.Function + 1);
                    if (define is not null)
                    {
                        SendReply(recv, HsmsConditionalDefine.DeepCopy(define), sent);     // 정의 원본의 Header가 바뀌지 않도록 복사본 사용
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
        }
    }
}
