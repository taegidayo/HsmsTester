using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Manager
{
    public class HsmsManager : IDisposable
    {
        public static HsmsManager Instance { get; } = new HsmsManager();

        private HsmsManager()
        {
            LibraryController.Instance.RegisterDisposable(this);
        }

        public void Start()
        {

        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
  