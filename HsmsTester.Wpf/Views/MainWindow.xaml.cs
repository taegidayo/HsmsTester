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
using System.Windows.Documents;
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
        private readonly TextBlock _status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.85 };

        // 웹(app.css)과 같은 색
        private static readonly Brush OkBrush = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
        private static readonly Brush BadBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        private static Brush Hex(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;

        // Main 화면 : 메시지 정의(편집 중인 복사본)와 편집 중인 메시지
        private List<HsmsMsgDefine> _defines = new List<HsmsMsgDefine>();
        private (int G, int P, string Side)? _sel = null;           // Side : "primary" | "secondary"
        private HsmsJson _msg = NewMsg();
        private readonly StackPanel _defineList = new StackPanel();
        // 긴 이름이 잘리지 않도록 가로 스크롤 허용
        private readonly ScrollViewer _defineScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = true, FocusVisualStyle = null };
        private object? _clipboard = null;      // 복사한 HsmsMsgPair / HsmsJson / HsmsField (복사 시점의 복사본)
        private HsmsField? _selField = null;    // 목록에서 선택한 Body 아이템 (_sel 메시지 안의 아이템)
        private readonly ContentControl _msgEditor = new ContentControl();

        // 로그
        private readonly TextBox _log = new TextBox
        {
            IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12, TextWrapping = TextWrapping.NoWrap,
            Background = Hex("#111111"), Foreground = Hex("#CCFFCC"), BorderThickness = new Thickness(0), Padding = new Thickness(6),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        private Guid _logSubscription;

        private readonly ConditionalView _conditionalView;
        private readonly UndefinedView _undefinedView;
        private readonly TabItem _undefinedTab = new TabItem { Header = "Undefined" };
        private readonly ConfigView _configView;

        public MainWindow()
        {
            ApiListener.Instance.StartAsync(40000);     // 웹도 같이 사용할 수 있도록 API 유지

            InitializeComponent();

            _conditionalView = new ConditionalView(() => _defines, SetStatus);
            _undefinedView = new UndefinedView(AddAsNewPair, AddAsSecondary, SetStatus);
            _undefinedTab.Content = _undefinedView;
            _configView = new ConfigView(UpdateConfigText);

            var header = BuildHeader();
            DockPanel.SetDock(header, Dock.Top);
            Root.Children.Add(header);
            Root.Children.Add(BuildMainPage());

            UpdateConfigText();
            LoadDefines();
            StartStatusPolling();
            StartAutoSave();
            Loaded += async (_, _) => await ReadLogsAsync();
            Closing += (_, _) => AutoSave();     // 종료 전 마지막 변경 저장
            Closed += (_, _) => LogBroadcaster.Instance.Unsubscribe(_logSubscription);
        }

        private void SetStatus(string text) => _status.Text = text;

        #region 자동 저장

        // 수정한 정의는 바로 저장 : 1초마다 마지막으로 저장한 내용과 비교해서 바뀌었으면 msgDefine.json / conditionalDefine.json 저장
        // ponytail: 매초 전체를 직렬화해서 비교 (정의가 수 MB로 커지면 변경 이벤트 방식으로)
        private string _savedDefinesJson = string.Empty;
        private readonly TextBlock _saveState = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray, FontSize = 11 };

        private void StartAutoSave()
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => AutoSave();
            timer.Start();
        }

        private void AutoSave()
        {
            var json = JsonSerializer.Serialize(_defines, JsonOptions);
            if (json != _savedDefinesJson)
            {
                _savedDefinesJson = json;     // 실패해도 같은 내용으로 매초 다시 시도하지 않음 (다음 변경 때 다시 저장)
                _saveState.Text = Manager.SaveMsgDefines(Clone(_defines)) == true
                    ? $"자동 저장됨 {DateTime.Now:HH:mm:ss}"
                    : "자동 저장 실패 (로그 확인)";
            }
            _conditionalView.AutoSave();
        }

        #endregion

        private void Run(string label, Func<string> action)
        {
            try { SetStatus($"{label}: {action()}"); }
            catch (Exception ex) { SetStatus($"{label} 실패: {ex.Message}"); }
        }

        #region 상단

        private UIElement BuildHeader()
        {
            _configText.MouseLeftButtonUp += (_, _) => { UpdateConfigText(); _configView.Load(); };

            var t3 = Check("T3 Timeout Check (S6F11 무응답 : 자동/조건 응답 모두)", Manager.T3TimeoutCheckFlag, v => Run("T3 Timeout Flag", () =>
            {
                Manager.T3TimeoutCheckFlag = v;     // /api/setT3TimeoutFlag
                return v.ToString();
            }));

            t3.Foreground = Brushes.White;
            var headerButton = (Style)FindResource("HeaderButton");
            var connect = Button("Connect", () => Run("TCP Start", () => { Manager.Start(); return "ok"; }));          // /api/tcpChange?onOff=true
            var disconnect = Button("Disconnect", () => Run("TCP Stop", () => { Manager.Dispose(); return "ok"; }));   // /api/tcpChange?onOff=false
            connect.Style = headerButton;
            disconnect.Style = headerButton;

            // 웹의 .target : 접속 대상은 어두운 상자 안에 고정폭 글꼴
            _configText.FontFamily = new FontFamily("Consolas");
            _configText.FontSize = 14;
            var target = new Border { Child = _configText, Background = Hex("#111827"), Padding = new Thickness(8, 3, 8, 3), CornerRadius = new CornerRadius(4) };

            var row = Row(
                new TextBlock { Text = "HSMS Tester", FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center },
                target, _connText, _selectedText, connect, disconnect, t3, _status);
            foreach (FrameworkElement child in row.Children) child.Margin = new Thickness(0, 0, 12, 0);
            var header = new Border { Child = row, Padding = new Thickness(16, 8, 16, 8), Background = Hex("#1F2937") };
            TextElement.SetForeground(header, Brushes.White);
            return header;
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
                int undefinedCount = Manager.UndefinedMsgs.Count;
                _undefinedTab.Header = undefinedCount > 0 ? $"Undefined ({undefinedCount})" : "Undefined";
                bool connected = Manager.IsConnected();
                _connText.Text = connected == true ? "● Connected" : "● Disconnected";
                _connText.Foreground = connected == true ? OkBrush : BadBrush;
                _selectedText.Text = Manager.IsSelected == true ? "● Selected" : "● Not Selected";
                _selectedText.Foreground = Manager.IsSelected == true ? OkBrush : BadBrush;
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
            var left = new StackPanel();
            left.Children.Add(Header("Message Defines"));
            var save = Button("Save", () => Run("Define 저장", () =>
                Manager.SaveMsgDefines(Clone(_defines)) == true ? $"{_defines.Count} groups" : throw new IOException("msgDefine.json 저장 실패 (로그 확인)")), "지금 저장 (수정하면 자동으로 저장됨)");
            save.Style = (Style)FindResource("PrimaryButton");
            left.Children.Add(Row(
                Button("Reload", LoadDefines, "현재 정의 다시 불러오기"),
                Button("Import", ImportFiles, "msgDefine.json(교체), .sml / .smd(그룹 추가)"),
                Button("Export", ExportFile),
                save));
            left.Children.Add(_saveState);
            left.Children.Add(Row(Button("+ Group", () =>
            {
                _defines.Add(new HsmsMsgDefine { Name = $"Group{_defines.Count + 1}" });
                RenderDefines();
            }), Button("모두 접기", CollapseAll, "모든 그룹, 쌍, 메시지 접기")));
            left.Children.Add(Row(BuildSearchBox()));
            left.Children.Add(_defineList);

            // 가운데 : 메시지 편집
            var json = new TextBox { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.NoWrap };
            var expander = new Expander { Header = "JSON", Content = json, Margin = new Thickness(0, 8, 0, 0) };
            expander.Expanded += (_, _) => json.Text = JsonSerializer.Serialize(_msg, JsonOptions);

            var center = new StackPanel();
            center.Children.Add(_msgEditor);
            var send = Button("Send", SendMsg);
            send.Style = (Style)FindResource("PrimaryButton");
            center.Children.Add(Row(
                send,
                Button("정의에 덮어쓰기", () => SaveToDefine(false)),
                Button("새 쌍으로 추가", () => SaveToDefine(true)),
                Button("Secondary로 저장", () => SaveToDefine(false, "secondary"), "선택된 쌍의 Secondary(응답)로 저장"),
                Button("New", () => { _sel = null; SetMsg(NewMsg()); RenderDefines(); })));
            center.Children.Add(expander);

            // 오른쪽 : 로그
            // Clear : 화면과 서버의 로그 기록을 함께 지움 (웹에서 새로 접속해도 이전 로그가 나오지 않도록)
            var logHeader = Row(Header("Log"), Button("Clear", () => { LogBroadcaster.Instance.Clear(); _log.Clear(); }));
            DockPanel.SetDock(logHeader, Dock.Top);
            var logPanel = new DockPanel();
            logPanel.Children.Add(logHeader);
            logPanel.Children.Add(_log);

            // 정의 목록 | 탭 | 로그. 사이의 경계(GridSplitter)를 끌어서 폭 조절
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420), MinWidth = 200 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 300 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420), MinWidth = 150 });
            // 목록에서 Ctrl+C / Ctrl+V를 받기 위해 포커스 가능하게 (항목 클릭 시 포커스 이동)
            var leftScroll = _defineScroll;
            leftScroll.Content = left;
            leftScroll.PreviewKeyDown += OnDefineKeyDown;
            var centerScroll = new ScrollViewer { Content = center, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };

            // 가운데 : Message / Conditional / Config 탭 (왼쪽 정의 목록, 오른쪽 로그는 항상 표시)
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "Message", Content = centerScroll });
            tabs.Items.Add(new TabItem { Header = "Conditional", Content = _conditionalView });
            tabs.Items.Add(_undefinedTab);
            tabs.Items.Add(new TabItem { Header = "Config", Content = _configView });

            // 웹의 section : 흰 카드 (회색 테두리, 둥근 모서리)
            Border Card(UIElement child, int column)
            {
                var card = new Border
                {
                    Child = child, Background = Brushes.White, BorderBrush = Hex("#DDDDDD"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(8),
                };
                Grid.SetColumn(card, column);
                return card;
            }
            grid.Margin = new Thickness(8);
            grid.Children.Add(Card(leftScroll, 0));
            grid.Children.Add(Card(tabs, 2));
            grid.Children.Add(Card(logPanel, 4));
            foreach (var column in new[] { 1, 3 })
            {
                var splitter = new GridSplitter { Width = 8, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, ToolTip = "끌어서 폭 조절" };
                Grid.SetColumn(splitter, column);
                grid.Children.Add(splitter);
            }

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
            _savedDefinesJson = JsonSerializer.Serialize(_defines, JsonOptions);     // 불러온 내용은 다시 저장하지 않음
            _sel = null;
            RenderDefines();
            SetStatus($"Define 불러오기: {_defines.Count} groups");
        }

        // 메시지 정의 트리 : 그룹 → 쌍 → 메시지(Primary / Secondary) → Body 아이템
        private void RenderDefines()
        {
            HsmsMsgDefine.EnsureOneSelected(_defines);     // 추가/삭제/불러오기 후에도 사용 그룹은 항상 하나
            _defineList.Children.Clear();
            for (int g = 0; g < _defines.Count; g++)
            {
                int gi = g;
                var group = _defines[g];
                var box = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
                bool groupExpanded = IsFiltering == true || IsExpanded(group, true);     // 검색 중에는 모두 펼침
                var header = Row(
                    Toggle(groupExpanded, () => ToggleExpand(group)),
                    Text(group.Name, v => group.Name = v, 160, hint: "그룹 이름"),
                    Muted($"({group.Pairs.Count})"),
                    UseRadio(group),
                    Button("+ 쌍", () => AddPair(gi, group.Pairs.Count), "새 쌍(메시지) 추가"),
                    DangerButton(() => { _defines.RemoveAt(gi); _sel = null; RenderDefines(); }, "그룹 삭제"));
                header.ContextMenu = Menu(
                    ("새 쌍 추가", () => AddPair(gi, group.Pairs.Count)),
                    ("-", null),
                    ("그룹 끝에 붙여넣기", () => Paste(gi, group.Pairs.Count)));
                // 그룹 줄에 쌍을 놓으면 그룹 끝으로 이동 (숨김 해제)
                var headerBorder = new Border { Child = header };
                MakeDropTarget(headerBorder, data => data is PairDrag, (data, _) => MovePair((PairDrag)data, gi, group.Pairs.Count, false));
                box.Children.Add(headerBorder);
                _defineList.Children.Add(box);
                if (groupExpanded == false) continue;

                if (IsFiltering == true)
                {
                    // 검색 : 일치하는 쌍만 (숨긴 쌍 포함)
                    var found = Enumerable.Range(0, group.Pairs.Count).Where(i => MatchesFilter(group.Pairs[i]) == true).ToList();
                    if (found.Count == 0) box.Children.Add(Item(ItemContent("일치하는 메시지 없음", MutedBrush), false, () => { }, 12));
                    foreach (var pi in found) AddPairRows(box, gi, pi, 0);
                    continue;
                }

                var hidden = new List<int>();
                for (int p = 0; p < group.Pairs.Count; p++)
                {
                    if (group.Pairs[p].IsHidden == true) hidden.Add(p);
                    else AddPairRows(box, gi, p, 0);
                }

                // Hidden Defines : 펼쳐야만 숨긴 쌍이 보임
                box.Children.Add(HiddenNode(gi, hidden.Count));
                if (_hiddenOpen.Contains(group) == true)
                {
                    foreach (var p in hidden) AddPairRows(box, gi, p, 14);
                }
            }
        }

        // 쌍 한 줄 + 펼쳐져 있으면 Primary / Secondary. 드래그로 순서 변경, Hidden Defines로 이동
        private void AddPairRows(Panel box, int gi, int pi, double extraIndent)
        {
            var group = _defines[gi];
            var pair = group.Pairs[pi];
            bool pairExpanded = IsExpanded(pair, false);

            // 쌍 : S1F3 W - 쌍 이름
            var pairName = pair.Name != string.Empty ? pair.Name : pair.Primary.Name;
            var pairLine = new StackPanel { Orientation = Orientation.Horizontal };
            pairLine.Children.Add(new TextBlock { Text = $"S{pair.Primary.Stream}F{pair.Primary.Function}{(pair.Primary.Wbit == true ? " W" : string.Empty)} - " });
            pairLine.Children.Add(Editable(pair, "name", pairName, pair.Name, "쌍 이름", v => pair.Name = v, RenderDefines));
            if (pair.Primary.IsAutoResponse == true) pairLine.Children.Add(new TextBlock { Text = " ⟲" });
            if (pair.IsHidden == true && IsFiltering == true) pairLine.Children.Add(new TextBlock { Text = " (숨김)", Foreground = MutedBrush });
            var pairContent = RowContent(Toggle(pairExpanded, () => ToggleExpand(pair)), pairLine,
                DangerButton(() => { group.Pairs.RemoveAt(pi); _sel = null; RenderDefines(); }, "쌍 삭제"));
            var pairItem = Item(pairContent, IsSel(gi, pi, "primary") && _selField is null, () => Pick(gi, pi, "primary"), 12 + extraIndent);
            pairItem.ToolTip = $"{pairName}  (드래그해서 순서 변경, Hidden Defines로 이동)";
            pairItem.ContextMenu = Menu(
                ("Send (Primary)", () => Send(pair.Primary)),
                ("-", null),
                ("새 쌍 추가 (아래에)", () => AddPair(gi, pi + 1)),
                ("응답(Secondary) 추가", pair.Secondary is null ? () => AddSecondary(gi, pi) : null),
                (pair.IsHidden == true ? "숨김 해제" : "숨기기 (Hidden Defines로)", () => SetHidden(gi, pi, pair.IsHidden == false)),
                ("-", null),
                ("쌍 복사 (Ctrl+C)", () => CopyPair(pair)),
                ("-", null),
                ("아래에 붙여넣기 (Ctrl+V)", () => Paste(gi, pi + 1)));
            MakeDragSource(pairItem, () => new PairDrag(gi, pi));
            MakeDropTarget(pairItem, data => data is PairDrag d && (d.G != gi || d.P != pi),
                (data, after) => MovePair((PairDrag)data, gi, after == true ? pi + 1 : pi, pair.IsHidden));
            box.Children.Add(pairItem);
            if (pairExpanded == false) return;

            AddMsgRows(box, gi, pi, "primary");
            AddMsgRows(box, gi, pi, "secondary");
        }

        // 메시지 한 줄 + 펼쳐져 있으면 Body 아이템들
        private void AddMsgRows(Panel box, int g, int p, string side)
        {
            var pair = _defines[g].Pairs[p];
            var msg = side == "primary" ? pair.Primary : pair.Secondary;
            const double indent = 30;

            if (msg is null)
            {
                var noReply = Item(RowContent(new TextBlock(), new TextBlock { Text = "↳ 응답 없음", Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center },
                    Button($"+ 응답 추가 (S{pair.Primary.Stream}F{pair.Primary.Function + 1})", () => AddSecondary(g, p), "Primary의 S, F+1로 응답(Secondary) 만들기")), false, () => { }, indent + 14);
                noReply.ContextMenu = Menu(
                    ("응답(Secondary) 추가", () => AddSecondary(g, p)),
                    ("Secondary로 붙여넣기", () => PasteAsSecondary(g, p)));
                box.Children.Add(noReply);
                return;
            }

            bool expanded = IsExpanded(msg, false);
            var right = side == "secondary"
                ? new UIElement[] { DangerButton(() => { pair.Secondary = null; _sel = null; RenderDefines(); }, "Secondary 삭제") }
                : Array.Empty<UIElement>();
            void Done() => Pick(g, p, side);
            var line1 = new StackPanel { Orientation = Orientation.Horizontal };
            if (side == "secondary") line1.Children.Add(new TextBlock { Text = "↳ " });
            line1.Children.Add(new TextBlock { Text = "S" });
            line1.Children.Add(Editable(msg, "stream", msg.Stream.ToString(), msg.Stream.ToString(), "Stream (0~127)", v => msg.Stream = int.Parse(v), Done,
                validate: v => int.TryParse(v, out var n) == true && n >= 0 && n <= 127 ? null : "Stream은 0~127 숫자"));
            line1.Children.Add(new TextBlock { Text = "F" });
            line1.Children.Add(Editable(msg, "function", msg.Function.ToString(), msg.Function.ToString(), "Function (0~255)", v => msg.Function = int.Parse(v), Done,
                validate: v => int.TryParse(v, out var n) == true && n >= 0 && n <= 255 ? null : "Function은 0~255 숫자"));
            line1.Children.Add(Flag(" W", msg.Wbit, "W-bit (응답 요구)", () => { msg.Wbit = msg.Wbit == false; Done(); }));
            line1.Children.Add(new TextBlock { Text = " " });
            line1.Children.Add(Editable(msg, "name", msg.Name == string.Empty ? "(이름 없음)" : msg.Name, msg.Name, "메시지 이름", v => msg.Name = v, Done,
                msg.Name == string.Empty ? MutedBrush : null));
            line1.Children.Add(Flag(" ⟲", msg.IsAutoResponse, "AutoResponse (같은 쌍의 Secondary로 자동 응답)", () => { msg.IsAutoResponse = msg.IsAutoResponse == false; Done(); }));
            var lines = new StackPanel();
            lines.Children.Add(line1);
            var subName = Editable(msg, "subName", msg.SubName == string.Empty ? "SubName 없음" : msg.SubName, msg.SubName, "부가 이름", v => msg.SubName = v, Done,
                msg.SubName == string.Empty ? Brushes.Gainsboro : MutedBrush, 11);
            if (subName is FrameworkElement fe) fe.Margin = new Thickness(side == "secondary" ? 12 : 0, 0, 0, 0);
            lines.Children.Add(subName);
            var content = RowContent(Toggle(expanded, () => ToggleExpand(msg), msg.Body is not null), lines, right);
            var item = Item(content, IsSel(g, p, side) && _selField is null, () => Pick(g, p, side), indent);
            var addBody = msg.Body is null ? ("Body 추가 (LIST)", (Action?)(() =>
            {
                msg.Body = NewListField();
                _toggled.Add(msg);      // 메시지 펼침
                PickField(g, p, side, msg.Body);
            })) : ("-", null);
            item.ContextMenu = side == "primary"
                ? Menu(
                    ("Send", () => Send(msg)),
                    ("-", null),
                    addBody,
                    ("메시지 복사 (Ctrl+C)", () => CopyMsg(msg)),
                    ("-", null),
                    ("아래에 붙여넣기 (새 쌍)", () => Paste(g, p + 1)),
                    ("Body로 붙여넣기 (덮어쓰기)", () => PasteAsBody(g, p, side)))
                : Menu(
                    ("Send", () => Send(msg)),
                    ("-", null),
                    addBody,
                    ("메시지 복사 (Ctrl+C)", () => CopyMsg(msg)),
                    ("-", null),
                    ("아래에 붙여넣기 (새 쌍)", () => Paste(g, p + 1)),
                    ("Secondary로 붙여넣기 (덮어쓰기)", () => PasteAsSecondary(g, p)),
                    ("Body로 붙여넣기 (덮어쓰기)", () => PasteAsBody(g, p, side)));
            box.Children.Add(item);

            if (expanded == true && msg.Body is not null)
            {
                AddFieldRows(box, g, p, side, msg.Body, null, 0);
            }
        }

        // Body 아이템 (XCom 형태 : L 3 / U4 1 SVID '10004'). LIST는 하위 아이템을 한 단계 들여씀
        private void AddFieldRows(Panel box, int g, int p, string side, HsmsField field, HsmsField? parent, int depth)
        {
            bool expanded = IsExpanded(field, true);
            var line = new StackPanel { Orientation = Orientation.Horizontal };
            line.Children.Add(Toggle(expanded, () => ToggleExpand(field), field.IsList == true && field.Fields.Count > 0));
            foreach (var run in FieldText(g, p, side, field)) line.Children.Add(run);

            var item = Item(line, ReferenceEquals(_selField, field) == true, () => PickField(g, p, side, field), 44 + depth * 14);
            // 드래그로 순서 변경 (최상위 Body는 이동 불가). 자기 자신이나 하위로는 놓을 수 없음
            if (parent is not null) MakeDragSource(item, () => new FieldDrag(g, p, side, field, parent));
            MakeDropTarget(item, data => data is FieldDrag d && IsSelfOrDescendant(d.Field, field) == false,
                (data, after) => DropField((FieldDrag)data, g, p, side, field, parent, after));
            item.ContextMenu = Menu(
                ("이 메시지 Send", () => Send(side == "primary" ? _defines[g].Pairs[p].Primary : _defines[g].Pairs[p].Secondary!)),
                ("-", null),
                ("아이템 추가 : 안에 (Children)", field.IsList == true ? () => AddField(g, p, side, field, null) : null),
                ("아이템 추가 : 아래에", parent is null ? null : () => AddField(g, p, side, parent, field)),
                ("-", null),
                ("아이템 복사 (Ctrl+C)", () => CopyField(field)),
                ("-", null),
                ("아래에 붙여넣기 (Ctrl+V)", () => PasteField(g, p, side, field, false)),
                ("안에 붙여넣기 (LIST)", () => PasteField(g, p, side, field, true)),
                ("-", null),
                ("아이템 삭제", parent is null ? null : () =>
                {
                    int before = parent.Fields.Count;
                    parent.Fields.Remove(field);
                    SyncListCount(parent, before);
                    _selField = null;
                    Pick(g, p, side);
                }));
            box.Children.Add(item);

            if (field.IsList == true && expanded == true)
            {
                foreach (var child in field.Fields)
                    AddFieldRows(box, g, p, side, child, field, depth + 1);
            }
        }

        // 아이템 표시 : 타입(색) 개수 이름 '값'. 타입 / 이름 / 값을 클릭하면 바로 수정
        private IEnumerable<UIElement> FieldText(int g, int p, string side, HsmsField field)
        {
            void Done() => PickField(g, p, side, field);

            yield return EditableType(field, Done);

            var text = ValueToText(field.Value);
            int count = field.IsList == true ? field.Fields.Count
                : field.Type is HsmsTester.eHsmsDataType.ASCII or HsmsTester.eHsmsDataType.JIS8 ? text.Length
                : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            yield return new TextBlock { Text = $"{count}{(field.IsList == true && field.Count == 0 ? " (가변)" : string.Empty)} " };

            yield return Editable(field, "name", field.Name == string.Empty ? "(이름)" : field.Name, field.Name, "이름 (SVID 등)",
                v => field.Name = v, Done, field.Name == string.Empty ? Brushes.Gainsboro : null);

            if (field.IsList == true) yield break;

            yield return new TextBlock { Text = " " };
            yield return Editable(field, "value", $"'{text}'", text,
                field.Type is HsmsTester.eHsmsDataType.ASCII or HsmsTester.eHsmsDataType.JIS8 ? "문자열 값" : "값 (여러 개는 공백 구분)",
                v => field.Value = TextToValue(field.Type, v), Done);
        }

        #region 트리에서 바로 수정

        // 수정 중인 대상 (Target : 쌍 / 메시지 / 아이템, Part : "name" / "subName" / "type" / "value")
        private (object Target, string Part)? _editing = null;

        private bool IsEditing(object target, string part) => _editing is { } e && ReferenceEquals(e.Target, target) == true && e.Part == part;

        private void StartEdit(object target, string part)
        {
            _editing = (target, part);
            RenderDefines();
        }

        // 토글 + 내용 + 버튼 한 줄. 목록이 가로로 스크롤되므로 버튼은 줄 끝이 아니라 내용 바로 뒤에 붙임 (화면 밖으로 밀리지 않도록)
        private static StackPanel RowContent(UIElement left, UIElement center, params UIElement[] right)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(left);
            row.Children.Add(center);
            foreach (var r in right)
            {
                if (r is FrameworkElement fe) fe.Margin = new Thickness(8, 0, 0, 0);
                row.Children.Add(r);
            }
            return row;
        }

        // 클릭하면 입력칸으로 바뀌는 텍스트. Enter / 포커스 이동 = 적용, Esc = 취소. done : 적용/취소 후 다시 그리기 (편집기 갱신 포함)
        // validate : 잘못된 값이면 오류 문구 반환 (적용 안 함)
        private UIElement Editable(object target, string part, string display, string current, string hint, Action<string> apply, Action done,
            Brush? foreground = null, double fontSize = 0, Func<string, string?>? validate = null)
        {
            if (IsEditing(target, part) == true) return InlineTextEditor(current, hint, apply, done, validate);

            var text = new TextBlock { Text = display, Cursor = System.Windows.Input.Cursors.IBeam, ToolTip = $"클릭해서 수정 : {hint}", VerticalAlignment = VerticalAlignment.Center };
            if (foreground is not null) text.Foreground = foreground;
            if (fontSize > 0) text.FontSize = fontSize;
            text.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;       // 항목 선택으로 전달하지 않음
                StartEdit(target, part);
            };
            return text;
        }

        private TextBox InlineTextEditor(string current, string hint, Action<string> apply, Action done, Func<string, string?>? validate)
        {
            var box = new TextBox { Text = current, MinWidth = current.Length <= 3 ? 36 : 80, Padding = new Thickness(1, 0, 1, 0) };     // 숫자 등 짧은 값은 좁게
            SetHint(box, hint);
            bool finished = false;
            void Finish(bool ok)
            {
                if (finished == true) return;
                finished = true;
                _editing = null;
                if (ok == true && box.Text != current)
                {
                    if (validate?.Invoke(box.Text) is string error)
                    {
                        SetStatus($"수정 안 됨: {error}");
                    }
                    else
                    {
                        apply(box.Text);
                        SetStatus($"수정: {hint} = '{box.Text}'");
                    }
                }
                done();
            }
            box.KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) { Finish(true); e.Handled = true; }
                else if (e.Key == System.Windows.Input.Key.Escape) { Finish(false); e.Handled = true; }
            };
            box.LostKeyboardFocus += (_, _) => Finish(true);
            box.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
            return box;
        }

        // 켜고 끄는 표시 (W, ⟲). 켜져 있으면 진하게, 꺼져 있으면 연하게. 클릭하면 전환
        private static TextBlock Flag(string text, bool on, string tooltip, Action toggle)
        {
            var flag = new TextBlock
            {
                Text = text, Cursor = System.Windows.Input.Cursors.Hand,
                Foreground = on == true ? Brushes.Black : Brushes.Gainsboro,
                FontWeight = on == true ? FontWeights.SemiBold : FontWeights.Normal,
                ToolTip = $"{tooltip} : {(on == true ? "켜짐" : "꺼짐")} (클릭해서 전환)",
            };
            flag.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                toggle();
            };
            return flag;
        }

        private static HsmsField NewListField() => new HsmsField { Type = HsmsTester.eHsmsDataType.LIST };

        // 새 쌍 추가 (기본 S1F1 W). 추가 후 선택하고 펼쳐서 바로 수정할 수 있게
        private void AddPair(int g, int insertAt)
        {
            var primary = new HsmsJson { Stream = 1, Function = 1, Wbit = true, Name = "NewMessage" };
            var pair = new HsmsMsgPair { Name = primary.Name, Primary = primary };
            _defines[g].Pairs.Insert(insertAt, pair);
            _toggled.Remove(_defines[g]);   // 그룹 펼침
            _toggled.Add(pair);             // 쌍 펼침
            SetStatus("새 쌍 추가 : S/F, 이름을 클릭해서 수정하세요");
            Pick(g, insertAt, "primary");
        }

        // 응답(Secondary) 추가 : Primary의 S, F + 1, W-bit 없음
        private void AddSecondary(int g, int p)
        {
            var pair = _defines[g].Pairs[p];
            pair.Secondary = new HsmsJson { Stream = pair.Primary.Stream, Function = pair.Primary.Function + 1, Wbit = false, Name = pair.Primary.Name };
            _toggled.Add(pair);
            SetStatus($"응답 추가 : S{pair.Secondary.Stream}F{pair.Secondary.Function}");
            Pick(g, p, "secondary");
        }

        // 아이템 추가 (기본 LIST). after가 null이면 list의 마지막(Children)에, 아니면 after 바로 아래에
        private void AddField(int g, int p, string side, HsmsField list, HsmsField? after)
        {
            var added = NewListField();
            int before = list.Fields.Count;
            list.Fields.Insert(after is null ? list.Fields.Count : list.Fields.IndexOf(after) + 1, added);
            SyncListCount(list, before);
            _toggled.Remove(list);      // 추가한 위치가 보이도록 펼침
            SetStatus("아이템 추가 (LIST)");
            PickField(g, p, side, added);
        }

        // 타입 : 클릭하면 드롭다운. LIST로 바꾸면 count는 항목 수(고정)
        private UIElement EditableType(HsmsField field, Action done)
        {
            var name = HsmsJson.SmlTypeNames.TryGetValue(field.Type, out var t) == true ? t : field.Type.ToString();
            var color = field.Type switch
            {
                HsmsTester.eHsmsDataType.LIST => Brushes.RoyalBlue,
                HsmsTester.eHsmsDataType.ASCII or HsmsTester.eHsmsDataType.JIS8 => Brushes.SeaGreen,
                HsmsTester.eHsmsDataType.BINARY or HsmsTester.eHsmsDataType.BOOL => Brushes.DarkOrange,
                _ => Brushes.MediumPurple,
            };

            if (IsEditing(field, "type") == false)
            {
                var text = new TextBlock { Text = name, Foreground = color, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 6, 0), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "클릭해서 타입 변경" };
                text.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    StartEdit(field, "type");
                };
                return text;
            }

            var combo = new ComboBox { ItemsSource = Enum.GetValues<HsmsTester.eHsmsDataType>(), SelectedItem = field.Type, Margin = new Thickness(0, 0, 6, 0) };
            bool finished = false;
            void Finish()
            {
                if (finished == true) return;
                finished = true;
                _editing = null;
                done();
            }
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is HsmsTester.eHsmsDataType v && v != field.Type)
                {
                    field.Type = v;
                    if (v == HsmsTester.eHsmsDataType.LIST) field.Count = field.Fields.Count;
                    SetStatus($"타입 변경: {field.Name} → {v}");
                }
                combo.Dispatcher.BeginInvoke(Finish);     // 드롭다운이 닫힌 뒤 다시 그림 (Ui.Combo 참고)
            };
            combo.DropDownClosed += (_, _) => Finish();
            combo.Loaded += (_, _) => { combo.Focus(); combo.IsDropDownOpen = true; };
            return combo;
        }

        #endregion

        #region 처음 보는 메시지 → 정의에 추가

        // 사용 그룹(없으면 새 그룹)에 새 쌍(Primary)으로 추가
        private string AddAsNewPair(HsmsJson msg)
        {
            if (_defines.Count == 0) _defines.Add(new HsmsMsgDefine { Name = "Group1" });
            int g = Math.Max(0, _defines.FindIndex(d => d.IsSelected == true));
            var group = _defines[g];
            group.Pairs.Add(new HsmsMsgPair { Name = msg.Name, Primary = msg });
            int p = group.Pairs.Count - 1;
            _toggled.Remove(group);     // 그룹 펼침
            _toggled.Add(group.Pairs[p]);   // 쌍 펼침
            Pick(g, p, "primary");
            return $"추가됨: {group.Name}에 새 쌍 S{msg.Stream}F{msg.Function}";
        }

        // Message Defines에서 선택한 쌍의 Secondary로 추가 (기존 Secondary는 덮어씀)
        private string AddAsSecondary(HsmsJson msg)
        {
            if (_sel is not { } s) return "Message Defines에서 Secondary를 넣을 쌍을 먼저 선택하세요";
            var pair = _defines[s.G].Pairs[s.P];
            bool replaced = pair.Secondary is not null;
            pair.Secondary = msg;
            _toggled.Add(pair);
            Pick(s.G, s.P, "secondary");
            return $"추가됨: S{pair.Primary.Stream}F{pair.Primary.Function} 쌍의 Secondary{(replaced == true ? " (기존 Secondary 덮어씀)" : string.Empty)}";
        }

        #endregion

        // 사용 그룹 선택 (라디오 : 하나만 선택). 받은 메시지의 정의는 이 그룹에서 찾는다
        private RadioButton UseRadio(HsmsMsgDefine group)
        {
            var radio = new RadioButton
            {
                Content = "사용", IsChecked = group.IsSelected, GroupName = "UseDefineGroup",
                VerticalAlignment = VerticalAlignment.Center, ToolTip = "받은 메시지의 정의를 찾을 때 사용할 그룹 (하나만 선택)",
            };
            radio.Checked += (_, _) =>
            {
                if (group.IsSelected == true) return;
                foreach (var d in _defines) d.IsSelected = ReferenceEquals(d, group) == true;
                RenderDefines();
            };
            return radio;
        }

        // 펼침 상태 : 기본값(그룹 펼침, 쌍/메시지 접힘, LIST 펼침)과 다르게 바꾼 대상만 기억. record는 값으로 비교되므로 참조로 구분
        private readonly HashSet<object> _toggled = new HashSet<object>(ReferenceEqualityComparer.Instance);

        private bool IsExpanded(object target, bool defaultExpanded) => _toggled.Contains(target) != defaultExpanded;

        // 모두 접기 : 펼침 상태를 초기화하고 그룹까지 접음 (그룹을 펼치면 쌍/메시지는 접힌 기본 상태)
        private void CollapseAll()
        {
            _toggled.Clear();
            foreach (var group in _defines) _toggled.Add(group);
            RenderDefines();
        }

        private void ToggleExpand(object target)
        {
            if (_toggled.Remove(target) == false) _toggled.Add(target);
            RenderDefines();
        }

        private bool IsSel(int g, int p, string side) => _sel is { } s && s.G == g && s.P == p && s.Side == side;

        #region 복사 / 붙여넣기 (그룹 아래의 쌍, 메시지)

        private void CopyPair(HsmsMsgPair pair)
        {
            _clipboard = Clone(pair);
            SetStatus($"쌍 복사: {MsgLabel(pair.Primary)}{(pair.Secondary is null ? string.Empty : $" / S{pair.Secondary.Stream}F{pair.Secondary.Function}")}");
        }

        private void CopyMsg(HsmsJson msg)
        {
            _clipboard = Clone(msg);
            SetStatus($"메시지 복사: {MsgLabel(msg)}");
        }

        // insertAt 위치에 붙여넣기. 쌍은 그대로, 메시지는 그 메시지를 Primary로 하는 새 쌍으로 삽입
        private void Paste(int g, int insertAt)
        {
            HsmsMsgPair pair;
            switch (_clipboard)
            {
                case HsmsMsgPair copied:
                    pair = Clone(copied);
                    break;
                case HsmsJson msg:
                    pair = new HsmsMsgPair { Name = msg.Name, Primary = Clone(msg) };
                    break;
                default:
                    SetStatus("복사한 항목이 없습니다");
                    return;
            }

            _defines[g].Pairs.Insert(insertAt, pair);
            SetStatus($"붙여넣기: {_defines[g].Name} {insertAt + 1}번째");
            Pick(g, insertAt, "primary");
        }

        // 복사한 메시지를 쌍의 Secondary로 (기존 Secondary는 덮어씀)
        private void PasteAsSecondary(int g, int p)
        {
            if (_clipboard is not HsmsJson msg)
            {
                SetStatus(_clipboard is null ? "복사한 항목이 없습니다" : "Secondary에는 메시지만 붙여넣을 수 있습니다 (쌍 X)");
                return;
            }

            _defines[g].Pairs[p].Secondary = Clone(msg);
            SetStatus("Secondary로 붙여넣기");
            Pick(g, p, "secondary");
        }

        private void CopyField(HsmsField field)
        {
            _clipboard = Clone(field);
            SetStatus($"아이템 복사: {field.Type} {field.Name}");
        }

        // 복사한 아이템을 target 아래(같은 레벨) 또는 target 안(LIST의 마지막)에 붙여넣기
        private void PasteField(int g, int p, string side, HsmsField target, bool inside)
        {
            if (_clipboard is not HsmsField copied)
            {
                SetStatus(_clipboard is null ? "복사한 항목이 없습니다" : "아이템에는 아이템만 붙여넣을 수 있습니다");
                return;
            }

            var pair = _defines[g].Pairs[p];
            var body = (side == "primary" ? pair.Primary : pair.Secondary)?.Body;
            var parent = inside == true ? target : FindParent(body, target);
            if (parent is null || parent.IsList == false)
            {
                SetStatus(inside == true ? "LIST 안에만 붙여넣을 수 있습니다" : "최상위 아이템 옆에는 붙여넣을 수 없습니다 (안에 붙여넣기 사용)");
                return;
            }

            var pasted = Clone(copied);
            int index = inside == true ? parent.Fields.Count : parent.Fields.IndexOf(target) + 1;
            int before = parent.Fields.Count;
            parent.Fields.Insert(index, pasted);
            SyncListCount(parent, before);
            _toggled.Remove(parent);    // 붙여넣은 위치가 보이도록 펼침

            SetStatus($"아이템 붙여넣기: {pasted.Type} {pasted.Name}");
            PickField(g, p, side, pasted);
        }

        // 복사한 아이템으로 메시지 Body를 교체
        private void PasteAsBody(int g, int p, string side)
        {
            if (_clipboard is not HsmsField copied)
            {
                SetStatus(_clipboard is null ? "복사한 항목이 없습니다" : "Body에는 아이템만 붙여넣을 수 있습니다");
                return;
            }

            var pair = _defines[g].Pairs[p];
            var msg = side == "primary" ? pair.Primary : pair.Secondary!;
            msg.Body = Clone(copied);
            _toggled.Add(msg);      // 메시지 펼침
            SetStatus("Body로 붙여넣기");
            Pick(g, p, side);
        }

        private static HsmsField? FindParent(HsmsField? node, HsmsField target)
        {
            if (node is null) return null;
            foreach (var child in node.Fields)
            {
                if (ReferenceEquals(child, target) == true) return node;
                var found = FindParent(child, target);
                if (found is not null) return found;
            }
            return null;
        }

        // 목록에 포커스가 있을 때
        //  Ctrl+C : 선택한 아이템 / Primary면 쌍 / Secondary면 메시지 복사
        //  Ctrl+V : 복사한 게 아이템이면 선택한 아이템 아래(최상위면 안), 그 외는 선택한 쌍 아래에 붙여넣기
        private void OnDefineKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Control) return;
            if (e.OriginalSource is TextBox) return;      // 그룹 이름 입력 중에는 일반 텍스트 복사/붙여넣기

            if (e.Key == System.Windows.Input.Key.C && _sel is { } s)
            {
                var pair = _defines[s.G].Pairs[s.P];
                if (_selField is not null) CopyField(_selField);
                else if (s.Side == "primary") CopyPair(pair);
                else CopyMsg(pair.Secondary!);
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.V && _defines.Count > 0)
            {
                if (_clipboard is HsmsField && _sel is { } f && _selField is not null)
                {
                    var pair = _defines[f.G].Pairs[f.P];
                    var body = (f.Side == "primary" ? pair.Primary : pair.Secondary)?.Body;
                    PasteField(f.G, f.P, f.Side, _selField, ReferenceEquals(body, _selField));
                }
                else if (_clipboard is HsmsField && _sel is { } m)
                {
                    PasteAsBody(m.G, m.P, m.Side);
                }
                else if (_sel is { } t) Paste(t.G, t.P + 1);
                else Paste(0, _defines[0].Pairs.Count);
                e.Handled = true;
            }
        }

        #endregion

        // 아이템 선택 : 그 아이템이 있는 메시지를 편집기에 열고 아이템 선택 표시
        private void PickField(int g, int p, string side, HsmsField field)
        {
            Pick(g, p, side, keepField: true);
            _selField = field;
            RenderDefines();
        }

        private void Pick(int g, int p, string side, bool keepField = false)
        {
            if (keepField == false) _selField = null;
            var pair = _defines[g].Pairs[p];
            _sel = (g, p, side);
            SetMsg(Clone(side == "primary" ? pair.Primary : pair.Secondary!));
            RenderDefines();
            _defineScroll.Focus();      // Ctrl+C / Ctrl+V가 목록으로 오도록
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
        private void SendMsg() => Send(_msg);

        // /api/sendMsg 와 동일 : 복사본에 Header를 만들어 전송 큐에 넣음 (원본은 그대로)
        private void Send(HsmsJson source) => Run("Send", () =>
        {
            var msg = Clone(source);
            Manager.SendMsg(msg);       // Header 작성 + 전송 + 로그
            return $"S{msg.Stream}F{msg.Function} {msg.Name} 전송 요청";
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
