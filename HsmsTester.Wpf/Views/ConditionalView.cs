using HsmsTester.Hsms.Define;
using HsmsTester.Hsms.Struct;
using HsmsTester.Manager;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using static HsmsProtocol.Views.Ui;

namespace HsmsProtocol.Views
{
    // 조건 응답 화면 (웹 ConditionalPage와 동일). 편집은 복사본에 하고 저장 시 HsmsManager에 반영
    internal class ConditionalView : UserControl
    {
        private readonly Func<List<HsmsMsgDefine>> _getDefines;     // "정의에서 복사"용 (Main 화면에서 편집 중인 정의)
        private readonly Action<string> _setStatus;

        private List<HsmsConditionalDefine> _conds = new List<HsmsConditionalDefine>();
        private int _sel = 0;

        private readonly StackPanel _list = new StackPanel();
        private readonly ContentControl _editor = new ContentControl();

        public ConditionalView(Func<List<HsmsMsgDefine>> getDefines, Action<string> setStatus)
        {
            _getDefines = getDefines;
            _setStatus = setStatus;

            var left = new StackPanel { Margin = new Thickness(8) };
            left.Children.Add(Header("Conditional Rules"));
            left.Children.Add(Row(Button("Reload", Reload), Button("Save", Save)));
            left.Children.Add(Row(Button("+ Rule", () =>
            {
                _conds.Add(new HsmsConditionalDefine { Name = "NewRule", Stream = 1, Function = 1, Reply = NewMsg() });
                _sel = _conds.Count - 1;
                Render();
            })));
            left.Children.Add(_list);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var leftScroll = new ScrollViewer { Content = left, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var rightScroll = new ScrollViewer { Content = _editor, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8) };
            Grid.SetColumn(rightScroll, 1);
            grid.Children.Add(leftScroll);
            grid.Children.Add(rightScroll);
            Content = grid;

            Reload();
        }

        // /api/conditionalDefineLoad 와 동일
        public void Reload()
        {
            _conds = Clone(HsmsManager.Instance.HsmsConditionalDefines);
            _sel = 0;
            Render();
            _setStatus($"조건 응답 불러오기: {_conds.Count} rules");
        }

        // /api/conditionalDefineSave 와 동일
        private void Save()
        {
            var ok = HsmsManager.Instance.SaveConditionalDefines(Clone(_conds));
            _setStatus(ok == true ? $"조건 응답 저장: {_conds.Count} rules" : "조건 응답 저장 실패 (로그 확인)");
        }

        private void Render()
        {
            _list.Children.Clear();
            for (int i = 0; i < _conds.Count; i++)
            {
                int index = i;
                var r = _conds[i];
                var title = $"{(r.IsSelectedTrigger == true ? "[Selected]" : $"S{r.Stream}F{r.Function}")} {r.Name}";
                var content = ItemContent(title, r.Enabled == true ? null : MutedBrush, DangerButton(() =>
                {
                    _conds.RemoveAt(index);
                    _sel = 0;
                    Render();
                }));
                _list.Children.Add(Item(content, index == _sel, () => { _sel = index; Render(); }));
            }
            _editor.Content = _sel < _conds.Count ? RuleEditor(_conds[_sel]) : Muted("Rule을 추가하거나 선택하세요");
        }

        private UIElement RuleEditor(HsmsConditionalDefine rule)
        {
            var panel = new StackPanel();
            panel.Children.Add(Header("Rule"));
            panel.Children.Add(Row(
                Label("Name"), Text(rule.Name, v => rule.Name = v, 220),
                Check("Enabled", rule.Enabled, v => { rule.Enabled = v; Render(); })));

            // Trigger
            var triggerTitle = Row(Header("Trigger"));
            if (rule.IsSelectedTrigger == true) triggerTitle.Children.Add(Muted("Selected 조건 → Select 완료 시 전송 (S/F, 다른 조건 무시)"));
            panel.Children.Add(triggerTitle);
            panel.Children.Add(Row(
                Label("S"), Num(rule.Stream, v => rule.Stream = v, 0, 127),
                Label("F"), Num(rule.Function, v => rule.Function = v, 0, 255),
                Button("+ Condition", () => { rule.Conditions.Add(new HsmsCondition()); Render(); })));
            if (rule.Conditions.Count == 0) panel.Children.Add(Muted("조건 없음 → S/F만 일치하면 응답 (조건은 모두 만족해야 발동, AND)"));
            foreach (var c in rule.Conditions.ToList())
            {
                panel.Children.Add(Row(
                    Text(c.Path, v => c.Path = v, 100, $"path (예: 1/0). {PathHint}"),
                    Combo(Enum.GetValues<ConditionalType>(), c.Op, v => { c.Op = v; Render(); }),
                    Text(c.Value, v => c.Value = v, 200, "value (숫자끼리는 숫자 비교)"),
                    DangerButton(() => { rule.Conditions.Remove(c); Render(); })));
            }

            // Reply : 메시지 정의에서 복사하거나 직접 편집
            var copy = new ComboBox { MinWidth = 260 };
            copy.Items.Add(new ComboBoxItem { Content = "정의에서 복사...", IsSelected = true });
            foreach (var group in _getDefines())
            {
                copy.Items.Add(new ComboBoxItem { Content = $"── {group.Name}", IsEnabled = false });
                foreach (var pair in group.Pairs)
                {
                    copy.Items.Add(new ComboBoxItem { Content = $"   {MsgLabel(pair.Primary)}", Tag = pair.Primary });
                    if (pair.Secondary is not null) copy.Items.Add(new ComboBoxItem { Content = $"   ↳ {MsgLabel(pair.Secondary)}", Tag = pair.Secondary });
                }
            }
            copy.SelectionChanged += (_, _) =>
            {
                if (copy.SelectedItem is ComboBoxItem { Tag: HsmsJson m })
                {
                    rule.Reply = Clone(m);
                    Render();
                }
            };
            panel.Children.Add(Row(Header("Reply"), copy));
            panel.Children.Add(new Border { Child = MsgEditor(rule.Reply), Margin = new Thickness(8, 0, 0, 0) });

            // Actions
            panel.Children.Add(Row(Header("Actions"), Muted("Reply 복사 후 순서대로 적용"),
                Button("+ Action", () => { rule.Actions.Add(new HsmsReplyAction()); Render(); })));
            panel.Children.Add(ActionList(rule.Actions, Render));

            var json = new TextBox { IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), TextWrapping = TextWrapping.NoWrap };
            var expander = new Expander { Header = "JSON", Content = json, Margin = new Thickness(0, 8, 0, 0) };
            expander.Expanded += (_, _) => json.Text = JsonSerializer.Serialize(rule, JsonOptions);
            panel.Children.Add(expander);
            return panel;
        }
    }
}
