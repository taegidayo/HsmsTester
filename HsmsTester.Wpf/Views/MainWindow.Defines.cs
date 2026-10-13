using HsmsTester.Hsms.Define;
using HsmsTester.Hsms.Struct;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static HsmsProtocol.Views.Ui;

namespace HsmsProtocol
{
    // Message Defines 트리 : 드래그로 순서 변경, 검색, Hidden Defines
    public partial class MainWindow
    {
        #region 검색

        // 검색어 : "S6F11" = S, F 모두 / "S6" = Stream / "F11" = Function / 그 외 = 이름, SubName, 쌍 이름 포함 (대소문자 무시)
        private string _filter = string.Empty;
        private bool IsFiltering => _filter.Trim() != string.Empty;

        private TextBox BuildSearchBox()
        {
            var box = Text(string.Empty, v => { _filter = v; RenderDefines(); }, 260,
                "검색 : S6F11 / S6 / F11 / 이름", "검색 (S6F11, S6, F11, 이름)");
            return box;
        }

        private bool MatchesFilter(HsmsMsgPair pair)
        {
            var q = Regex.Replace(_filter, @"\s+", string.Empty);
            if (q == string.Empty) return true;

            var sf = Regex.Match(q, @"^(?:S(?<s>\d+))?(?:F(?<f>\d+))?$", RegexOptions.IgnoreCase);
            if (sf.Success == true && (sf.Groups["s"].Success == true || sf.Groups["f"].Success == true))
            {
                bool Match(HsmsJson? m) => m is not null
                    && (sf.Groups["s"].Success == false || m.Stream == int.Parse(sf.Groups["s"].Value))
                    && (sf.Groups["f"].Success == false || m.Function == int.Parse(sf.Groups["f"].Value));
                return Match(pair.Primary) == true || Match(pair.Secondary) == true;
            }

            bool Has(string text) => text.Contains(_filter.Trim(), StringComparison.OrdinalIgnoreCase) == true;
            return Has(pair.Name) == true
                || Has(pair.Primary.Name) == true || Has(pair.Primary.SubName) == true
                || (pair.Secondary is not null && (Has(pair.Secondary.Name) == true || Has(pair.Secondary.SubName) == true));
        }

        #endregion

        #region Hidden Defines

        // 펼친 Hidden Defines 노드 (그룹 단위, 기본 접힘)
        private readonly HashSet<HsmsMsgDefine> _hiddenOpen = new HashSet<HsmsMsgDefine>(ReferenceEqualityComparer.Instance);

        // 그룹 끝의 "Hidden Defines (n)" 노드. 쌍을 여기에 드래그하면 숨김
        private UIElement HiddenNode(int g, int hiddenCount)
        {
            var group = _defines[g];
            bool open = _hiddenOpen.Contains(group);
            var content = RowContent(
                Toggle(open, () => { if (_hiddenOpen.Remove(group) == false) _hiddenOpen.Add(group); RenderDefines(); }, hiddenCount > 0),
                new TextBlock { Text = $"Hidden Defines ({hiddenCount})", Foreground = MutedBrush, FontStyle = FontStyles.Italic, VerticalAlignment = VerticalAlignment.Center });
            var node = Item(content, false, () => { if (_hiddenOpen.Remove(group) == false) _hiddenOpen.Add(group); RenderDefines(); }, 12);
            node.ToolTip = "자주 볼 필요 없는 쌍을 여기로 드래그해서 숨김 (매칭, 자동 응답에는 그대로 사용)";
            MakeDropTarget(node, data => data is PairDrag, (data, _) => MovePair((PairDrag)data, g, group.Pairs.Count, true));
            return node;
        }

        private void SetHidden(int g, int p, bool hidden)
        {
            _defines[g].Pairs[p].IsHidden = hidden;
            SetStatus(hidden == true ? "Hidden Defines로 이동" : "숨김 해제");
            RenderDefines();
        }

        #endregion

        #region 드래그로 순서 변경

        private const string DragFormat = "HsmsDefineTreeDrag";
        private sealed record PairDrag(int G, int P);
        private sealed record FieldDrag(int G, int P, string Side, HsmsField Field, HsmsField Parent);

        // 왼쪽 버튼을 누른 채 일정 거리 이상 움직이면 드래그 시작
        private static void MakeDragSource(FrameworkElement element, Func<object> payload)
        {
            Point? start = null;
            element.PreviewMouseLeftButtonDown += (_, e) => start = e.GetPosition(null);
            element.PreviewMouseLeftButtonUp += (_, _) => start = null;
            element.PreviewMouseMove += (_, e) =>
            {
                if (start is null || e.LeftButton != MouseButtonState.Pressed) return;
                var delta = e.GetPosition(null) - start.Value;
                if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                start = null;
                DragDrop.DoDragDrop(element, new DataObject(DragFormat, payload()), DragDropEffects.Move);
            };
        }

        // 놓을 수 있는 곳 : 위쪽 절반 = 앞에, 아래쪽 절반 = 뒤에 (파란 선으로 표시)
        private static void MakeDropTarget(Border target, Func<object, bool> accepts, Action<object, bool> drop)
        {
            target.AllowDrop = true;
            var indicator = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
            void Clear() { target.BorderThickness = new Thickness(0); }
            bool IsAfter(DragEventArgs e) => e.GetPosition(target).Y > target.ActualHeight / 2;

            target.DragOver += (_, e) =>
            {
                var data = e.Data.GetData(DragFormat);
                if (data is null || accepts(data) == false)
                {
                    e.Effects = DragDropEffects.None;
                    Clear();
                }
                else
                {
                    e.Effects = DragDropEffects.Move;
                    target.BorderBrush = indicator;
                    target.BorderThickness = IsAfter(e) == true ? new Thickness(0, 0, 0, 2) : new Thickness(0, 2, 0, 0);
                }
                e.Handled = true;
            };
            target.DragLeave += (_, _) => Clear();
            target.Drop += (_, e) =>
            {
                Clear();
                var data = e.Data.GetData(DragFormat);
                if (data is not null && accepts(data) == true) drop(data, IsAfter(e));
                e.Handled = true;
            };
        }

        // 쌍 이동 (다른 그룹으로도 가능). hidden : 옮긴 뒤 숨김 여부
        private void MovePair(PairDrag src, int toG, int toIndex, bool hidden)
        {
            var pair = _defines[src.G].Pairs[src.P];
            _defines[src.G].Pairs.RemoveAt(src.P);
            if (src.G == toG && src.P < toIndex) toIndex--;
            toIndex = Math.Clamp(toIndex, 0, _defines[toG].Pairs.Count);
            pair.IsHidden = hidden;
            _defines[toG].Pairs.Insert(toIndex, pair);
            SetStatus(hidden == true ? "Hidden Defines로 이동" : "쌍 이동");
            Pick(toG, toIndex, "primary");
        }

        // 아이템 이동 : target 앞/뒤(같은 레벨)에. target이 최상위(Body)면 그 안의 마지막에
        private void DropField(FieldDrag src, int g, int p, string side, HsmsField target, HsmsField? targetParent, bool after)
        {
            HsmsField parent;
            int index;
            if (targetParent is null)
            {
                if (target.IsList == false) return;
                parent = target;
                index = target.Fields.Count;
            }
            else
            {
                parent = targetParent;
                index = parent.Fields.IndexOf(target) + (after == true ? 1 : 0);
            }

            int srcBefore = src.Parent.Fields.Count;
            int srcIndex = src.Parent.Fields.IndexOf(src.Field);
            if (srcIndex < 0) return;
            src.Parent.Fields.RemoveAt(srcIndex);
            if (ReferenceEquals(src.Parent, parent) == true && srcIndex < index) index--;

            int dstBefore = parent.Fields.Count;
            parent.Fields.Insert(Math.Clamp(index, 0, parent.Fields.Count), src.Field);
            if (ReferenceEquals(src.Parent, parent) == false)
            {
                SyncListCount(src.Parent, srcBefore);
                SyncListCount(parent, dstBefore);
            }
            SetStatus("아이템 이동");
            PickField(g, p, side, src.Field);
        }

        // 아이템을 자기 자신이나 하위로 옮기지 않도록
        private static bool IsSelfOrDescendant(HsmsField node, HsmsField target) =>
            ReferenceEquals(node, target) == true || node.Fields.Any(c => IsSelfOrDescendant(c, target) == true);

        #endregion
    }
}
