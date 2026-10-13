using HsmsTester.Hsms.Define;
using HsmsTester.Hsms.Struct;
using HsmsTester.Manager;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using static HsmsProtocol.Views.Ui;

namespace HsmsProtocol.Views
{
    // 처음 보는 메시지 (정의와 매칭되지 않은 받은 메시지) 화면. 골라서 Message Defines에 추가
    internal class UndefinedView : UserControl
    {
        private readonly Func<HsmsJson, string> _addAsNewPair;        // 사용 그룹에 새 쌍(Primary)으로 추가, 결과 문구 반환
        private readonly Func<HsmsJson, string> _addAsSecondary;      // 정의 트리에서 선택한 쌍의 Secondary로 추가, 결과 문구 반환
        private readonly Action<string> _setStatus;

        private List<HsmsUndefinedMsg> _items = new List<HsmsUndefinedMsg>();
        private int? _selId = null;

        private readonly StackPanel _list = new StackPanel();
        private readonly TextBox _detail = new TextBox
        {
            IsReadOnly = true, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        public UndefinedView(Func<HsmsJson, string> addAsNewPair, Func<HsmsJson, string> addAsSecondary, Action<string> setStatus)
        {
            _addAsNewPair = addAsNewPair;
            _addAsSecondary = addAsSecondary;
            _setStatus = setStatus;

            var left = new StackPanel { Margin = new Thickness(8) };
            left.Children.Add(Header("Undefined Messages"));
            left.Children.Add(Muted("정의와 매칭되지 않은 받은 메시지 (같은 S/F, 같은 구조는 하나로 묶음)"));
            left.Children.Add(Row(Button("Refresh", Refresh), Button("모두 지우기", () => { HsmsManager.Instance.ClearUndefined(); _selId = null; Refresh(); })));
            left.Children.Add(_list);

            var actions = Row(
                Button("새 쌍으로 추가", () => Add(_addAsNewPair), "사용 중인 그룹에 새 쌍(Primary)으로 추가"),
                Button("선택한 쌍의 Secondary로", () => Add(_addAsSecondary), "Message Defines에서 선택한 쌍의 Secondary(응답)로 추가"),
                DangerButton(() =>
                {
                    if (_selId is int id) HsmsManager.Instance.RemoveUndefined(id);
                    _selId = null;
                    Refresh();
                }, "목록에서 삭제"));
            DockPanel.SetDock(actions, Dock.Top);
            var right = new DockPanel { Margin = new Thickness(8) };
            right.Children.Add(actions);
            right.Children.Add(_detail);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var leftScroll = new ScrollViewer { Content = left, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetColumn(right, 1);
            grid.Children.Add(leftScroll);
            grid.Children.Add(right);
            Content = grid;

            // 화면에 보이는 동안 2초마다 새로 고침
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) => { if (IsVisible == true) Refresh(); };
            timer.Start();
            IsVisibleChanged += (_, _) => { if (IsVisible == true) Refresh(); };
        }

        public void Refresh()
        {
            _items = HsmsManager.Instance.UndefinedMsgs;
            if (_items.Any(u => u.Id == _selId) == false) _selId = _items.FirstOrDefault()?.Id;

            _list.Children.Clear();
            if (_items.Count == 0) _list.Children.Add(Muted("처음 보는 메시지가 없습니다"));
            foreach (var u in _items)
            {
                var m = u.Msg;
                var text = $"S{m.Stream}F{m.Function}{(m.Wbit == true ? " W" : string.Empty)}  ×{u.Count}  {u.LastTime:HH:mm:ss}";
                _list.Children.Add(Item(ItemContent(text, null), u.Id == _selId, () => { _selId = u.Id; Refresh(); }));
            }

            var sel = _items.FirstOrDefault(u => u.Id == _selId);
            _detail.Text = sel is null ? string.Empty
                : $"S{sel.Msg.Stream}F{sel.Msg.Function}{(sel.Msg.Wbit == true ? " W" : string.Empty)}   받은 횟수 {sel.Count}   처음 {sel.FirstTime:HH:mm:ss}   마지막 {sel.LastTime:HH:mm:ss}\n\n{sel.Sml}";
        }

        // 정의에 추가하고 목록에서 뺀다 (추가한 정의는 MainWindow 자동 저장으로 저장)
        private void Add(Func<HsmsJson, string> add)
        {
            var sel = _items.FirstOrDefault(u => u.Id == _selId);
            if (sel is null)
            {
                _setStatus("추가할 메시지를 선택하세요");
                return;
            }

            var msg = Clone(sel.Msg);
            msg.Name = msg.Name == string.Empty ? $"S{msg.Stream}F{msg.Function}" : msg.Name;
            var result = add(msg);
            _setStatus(result);
            if (result.StartsWith("추가") == true)
            {
                HsmsManager.Instance.RemoveUndefined(sel.Id);
                _selId = null;
                Refresh();
            }
        }
    }
}
