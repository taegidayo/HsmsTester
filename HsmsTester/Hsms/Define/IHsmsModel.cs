using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Hsms.Struct
{

    public abstract class IHsmsModel
    {
        private HsmsHeaderMessage _header;

        /// <summary>
        /// Stream 번호
        /// </summary>
        public int Stream { get; protected set; }

        /// <summary>
        /// Function 번호
        /// </summary>
        public int Function { get; protected set; }

        /// <summary>
        /// 메세지 전체 명
        /// </summary>
        public string FullName { get; protected set; }

        /// <summary>
        /// 메세지 명
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// 메세지 하위 명
        /// </summary>
        public string SubName { get; protected set; }

        public bool IsUnDefined { get; protected set; }

        public string StreamFunction => string.Format("S{0}F{1}", this.Stream, this.Function);

        public int SysByte { get => this._header.SystemByte; set => this._header.SystemByte = value; }

        public void SetHsmsMsgInfo(HsmsHeaderMessage header)
        {
            this._header = header;
        }

        public virtual void RecvData() { }

        public virtual void SendData() { }

        public override string ToString()
        {
            return string.Format("{0} {1}", this.StreamFunction, this.FullName);
        }
    }
}
