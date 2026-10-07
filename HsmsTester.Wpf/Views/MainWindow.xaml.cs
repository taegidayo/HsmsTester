using HsmsProtocol.Views;
using HsmsTester.API;
using HsmsTester.Hsms.Define;
using HsmsTester.Hsms.Struct;
using HsmsTester.Manager;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using static HsmsProtocol.Views.Ui;

namespace HsmsProtocol
{
    /// <summary>
    /// 웹(HsmsTester_Web)과 같은 화면. API 호출 대신 HsmsManager 등을 직접 호출한다.
    /// </summary>
    public partial class MainWindow : Window
    {
        private static HsmsManager Manager => HsmsManager.Instance;

        // 상단 상태 표시
        private readonly TextBlock _configText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "Config 다시 불러오기" };
        private readonly TextBlock _connText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _selectedText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray };

        // Main 화면 : 메시지 정의(편집 중인 복사본)와 편집 중인 메시지
        private List<HsmsMsgDefine> _defines = new List<HsmsMsgDefine>();
        private (int G, int P, string Side)? _sel = null;           // Side : "primary" | "secondary"
        private HsmsJson _msg = NewMsg();
        private readonly StackPanel _defineList = new StackPanel();
        private readonly ContentControl _msgEditor = new ContentControl();

        // 로그
        private readonly TextBox _log = new TextBox
        {
            IsReadOnly = true, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        private Guid _logSubscription;

        private readonly ConditionalView _conditionalView;
        private readonly ConfigView _configView;

        public MainWindow()
        {
            ApiListener.Instance.StartAsync(40000);     // 웹도 같이 사용할 수 있도록 API 유지

            InitializeComponent();

            _conditionalView = new ConditionalView(() => _defines, SetStatus);
            _configView = new ConfigView(UpdateConfigText);

            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "Main", Content = BuildMainPage() });
            tabs.Items.Add(new TabItem { Header = "Conditional", Content = _conditionalView });
            tabs.Items.Add(new TabItem { Header = "Config", Content = _configView });

            var header = BuildHeader();
            DockPanel.SetDock(header, Dock.Top);
            Root.Children.Add(header);
            Root.Children.Add(tabs);

            UpdateConfigText();
            LoadDefines();
            StartStatusPolling();
            Loaded += async (_, _) => await ReadLogsAsync();
            Closed += (_, _) => LogBroadcaster.Instance.Unsubscribe(_logSubscription);
        }

        private void SetStatus(string text) => _status.Text = text;

        private void Run(string label, Func<string> action)
        {
            try { SetStatus($"{label}: {action()}"); }
            catch (Exception ex) { SetStatus($"{label} 실패: {ex.Message}"); }
        }

        #region 상단

        private UIElement BuildHeader()
        {
            _configText.MouseLeftButtonUp += (_, _) => { UpdateConfigText(); _configView.Load(); };

            var t3 = Check("T3 Timeout Check (S6F11 무응답)", Manager.T3TimeoutCheckFlag, v => Run("T3 Timeout Flag", () =>
            {
                Manager.T3TimeoutCheckFlag = v;     // /api/setT3TimeoutFlag
                return v.ToString();
            }));

            var row = Row(
                new TextBlock { Text = "HSMS Tester", FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center },
                _configText, _connText, _selectedText,
                Button("Connect", () => Run("TCP Start", () => { Manager.Start(); return "ok"; })),        // /api/tcpChange?onOff=true
                Button("Disconnect", () => Run("TCP Stop", () => { Manager.Dispose(); return "ok"; })),   // /api/tcpChange?onOff=false
                t3, _status);
            return new Border { Child = row, Padding = new Thickness(8, 4, 8, 4), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) };
        }

        private void UpdateConfigText()
        {
            var c = Manager.Config.ToData();
            _configText.Text = $"{(c.Active == true ? "Active" : "Passive")} {c.IPAddress}:{c.Port} (Device {c.DeviceID})";
        }

        // ponytail: 2초 폴링, 실시간이 필요하면 IsSelected 변경 이벤트로
        private void StartStatusPolling()
        {
            void Poll()
            {
                bool connected = Manager.IsConnected();
                _connText.Text = connected == true ? "● Connected" : "● Disconnected";
                _connText.Foreground = connected == true ? Brushes.Green : Brushes.Firebrick;
                _selectedText.Text = Manager.IsSelected == true ? "● Selected" : "● Not Selected";
                _selectedText.Foreground = Manager.IsSelected == true ? Brushes.Green : Brushes.Firebrick;
            }
            Poll();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) => Poll();
            timer.Start();
        }

        #endregion

        #region Main 화면

        private UIElement BuildMainPage()
        {
            // 왼쪽 : 메시지 정의
            var left = new StackPanel { Margin = new Thickness(8) };
            left.Children.Add(Header("Message Defines"));
            left.Children.Add(Row(
                Button("Reload", LoadDefines, "현재 정의 다시 불러오기"),
                Button("Import", ImportFiles, "msgDefine.json(교체), .sml / .smd(그룹 추가)"),
                Button("Export", ExportFile),
                Button("Save", () => Run("Define 저장", () =>
                    Manager.SaveMsgDefines(Clone(_defines)) == true ? $"{_defines.Count} groups" : throw new IOException("msgDefine.json 저장 실패 (로그 확인)")))));
            left.Children.Add(Row(Button("+ Group", () =>
            {
                _defines.Add(new HsmsMsgDefine { Name = $"Group{_defines.Count + 1}" });
                RenderDefines();
            })));
            left.Children.Add(_defineList);

            // 가운데 : 메시지 편집
            var json = new TextBox { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.NoWrap };
            var expander = new Expander { Header = "JSON", Content = json, Margin = new Thickness(0, 8, 0, 0) };
            expander.Expanded += (_, _) => json.Text = JsonSerializer.Serialize(_msg, JsonOptions);

            var center = new StackPanel { Margin = new Thickness(8) };
            center.Children.Add(Header("Message"));
            center.Children.Add(_msgEditor);
            center.Children.Add(Row(
                Button("Send", SendMsg),
                Button("정의에 덮어쓰기", () => SaveToDefine(false)),
                Button("새 쌍으로 추가", () => SaveToDefine(true)),
                Button("Secondary로 저장", () => SaveToDefine(false, "secondary"), "선택된 쌍의 Secondary(응답)로 저장"),
                Button("New", () => { _sel = null; SetMsg(NewMsg()); RenderDefines(); })));
            center.Children.Add(expander);

            // 오른쪽 : 로그
            var logHeader = Row(Header("Log"), Button("Clear", () => _log.Clear()));
            DockPanel.SetDock(logHeader, Dock.Top);
            var logPanel = new DockPanel { Margin = new Thickness(8) };
            logPanel.Children.Add(logHeader);
            logPanel.Children.Add(_log);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(480) });
            var leftScroll = new ScrollViewer { Content = left, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var centerScroll = new ScrollViewer { Content = center, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetColumn(centerScroll, 1);
            Grid.SetColumn(logPanel, 2);
            grid.Children.Add(leftScroll);
            grid.Children.Add(centerScroll);
            grid.Children.Add(logPanel);

            SetMsg(_msg);
            return grid;
        }

        private void SetMsg(HsmsJson msg)
        {
            _msg = msg;
            _msgEditor.Content = MsgEditor(_msg);
        }

        // /api/defineMsgLoad 와 동일 (편집은 복사본에)
        private void LoadDefines()
        {
            _defines = Clone(Manager.HsmsMsgDefines);
            _sel = null;
            RenderDefines();
            SetStatus($"Define 불러오기: {_defines.Count} groups");
        }

        private void RenderDefines()
        {
            _defineList.Children.Clear();
            for (int g = 0; g < _defines.Count; g++)
            {
                int gi = g;
                var group = _defines[g];
                var box = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
                box.Children.Add(Row(
                    Text(group.Name, v => group.Name = v, 180),
                    Check("사용", group.IsSelected, v => group.IsSelected = v),
                    DangerButton(() => { _defines.RemoveAt(gi); _sel = null; RenderDefines(); }, "그룹 삭제")));

                for (int p = 0; p < group.Pairs.Count; p++)
                {
                    int pi = p;
                    var pair = group.Pairs[p];
                    var primary = ItemContent($"{MsgLabel(pair.Primary)}{(pair.Primary.IsAutoResponse == true ? " ⟲" : string.Empty)}", null,
                        DangerButton(() => { group.Pairs.RemoveAt(pi); _sel = null; RenderDefines(); }, "쌍 삭제"));
                    var primaryItem = Item(primary, IsSel(gi, pi, "primary"), () => Pick(gi, pi, "primary"));
                    primaryItem.ToolTip = pair.Name;
                    box.Children.Add(primaryItem);

                    if (pair.Secondary is not null)
                    {
                        var secondary = ItemContent($"↳ {MsgLabel(pair.Secondary)}", null,
                            DangerButton(() => { pair.Secondary = null; _sel = null; RenderDefines(); }, "Secondary 삭제"));
                        box.Children.Add(Item(secondary, IsSel(gi, pi, "secondary"), () => Pick(gi, pi, "secondary"), 16));
                    }
                    else
                    {
                        box.Children.Add(Item(ItemContent("↳ 응답 없음", MutedBrush), false, () => { }, 16));
                    }
                }
                _defineList.Children.Add(box);
            }
        }

        private bool IsSel(int g, int p, string side) => _sel is { } s && s.G == g && s.P == p && s.Side == side;

        private void Pick(int g, int p, string side)
        {
            var pair = _defines[g].Pairs[p];
            _sel = (g, p, side);
            SetMsg(Clone(side == "primary" ? pair.Primary : pair.Secondary!));
            RenderDefines();
        }

        // asNew : 편집 중인 메시지를 Primary로 하는 새 쌍 추가 / 그 외 : 선택된 쌍의 side(Primary 또는 Secondary)에 저장
        private void SaveToDefine(bool asNew, string? side = null)
        {
            if (_defines.Count == 0)
            {
                SetStatus("그룹을 먼저 추가하세요");
                return;
            }

            if (asNew == true || _sel is null)
            {
                int g = _sel?.G ?? 0;
                _defines[g].Pairs.Add(new HsmsMsgPair { Name = _msg.Name, Primary = Clone(_msg) });
                _sel = (g, _defines[g].Pairs.Count - 1, "primary");
            }
            else
            {
                var s = _sel.Value;
                var target = side ?? s.Side;
                var pair = _defines[s.G].Pairs[s.P];
                if (target == "primary") pair.Primary = Clone(_msg);
                else pair.Secondary = Clone(_msg);
                _sel = (s.G, s.P, target);
            }
            RenderDefines();
        }

        // /api/sendMsg 와 동일
        private void SendMsg() => Run("Send", () =>
        {
            var msg = Clone(_msg);
            msg.SetHeader();
            Manager.EnequeueSendMsg(msg.Header, msg.ToHsmsBytes());
            return $"S{msg.Stream}F{msg.Function} 전송 요청";
        });

        // .json(msgDefine.json) : 전체 교체 / .sml, .smd : 파일명으로 그룹 추가
        private void ImportFiles()
        {
            var dialog = new OpenFileDialog { Filter = "정의 파일 (*.json;*.sml;*.smd)|*.json;*.sml;*.smd|모든 파일 (*.*)|*.*", Multiselect = true };
            if (dialog.ShowDialog(this) != true) return;

            foreach (var path in dialog.FileNames)
            {
                var fileName = Path.GetFileName(path);
                try
                {
                    var text = File.ReadAllText(path);
                    var groupName = Path.GetFileNameWithoutExtension(path);
                    switch (Path.GetExtension(path).ToLowerInvariant())
                    {
                        case ".json":
                            _defines = JsonSerializer.Deserialize<List<HsmsMsgDefine>>(text, JsonOptions) ?? throw new FormatException("최상위가 배열이 아님");
                            _sel = null;
                            SetStatus($"불러옴: {fileName} ({_defines.Count} groups)");
                            break;
                        case ".smd":
                            var smd = SecsFileParser.ParseSmd(text, groupName);
                            _defines.Add(smd);
                            SetStatus($"추가됨: {fileName} ({smd.Pairs.Count} pairs)");
                            break;
                        default:
                            var sml = SecsFileParser.ParseSml(text, groupName);
                            _defines.Add(sml);
                            SetStatus($"추가됨: {fileName} ({sml.Pairs.Count} pairs)");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    SetStatus($"{fileName} 불러오기 실패: {ex.Message}");
                }
            }
            RenderDefines();
        }

        private void ExportFile()
        {
            var dialog = new SaveFileDialog { FileName = "msgDefine.json", Filter = "JSON (*.json)|*.json" };
            if (dialog.ShowDialog(this) != true) return;
            Run("Export", () =>
            {
                File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_defines, JsonOptions));
                return dialog.FileName;
            });
        }

        #endregion

        #region 로그

        // /api/logs/stream 과 동일 : 최근 로그부터 표시 후 새 로그를 계속 추가
        private async Task ReadLogsAsync()
        {
            var (id, reader, history) = LogBroadcaster.Instance.Subscribe();
            _logSubscription = id;
            foreach (var line in history) AppendLog(line);

            await foreach (var line in reader.ReadAllAsync())
            {
                AppendLog(line);
            }
        }

        private void AppendLog(string line)
        {
            _log.AppendText(line + Environment.NewLine + Environment.NewLine);
            if (_log.Text.Length > 300_000) _log.Text = _log.Text[^200_000..];      // ponytail: 오래된 로그 잘라냄, 전체 보관 필요하면 파일 로그 사용
            _log.ScrollToEnd();
        }

        #endregion
    }
}
