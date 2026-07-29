using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadDevLoader
{
    public sealed class DevLoaderCommands : IExtensionApplication
    {
        private static readonly object SyncRoot = new object();
        private static LoadedPlugin _current;
        private static Form _quickBar;
        private static FlowLayoutPanel _commandsPanel;
        private static TextBox _searchBox;
        private static Label _dllInfo;
        private static Label _buildInfo;
        private static Label _warningInfo;
        private static Label _errorInfo;
        private static Label _statusDot;
        private static Button _reloadButton;
        private static Button _moreButton;
        private static Button _languageButton;
        private static Timer _watchTimer;
        private static ToolTip _toolTip;
        private static DateTime _observedWriteUtc;
        private static ResolveEventHandler _dependencyResolver;
        private static string _lastError = "";
        private static int _reloadCount;
        private static bool _useEnglish;
        private static readonly HashSet<string> Favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> RecentCommands = new List<string>();

        private static string SettingsDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CadDevLoader"); } }
        private static string LastDllFile { get { return Path.Combine(SettingsDirectory, "last-dll.txt"); } }
        private static string FavoritesFile { get { return Path.Combine(SettingsDirectory, "favorites.txt"); } }
        private static string LanguageFile { get { return Path.Combine(SettingsDirectory, "language.txt"); } }

        private static string T(string vietnamese, string english)
        {
            return _useEnglish ? english : vietnamese;
        }

        private static string SearchPlaceholder
        {
            get { return T("Tìm lệnh...", "Search commands..."); }
        }

        public void Initialize()
        {
            LoadPreferences();
            WriteLine(T(
                "\nCadDevLoader đã sẵn sàng. Lệnh: DEVSHOW, DEVLOAD, DEVRELOAD, DEVLIST, DEVRUN, DEVSTATUS.",
                "\nCadDevLoader ready. Commands: DEVSHOW, DEVLOAD, DEVRELOAD, DEVLIST, DEVRUN, DEVSTATUS."));
            ShowQuickBar();
            StartBuildWatcher();
        }

        public void Terminate()
        {
            LoadedPlugin plugin;
            lock (SyncRoot) plugin = _current;
            if (plugin != null) TerminateExtensions(plugin);
            if (_dependencyResolver != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= _dependencyResolver;
                _dependencyResolver = null;
            }
            if (_quickBar != null && !_quickBar.IsDisposed)
                _quickBar.Close();
            _quickBar = null;
            if (_watchTimer != null) _watchTimer.Stop();
            _watchTimer = null;
        }

        private static void LoadPreferences()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                Favorites.Clear();
                if (File.Exists(FavoritesFile))
                    foreach (string item in File.ReadAllLines(FavoritesFile))
                        if (!String.IsNullOrWhiteSpace(item)) Favorites.Add(item.Trim());
                _useEnglish = File.Exists(LanguageFile)
                    && String.Equals(File.ReadAllText(LanguageFile).Trim(), "en", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
        }

        private static string ReadLastDll()
        {
            try { return File.Exists(LastDllFile) ? File.ReadAllText(LastDllFile).Trim() : null; }
            catch { return null; }
        }

        private static void SaveLastDll(string path)
        {
            try { Directory.CreateDirectory(SettingsDirectory); File.WriteAllText(LastDllFile, path ?? ""); }
            catch { }
        }

        private static void SaveFavorites()
        {
            try { Directory.CreateDirectory(SettingsDirectory); File.WriteAllLines(FavoritesFile, Favorites.OrderBy(x => x).ToArray()); }
            catch { }
        }

        private static void SaveLanguage()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllText(LanguageFile, _useEnglish ? "en" : "vi");
            }
            catch { }
        }

        private static void ToggleLanguage()
        {
            _useEnglish = !_useEnglish;
            SaveLanguage();

            Form current = _quickBar;
            if (current == null || current.IsDisposed) return;
            Point location = current.Location;
            current.BeginInvoke((MethodInvoker)delegate
            {
                if (!current.IsDisposed) current.Close();
                _quickBar = null;
                _commandsPanel = null;
                if (_toolTip != null) _toolTip.RemoveAll();
                ShowQuickBar();
                if (_quickBar != null && !_quickBar.IsDisposed) _quickBar.Location = location;
            });
        }

        private static void StartBuildWatcher()
        {
            if (_watchTimer != null) return;
            _watchTimer = new Timer { Interval = 1200 };
            _watchTimer.Tick += (s, e) => CheckForNewBuild();
            _watchTimer.Start();
        }

        private static void CheckForNewBuild()
        {
            string path;
            lock (SyncRoot) path = _current != null ? _current.SourcePath : ReadLastDll();
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            DateTime writeUtc = File.GetLastWriteTimeUtc(path);
            if (_observedWriteUtc == DateTime.MinValue) _observedWriteUtc = writeUtc;
            if (writeUtc <= _observedWriteUtc) return;
            if (_reloadButton != null && !_reloadButton.IsDisposed)
            {
                _reloadButton.Text = T("● BUILD MỚI — RELOAD", "● NEW BUILD — RELOAD");
                _reloadButton.BackColor = Color.FromArgb(225, 132, 24);
            }
            if (_buildInfo != null) _buildInfo.Text = T("Bản build mới lúc ", "New build at ") + writeUtc.ToLocalTime().ToString("HH:mm:ss");
        }

        private static void UpdateInfoPanel()
        {
            if (_dllInfo == null || _dllInfo.IsDisposed) return;
            LoadedPlugin plugin;
            lock (SyncRoot) plugin = _current;
            string path = plugin != null ? plugin.SourcePath : ReadLastDll();
            _dllInfo.Text = String.IsNullOrWhiteSpace(path) ? T("Chưa chọn DLL", "No DLL selected") : Path.GetFileName(path);
            if (plugin != null) _buildInfo.Text = plugin.Commands.Count + T(" lệnh · Reload ", " commands · Reloaded ") + plugin.LoadedAt.ToString("HH:mm:ss");
            else if (!String.IsNullOrWhiteSpace(path) && File.Exists(path)) _buildInfo.Text = T("Đã nhớ DLL · Build ", "Remembered DLL · Build ") + File.GetLastWriteTime(path).ToString("HH:mm:ss");
            else _buildInfo.Text = T("Chọn DLL để bắt đầu phát triển", "Choose a DLL to start developing");
            if (_statusDot != null)
            {
                _statusDot.Text = plugin != null
                    ? T("● ĐÃ NẠP", "● LOADED")
                    : (!String.IsNullOrWhiteSpace(path) && File.Exists(path) ? T("● ĐÃ NHỚ", "● SAVED") : T("○ CHƯA NẠP", "○ NOT LOADED"));
                _statusDot.ForeColor = plugin != null
                    ? Color.FromArgb(80, 215, 154)
                    : (!String.IsNullOrWhiteSpace(path) && File.Exists(path) ? Color.FromArgb(255, 205, 72) : Color.FromArgb(145, 158, 180));
            }
            if (_reloadButton != null)
            {
                bool hasDll = !String.IsNullOrWhiteSpace(path) && File.Exists(path);
                bool hasNewBuild = hasDll && _observedWriteUtc != DateTime.MinValue && File.GetLastWriteTimeUtc(path) > _observedWriteUtc;
                _reloadButton.Text = !hasDll
                    ? T("+ CHỌN DLL", "+ CHOOSE DLL")
                    : (hasNewBuild ? T("● BUILD MỚI — RELOAD", "● NEW BUILD — RELOAD") : T("↻ RELOAD BẢN BUILD MỚI", "↻ RELOAD LATEST BUILD"));
                _reloadButton.BackColor = !hasDll
                    ? Color.FromArgb(0, 157, 183)
                    : (hasNewBuild ? Color.FromArgb(225, 132, 24) : Color.FromArgb(28, 166, 122));
            }
            if (_warningInfo != null)
            {
                _warningInfo.Visible = _reloadCount >= 8;
                _warningInfo.Text = _reloadCount >= 8 ? T("⚠ Reload nhiều lần — nên mở lại AutoCAD", "⚠ Many reloads — restart AutoCAD soon") : "";
            }
            if (_errorInfo != null)
            {
                bool hasError = !String.IsNullOrWhiteSpace(_lastError);
                _errorInfo.Text = hasError ? "⚠ " + _lastError : T("✓ Không có lỗi gần đây", "✓ No recent errors");
                _errorInfo.ForeColor = hasError ? Color.FromArgb(255, 143, 143) : Color.FromArgb(112, 210, 164);
            }
        }

        private static void ShowQuickBar()
        {
            if (_quickBar != null && !_quickBar.IsDisposed)
            {
                _quickBar.Show();
                _quickBar.BringToFront();
                return;
            }

            Form bar = new Form
            {
                Text = "CAD DEV LOADER",
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(300, 610),
                MinimumSize = new Size(300, 500),
                BackColor = Color.FromArgb(24, 29, 38),
                ForeColor = Color.White,
                TopMost = true,
                ShowInTaskbar = false,
                MaximizeBox = false,
                MinimizeBox = false,
                Font = new Font("Segoe UI", 9F),
                AutoScroll = false,
                AutoScaleMode = AutoScaleMode.Dpi
            };

            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            bar.Location = new Point(work.Right - bar.Width - 18, work.Top + 90);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.FromArgb(35, 43, 55) };
            Label mark = new Label { Text = "D", Location = new Point(12, 10), Size = new Size(28, 28), BackColor = Color.FromArgb(255, 211, 78), ForeColor = Color.FromArgb(23, 32, 51), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
            Label title = new Label { Text = "CAD DEV LOADER", Location = new Point(49, 7), Size = new Size(168, 20), ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            Label subtitle = new Label { Text = T("Reload nhanh plugin AutoCAD .NET", "Hot reload AutoCAD .NET"), Location = new Point(49, 26), Size = new Size(168, 16), ForeColor = Color.FromArgb(145, 158, 180), Font = new Font("Segoe UI", 7.5F) };
            _languageButton = new Button { Text = _useEnglish ? "EN" : "VI", Location = new Point(222, 10), Size = new Size(36, 24), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(54, 63, 78), ForeColor = Color.FromArgb(255, 211, 78), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), Cursor = Cursors.Hand };
            _languageButton.FlatAppearance.BorderSize = 0;
            _languageButton.Click += (s, e) => ToggleLanguage();
            Button close = new Button { Text = "×", Location = new Point(264, 10), Size = new Size(24, 24), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(54, 63, 78), ForeColor = Color.White, Font = new Font("Segoe UI", 10F), Cursor = Cursors.Hand };
            close.FlatAppearance.BorderSize = 0;
            close.Click += (s, e) => bar.Hide();
            header.Controls.Add(mark); header.Controls.Add(title); header.Controls.Add(subtitle); header.Controls.Add(_languageButton); header.Controls.Add(close);
            bar.Controls.Add(header);

            Point dragOrigin = Point.Empty;
            header.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) dragOrigin = e.Location; };
            header.MouseMove += (s, e) => { if (e.Button == MouseButtons.Left) bar.Location = new Point(bar.Left + e.X - dragOrigin.X, bar.Top + e.Y - dragOrigin.Y); };
            title.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) dragOrigin = new Point(e.X + title.Left, e.Y + title.Top); };
            title.MouseMove += (s, e) => { if (e.Button == MouseButtons.Left) bar.Location = new Point(bar.Left + e.X + title.Left - dragOrigin.X, bar.Top + e.Y + title.Top - dragOrigin.Y); };

            _toolTip = _toolTip ?? new ToolTip { InitialDelay = 350, ReshowDelay = 100, AutoPopDelay = 7000 };
            _toolTip.SetToolTip(_languageButton, T("Chuyển sang tiếng Anh", "Switch to Vietnamese"));

            Panel statusCard = new Panel { Location = new Point(18, 59), Size = new Size(264, 62), BackColor = Color.FromArgb(30, 36, 47), Cursor = Cursors.Hand };
            _statusDot = new Label { Location = new Point(10, 8), Size = new Size(72, 18), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            _dllInfo = new Label { Location = new Point(84, 7), Size = new Size(168, 20), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleRight };
            _buildInfo = new Label { Location = new Point(10, 29), Size = new Size(242, 17), ForeColor = Color.FromArgb(145, 158, 180), Font = new Font("Segoe UI", 8F), AutoEllipsis = true };
            _warningInfo = new Label { Location = new Point(10, 45), Size = new Size(242, 15), ForeColor = Color.FromArgb(255, 184, 77), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), Visible = false };
            statusCard.Controls.Add(_statusDot); statusCard.Controls.Add(_dllInfo); statusCard.Controls.Add(_buildInfo); statusCard.Controls.Add(_warningInfo);
            Action showStatus = () => QueueCommand("DEVSTATUS");
            statusCard.Click += (s, e) => showStatus();
            _statusDot.Click += (s, e) => showStatus();
            _dllInfo.Click += (s, e) => showStatus();
            _buildInfo.Click += (s, e) => showStatus();
            bar.Controls.Add(statusCard);

            _reloadButton = AddQuickButton(bar, T("↻ RELOAD BẢN BUILD MỚI", "↻ RELOAD LATEST BUILD"), 18, 131, Color.FromArgb(28, 166, 122), ReloadOrChooseDll);
            _reloadButton.Size = new Size(218, 36);
            _moreButton = AddQuickButton(bar, "⋯", 244, 131, Color.FromArgb(54, 63, 78), () => { });
            _moreButton.Size = new Size(38, 36);
            _moreButton.Font = new Font("Segoe UI", 13F, FontStyle.Bold);

            ContextMenuStrip moreMenu = new ContextMenuStrip { BackColor = Color.FromArgb(35, 43, 55), ForeColor = Color.White, ShowImageMargin = false, Font = new Font("Segoe UI", 9F) };
            moreMenu.Items.Add(T("Nạp / đổi DLL", "Load / change DLL"), null, (s, e) => QueueCommand("DEVLOAD"));
            moreMenu.Items.Add(T("Xem trạng thái", "View status"), null, (s, e) => QueueCommand("DEVSTATUS"));
            moreMenu.Items.Add(new ToolStripSeparator());
            moreMenu.Items.Add(T("Dọn cache cũ", "Clean old cache"), null, (s, e) => CleanCache());
            moreMenu.Items.Add(T("Sao chép log lỗi", "Copy error log"), null, (s, e) => CopyLastError());
            _moreButton.Click += (s, e) => moreMenu.Show(_moreButton, new Point(0, _moreButton.Height));
            _toolTip.SetToolTip(_moreButton, T("Nạp DLL, trạng thái và công cụ bảo trì", "DLL loading, status and maintenance tools"));

            _searchBox = new TextBox { Location = new Point(18, 178), Size = new Size(264, 27), BackColor = Color.FromArgb(39, 46, 59), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9F) };
            _searchBox.Text = SearchPlaceholder;
            _searchBox.GotFocus += (s, e) => { if (_searchBox.Text == SearchPlaceholder) _searchBox.Text = ""; };
            _searchBox.LostFocus += (s, e) => { if (String.IsNullOrWhiteSpace(_searchBox.Text)) _searchBox.Text = SearchPlaceholder; };
            _searchBox.TextChanged += (s, e) => RefreshQuickCommands();
            bar.Controls.Add(_searchBox);

            Label tools = new Label
            {
                Text = T("CÔNG CỤ TRONG DLL", "TOOLS IN DLL"),
                Location = new Point(18, 210),
                Size = new Size(264, 22),
                ForeColor = Color.FromArgb(255, 211, 78),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            bar.Controls.Add(tools);

            _commandsPanel = new FlowLayoutPanel
            {
                Location = new Point(18, 234),
                Size = new Size(264, 292),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 36, 47),
                Padding = new Padding(6)
            };
            _commandsPanel.HorizontalScroll.Enabled = false;
            _commandsPanel.HorizontalScroll.Visible = false;
            bar.Controls.Add(_commandsPanel);

            _errorInfo = new Label { Location = new Point(18, 538), Size = new Size(198, 42), BackColor = Color.FromArgb(30, 36, 47), ForeColor = Color.FromArgb(112, 210, 164), Padding = new Padding(9, 0, 7, 0), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand };
            _errorInfo.Click += (s, e) => CopyLastError();
            Button copyError = AddQuickButton(bar, T("SAO CHÉP", "COPY LOG"), 222, 538, Color.FromArgb(54, 63, 78), CopyLastError);
            copyError.Size = new Size(60, 42);
            copyError.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            bar.Controls.Add(_errorInfo);

            RefreshQuickCommands();
            UpdateInfoPanel();

            _quickBar = bar;
            AcadApplication.ShowModelessDialog(bar);
        }

        private static Button AddQuickButton(Control parent, string text, int x, int y, Color color, Action click)
        {
            Button button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(parent is FlowLayoutPanel ? 226 : 264, 34),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Margin = parent is FlowLayoutPanel ? new Padding(0, 0, 0, 7) : new Padding(0)
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.08F);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(color, 0.08F);
            button.Click += (s, e) => click();
            parent.Controls.Add(button);
            return button;
        }

        private static void RefreshQuickCommands()
        {
            if (_commandsPanel == null || _commandsPanel.IsDisposed) return;
            if (_commandsPanel.InvokeRequired)
            {
                _commandsPanel.BeginInvoke(new Action(RefreshQuickCommands));
                return;
            }

            _commandsPanel.SuspendLayout();
            _commandsPanel.Controls.Clear();
            LoadedPlugin plugin;
            lock (SyncRoot) plugin = _current;

            if (plugin == null || plugin.Commands.Count == 0)
            {
                _commandsPanel.Controls.Add(new Label
                {
                    Text = T("Chưa có DLL nào được nạp.\nBấm CHỌN DLL để bắt đầu.", "No DLL is loaded.\nChoose a DLL to get started."),
                    Size = new Size(226, 70),
                    ForeColor = Color.FromArgb(170, 180, 196),
                    TextAlign = ContentAlignment.MiddleCenter
                });
                _commandsPanel.ResumeLayout();
                return;
            }

            string search = _searchBox == null || _searchBox.Text == SearchPlaceholder ? "" : _searchBox.Text.Trim();
            List<PluginCommand> commands = plugin.Commands
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .Where(x => String.IsNullOrWhiteSpace(search) || x.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || GetCommandDisplayName(x.Name).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(x => GetCommandDisplayName(x.Name))
                .ToList();

            if (!String.IsNullOrWhiteSpace(search))
            {
                AddCommandGroup(T("KẾT QUẢ TÌM KIẾM", "SEARCH RESULTS"));
                foreach (PluginCommand command in commands) AddCommandRow(command, false);
            }
            else
            {
                List<PluginCommand> favorites = commands.Where(x => Favorites.Contains(x.Name)).ToList();
                if (favorites.Count > 0)
                {
                    AddCommandGroup(T("★ YÊU THÍCH", "★ FAVORITES"));
                    foreach (PluginCommand command in favorites) AddCommandRow(command, false);
                }

                List<PluginCommand> recent = RecentCommands
                    .Select(name => commands.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    .Where(command => command != null && !Favorites.Contains(command.Name))
                    .ToList();
                if (recent.Count > 0)
                {
                    AddCommandGroup(T("DÙNG GẦN ĐÂY", "RECENTLY USED"));
                    foreach (PluginCommand command in recent) AddCommandRow(command, true);
                }

                HashSet<string> promoted = new HashSet<string>(favorites.Select(x => x.Name).Concat(recent.Select(x => x.Name)), StringComparer.OrdinalIgnoreCase);
                List<PluginCommand> remaining = commands.Where(x => !promoted.Contains(x.Name)).ToList();
                if (remaining.Count > 0)
                {
                    AddCommandGroup(T("TẤT CẢ LỆNH", "ALL COMMANDS"));
                    foreach (PluginCommand command in remaining) AddCommandRow(command, false);
                }
            }

            if (commands.Count == 0)
                _commandsPanel.Controls.Add(new Label { Text = T("Không tìm thấy lệnh phù hợp.", "No matching commands found."), Size = new Size(226, 52), ForeColor = Color.FromArgb(170, 180, 196), TextAlign = ContentAlignment.MiddleCenter });
            _commandsPanel.ResumeLayout();
        }

        private static void AddCommandGroup(string text)
        {
            _commandsPanel.Controls.Add(new Label
            {
                Text = text,
                Size = new Size(226, 22),
                Margin = new Padding(0, 2, 0, 3),
                ForeColor = Color.FromArgb(255, 205, 72),
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            });
        }

        private static void AddCommandRow(PluginCommand command, bool isRecent)
        {
            string commandName = command.Name;
            bool isFavorite = Favorites.Contains(commandName);
            Panel row = new Panel
            {
                Size = new Size(226, 36),
                Margin = new Padding(0, 0, 0, 5),
                BackColor = isRecent ? Color.FromArgb(34, 60, 70) : Color.FromArgb(39, 47, 60)
            };
            Button star = new Button
            {
                Text = isFavorite ? "★" : "☆",
                Location = new Point(3, 3),
                Size = new Size(30, 30),
                BackColor = row.BackColor,
                ForeColor = isFavorite ? Color.FromArgb(255, 205, 72) : Color.FromArgb(132, 145, 166),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Symbol", 10F),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            star.FlatAppearance.BorderSize = 0;
            Button run = new Button
            {
                Text = GetCommandDisplayName(commandName),
                Location = new Point(34, 3),
                Size = new Size(189, 30),
                BackColor = row.BackColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(7, 0, 2, 0),
                Cursor = Cursors.Hand,
                AutoEllipsis = true
            };
            run.FlatAppearance.BorderSize = 0;
            run.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 122, 164);
            run.Click += (s, e) => QueueDevRun(commandName);
            star.Click += (s, e) =>
            {
                if (Favorites.Contains(commandName)) Favorites.Remove(commandName); else Favorites.Add(commandName);
                SaveFavorites();
                QueueQuickCommandRefresh();
            };
            _toolTip.SetToolTip(run, T("Chạy lệnh ", "Run command ") + commandName);
            _toolTip.SetToolTip(star, isFavorite ? T("Bỏ khỏi yêu thích", "Remove from favorites") : T("Thêm vào yêu thích", "Add to favorites"));
            row.Controls.Add(star);
            row.Controls.Add(run);
            _commandsPanel.Controls.Add(row);
        }

        private static void ReloadOrChooseDll()
        {
            LoadedPlugin plugin;
            lock (SyncRoot) plugin = _current;
            string path = plugin != null ? plugin.SourcePath : ReadLastDll();
            QueueCommand(!String.IsNullOrWhiteSpace(path) && File.Exists(path) ? "DEVRELOAD" : "DEVLOAD");
        }

        private static void RememberRecentCommand(string commandName)
        {
            RecentCommands.RemoveAll(name => name.Equals(commandName, StringComparison.OrdinalIgnoreCase));
            RecentCommands.Insert(0, commandName);
            if (RecentCommands.Count > 4) RecentCommands.RemoveRange(4, RecentCommands.Count - 4);
            QueueQuickCommandRefresh();
        }

        private static void QueueQuickCommandRefresh()
        {
            if (_commandsPanel == null || _commandsPanel.IsDisposed) return;
            if (_commandsPanel.IsHandleCreated)
                _commandsPanel.BeginInvoke(new Action(RefreshQuickCommands));
        }

        private static void CleanCache()
        {
            try
            {
                string root = Path.Combine(Path.GetTempPath(), "CadDevLoader");
                int removed = 0;
                LoadedPlugin plugin;
                lock (SyncRoot) plugin = _current;
                string activeDirectory = plugin == null ? null : Path.GetDirectoryName(plugin.LoadedPath);
                if (Directory.Exists(root))
                {
                    string[] directories = Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                        .OrderByDescending(path => path.Length)
                        .ToArray();
                    foreach (string directory in directories)
                    {
                        if (!String.IsNullOrWhiteSpace(activeDirectory) &&
                            (String.Equals(directory, activeDirectory, StringComparison.OrdinalIgnoreCase) ||
                             activeDirectory.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        try
                        {
                            if (Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly).Length > 0)
                            {
                                Directory.Delete(directory, true);
                                removed++;
                            }
                            else if (Directory.GetFileSystemEntries(directory).Length == 0)
                            {
                                Directory.Delete(directory, false);
                            }
                        }
                        catch { }
                    }
                }
                _lastError = "";
                WriteLine(T("\nĐã dọn ", "\nCleaned ") + removed + T(" thư mục cache cũ.", " old cache folders."));
            }
            catch (System.Exception exception) { _lastError = T("Dọn cache thất bại: ", "Cache cleanup failed: ") + exception.Message; }
            UpdateInfoPanel();
        }

        private static void CopyLastError()
        {
            if (String.IsNullOrWhiteSpace(_lastError)) return;
            try
            {
                Clipboard.SetText(_lastError);
            }
            catch (System.Exception exception)
            {
                _lastError = T("Không thể sao chép log: ", "Could not copy the log: ") + exception.Message;
                UpdateInfoPanel();
            }
        }

        private static string GetCommandDisplayName(string command)
        {
            var known = new Dictionary<string, Tuple<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "CMD_BANQUYEN", Tuple.Create("Tài khoản & Bản quyền", "Account & License") },
                { "CMD_TIENDOMAT", Tuple.Create("Tiến độ mặt", "Linear Schedule") },
                { "CMD_CHUDAO", Tuple.Create("Tiến độ chủ đạo", "Master Schedule") },
                { "CMD_SODOLU", Tuple.Create("Sơ đồ lu", "Compaction Diagram") },
                { "CMD_BDDD", Tuple.Create("Đào đắp & ĐCTL", "Earthwork & Roadworks") },
                { "CMD_PHANMANH", Tuple.Create("Ổn định mái dốc", "Slope Stability") },
                { "CMD_DAMT", Tuple.Create("Vẽ dầm T", "Draw T-Beam") },
                { "CMD_TEXT_PROCESSOR", Tuple.Create("Siêu công cụ Text", "Advanced Text Tool") },
                { "CMD_CHANGETEXT", Tuple.Create("Sửa Text hàng loạt", "Batch Text Editor") },
                { "CMD_TAOLAYER", Tuple.Create("Tạo Layer chuẩn", "Create Standard Layers") },
                { "CMD_DIENTICH", Tuple.Create("Tính diện tích", "Calculate Area") }
            };
            Tuple<string, string> display;
            if (known.TryGetValue(command, out display)) return T(display.Item1, display.Item2);
            return command.Replace("CMD_", "").Replace('_', ' ').Trim();
        }

        private static void QueueCommand(string command)
        {
            Document document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document != null)
                document.SendStringToExecute(command + " ", true, false, false);
        }

        private static void QueueDevRun(string command)
        {
            Document document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document != null)
                document.SendStringToExecute("DEVRUN " + command + " ", true, false, false);
        }

        [CommandMethod("DEVLOAD", CommandFlags.Modal)]
        public void Load()
        {
            Editor editor = GetEditor();
            PromptOpenFileOptions options =
                new PromptOpenFileOptions(T("\nChọn DLL plugin cần nạp:", "\nSelect the plug-in DLL to load:"));
            options.Filter = T("DLL .NET (*.dll)|*.dll", "Managed DLL (*.dll)|*.dll");

            PromptFileNameResult result = editor.GetFileNameForOpen(options);
            if (result.Status != PromptStatus.OK)
                return;

            LoadPlugin(result.StringResult);
        }

        [CommandMethod("DEVRELOAD", CommandFlags.Modal)]
        public void Reload()
        {
            string sourcePath;
            lock (SyncRoot)
                sourcePath = _current == null ? null : _current.SourcePath;

            if (String.IsNullOrWhiteSpace(sourcePath)) sourcePath = ReadLastDll();

            if (String.IsNullOrWhiteSpace(sourcePath))
            {
                WriteLine(T("\nChưa có DLL nào được ghi nhớ. Hãy chạy DEVLOAD trước.", "\nNo remembered plug-in DLL. Run DEVLOAD first."));
                return;
            }

            LoadPlugin(sourcePath);
        }

        [CommandMethod("DEVLIST", CommandFlags.Modal)]
        public void ListCommands()
        {
            LoadedPlugin plugin = GetCurrent();
            if (plugin == null)
                return;

            WriteLine(T("\nCác lệnh trong bản development mới nhất:", "\nCommands in the latest development build:"));
            foreach (PluginCommand command in plugin.Commands.OrderBy(x => x.Name))
                WriteLine("\n  " + command.Name + "  [" + command.Method.DeclaringType.FullName + "]");
        }

        [CommandMethod("DEVRUN", CommandFlags.Modal)]
        public void RunCommand()
        {
            LoadedPlugin plugin = GetCurrent();
            if (plugin == null)
                return;

            Editor editor = GetEditor();
            PromptStringOptions options =
                new PromptStringOptions(T("\nTên lệnh development:", "\nDevelopment command name:"));
            options.AllowSpaces = false;

            PromptResult result = editor.GetString(options);
            if (result.Status != PromptStatus.OK)
                return;

            PluginCommand command = plugin.Commands.FirstOrDefault(
                x => String.Equals(x.Name, result.StringResult, StringComparison.OrdinalIgnoreCase));

            if (command == null)
            {
                WriteLine(T("\nKhông tìm thấy lệnh. Chạy DEVLIST để xem các lệnh khả dụng.", "\nCommand not found. Run DEVLIST to see available commands."));
                return;
            }

            Invoke(command);
        }

        [CommandMethod("DEVSTATUS", CommandFlags.Modal)]
        public void Status()
        {
            LoadedPlugin plugin;
            lock (SyncRoot)
                plugin = _current;

            if (plugin == null)
            {
                WriteLine(T("\nChưa nạp plugin development nào.", "\nNo development plug-in is loaded."));
                return;
            }

            WriteLine(T("\nDLL nguồn: ", "\nSource: ") + plugin.SourcePath);
            WriteLine(T("\nBản sao đã nạp: ", "\nLoaded copy: ") + plugin.LoadedPath);
            WriteLine(T("\nThời điểm nạp: ", "\nLoaded at: ") + plugin.LoadedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            WriteLine(T("\nSố lệnh: ", "\nCommands: ") + plugin.Commands.Count);
        }

        [CommandMethod("DEVSHOW", CommandFlags.Modal)]
        public void ShowPanel()
        {
            ShowQuickBar();
            RefreshQuickCommands();
            UpdateInfoPanel();
        }

        private static void LoadPlugin(string sourcePath)
        {
            LoadedPlugin previous = null;
            ResolveEventHandler previousResolver = null;
            ResolveEventHandler nextResolver = null;
            bool committed = false;
            try
            {
                string fullSourcePath = Path.GetFullPath(sourcePath);
                if (!File.Exists(fullSourcePath))
                    throw new FileNotFoundException(T("Không tìm thấy DLL plugin.", "Plug-in DLL was not found."), fullSourcePath);

                lock (SyncRoot)
                {
                    previous = _current;
                    previousResolver = _dependencyResolver;
                }

                if (previousResolver != null)
                {
                    AppDomain.CurrentDomain.AssemblyResolve -= previousResolver;
                    _dependencyResolver = null;
                }

                string sourceDirectory = Path.GetDirectoryName(fullSourcePath);
                string cacheDirectory = CreateCacheDirectory(fullSourcePath);
                CopyRuntimeFiles(sourceDirectory, cacheDirectory);

                nextResolver = delegate(object sender, ResolveEventArgs args)
                {
                    return ResolveDependency(args, cacheDirectory);
                };
                AppDomain.CurrentDomain.AssemblyResolve += nextResolver;

                string loadedPath = Path.Combine(cacheDirectory, Path.GetFileName(fullSourcePath));
                AppDomain.CurrentDomain.SetData("CadDevLoader.SourcePath", fullSourcePath);
                AppDomain.CurrentDomain.SetData("CadDevLoader.CacheDirectory", cacheDirectory);
                byte[] assemblyBytes = File.ReadAllBytes(loadedPath);
                string pdbPath = Path.ChangeExtension(loadedPath, ".pdb");
                Assembly assembly = File.Exists(pdbPath)
                    ? Assembly.Load(assemblyBytes, File.ReadAllBytes(pdbPath))
                    : Assembly.Load(assemblyBytes);
                List<PluginCommand> commands = DiscoverCommands(assembly);
                string cleanupAssemblyName = previous != null
                    ? previous.Assembly.GetName().Name
                    : assembly.GetName().Name;

                if (previous != null) TerminateExtensions(previous);
                TryCleanupAllPluginUi(cleanupAssemblyName);
                List<IExtensionApplication> extensions = InitializeExtensions(assembly);

                bool isReload = previous != null;

                LoadedPlugin plugin = new LoadedPlugin(
                    fullSourcePath,
                    loadedPath,
                    DateTime.Now,
                    assembly,
                    commands,
                    extensions);

                lock (SyncRoot)
                {
                    _current = plugin;
                    _dependencyResolver = nextResolver;
                }
                committed = true;

                SaveLastDll(fullSourcePath);
                _observedWriteUtc = File.GetLastWriteTimeUtc(fullSourcePath);
                if (isReload) _reloadCount++;
                _lastError = "";
                if (_reloadButton != null)
                {
                    _reloadButton.Text = T("RELOAD BẢN BUILD MỚI", "RELOAD LATEST BUILD");
                    _reloadButton.BackColor = Color.FromArgb(39, 174, 116);
                }
                RefreshQuickCommands();
                UpdateInfoPanel();

                WriteLine(T("\nĐã nạp bản sao development: ", "\nLoaded development copy: ") + loadedPath);
                WriteLine("\nAssembly MVID: " + assembly.ManifestModule.ModuleVersionId);
                WriteLine(T("\nTìm thấy ", "\nFound ") + commands.Count + T(" lệnh không tham số.", " parameterless command(s)."));
            }
            catch (System.Exception exception)
            {
                if (!committed)
                {
                    if (nextResolver != null)
                        AppDomain.CurrentDomain.AssemblyResolve -= nextResolver;
                    if (previousResolver != null)
                    {
                        AppDomain.CurrentDomain.AssemblyResolve += previousResolver;
                        _dependencyResolver = previousResolver;
                    }
                    lock (SyncRoot) _current = previous;
                }
                WriteException(T("DEVLOAD thất bại", "DEVLOAD failed"), exception);
            }
        }

        private static void TryCleanupPreviousUi(LoadedPlugin plugin)
        {
            try
            {
                MethodInfo cleanup = GetLoadableTypes(plugin.Assembly)
                    .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    .FirstOrDefault(method =>
                        method.GetParameters().Length == 0 &&
                        (String.Equals(method.Name, "CloseAllPalettes", StringComparison.OrdinalIgnoreCase) ||
                         String.Equals(method.Name, "DevCleanup", StringComparison.OrdinalIgnoreCase)));

                if (cleanup != null)
                {
                    cleanup.Invoke(null, null);
                    WriteLine(T("\nĐã đóng giao diện của bản development trước.", "\nClosed UI from the previous development build."));
                }
            }
            catch (System.Exception exception)
            {
                _lastError = T("Dọn giao diện cũ: ", "Cleanup previous UI: ") + exception.Message;
                UpdateInfoPanel();
            }
        }

        private static void TryCleanupAllPluginUi(string targetAssemblyName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == typeof(DevLoaderCommands).Assembly) continue;
                string assemblyName = assembly.GetName().Name ?? "";
                if (!assemblyName.Equals(targetAssemblyName, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    MethodInfo cleanup = GetLoadableTypes(assembly)
                        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                        .FirstOrDefault(method =>
                            method.GetParameters().Length == 0 &&
                            (String.Equals(method.Name, "CloseAllPalettes", StringComparison.OrdinalIgnoreCase) ||
                             String.Equals(method.Name, "DevCleanup", StringComparison.OrdinalIgnoreCase)));

                    if (cleanup == null) continue;
                    cleanup.Invoke(null, null);
                    WriteLine(T("\nĐã đóng giao diện plugin cũ: ", "\nClosed old plug-in UI: ") + assembly.GetName().Name);
                }
                catch (System.Exception exception)
                {
                    _lastError = T("Dọn giao diện ", "Cleanup ") + assembly.GetName().Name + ": " + exception.Message;
                }
            }
            UpdateInfoPanel();
        }

        private static List<IExtensionApplication> InitializeExtensions(Assembly assembly)
        {
            var extensions = new List<IExtensionApplication>();
            foreach (Type type in GetLoadableTypes(assembly))
            {
                if (type.IsAbstract || !typeof(IExtensionApplication).IsAssignableFrom(type))
                    continue;

                try
                {
                    var extension = (IExtensionApplication)Activator.CreateInstance(type, true);
                    extension.Initialize();
                    extensions.Add(extension);
                }
                catch (System.Exception exception)
                {
                    _lastError = T("Khởi tạo extension ", "Initialize extension ") + type.FullName + ": " + exception.Message;
                    WriteLine("\n" + _lastError);
                }
            }
            return extensions;
        }

        private static void TerminateExtensions(LoadedPlugin plugin)
        {
            if (plugin == null || plugin.Extensions == null) return;
            for (int index = plugin.Extensions.Count - 1; index >= 0; index--)
            {
                try
                {
                    plugin.Extensions[index].Terminate();
                }
                catch (System.Exception exception)
                {
                    _lastError = T("Kết thúc extension cũ: ", "Terminate previous extension: ") + exception.Message;
                    WriteLine("\n" + _lastError);
                }
            }
        }

        private static Assembly ResolveDependency(ResolveEventArgs args, string cacheDirectory)
        {
            try
            {
                string simpleName = new AssemblyName(args.Name).Name;
                Assembly loaded = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(item => String.Equals(item.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
                if (loaded != null) return loaded;

                string candidate = Path.Combine(cacheDirectory, simpleName + ".dll");
                if (!File.Exists(candidate)) return null;
                return Assembly.Load(File.ReadAllBytes(candidate));
            }
            catch (System.Exception exception)
            {
                _lastError = T("Nạp dependency thất bại: ", "Dependency load failed: ") + exception.Message;
                return null;
            }
        }

        private static string CreateCacheDirectory(string sourcePath)
        {
            string name = Path.GetFileNameWithoutExtension(sourcePath);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string root = Path.Combine(
                Path.GetTempPath(),
                "CadDevLoader",
                GetAutoCadVersion(),
                name);
            string directory = Path.Combine(root, stamp);
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string GetAutoCadVersion()
        {
            try
            {
                return AcadApplication.Version.Major + "." + AcadApplication.Version.Minor;
            }
            catch
            {
                return "unknown";
            }
        }

        private static void CopyRuntimeFiles(string sourceDirectory, string targetDirectory)
        {
            string[] patterns = { "*.dll", "*.pdb", "*.json", "*.config" };
            string[] hostAssemblies = { "AcCoreMgd", "AcMgd", "AcDbMgd", "AcCui", "AcWindows", "AdWindows" };
            foreach (string pattern in patterns)
            {
                foreach (string file in Directory.GetFiles(sourceDirectory, pattern))
                {
                    string runtimeName = Path.GetFileNameWithoutExtension(file);
                    if (hostAssemblies.Any(name => String.Equals(name, runtimeName, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    string destination = Path.Combine(targetDirectory, Path.GetFileName(file));
                    File.Copy(file, destination, true);
                }
            }
        }

        private static List<PluginCommand> DiscoverCommands(Assembly assembly)
        {
            var commands = new List<PluginCommand>();
            foreach (Type type in GetLoadableTypes(assembly))
            {
                const BindingFlags flags =
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static;

                foreach (MethodInfo method in type.GetMethods(flags))
                {
                    if (method.GetParameters().Length != 0)
                        continue;

                    object[] attributes =
                        method.GetCustomAttributes(typeof(CommandMethodAttribute), false);
                    foreach (CommandMethodAttribute attribute in attributes)
                    {
                        if (!String.IsNullOrWhiteSpace(attribute.GlobalName))
                            commands.Add(new PluginCommand(attribute.GlobalName, method));
                    }
                }
            }

            return commands;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                foreach (string warning in exception.LoaderExceptions
                    .Where(item => item != null)
                    .Select(item => item.Message)
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                    WriteLine(T("\nCảnh báo dependency: ", "\nDependency warning: ") + warning);

                return exception.Types.Where(x => x != null);
            }
        }

        private static void Invoke(PluginCommand command)
        {
            try
            {
                object instance = null;
                if (!command.Method.IsStatic)
                    instance = Activator.CreateInstance(command.Method.DeclaringType, true);

                object returnValue = command.Method.Invoke(instance, null);
                RememberRecentCommand(command.Name);
                WriteLine(T("\nĐã chạy lệnh development: ", "\nExecuted development command: ") + command.Name);

                if (returnValue != null)
                    WriteLine(T("\nGiá trị trả về: ", "\nReturn value: ") + returnValue);
            }
            catch (TargetInvocationException exception)
            {
                WriteException(
                    T("Lệnh ", "Command ") + command.Name + T(" thất bại", " failed"),
                    exception.InnerException ?? exception);
            }
            catch (System.Exception exception)
            {
                WriteException(T("Lệnh ", "Command ") + command.Name + T(" thất bại", " failed"), exception);
            }
        }

        private static LoadedPlugin GetCurrent()
        {
            lock (SyncRoot)
            {
                if (_current != null)
                    return _current;
            }

            WriteLine(T("\nChưa nạp plugin development. Hãy chạy DEVLOAD trước.", "\nNo development plug-in is loaded. Run DEVLOAD first."));
            return null;
        }

        private static Editor GetEditor()
        {
            Document document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                throw new InvalidOperationException(T("Không có bản vẽ AutoCAD đang hoạt động.", "No active AutoCAD document."));

            return document.Editor;
        }

        private static void WriteException(string title, System.Exception exception)
        {
            _lastError = title + ": " + exception.ToString();
            UpdateInfoPanel();
            WriteLine("\n" + title + ": " + exception.Message);
        }

        private static void WriteLine(string message)
        {
            Document document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document != null)
                document.Editor.WriteMessage(message);
        }

        private sealed class LoadedPlugin
        {
            public LoadedPlugin(
                string sourcePath,
                string loadedPath,
                DateTime loadedAt,
                Assembly assembly,
                List<PluginCommand> commands,
                List<IExtensionApplication> extensions)
            {
                SourcePath = sourcePath;
                LoadedPath = loadedPath;
                LoadedAt = loadedAt;
                Assembly = assembly;
                Commands = commands;
                Extensions = extensions;
            }

            public string SourcePath { get; private set; }
            public string LoadedPath { get; private set; }
            public DateTime LoadedAt { get; private set; }
            public Assembly Assembly { get; private set; }
            public List<PluginCommand> Commands { get; private set; }
            public List<IExtensionApplication> Extensions { get; private set; }
        }

        private sealed class PluginCommand
        {
            public PluginCommand(string name, MethodInfo method)
            {
                Name = name;
                Method = method;
            }

            public string Name { get; private set; }
            public MethodInfo Method { get; private set; }
        }
    }
}
