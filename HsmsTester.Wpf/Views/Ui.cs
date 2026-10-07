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
        public const string PathHint = "Body 기준 Fields 인덱스를 / 로 연결. \"\" = Body, \"1/0\" = Body.Fields[1].Fields[0]";

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

        public static TextBox Text(string value, Action<string> onChange, double width = 120, string? tooltip = null)
        {
            var box = new TextBox { Text = value, Width = width, ToolTip = tooltip, VerticalContentAlignment = VerticalAlignment.Center };
            box.TextChanged += (_, _) => onChange(box.Text);
            return box;
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

        public static CheckBox Check(string text, bool value, Action<bool> onChange)
        {
            var check = new CheckBox { Content = text, IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
            check.Click += (_, _) => onChange(check.IsChecked == true);
            return check;
        }

        public static ComboBox Combo<T>(IEnumerable<T> items, T selected, Action<T> onChange)
        {
            var combo = new ComboBox { ItemsSource = items.ToList(), SelectedItem = selected, MinWidth = 90 };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is T v) onChange(v);
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
            dock.Children.Add(new TextBlock { Text = text, Foreground = foreground ?? Brushes.Black, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
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
                    Check("AutoResponse", msg.IsAutoResponse, v => msg.IsAutoResponse = v)));
                root.Children.Add(Row(
                    Label("Name"), Text(msg.Name, v => msg.Name = v, 150),
                    Label("SubName"), Text(msg.SubName, v => msg.SubName = v, 220)));

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
                Text(field.Name, v => field.Name = v, 110, "name"));

            if (field.IsList == true)
            {
                row.Children.Add(Label("count", "0 = List N (가변)"));
                row.Children.Add(Num(field.Count, v => field.Count = v, 0, int.MaxValue, "0 = List N (가변)"));
                row.Children.Add(Button("+ item", () => { field.Fields.Add(NewField()); rerender(); }));
            }
            else
            {
                var hint = field.Type is eHsmsDataType.ASCII or eHsmsDataType.JIS8 ? "text" : "value (여러 개는 공백 구분)";
                row.Children.Add(Text(ValueToText(field.Value), v => field.Value = TextToValue(field.Type, v), 220, hint));
            }
            if (onRemove is not null) row.Children.Add(DangerButton(onRemove));
            panel.Children.Add(row);

            if (field.IsList == true)
            {
                foreach (var item in field.Fields.ToList())
                {
                    panel.Children.Add(FieldEditor(item, () => { field.Fields.Remove(item); rerender(); }, depth + 1, rerender));
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
