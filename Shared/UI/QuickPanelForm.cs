using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CadDevLoader.Shared.Commands;
using CadDevLoader.Shared.Data;
using CadDevLoader.Shared.Localization;
using CadDevLoader.Shared.Logging;
using CadDevLoader.Shared.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadDevLoader.Shared.UI
{
    public static class QuickPanelForm
    {
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
        private static ToolTip _toolTip;
        private static CheckBox _autoReloadCheck;
        private static int _reloadCount;

        public static Func<LoadedPlugin> GetCurrentPlugin { get; set; }
        public static Action QueueReloadOrChooseDll { get; set; }
        public static Action CleanCacheAction { get; set; }

        public static void Initialize(Func<LoadedPlugin> getCurrentPlugin, Action queueReloadOrChooseDll, Action cleanCacheAction)
        {
            GetCurrentPlugin = getCurrentPlugin;
            QueueReloadOrChooseDll = queueReloadOrChooseDll;
            CleanCacheAction = cleanCacheAction;
            DevLogger.ErrorUpdated += UpdateInfoPanel;
        }

        public static void SetReloadCount(int count)
        {
            _reloadCount = count;
            UpdateInfoPanel();
        }

        private static void ShowAboutDialog()
        {
            Form about = new Form
            {
                Text = L10n.T("Giới thiệu", "About"),
                Size = new Size(360, 220),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(24, 29, 38),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };
            
            Label title = new Label { Text = "CAD DEV LOADER", Location = new Point(20, 20), Size = new Size(300, 25), Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = Color.FromArgb(255, 211, 78) };
            Label version = new Label { Text = "Phiên bản (Version) 2.0.0", Location = new Point(20, 50), Size = new Size(300, 20), ForeColor = Color.FromArgb(145, 158, 180) };
            Label desc = new Label { Text = L10n.T("Công cụ Hot-Reload hỗ trợ phát triển Plugin AutoCAD .NET.\nTự động nạp lại DLL khi có bản build mới.", "Hot-Reload tool for AutoCAD .NET Plugin development.\nAutomatically reloads DLL on new builds."), Location = new Point(20, 80), Size = new Size(300, 40) };
            
            LinkLabel link = new LinkLabel { Text = "https://github.com/Congthanhx1/CadDevLoader", Location = new Point(20, 130), Size = new Size(300, 20), LinkColor = Color.FromArgb(112, 210, 164), ActiveLinkColor = Color.White };
            link.LinkClicked += (s, e) => System.Diagnostics.Process.Start(link.Text);
            
            about.Controls.Add(title);
            about.Controls.Add(version);
            about.Controls.Add(desc);
            about.Controls.Add(link);
            
            about.ShowDialog();
        }

        public static void ShowQuickBar()
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
                ShowInTaskbar = false,
                MaximizeBox = false,
                MinimizeBox = false,
                Font = new Font("Segoe UI", 9F),
                AutoScroll = false,
                AutoScaleMode = AutoScaleMode.Dpi
            };

            Point savedPos = SettingsStore.LoadPanelPosition();
            bar.Location = savedPos != Point.Empty
                ? savedPos
                : new Point(SettingsStore.GetAcadScreen().WorkingArea.Right - bar.Width - 18, SettingsStore.GetAcadScreen().WorkingArea.Top + 90);
            bar.FormClosing += (s, e) => SettingsStore.SavePanelPosition(bar.Location);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.FromArgb(35, 43, 55) };
            Label mark = new Label { Text = "D", Location = new Point(12, 10), Size = new Size(28, 28), BackColor = Color.FromArgb(255, 211, 78), ForeColor = Color.FromArgb(23, 32, 51), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
            Label title = new Label { Text = "CAD DEV LOADER", Location = new Point(49, 7), Size = new Size(140, 20), ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            Label subtitle = new Label { Text = L10n.T("Reload nhanh plugin AutoCAD .NET", "Hot reload AutoCAD .NET"), Location = new Point(49, 26), Size = new Size(140, 16), ForeColor = Color.FromArgb(145, 158, 180), Font = new Font("Segoe UI", 7.5F) };
            Button infoButton = new Button { Text = "i", Location = new Point(194, 10), Size = new Size(24, 24), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(54, 63, 78), ForeColor = Color.FromArgb(145, 158, 180), Font = new Font("Segoe UI", 9F, FontStyle.Bold | FontStyle.Italic), Cursor = Cursors.Hand };
            infoButton.FlatAppearance.BorderSize = 0;
            infoButton.Click += (s, e) => ShowAboutDialog();
            _languageButton = new Button { Text = SettingsStore.UseEnglish ? "EN" : "VI", Location = new Point(222, 10), Size = new Size(36, 24), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(54, 63, 78), ForeColor = Color.FromArgb(255, 211, 78), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), Cursor = Cursors.Hand };
            _languageButton.FlatAppearance.BorderSize = 0;
            _languageButton.Click += (s, e) => ToggleLanguage();
            Button close = new Button { Text = "×", Location = new Point(264, 10), Size = new Size(24, 24), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(54, 63, 78), ForeColor = Color.White, Font = new Font("Segoe UI", 10F), Cursor = Cursors.Hand };
            close.FlatAppearance.BorderSize = 0;
            close.Click += (s, e) => { SettingsStore.SavePanelPosition(bar.Location); bar.Hide(); };
            header.Controls.Add(mark); header.Controls.Add(title); header.Controls.Add(subtitle); header.Controls.Add(infoButton); header.Controls.Add(_languageButton); header.Controls.Add(close);
            infoButton.BringToFront();
            bar.Controls.Add(header);

            Point dragOrigin = Point.Empty;
            header.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) dragOrigin = e.Location; };
            header.MouseMove += (s, e) => { if (e.Button == MouseButtons.Left) bar.Location = new Point(bar.Left + e.X - dragOrigin.X, bar.Top + e.Y - dragOrigin.Y); };
            header.MouseUp   += (s, e) => { if (e.Button == MouseButtons.Left) SettingsStore.SavePanelPosition(bar.Location); };
            title.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) dragOrigin = new Point(e.X + title.Left, e.Y + title.Top); };
            title.MouseMove += (s, e) => { if (e.Button == MouseButtons.Left) bar.Location = new Point(bar.Left + e.X + title.Left - dragOrigin.X, bar.Top + e.Y + title.Top - dragOrigin.Y); };
            title.MouseUp   += (s, e) => { if (e.Button == MouseButtons.Left) SettingsStore.SavePanelPosition(bar.Location); };

            _toolTip = _toolTip ?? new ToolTip { InitialDelay = 350, ReshowDelay = 100, AutoPopDelay = 7000 };
            _toolTip.SetToolTip(_languageButton, L10n.T("Chuyển sang tiếng Anh", "Switch to Vietnamese"));

            Panel statusCard = new Panel { Location = new Point(18, 59), Size = new Size(264, 62), BackColor = Color.FromArgb(30, 36, 47), Cursor = Cursors.Hand };
            _statusDot = new Label { Location = new Point(10, 8), Size = new Size(72, 18), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            _dllInfo = new Label { Location = new Point(84, 7), Size = new Size(168, 20), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleRight };
            _buildInfo = new Label { Location = new Point(10, 29), Size = new Size(242, 17), ForeColor = Color.FromArgb(145, 158, 180), Font = new Font("Segoe UI", 8F), AutoEllipsis = true };
            _warningInfo = new Label { Location = new Point(10, 45), Size = new Size(242, 15), ForeColor = Color.FromArgb(255, 184, 77), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), Visible = false };
            statusCard.Controls.Add(_statusDot); statusCard.Controls.Add(_dllInfo); statusCard.Controls.Add(_buildInfo); statusCard.Controls.Add(_warningInfo);
            Action showStatus = () => CommandExecutor.QueueCommand("DEVSTATUS");
            statusCard.Click += (s, e) => showStatus();
            _statusDot.Click += (s, e) => showStatus();
            _dllInfo.Click += (s, e) => showStatus();
            _buildInfo.Click += (s, e) => showStatus();
            bar.Controls.Add(statusCard);

            _reloadButton = AddQuickButton(bar, L10n.T("↻ RELOAD BẢN BUILD MỚI", "↻ RELOAD LATEST BUILD"), 18, 131, Color.FromArgb(28, 166, 122), () => QueueReloadOrChooseDll?.Invoke());
            _reloadButton.Size = new Size(218, 36);
            _moreButton = AddQuickButton(bar, "⋯", 244, 131, Color.FromArgb(54, 63, 78), () => { });
            _moreButton.Size = new Size(38, 36);
            _moreButton.Font = new Font("Segoe UI", 13F, FontStyle.Bold);

            ContextMenuStrip moreMenu = new ContextMenuStrip { BackColor = Color.FromArgb(35, 43, 55), ForeColor = Color.White, ShowImageMargin = false, Font = new Font("Segoe UI", 9F) };
            moreMenu.Items.Add(L10n.T("Nạp / đổi DLL", "Load / change DLL"), null, (s, e) => CommandExecutor.QueueCommand("DEVLOAD"));
            moreMenu.Items.Add(L10n.T("Xem trạng thái", "View status"), null, (s, e) => CommandExecutor.QueueCommand("DEVSTATUS"));
            moreMenu.Items.Add(new ToolStripSeparator());
            moreMenu.Items.Add(L10n.T("Dọn cache cũ", "Clean old cache"), null, (s, e) => CleanCacheAction?.Invoke());
            moreMenu.Items.Add(L10n.T("Sao chép log lỗi", "Copy error log"), null, (s, e) => CopyLastError());
            moreMenu.Items.Add(L10n.T("Mở thư mục log", "Open log folder"), null, (s, e) => DevLogger.OpenLogFolder());
            _moreButton.Click += (s, e) => moreMenu.Show(_moreButton, new Point(0, _moreButton.Height));
            _toolTip.SetToolTip(_moreButton, L10n.T("Nạp DLL, trạng thái và công cụ bảo trì", "DLL loading, status and maintenance tools"));

            CheckBox autoReloadBox = new CheckBox
            {
                Text = L10n.T("Tự động reload khi có build mới", "Auto-reload on new build"),
                Location = new Point(20, 172),
                Size = new Size(260, 20),
                Checked = SettingsStore.AutoReload,
                ForeColor = Color.FromArgb(145, 158, 180),
                Font = new Font("Segoe UI", 7.5F),
                BackColor = Color.FromArgb(24, 29, 38),
                Cursor = Cursors.Hand
            };
            autoReloadBox.CheckedChanged += (s, e) =>
            {
                SettingsStore.AutoReload = autoReloadBox.Checked;
                SettingsStore.SaveAutoReload();
            };
            _autoReloadCheck = autoReloadBox;
            bar.Controls.Add(autoReloadBox);

            _searchBox = new TextBox { Location = new Point(18, 202), Size = new Size(264, 27), BackColor = Color.FromArgb(39, 46, 59), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9F) };
            _searchBox.Text = SearchPlaceholder();
            _searchBox.GotFocus += (s, e) => { if (_searchBox.Text == SearchPlaceholder()) _searchBox.Text = ""; };
            _searchBox.LostFocus += (s, e) => { if (String.IsNullOrWhiteSpace(_searchBox.Text)) _searchBox.Text = SearchPlaceholder(); };
            _searchBox.TextChanged += (s, e) => RefreshQuickCommands();
            bar.Controls.Add(_searchBox);

            Label tools = new Label
            {
                Text = L10n.T("CÔNG CỤ TRONG DLL", "TOOLS IN DLL"),
                Location = new Point(18, 234),
                Size = new Size(264, 22),
                ForeColor = Color.FromArgb(255, 211, 78),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            bar.Controls.Add(tools);

            _commandsPanel = new FlowLayoutPanel
            {
                Location = new Point(18, 258),
                Size = new Size(264, 268),
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
            Button copyError = AddQuickButton(bar, L10n.T("SAO CHÉP", "COPY LOG"), 222, 538, Color.FromArgb(54, 63, 78), CopyLastError);
            copyError.Size = new Size(60, 42);
            copyError.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            bar.Controls.Add(_errorInfo);

            RefreshQuickCommands();
            UpdateInfoPanel();

            _quickBar = bar;
            AcadApplication.ShowModelessDialog(bar);
        }

        private static string SearchPlaceholder()
        {
            return L10n.T("Tìm lệnh...", "Search commands...");
        }

        public static void HideQuickBar()
        {
            if (_quickBar != null && !_quickBar.IsDisposed)
                _quickBar.Close();
            _quickBar = null;
        }

        private static void ToggleLanguage()
        {
            SettingsStore.UseEnglish = !SettingsStore.UseEnglish;
            SettingsStore.SaveLanguage();

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

        public static void UpdateInfoPanel()
        {
            if (_dllInfo == null || _dllInfo.IsDisposed) return;
            if (_dllInfo.InvokeRequired)
            {
                _dllInfo.BeginInvoke(new Action(UpdateInfoPanel));
                return;
            }

            LoadedPlugin plugin = GetCurrentPlugin?.Invoke();
            string path = plugin != null ? plugin.SourcePath : SettingsStore.ReadLastDll();
            
            _dllInfo.Text = String.IsNullOrWhiteSpace(path) ? L10n.T("Chưa chọn DLL", "No DLL selected") : Path.GetFileName(path);
            
            if (plugin != null) _buildInfo.Text = plugin.Commands.Count + L10n.T(" lệnh · Reload ", " commands · Reloaded ") + plugin.LoadedAt.ToString("HH:mm:ss");
            else if (!String.IsNullOrWhiteSpace(path) && File.Exists(path)) _buildInfo.Text = L10n.T("Đã nhớ DLL · Build ", "Remembered DLL · Build ") + File.GetLastWriteTime(path).ToString("HH:mm:ss");
            else _buildInfo.Text = L10n.T("Chọn DLL để bắt đầu phát triển", "Choose a DLL to start developing");
            
            if (_statusDot != null)
            {
                _statusDot.Text = plugin != null
                    ? L10n.T("● ĐÃ NẠP", "● LOADED")
                    : (!String.IsNullOrWhiteSpace(path) && File.Exists(path) ? L10n.T("● ĐÃ NHỚ", "● SAVED") : L10n.T("○ CHƯA NẠP", "○ NOT LOADED"));
                _statusDot.ForeColor = plugin != null
                    ? Color.FromArgb(80, 215, 154)
                    : (!String.IsNullOrWhiteSpace(path) && File.Exists(path) ? Color.FromArgb(255, 205, 72) : Color.FromArgb(145, 158, 180));
            }
            
            if (_reloadButton != null)
            {
                bool hasDll = !String.IsNullOrWhiteSpace(path) && File.Exists(path);
                bool hasNewBuild = hasDll && CadDevLoader.Shared.Watching.BuildWatcher.ObservedWriteUtc != DateTime.MinValue && File.GetLastWriteTimeUtc(path) > CadDevLoader.Shared.Watching.BuildWatcher.ObservedWriteUtc;
                _reloadButton.Text = !hasDll
                    ? L10n.T("+ CHỌN DLL", "+ CHOOSE DLL")
                    : (hasNewBuild ? L10n.T("● BUILD MỚI — RELOAD", "● NEW BUILD — RELOAD") : L10n.T("↻ RELOAD BẢN BUILD MỚI", "↻ RELOAD LATEST BUILD"));
                _reloadButton.BackColor = !hasDll
                    ? Color.FromArgb(0, 157, 183)
                    : (hasNewBuild ? Color.FromArgb(225, 132, 24) : Color.FromArgb(28, 166, 122));
            }
            
            if (_warningInfo != null)
            {
                _warningInfo.Visible = _reloadCount >= 8;
                _warningInfo.Text = _reloadCount >= 8 ? L10n.T("⚠ Reload nhiều lần — nên mở lại AutoCAD", "⚠ Many reloads — restart AutoCAD soon") : "";
            }
            
            if (_errorInfo != null)
            {
                bool hasError = !String.IsNullOrWhiteSpace(DevLogger.LastError);
                _errorInfo.Text = hasError ? "⚠ " + DevLogger.LastError : L10n.T("✓ Không có lỗi gần đây", "✓ No recent errors");
                _errorInfo.ForeColor = hasError ? Color.FromArgb(255, 143, 143) : Color.FromArgb(112, 210, 164);
            }
        }

        public static void QueueQuickCommandRefresh()
        {
            if (_commandsPanel == null || _commandsPanel.IsDisposed) return;
            if (_commandsPanel.IsHandleCreated)
                _commandsPanel.BeginInvoke(new Action(RefreshQuickCommands));
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
            LoadedPlugin plugin = GetCurrentPlugin?.Invoke();

            if (plugin == null || plugin.Commands.Count == 0)
            {
                _commandsPanel.Controls.Add(new Label
                {
                    Text = L10n.T("Chưa có DLL nào được nạp.\nBấm CHỌN DLL để bắt đầu.", "No DLL is loaded.\nChoose a DLL to get started."),
                    Size = new Size(226, 70),
                    ForeColor = Color.FromArgb(170, 180, 196),
                    TextAlign = ContentAlignment.MiddleCenter
                });
                _commandsPanel.ResumeLayout();
                return;
            }

            string search = _searchBox == null || _searchBox.Text == SearchPlaceholder() ? "" : _searchBox.Text.Trim();
            List<PluginCommand> commands = plugin.Commands
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .Where(x => String.IsNullOrWhiteSpace(search) || x.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || CommandExecutor.GetCommandDisplayName(x.Name).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(x => CommandExecutor.GetCommandDisplayName(x.Name))
                .ToList();

            if (!String.IsNullOrWhiteSpace(search))
            {
                AddCommandGroup(L10n.T("KẾT QUẢ TÌM KIẾM", "SEARCH RESULTS"));
                foreach (PluginCommand command in commands) AddCommandRow(command, false);
            }
            else
            {
                List<PluginCommand> favorites = commands.Where(x => SettingsStore.Favorites.Contains(x.Name)).ToList();
                if (favorites.Count > 0)
                {
                    AddCommandGroup(L10n.T("★ YÊU THÍCH", "★ FAVORITES"));
                    foreach (PluginCommand command in favorites) AddCommandRow(command, false);
                }

                List<PluginCommand> recent = CadDevLoader.Shared.Settings.SettingsStore.RecentCommands
                    .Select(name => commands.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    .Where(command => command != null && !SettingsStore.Favorites.Contains(command.Name))
                    .ToList();
                if (recent.Count > 0)
                {
                    AddCommandGroup(L10n.T("DÙNG GẦN ĐÂY", "RECENTLY USED"));
                    foreach (PluginCommand command in recent) AddCommandRow(command, true);
                }

                HashSet<string> promoted = new HashSet<string>(favorites.Select(x => x.Name).Concat(recent.Select(x => x.Name)), StringComparer.OrdinalIgnoreCase);
                List<PluginCommand> remaining = commands.Where(x => !promoted.Contains(x.Name)).ToList();
                if (remaining.Count > 0)
                {
                    AddCommandGroup(L10n.T("TẤT CẢ LỆNH", "ALL COMMANDS"));
                    foreach (PluginCommand command in remaining) AddCommandRow(command, false);
                }
            }

            if (commands.Count == 0)
                _commandsPanel.Controls.Add(new Label { Text = L10n.T("Không tìm thấy lệnh phù hợp.", "No matching commands found."), Size = new Size(226, 52), ForeColor = Color.FromArgb(170, 180, 196), TextAlign = ContentAlignment.MiddleCenter });
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
            bool isFavorite = SettingsStore.Favorites.Contains(commandName);
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
                Text = CommandExecutor.GetCommandDisplayName(commandName),
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
            run.Click += (s, e) => CommandExecutor.QueueDevRun(commandName);
            star.Click += (s, e) =>
            {
                if (SettingsStore.Favorites.Contains(commandName)) SettingsStore.Favorites.Remove(commandName); else SettingsStore.Favorites.Add(commandName);
                SettingsStore.SaveFavorites();
                QueueQuickCommandRefresh();
            };
            string flagHint = "";
            if (command.IsSession) flagHint += L10n.T(" [Session]", " [Session]");
            if (command.IsAsync) flagHint += L10n.T(" [Async]", " [Async]");
            _toolTip.SetToolTip(run, L10n.T("Chạy lệnh ", "Run command ") + commandName + flagHint);
            _toolTip.SetToolTip(star, isFavorite ? L10n.T("Bỏ khỏi yêu thích", "Remove from favorites") : L10n.T("Thêm vào yêu thích", "Add to favorites"));
            row.Controls.Add(star);
            row.Controls.Add(run);
            _commandsPanel.Controls.Add(row);
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

        private static void CopyLastError()
        {
            if (String.IsNullOrWhiteSpace(DevLogger.LastError)) return;
            try
            {
                Clipboard.SetText(DevLogger.LastError);
            }
            catch (Exception exception)
            {
                DevLogger.LastError = L10n.T("Không thể sao chép log: ", "Could not copy the log: ") + exception.Message;
            }
        }
    }
}
