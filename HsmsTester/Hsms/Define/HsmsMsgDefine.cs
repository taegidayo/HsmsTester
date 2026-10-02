using HsmsTester.Hsms.Struct;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Hsms.Define
{
    public record class HsmsMsgDefine
    {
        public string Name { get; set; } = string.Empty;

        public List<HsmsJson> Msg { get; set; } = new List<HsmsJson>();

        public bool IsSelected { get; set; } = false;

    }
}
