using HsmsTester.Hsms.Define;
using HsmsTester.Manager;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using static HsmsProtocol.Views.Ui;

namespace HsmsProtocol.Views
{
    // HSMS 설정 화면 (웹 ConfigPage와 동일). HsmsConfigData 항목을 그대로 폼으로 표시 (bool → 체크박스, 나머지 → 텍스트)
    internal class ConfigView : UserControl
    {
        private static readonly Dictionary<string, string> Hints = new()
        {
            [nameof(HsmsConfigData.Host)] = "Host(true) / Equipment(false)",
            [nameof(HsmsConfigData.Active)] = "Active: 접속 시도 / Passive: 접속 대기",
            [nameof(HsmsConfigData.T3)] = "Reply Timeout (초) : Primary 전송 후 Secondary 대기",
            [nameof(HsmsConfigData.T5)] = "Connect Separation Timeout (초)",
            [nameof(HsmsConfigData.T6)] = "Control Transaction Timeout (초) : Select.req 후 Select.rsp 대기",
            [nameof(HsmsConfigData.T7)] = "Not Selected Timeout (초)",
            [nameof(HsmsConfigData.T8)] = "Network Intercharacter Timeout (초)",
            [nameof(HsmsConfigData.KeepDays)] = "로그 보관 일수",
        };

        private readonly Action _onSaved;
        private HsmsConfigData _form = new HsmsConfigData();
        private readonly TextBlock _message = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        private readonly List<string> _parseErrors = new List<string>();

        public ConfigView(Action onSaved)
        {
            _onSaved = onSaved;
            Load();
        }

        public void Load()
        {
            _form = HsmsManager.Instance.Config.ToData();
            _message.Text = string.Empty;
            Render();
        }

        private void Render()
        {
            _parseErrors.Clear();
            var grid = new Grid { Margin = new Thickness(8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            int row = 0;
            void AddRow(UIElement a, UIElement b, UIElement c)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                foreach (var (el, col) in new[] { (a, 0), (b, 1), (c, 2) })
                {
                    if (el is FrameworkElement fe) fe.Margin = new Thickness(0, 2, 8, 2);
                    Grid.SetRow(el, row);
                    Grid.SetColumn(el, col);
                    grid.Children.Add(el);
                }
                row++;
            }

            AddRow(Header("HSMS Config"), new TextBlock(), new TextBlock());
            foreach (var prop in typeof(HsmsConfigData).GetProperties())
            {
                var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var value = prop.GetValue(_form);
                UIElement editor;
                if (type == typeof(bool))
                {
                    editor = Check(string.Empty, value is true, v => prop.SetValue(_form, v));
                }
                else
                {
                    editor = Text(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, v =>
                    {
                        _parseErrors.Remove(prop.Name);
                        if (type == typeof(string)) { prop.SetValue(_form, v); return; }
                        try { prop.SetValue(_form, Convert.ChangeType(v, type, CultureInfo.InvariantCulture)); }
                        catch (Exception) { _parseErrors.Add(prop.Name); }
                    }, 200);
                }
                AddRow(Label(prop.Name, Hints.GetValueOrDefault(prop.Name)), editor, Muted(Hints.GetValueOrDefault(prop.Name) ?? string.Empty));
            }

            var actions = Row(Button("Save", Save), Button("Reset", Load), _message);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(actions, row);
            Grid.SetColumnSpan(actions, 3);
            grid.Children.Add(actions);

            Content = new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        // /api/configSave 와 동일 : 검증 후 적용하고 새 설정으로 재연결
        private void Save()
        {
            if (_parseErrors.Count > 0)
            {
                _message.Text = $"저장 실패: 숫자 형식 오류 ({string.Join(", ", _parseErrors)})";
                return;
            }

            var errors = HsmsManager.Instance.Config.Apply(_form);
            if (errors.Count > 0)
            {
                _message.Text = $"저장 실패: {string.Join(", ", errors)}";
                return;
            }

            HsmsManager.Instance.Start();
            _form = HsmsManager.Instance.Config.ToData();
            _onSaved();
            _message.Text = "저장됨 (새 설정으로 재연결)";
        }
    }
}
