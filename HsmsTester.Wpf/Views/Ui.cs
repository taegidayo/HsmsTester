using HsmsTester;
using HsmsTester.Hsms.Define;
using HsmsTester.Hsms.Struct;
using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HsmsProtocol.Views
{
    // 웹(HsmsTester_Web)의 편집기와 같은 화면을 코드로 만든다.
    // 라이브러리 모델(HsmsJson 등)은 변경 알림이 없으므로, 구조가 바뀌면(아이템 추가/삭제, 타입 변경) rerender로 다시 그린다.
    internal static class Ui
    {
        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        public static T Clone<T>(T src) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(src, JsonOptions), JsonOptions)!;

        public static readonly Brush MutedBrush = Brushes.Gray;
        public static readonly Brush ActiveBrush = new SolidColorBrush(Color.FromRgb(0xDB, 0xE6, 0xFF));
        public static readonly Brush DangerBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));

        // Body 기준 Fields 인덱스 경로 설명 (HsmsConditionalDefine 주석과 동일)
        public const string PathHint = "Body 기준 Fields 인덱스를 / 로 연결. \"\" = Body, \"1/0\" = Body.Fields[1].Fields[0]. 이름(ex. RPTID) = 하위 전체에서 같은 Name, * = 모든 하위 항목 (Action은 첫 항목)";

        #region 기본 컨트롤

        public static StackPanel Row(params UIElement[] children)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            foreach (var child in children)
            {
                if (child is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 6, 0);
                row.Children.Add(child);
            }
            return row;
        }

        public static TextBlock Label(string text, string? tooltip = null) =>
            new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, ToolTip = tooltip };

        public static TextBlock Muted(string text) =>
            new TextBlock { Text = text, Foreground = MutedBrush, Margin = new Thickness(0, 2, 0, 2), TextWrapping = TextWrapping.Wrap };

        public static TextBlock Header(string text) =>
            new TextBlock { Text = text, FontWeight = FontWeights.Bold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };

        // hint : 비어 있을 때 회색으로 보이는 안내 문구. 없으면 tooltip의 첫 문장 사용
        public static TextBox Text(string value, Action<string> onChange, double width = 120, string? tooltip = null, string? hint = null)
        {
            var box = new TextBox { Text = value, Width = width, ToolTip = tooltip ?? hint, VerticalContentAlignment = VerticalAlignment.Center };
            hint ??= tooltip?.Split(". ")[0];
            if (hint is not null) SetHint(box, hint);
            box.TextChanged += (_, _) => onChange(box.Text);
            return box;
        }

        // WPF TextBox에는 placeholder가 없으므로, 비어 있을 때만 배경에 안내 문구를 그린다
        public static void SetHint(TextBox box, string hint)
        {
            var hintBrush = new VisualBrush(new TextBlock { Text = hint, Foreground = Brushes.Silver, FontStyle = FontStyles.Italic, Margin = new Thickness(3, 0, 0, 0) })
            {
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Center,
                Stretch = Stretch.None,
            };
            void Update() => box.Background = box.Text == string.Empty ? hintBrush : Brushes.White;
            box.TextChanged += (_, _) => Update();
            Update();
        }

        public static TextBox Num(int value, Action<int> onChange, int min = 0, int max = int.MaxValue, string? tooltip = null)
        {
            var box = new TextBox { Text = value.ToString(), Width = 50, ToolTip = tooltip, VerticalContentAlignment = VerticalAlignment.Center };
            box.TextChanged += (_, _) =>
            {
                if (int.TryParse(box.Text, out var v) == true && v >= min && v <= max) onChange(v);
            };
            return box;
        }

        public static T WithTooltip<T>(T element, string tooltip) where T : FrameworkElement
        {
            element.ToolTip = tooltip;
            return element;
        }

        public static CheckBox Check(string text, bool value, Action<bool> onChange)
        {
            var check = new CheckBox { Content = text, IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
            check.Click += (_, _) => onChange(check.IsChecked == true);
            return check;
        }

        public static ComboBox Combo<T>(IEnumerable<T> items, T selected, Action<T> onChange)
        {
            var combo = new ComboBox { ItemsSource = items.ToList(), SelectedItem = selected, MinWidth = 90 };
            // 드롭다운이 닫힌 뒤 처리 : SelectionChanged 안에서 콤보를 포함한 화면을 다시 그리면 다음 입력 전까지 갱신이 안 보일 때가 있음
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is T v) combo.Dispatcher.BeginInvoke(() => onChange(v));
            };
            return combo;
        }

        public static Button Button(string text, Action onClick, string? tooltip = null)
        {
            var button = new Button { Content = text, Padding = new Thickness(8, 2, 8, 2), ToolTip = tooltip };
            button.Click += (_, _) => onClick();
            return button;
        }

        public static Button DangerButton(Action onClick, string? tooltip = null)
        {
            var button = Button("✕", onClick, tooltip);
            button.Foreground = DangerBrush;
            button.Padding = new Thickness(5, 0, 5, 0);
            return button;
        }

        // 목록의 클릭 가능한 한 줄 (웹의 .item)
        public static Border Item(UIElement content, bool active, Action onClick, double indent = 0)
        {
            var border = new Border
            {
                Child = content,
                Padding = new Thickness(4 + indent, 2, 4, 2),
                Background = active == true ? ActiveBrush : Brushes.Transparent,
                Cursor = Cursors.Hand,
                CornerRadius = new CornerRadius(3),
            };
            border.MouseLeftButtonUp += (_, _) => onClick();
            return border;
        }

        // 왼쪽 텍스트 + 오른쪽 버튼 한 줄
        public static DockPanel ItemContent(string text, Brush? foreground, params UIElement[] right)
        {
            var dock = new DockPanel { LastChildFill = true };
            foreach (var r in right)
            {
                DockPanel.SetDock(r, Dock.Right);
                dock.Children.Add(r);
            }
            dock.Children.Add(new TextBlock { Text = text, Foreground = foreground ?? Brushes.Black, VerticalAlignment = VerticalAlignment.Center });
            return dock;
        }

        // 우클릭 메뉴. (메뉴 이름, 동작) 목록, 이름이 "-"이면 구분선
        public static ContextMenu Menu(params (string Header, Action? OnClick)[] items)
        {
            var menu = new ContextMenu();
            foreach (var (header, onClick) in items)
            {
                if (header == "-")
                {
                    menu.Items.Add(new Separator());
                    continue;
                }
                var item = new MenuItem { Header = header };
                if (onClick is not null) item.Click += (_, _) => onClick();
                menu.Items.Add(item);
            }
            return menu;
        }

        // 접기/펴기 토글 (▼ 펼침 / ▶ 접힘). 클릭이 목록 항목 선택으로 전달되지 않도록 Handled 처리
        public static TextBlock Toggle(bool expanded, Action onToggle, bool visible = true)
        {
            var toggle = new TextBlock
            {
                Text = expanded == true ? "▼" : "▶",
                Width = 14,
                FontSize = 10,
                Foreground = MutedBrush,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = visible == true ? Visibility.Visible : Visibility.Hidden,
            };
            toggle.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onToggle();
            };
            return toggle;
        }

        // 메시지 목록 한 항목 : 1줄 S/F/W + Name, 2줄 SubName (없으면 생략)
        public static DockPanel MsgItemContent(HsmsJson m, string prefix, UIElement left, params UIElement[] right)
        {
            var dock = new DockPanel { LastChildFill = true };
            foreach (var r in right)
            {
                DockPanel.SetDock(r, Dock.Right);
                dock.Children.Add(r);
            }
            DockPanel.SetDock(left, Dock.Left);
            dock.Children.Add(left);

            var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            lines.Children.Add(new TextBlock
            {
                Text = $"{prefix}S{m.Stream}F{m.Function}{(m.Wbit == true ? " W" : string.Empty)} {m.Name}{(m.IsAutoResponse == true ? " ⟲" : string.Empty)}",
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            if (m.SubName != string.Empty)
            {
                lines.Children.Add(new TextBlock
                {
                    Text = m.SubName,
                    FontSize = 11,
                    Foreground = MutedBrush,
                    Margin = new Thickness(prefix == string.Empty ? 0 : 12, 0, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = m.SubName,
                });
            }
            dock.Children.Add(lines);
            return dock;
        }

        #endregion

        #region 값 변환 (웹의 valueToText / textToValue)

        // Value는 단일 값 또는 배열. 화면에서는 공백으로 구분된 텍스트로 편집
        public static string ValueToText(object? value) => value switch
        {
            null => string.Empty,
            string s => s,
            JsonElement { ValueKind: JsonValueKind.Array } e => string.Join(" ", e.EnumerateArray().Select(x => ValueToText(x))),
            JsonElement { ValueKind: JsonValueKind.String } e => e.GetString() ?? string.Empty,
            JsonElement { ValueKind: JsonValueKind.Null } => string.Empty,
            JsonElement e => e.GetRawText(),
            IEnumerable list => string.Join(" ", list.Cast<object>().Select(ValueToText)),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };

        public static object TextToValue(eHsmsDataType type, string text)
        {
            if (type is eHsmsDataType.ASCII or eHsmsDataType.JIS8) return text;
            var parts = text.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1 ? parts[0] : parts.ToList();
        }

        #endregion

        #region 편집기

        // LIST 항목이 바뀐 뒤 count 맞춤 : 고정 List는 항목 수, 가변(List N, count 0)은 유지. 비어 있던 List에 처음 넣으면 고정으로 시작
        public static void SyncListCount(HsmsField list, int previousItemCount)
        {
            if (list.Count != 0 || previousItemCount == 0) list.Count = list.Fields.Count;
        }

        public static HsmsJson NewMsg() => new HsmsJson { Stream = 1, Function = 1, Wbit = true };
        public static HsmsField NewField() => new HsmsField { Type = eHsmsDataType.ASCII, Value = string.Empty };

        public static string MsgLabel(HsmsJson m) =>
            $"S{m.Stream}F{m.Function}{(m.Wbit == true ? " W" : string.Empty)} {m.Name}{(m.SubName == string.Empty ? string.Empty : $"-{m.SubName}")}";

        // HsmsJson 편집 (S/F/W, AutoResponse, Name, SubName, Body)
        public static UIElement MsgEditor(HsmsJson msg)
        {
            var root = new StackPanel();
            void Render()
            {
                root.Children.Clear();
                root.Children.Add(Row(
                    Label("S"), Num(msg.Stream, v => msg.Stream = v, 0, 127),
                    Label("F"), Num(msg.Function, v => msg.Function = v, 0, 255),
                    Check("W-bit", msg.Wbit, v => msg.Wbit = v),
                    WithTooltip(Check("AutoResponse", msg.IsAutoResponse, v => msg.IsAutoResponse = v), "같은 쌍의 Secondary로 자동 응답. 조건 응답(Conditional)에 이 메시지의 응답(S, F+1)이 있으면 그 응답을 대신 보냄")));
                root.Children.Add(Row(new TextBlock { Text = "Name", Width = 60, VerticalAlignment = VerticalAlignment.Center }, Text(msg.Name, v => msg.Name = v, 320, hint: "메시지 이름 (예: Are You There Request)")));
                root.Children.Add(Row(new TextBlock { Text = "SubName", Width = 60, VerticalAlignment = VerticalAlignment.Center }, Text(msg.SubName, v => msg.SubName = v, 320, hint: "부가 이름 (같은 S/F 메시지 구분용)")));

                var bodyButton = msg.Body is null
                    ? Button("+ Body", () => { msg.Body = NewField(); Render(); })
                    : Button("Header Only로", () => { msg.Body = null; Render(); });
                root.Children.Add(Row(Header("Body"), bodyButton));

                if (msg.Body is null) root.Children.Add(Muted("Body 없음 (Header만 전송)"));
                else root.Children.Add(FieldEditor(msg.Body, null, 0, Render));
            }
            Render();
            return root;
        }

        // Body 아이템 편집. LIST는 하위 아이템을 한 단계 들여써서 표시
        private static UIElement FieldEditor(HsmsField field, Action? onRemove, int depth, Action rerender)
        {
            var panel = new StackPanel { Margin = new Thickness(depth == 0 ? 0 : 16, 0, 0, 0) };

            var row = Row(
                Combo(Enum.GetValues<eHsmsDataType>(), field.Type, v =>
                {
                    if (field.Type == v) return;
                    field.Type = v;
                    rerender();
                }),
                Text(field.Name, v => field.Name = v, 110, hint: "이름 (SVID 등)"));

            if (field.IsList == true)
            {
                // count는 항목 수로 자동 계산 (수정 불가). 가변(List N)이면 count 0
                if (field.Count != 0) field.Count = field.Fields.Count;
                row.Children.Add(Label($"count {field.Fields.Count}", "항목 수 (자동 계산)"));
                var variable = Check("가변(N)", field.Count == 0, v => { field.Count = v == true ? 0 : field.Fields.Count; rerender(); });
                variable.IsEnabled = field.Fields.Count > 0;
                variable.ToolTip = "List N : 항목 수가 정해지지 않은 리스트 (첫 항목이 반복 틀). 체크하면 count 0으로 저장";
                row.Children.Add(variable);
                row.Children.Add(Button("+ item", () =>
                {
                    int before = field.Fields.Count;
                    field.Fields.Add(NewField());
                    SyncListCount(field, before);
                    rerender();
                }));
            }
            else
            {
                var hint = field.Type is eHsmsDataType.ASCII or eHsmsDataType.JIS8 ? "문자열 값" : "값 (여러 개는 공백 구분)";
                row.Children.Add(Text(ValueToText(field.Value), v => field.Value = TextToValue(field.Type, v), 220, hint));
            }
            if (onRemove is not null) row.Children.Add(DangerButton(onRemove));
            panel.Children.Add(row);

            if (field.IsList == true)
            {
                foreach (var item in field.Fields.ToList())
                {
                    panel.Children.Add(FieldEditor(item, () =>
                    {
                        int before = field.Fields.Count;
                        field.Fields.Remove(item);
                        SyncListCount(field, before);
                        rerender();
                    }, depth + 1, rerender));
                }
            }
            return panel;
        }

        // 조건 응답 Action 목록. Repeat의 itemActions는 한 단계 들여써서 표시
        public static UIElement ActionList(List<HsmsReplyAction> actions, Action rerender, int depth = 0)
        {
            var panel = new StackPanel { Margin = new Thickness(depth == 0 ? 0 : 16, 0, 0, 0) };
            foreach (var action in actions.ToList())
            {
                var row = Row(
                    Combo(Enum.GetValues<ReplyActionType>(), action.Type, v =>
                    {
                        if (action.Type == v) return;
                        action.Type = v;
                        rerender();
                    }),
                    Text(action.Target, v => action.Target = v, 110, $"target : Reply 안의 위치. {PathHint}"));

                if (action.Type == ReplyActionType.Set)
                {
                    row.Children.Add(Text(action.Value, v => action.Value = v, 180, "value (고정값)"));
                }
                else
                {
                    var hint = action.Type == ReplyActionType.Repeat ? "source (List면 항목 수, 아니면 값만큼 반복)" : "source (recv path)";
                    row.Children.Add(Text(action.Source, v => action.Source = v, 180, $"{hint}. {PathHint}"));
                }
                if (action.Type == ReplyActionType.Repeat)
                {
                    row.Children.Add(Button("+ item action", () => { action.ItemActions.Add(new HsmsReplyAction()); rerender(); }, "각 항목 기준 상대경로로 적용"));
                }
                row.Children.Add(DangerButton(() => { actions.Remove(action); rerender(); }));
                panel.Children.Add(row);

                if (action.Type == ReplyActionType.Repeat && action.ItemActions.Count > 0)
                {
                    panel.Children.Add(ActionList(action.ItemActions, rerender, depth + 1));
                }
            }
            return panel;
        }

        #endregion
    }
}
