using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Hsms.Struct
{
    // Hsms 메시지를 Json으로 받을 때 사용하는 타입

    public class HsmsJson
    {
        public int Stream { get; set; }     // S
        public int Function { get; set; }   // F
        public bool Wbit { get; set; }      // W
        public string Name { get; set; }
        public string SubName { get; set; }
        public string FullName => $"{Name}-{SubName}";

        public HsmsField Body { get; set; }    // 실제 Body 데이터 (Dictionary or List 가능)
    }

    public class HsmsField
    {
        public string Name { get; set; }            // 필드 이름
        public eHsmsDataType Type { get; set; }            // ascii, uint2, list 등
        public int Count { get; set; }           // Count==0 : List N , else Count

        public List<HsmsField> Fields { get; set; } // list일 때 하위 항목

        public bool IsList => Type == eHsmsDataType.LIST;
    }
}
