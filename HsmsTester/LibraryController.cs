using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester
{
    public class LibraryController
    {

        private static LibraryController? _instance = null;

        private LibraryController()
        {
            AppDomain.CurrentDomain.ProcessExit += OnProgramExit;
        }

        public static LibraryController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LibraryController();
                }
                return _instance;
            }
        }

        // 라이브러리 내의 IDisposable 객체들을 관리하기 위한 리스트
        internal List<IDisposable> Disposables { get; set; } = new List<IDisposable>();

        internal void RegisterDisposable(IDisposable disposable)
        {
            Disposables.Add(disposable);
        }

        // 프로그램 종료 시 호출되는 이벤트 핸들러  
        private void OnProgramExit(object sender, EventArgs e)
        {
            foreach (var disposable in Disposables)
            {
                disposable.Dispose();
            }
        }

    }
}
