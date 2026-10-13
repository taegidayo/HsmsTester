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
                _conds.Add(new HsmsConditionalDefine { Name = "NewRule", Group = SelectedGroup(), Stream = 1, Function = 1, Reply = NewMsg() });
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
            _savedJson = JsonSerializer.Serialize(_conds, JsonOptions);     // 불러온 내용은 다시 저장하지 않음
            _sel = 0;
            Render();
            _setStatus($"조건 응답 불러오기: {_conds.Count} rules");
        }

        // /api/conditionalDefineSave 와 동일
        // 수정한 규칙은 바로 저장 (MainWindow 자동 저장 타이머에서 1초마다 호출). 바뀌었을 때만 저장
        private string _savedJson = string.Empty;

        public void AutoSave()
        {
            var json = JsonSerializer.Serialize(_conds, JsonOptions);
            if (json == _savedJson) return;
            _savedJson = json;
            if (HsmsManager.Instance.SaveConditionalDefines(Clone(_conds)) == false) _setStatus("조건 응답 자동 저장 실패 (로그 확인)");
        }

        private void Save()
        {
            var ok = HsmsManager.Instance.SaveConditionalDefines(Clone(_conds));
            _setStatus(ok == true ? $"조건 응답 저장: {_conds.Count} rules" : "조건 응답 저장 실패 (로그 확인)");
        }

        // 사용(IsSelected) 그룹 이름. 그룹이 없으면 "" (공통)
        private string SelectedGroup() => _getDefines().FirstOrDefault(d => d.IsSelected == true)?.Name ?? string.Empty;

        // 목록 그룹 순서 : 공통, Message Defines 그룹 순서, 그 외(없는 그룹)
        private List<string> GroupOrder()
        {
            var names = _getDefines().Select(d => d.Name).ToList();
            return names.Prepend(string.Empty).Concat(_conds.Select(r => r.Group)).Distinct().ToList();
        }

        private void Render()
        {
            _list.Children.Clear();
            var names = _getDefines().Select(d => d.Name).ToList();
            var selectedGroup = SelectedGroup();
            foreach (var group in GroupOrder())
            {
                var indexes = Enumerable.Range(0, _conds.Count).Where(i => _conds[i].Group == group).ToList();
                if (indexes.Count == 0) continue;
                var note = group == string.Empty ? " (항상 동작)" : group == selectedGroup ? " ● 사용 중" : names.Contains(group) == false ? " (없는 그룹 : 동작 안 함)" : " (동작 안 함)";
                _list.Children.Add(Row(Header(group == string.Empty ? "공통" : group), Muted(note)));
                foreach (var i in indexes) AddRuleItem(i);
            }
            _editor.Content = _sel < _conds.Count ? RuleEditor(_conds[_sel]) : Muted("Rule을 추가하거나 선택하세요");
        }

        private void AddRuleItem(int index)
        {
            var r = _conds[index];
            var title = $"{(r.IsPeriodic == true ? $"[주기 {r.EffectiveIntervalMs}ms]" : r.IsSelectedTrigger == true ? "[Selected]" : $"S{r.Stream}F{r.Function}")} {r.Name}{(IsReplyRule(r) == true ? " ↩" : string.Empty)}";
            var content = ItemContent(title, r.Enabled == true ? null : MutedBrush, DangerButton(() =>
            {
                _conds.RemoveAt(index);
                _sel = 0;
                Render();
            }));
            // 규칙 적용 여부 (Enabled). 체크를 바꿔도 선택은 바뀌지 않음
            var enabled = new CheckBox { IsChecked = r.Enabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), ToolTip = "규칙 적용 (체크 해제 시 동작 안 함)" };
            enabled.Click += (_, e) =>
            {
                e.Handled = true;
                r.Enabled = enabled.IsChecked == true;
                Render();
            };
            DockPanel.SetDock(enabled, Dock.Left);
            content.Children.Insert(0, enabled);
            _list.Children.Add(Item(content, index == _sel, () => { _sel = index; Render(); }));
        }

        // 받은 메시지의 응답(같은 S, F + 1)을 보내는 규칙 : 자동 응답 대신 보내지고 받은 SystemByte 사용
        private static bool IsReplyRule(HsmsConditionalDefine r) =>
            r.IsPeriodic == false && r.IsSelectedTrigger == false && r.Reply.Stream == r.Stream && r.Reply.Function == r.Function + 1;

        private UIElement RuleEditor(HsmsConditionalDefine rule)
        {
            var panel = new StackPanel();
            panel.Children.Add(Header("Rule"));
            panel.Children.Add(Row(
                Label("Name"), Text(rule.Name, v => rule.Name = v, 220, hint: "규칙 이름"),
                Check("Enabled", rule.Enabled, v => { rule.Enabled = v; Render(); })));

            // Group : 사용 그룹일 때만 동작. "" = 공통 (항상 동작)
            var groups = GroupOrder();
            var groupCombo = new ComboBox { MinWidth = 220, ItemsSource = groups.Select(g => g == string.Empty ? "(공통 : 항상 동작)" : g).ToList(), SelectedIndex = groups.IndexOf(rule.Group) };
            groupCombo.SelectionChanged += (_, _) =>
            {
                if (groupCombo.SelectedIndex < 0) return;
                rule.Group = groups[groupCombo.SelectedIndex];
                Dispatcher.BeginInvoke(Render);     // 드롭다운이 닫힌 뒤 다시 그림 (Ui.Combo 참고)
            };
            panel.Children.Add(Row(Label("Group"), groupCombo, Muted("Message Defines의 사용 그룹일 때만 동작")));

            // Trigger
            var triggerTitle = Row(Header("Trigger"));
            if (rule.IsPeriodic == true) triggerTitle.Children.Add(Muted("주기 전송 → Selected 상태인 동안 주기마다 Reply 전송 (S/F, 조건 무시)"));
            else if (rule.IsSelectedTrigger == true) triggerTitle.Children.Add(Muted("Selected 조건 → Select 완료 시 전송 (S/F, 다른 조건 무시)"));
            panel.Children.Add(triggerTitle);

            // 주기 전송 (ms) : 0 = 사용 안 함, 최소 500ms. 바뀌면 목록/설명을 다시 그림 (포커스 이동 시)
            var interval = Text(rule.IntervalMs.ToString(), _ => { }, 80, "주기 (ms). 0 = 사용 안 함, 최소 500", "0 = 사용 안 함");
            var intervalNote = Muted(string.Empty);
            void UpdateIntervalNote(int v) =>
                intervalNote.Text = v == 0 ? "0 = 사용 안 함" : v < HsmsConditionalDefine.MIN_INTERVAL_MS ? $"최소 {HsmsConditionalDefine.MIN_INTERVAL_MS}ms로 전송됨 (저장 시 {HsmsConditionalDefine.MIN_INTERVAL_MS}로 맞춤)" : $"{v}ms마다 전송";
            interval.TextChanged += (_, _) =>
            {
                if (int.TryParse(interval.Text, out var v) == true && v >= 0)
                {
                    rule.IntervalMs = v;
                    UpdateIntervalNote(v);
                }
            };
            interval.LostKeyboardFocus += (_, _) => Render();
            UpdateIntervalNote(rule.IntervalMs);
            panel.Children.Add(Row(Label("주기 (ms)"), interval, intervalNote));

            // 주기를 쓰면 주기로만 전송 : 트리거 S/F, 조건은 사용하지 않으므로 숨김 (입력해 둔 조건은 지우지 않음)
            if (rule.IsPeriodic == true)
            {
                panel.Children.Add(Muted(rule.Conditions.Count > 0
                    ? $"주기로만 전송 (트리거 S/F, 조건 {rule.Conditions.Count}개는 사용 안 함. 주기를 0으로 하면 다시 사용)"
                    : "주기로만 전송 (트리거 S/F, 조건 사용 안 함)"));
            }
            else
            {
            panel.Children.Add(Row(
                Label("S"), Num(rule.Stream, v => rule.Stream = v, 0, 127),
                Label("F"), Num(rule.Function, v => rule.Function = v, 0, 255),
                Button("+ Condition", () => { rule.Conditions.Add(new HsmsCondition()); Render(); })));
            if (rule.Conditions.Count == 0) panel.Children.Add(Muted("조건 없음 → S/F만 일치하면 응답 (조건은 모두 만족해야 발동, AND)"));
            foreach (var c in rule.Conditions.ToList())
            {
                panel.Children.Add(Row(
                    Text(c.Path, v => c.Path = v, 100, $"path (예: 1/0, RPTID, 2/*/0). {PathHint}. 조건은 여러 항목 중 하나라도 만족하면 발동"),
                    Combo(Enum.GetValues<ConditionalType>(), c.Op, v => { c.Op = v; Render(); }),
                    Text(c.Value, v => c.Value = v, 200, "value (숫자끼리는 숫자 비교)"),
                    DangerButton(() => { rule.Conditions.Remove(c); Render(); })));
            }
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
                    Dispatcher.BeginInvoke(Render);     // 드롭다운이 닫힌 뒤 다시 그림 (Ui.Combo 참고)
                }
            };
            panel.Children.Add(Row(Header("Reply"), copy));
            if (IsReplyRule(rule) == true) panel.Children.Add(Muted("응답(Secondary) : 받은 메시지의 SystemByte로 전송, 같은 쌍의 자동 응답(AutoResponse) 대신 보냄. T3 Timeout Check 중에는 S6F11에 보내지 않음"));
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
