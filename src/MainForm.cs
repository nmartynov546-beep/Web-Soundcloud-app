using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SoundCloudDesktop
{
    internal sealed class MainForm : Form
    {
        private readonly AppSettings _settings;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private readonly Icon _appIcon;
        private readonly ToolTip _toolTip = new ToolTip();
        private readonly List<ToolStripMenuItem> _themeMenuItems = new List<ToolStripMenuItem>();

        private ThemeDefinition _activeTheme;

        private WebView2 _webView;
        private CoreWebView2Environment _env;
        private CoreWebView2Controller _controller;

        private Panel _toolbar;
        private Panel _sepLeft;
        private Panel _sepRight;
        private Button _btnBack;
        private Button _btnForward;
        private Button _btnReload;
        private Button _btnHome;
        private Button _btnLikes;
        private Button _btnLibrary;
        private Button _btnUpload;
        private Label _lblNowPlaying;
        private Button _btnPrev;
        private Button _btnPlayPause;
        private Button _btnNext;
        private Button _btnTheme;
        private Button _btnMini;
        private Button _btnTopMost;
        private Button _btnSettings;
        private Button[] _toolbarButtons;

        private ContextMenuStrip _themeMenu;
        private ContextMenuStrip _settingsMenu;
        private ToolStripMenuItem _miSettingsToolbar;
        private ToolStripMenuItem _miSettingsMini;
        private ToolStripMenuItem _miSettingsFullscreen;
        private ToolStripMenuItem _miSettingsTheme;
        private ToolStripMenuItem _miSettingsTopMost;
        private ToolStripMenuItem _miSettingsCleanMode;
        private ToolStripMenuItem _miSettingsCloseToTray;
        private ToolStripMenuItem _miSettingsLyrics;
        private ToolStripMenuItem _miLyricsLrclib;
        private ToolStripMenuItem _miLyricsYandex;
        private ToolStripMenuItem _miSettingsAudioQuality;
        private ToolStripMenuItem _miQualityHigh;
        private ToolStripMenuItem _miQualityAuto;
        private ToolStripMenuItem _miQualityMp3;

        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _miTrayShowHide;
        private ToolStripMenuItem _miTrayPlayPause;
        private ToolStripMenuItem _miTrayTheme;
        private ToolStripMenuItem _miTrayTopMost;
        private ToolStripMenuItem _miTrayCloseToTray;

        private System.Windows.Forms.Timer _copiedTimer;
        private bool _copiedFlashActive;
        private string _nowPlayingText = "SoundCloud";
        private string _currentTrackUrl = "";
        private string _pendingUrl;
        private bool _isPlaying;
        private bool _webReady;
        private bool _isMiniMode;
        private bool _isFullscreen;
        private bool _allowExit;
        private volatile bool _stopPipe;

        private Rectangle _normalBounds;
        private FormWindowState _normalWindowState;
        private Rectangle _fsSavedBounds;
        private FormWindowState _fsSavedWindowState;

        private int _lastToggleTick;

        public MainForm(string initialUrl)
        {
            _settings = AppSettings.Load();
            _activeTheme = ThemeCatalog.GetById(_settings.ThemeId);
            _settings.ThemeId = _activeTheme.Id;
            _pendingUrl = initialUrl;
            _appIcon = LoadAppIcon();

            Text = "SoundCloud";
            BackColor = _activeTheme.BodyBg;
            MinimumSize = new Size(420, 140);
            KeyPreview = true;
            if (_appIcon != null)
            {
                Icon = _appIcon;
            }

            RestoreInitialBounds();
            BuildToolbar();
            BuildThemeMenu();
            BuildSettingsMenu();
            BuildWebView();
            BuildTrayIcon();
            ApplyThemeToShell();

            _copiedTimer = new System.Windows.Forms.Timer();
            _copiedTimer.Interval = 1500;
            _copiedTimer.Tick += OnCopiedTimerTick;

            Load += OnFormLoad;
            FormClosing += OnMainFormClosing;
            Resize += OnMainFormResize;
            Move += OnMainFormMove;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            bool dark = _activeTheme == null || _activeTheme.DarkTitleBar;
            NativeMethods.EnableDarkTitleBar(Handle, dark);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (HandleShortcut(keyData))
            {
                return true;
            }

            if (keyData == (Keys.Control | Keys.K) || keyData == (Keys.Control | Keys.L))
            {
                if (_webReady && _webView != null && !_webView.Focused)
                {
                    _webView.Focus();
                    RunScript("if (window.__scDesktop) window.__scDesktop.focusSearch();");
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private Icon LoadAppIcon()
        {
            try
            {
                Icon exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (exeIcon != null) return exeIcon;
            }
            catch
            {
            }

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new string[]
                {
                    Path.Combine(baseDir, "assets\\soundcloud.ico"),
                    Path.Combine(baseDir, "..\\assets\\soundcloud.ico")
                };
                for (int i = 0; i < candidates.Length; i++)
                {
                    string path = Path.GetFullPath(candidates[i]);
                    if (File.Exists(path))
                    {
                        return new Icon(path);
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private void RestoreInitialBounds()
        {
            Size = new Size(_settings.WindowWidth, _settings.WindowHeight);
            if (_settings.WindowX >= 0 && _settings.WindowY >= 0 && IsPointVisibleOnScreen(_settings.WindowX, _settings.WindowY))
            {
                StartPosition = FormStartPosition.Manual;
                Location = new Point(_settings.WindowX, _settings.WindowY);
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
            }

            if (_settings.IsMaximized)
            {
                WindowState = FormWindowState.Maximized;
            }

            TopMost = _settings.AlwaysOnTop;
            _normalBounds = new Rectangle(Location, Size);
            _normalWindowState = _settings.IsMaximized ? FormWindowState.Maximized : FormWindowState.Normal;
        }

        private static bool IsPointVisibleOnScreen(int x, int y)
        {
            Rectangle probe = new Rectangle(x, y, 120, 60);
            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i].WorkingArea.IntersectsWith(probe))
                {
                    return true;
                }
            }
            return false;
        }

        private void BuildToolbar()
        {
            _toolbar = new Panel();
            _toolbar.Dock = DockStyle.Top;
            _toolbar.Height = 34;
            _toolbar.BackColor = _activeTheme.ToolbarBg;
            _toolbar.Padding = new Padding(6, 3, 6, 4);
            _toolbar.Paint += OnToolbarPaint;

            FlowLayoutPanel leftGroup = new FlowLayoutPanel();
            leftGroup.Dock = DockStyle.Left;
            leftGroup.AutoSize = true;
            leftGroup.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            leftGroup.WrapContents = false;
            leftGroup.BackColor = Color.Transparent;
            leftGroup.Margin = new Padding(0);
            leftGroup.Padding = new Padding(0);

            _btnBack = CreateToolbarButton("\u2190", 28, "Back (Alt+Left)");
            _btnBack.Click += delegate { GoBack(); };

            _btnForward = CreateToolbarButton("\u2192", 28, "Forward (Alt+Right)");
            _btnForward.Click += delegate { GoForward(); };

            _btnReload = CreateToolbarButton("\u21BB", 28, "Reload (F5 / Ctrl+R)");
            _btnReload.Click += delegate { ReloadPage(); };

            _sepLeft = new Panel();
            _sepLeft.Width = 1;
            _sepLeft.Height = 18;
            _sepLeft.BackColor = _activeTheme.BorderColor;
            _sepLeft.Margin = new Padding(4, 4, 4, 4);

            _btnHome = CreateToolbarButton("Home", 50, "Discover");
            _btnHome.Click += delegate { NavigateTo(_settings.HomeUrl); };

            _btnLikes = CreateToolbarButton("Likes", 48, "Likes");
            _btnLikes.Click += delegate { NavigateTo("https://soundcloud.com/you/likes"); };

            _btnLibrary = CreateToolbarButton("Library", 56, "Library");
            _btnLibrary.Click += delegate { NavigateTo("https://soundcloud.com/you/library"); };

            _btnUpload = CreateToolbarButton("Upload", 56, "Upload");
            _btnUpload.Click += delegate { NavigateTo("https://soundcloud.com/upload"); };

            leftGroup.Controls.Add(_btnBack);
            leftGroup.Controls.Add(_btnForward);
            leftGroup.Controls.Add(_btnReload);
            leftGroup.Controls.Add(_sepLeft);
            leftGroup.Controls.Add(_btnHome);
            leftGroup.Controls.Add(_btnLikes);
            leftGroup.Controls.Add(_btnLibrary);
            leftGroup.Controls.Add(_btnUpload);

            FlowLayoutPanel rightGroup = new FlowLayoutPanel();
            rightGroup.Dock = DockStyle.Right;
            rightGroup.AutoSize = true;
            rightGroup.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            rightGroup.WrapContents = false;
            rightGroup.BackColor = Color.Transparent;
            rightGroup.Margin = new Padding(0);
            rightGroup.Padding = new Padding(0);

            _btnPrev = CreateToolbarButton("|\u25C0", 30, "Previous");
            _btnPrev.Click += delegate { PrevTrack(); };

            _btnPlayPause = CreateToolbarButton("\u25B6", 32, "Play / Pause");
            _btnPlayPause.Click += delegate { TogglePlayPause(); };

            _btnNext = CreateToolbarButton("\u25B6|", 30, "Next");
            _btnNext.Click += delegate { NextTrack(); };

            _sepRight = new Panel();
            _sepRight.Width = 1;
            _sepRight.Height = 18;
            _sepRight.BackColor = _activeTheme.BorderColor;
            _sepRight.Margin = new Padding(4, 4, 4, 4);

            _btnTheme = CreateToolbarButton("Theme", 52, "Theme");
            _btnTheme.Click += OnThemeButtonClick;

            _btnMini = CreateToolbarButton("Mini", 44, "Mini-Player (Ctrl+M)");
            _btnMini.Click += delegate { ToggleMiniMode(); };

            _btnTopMost = CreateToolbarButton("Pin", 38, "Always on Top");
            _btnTopMost.Click += delegate { ToggleAlwaysOnTop(); };

            _btnSettings = CreateToolbarButton("\u2699", 30, "Settings");
            _btnSettings.Click += OnSettingsButtonClick;

            rightGroup.Controls.Add(_btnPrev);
            rightGroup.Controls.Add(_btnPlayPause);
            rightGroup.Controls.Add(_btnNext);
            rightGroup.Controls.Add(_sepRight);
            rightGroup.Controls.Add(_btnTheme);
            rightGroup.Controls.Add(_btnMini);
            rightGroup.Controls.Add(_btnTopMost);
            rightGroup.Controls.Add(_btnSettings);

            _toolbarButtons = new Button[]
            {
                _btnBack, _btnForward, _btnReload,
                _btnHome, _btnLikes, _btnLibrary, _btnUpload,
                _btnPrev, _btnPlayPause, _btnNext,
                _btnTheme, _btnMini, _btnTopMost, _btnSettings
            };

            _lblNowPlaying = new Label();
            _lblNowPlaying.Dock = DockStyle.Fill;
            _lblNowPlaying.AutoEllipsis = true;
            _lblNowPlaying.TextAlign = ContentAlignment.MiddleLeft;
            _lblNowPlaying.ForeColor = _activeTheme.TextMuted;
            _lblNowPlaying.BackColor = Color.Transparent;
            _lblNowPlaying.Font = new Font("Segoe UI", 9f);
            _lblNowPlaying.Padding = new Padding(10, 0, 10, 0);
            _lblNowPlaying.Cursor = Cursors.Hand;
            _lblNowPlaying.Text = "SoundCloud";
            _lblNowPlaying.Click += OnNowPlayingClick;
            _toolTip.SetToolTip(_lblNowPlaying, "Click to copy current track URL");

            _toolbar.Controls.Add(_lblNowPlaying);
            _toolbar.Controls.Add(leftGroup);
            _toolbar.Controls.Add(rightGroup);
        }

        private Button CreateToolbarButton(string text, int width, string tooltip)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Width = width;
            btn.Height = 26;
            btn.Margin = new Padding(1, 0, 1, 0);
            btn.Padding = new Padding(0);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = _activeTheme.ButtonHover;
            btn.FlatAppearance.MouseDownBackColor = _activeTheme.ButtonDown;
            btn.BackColor = Color.Transparent;
            btn.ForeColor = _activeTheme.TextPrimary;
            btn.Font = new Font("Segoe UI", 9f);
            btn.Cursor = Cursors.Hand;
            btn.TabStop = false;
            btn.UseCompatibleTextRendering = false;
            if (!string.IsNullOrEmpty(tooltip))
            {
                _toolTip.SetToolTip(btn, tooltip);
            }
            return btn;
        }

        private void OnToolbarPaint(object sender, PaintEventArgs e)
        {
            Color borderColor = _activeTheme != null ? _activeTheme.BorderColor : Color.FromArgb(40, 40, 40);
            using (Pen p = new Pen(borderColor))
            {
                e.Graphics.DrawLine(p, 0, _toolbar.Height - 1, _toolbar.Width, _toolbar.Height - 1);
            }
        }

        private void BuildThemeMenu()
        {
            _themeMenu = new ContextMenuStrip();
            _themeMenu.Renderer = new DarkMenuRenderer(_activeTheme);
            _themeMenu.ShowImageMargin = true;
            PopulateThemeMenuItems(_themeMenu.Items);
        }

        private void BuildSettingsMenu()
        {
            DarkMenuRenderer renderer = new DarkMenuRenderer(_activeTheme);
            _settingsMenu = new ContextMenuStrip();
            _settingsMenu.Renderer = renderer;
            _settingsMenu.ShowImageMargin = true;

            _miSettingsToolbar = new ToolStripMenuItem("Show Toolbar (Ctrl+H)");
            _miSettingsToolbar.Click += delegate { ToggleToolbar(); };

            _miSettingsMini = new ToolStripMenuItem("Mini-Player (Ctrl+M)");
            _miSettingsMini.Click += delegate { ToggleMiniMode(); };

            _miSettingsFullscreen = new ToolStripMenuItem("Fullscreen (F11)");
            _miSettingsFullscreen.Click += delegate { ToggleFullscreen(); };

            _miSettingsTheme = new ToolStripMenuItem("Theme");
            _miSettingsTheme.DropDown.Renderer = renderer;
            PopulateThemeMenuItems(_miSettingsTheme.DropDownItems);

            _miSettingsTopMost = new ToolStripMenuItem("Always on Top");
            _miSettingsTopMost.Click += delegate { ToggleAlwaysOnTop(); };

            _miSettingsCleanMode = new ToolStripMenuItem("Block Ads && Upsells");
            _miSettingsCleanMode.Click += delegate { ToggleCleanMode(); };

            _miSettingsCloseToTray = new ToolStripMenuItem("Close to Tray");
            _miSettingsCloseToTray.Click += delegate { ToggleCloseToTray(); };

            _miSettingsLyrics = new ToolStripMenuItem("Lyrics");
            _miSettingsLyrics.DropDown.Renderer = renderer;
            _miLyricsLrclib = new ToolStripMenuItem("LRCLIB (Default, Yandex fallback)");
            _miLyricsLrclib.Click += delegate { SetLyricsProvider("lrclib"); };
            _miLyricsYandex = new ToolStripMenuItem("Yandex Music (LRCLIB fallback)");
            _miLyricsYandex.Click += delegate { SetLyricsProvider("yandex"); };
            ToolStripMenuItem miYandexToken = new ToolStripMenuItem("Set Yandex Music Token...");
            miYandexToken.Click += delegate { ShowYandexTokenDialog(); };
            ToolStripMenuItem miOpenLyricsPage = new ToolStripMenuItem("Open Lyrics Page");
            miOpenLyricsPage.Click += delegate { OpenLyricsPage(); };
            _miSettingsLyrics.DropDownItems.Add(miOpenLyricsPage);
            _miSettingsLyrics.DropDownItems.Add(new ToolStripSeparator());
            _miSettingsLyrics.DropDownItems.Add(_miLyricsLrclib);
            _miSettingsLyrics.DropDownItems.Add(_miLyricsYandex);
            _miSettingsLyrics.DropDownItems.Add(new ToolStripSeparator());
            _miSettingsLyrics.DropDownItems.Add(miYandexToken);

            _miSettingsAudioQuality = new ToolStripMenuItem("Audio Quality");
            _miSettingsAudioQuality.DropDown.Renderer = renderer;
            _miQualityHigh = new ToolStripMenuItem("High (Prefer Opus)");
            _miQualityHigh.Click += delegate { SetAudioQuality("high"); };
            _miQualityAuto = new ToolStripMenuItem("Auto (SoundCloud Default)");
            _miQualityAuto.Click += delegate { SetAudioQuality("auto"); };
            _miQualityMp3 = new ToolStripMenuItem("Standard (Prefer MP3)");
            _miQualityMp3.Click += delegate { SetAudioQuality("mp3"); };
            _miSettingsAudioQuality.DropDownItems.Add(_miQualityHigh);
            _miSettingsAudioQuality.DropDownItems.Add(_miQualityAuto);
            _miSettingsAudioQuality.DropDownItems.Add(_miQualityMp3);

            ToolStripMenuItem miZoomIn = new ToolStripMenuItem("Zoom In (Ctrl++)");
            miZoomIn.Click += delegate { AdjustZoom(0.1); };

            ToolStripMenuItem miZoomOut = new ToolStripMenuItem("Zoom Out (Ctrl+-)");
            miZoomOut.Click += delegate { AdjustZoom(-0.1); };

            ToolStripMenuItem miZoomReset = new ToolStripMenuItem("Reset Zoom (Ctrl+0)");
            miZoomReset.Click += delegate { SetZoom(1.0); };

            ToolStripMenuItem miCopyUrl = new ToolStripMenuItem("Copy Track URL");
            miCopyUrl.Click += delegate { CopyCurrentTrackUrl(); };

            ToolStripMenuItem miOpenExternal = new ToolStripMenuItem("Open Current Page in Browser");
            miOpenExternal.Click += delegate
            {
                string url = GetCurrentUrl();
                if (!string.IsNullOrEmpty(url))
                {
                    OpenExternalUrl(url);
                }
            };

            ToolStripMenuItem miExit = new ToolStripMenuItem("Exit");
            miExit.Click += delegate { ExitApplication(); };

            _settingsMenu.Items.Add(_miSettingsToolbar);
            _settingsMenu.Items.Add(_miSettingsMini);
            _settingsMenu.Items.Add(_miSettingsFullscreen);
            _settingsMenu.Items.Add(_miSettingsTheme);
            _settingsMenu.Items.Add(new ToolStripSeparator());
            _settingsMenu.Items.Add(_miSettingsTopMost);
            _settingsMenu.Items.Add(_miSettingsCleanMode);
            _settingsMenu.Items.Add(_miSettingsCloseToTray);
            _settingsMenu.Items.Add(_miSettingsLyrics);
            _settingsMenu.Items.Add(_miSettingsAudioQuality);
            _settingsMenu.Items.Add(new ToolStripSeparator());
            _settingsMenu.Items.Add(miZoomIn);
            _settingsMenu.Items.Add(miZoomOut);
            _settingsMenu.Items.Add(miZoomReset);
            _settingsMenu.Items.Add(new ToolStripSeparator());
            _settingsMenu.Items.Add(miCopyUrl);
            _settingsMenu.Items.Add(miOpenExternal);
            _settingsMenu.Items.Add(new ToolStripSeparator());
            _settingsMenu.Items.Add(miExit);
        }

        private void BuildWebView()
        {
            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            _webView.DefaultBackgroundColor = _activeTheme.BodyBg;
            _webView.ZoomFactorChanged += OnWebViewZoomFactorChanged;
            _webView.KeyDown += OnWebViewKeyDown;

            Controls.Add(_webView);
            Controls.Add(_toolbar);
            _webView.BringToFront();
        }

        private void BuildTrayIcon()
        {
            DarkMenuRenderer renderer = new DarkMenuRenderer(_activeTheme);
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Renderer = renderer;

            _miTrayShowHide = new ToolStripMenuItem("Show / Hide");
            _miTrayShowHide.Click += delegate { ToggleWindowVisibility(); };

            _miTrayPlayPause = new ToolStripMenuItem("Play / Pause");
            _miTrayPlayPause.Click += delegate { TogglePlayPause(); };

            ToolStripMenuItem miTrayNext = new ToolStripMenuItem("Next");
            miTrayNext.Click += delegate { NextTrack(); };

            ToolStripMenuItem miTrayPrev = new ToolStripMenuItem("Previous");
            miTrayPrev.Click += delegate { PrevTrack(); };

            ToolStripMenuItem miTrayLyricsPage = new ToolStripMenuItem("Open Lyrics Page");
            miTrayLyricsPage.Click += delegate { OpenLyricsPage(); };

            _miTrayTheme = new ToolStripMenuItem("Theme");
            _miTrayTheme.DropDown.Renderer = renderer;
            PopulateThemeMenuItems(_miTrayTheme.DropDownItems);

            _miTrayTopMost = new ToolStripMenuItem("Always on Top");
            _miTrayTopMost.Click += delegate { ToggleAlwaysOnTop(); };

            _miTrayCloseToTray = new ToolStripMenuItem("Close to Tray");
            _miTrayCloseToTray.Click += delegate { ToggleCloseToTray(); };

            ToolStripMenuItem miTrayExit = new ToolStripMenuItem("Exit");
            miTrayExit.Click += delegate { ExitApplication(); };

            _trayMenu.Items.Add(_miTrayShowHide);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_miTrayPlayPause);
            _trayMenu.Items.Add(miTrayNext);
            _trayMenu.Items.Add(miTrayPrev);
            _trayMenu.Items.Add(miTrayLyricsPage);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_miTrayTheme);
            _trayMenu.Items.Add(_miTrayTopMost);
            _trayMenu.Items.Add(_miTrayCloseToTray);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(miTrayExit);

            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "SoundCloud";
            if (_appIcon != null)
            {
                _trayIcon.Icon = _appIcon;
            }
            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += delegate { ShowAndFocusWindow(); };
        }

        private void PopulateThemeMenuItems(ToolStripItemCollection items)
        {
            ThemeDefinition[] themes = ThemeCatalog.All;
            for (int i = 0; i < themes.Length; i++)
            {
                ThemeDefinition theme = themes[i];
                string themeId = theme.Id;
                ToolStripMenuItem item = new ToolStripMenuItem(theme.DisplayName);
                item.Tag = themeId;
                item.Click += delegate
                {
                    SetTheme(themeId);
                };
                items.Add(item);
                _themeMenuItems.Add(item);
            }
        }

        private async void OnFormLoad(object sender, EventArgs e)
        {
            StartPipeServer();

            try
            {
                CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
                _env = await CoreWebView2Environment.CreateAsync(null, AppSettings.UserDataFolder, options);
                await _webView.EnsureCoreWebView2Async(_env);

                CoreWebView2 core = _webView.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.AreDevToolsEnabled = true;

                await core.AddScriptToExecuteOnDocumentCreatedAsync(WebBridge.InitScript);
                await core.AddScriptToExecuteOnDocumentCreatedAsync(LyricsBridge.InitScript);

                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += OnCoreWebResourceRequested;
                core.WebMessageReceived += OnCoreWebMessageReceived;
                core.NewWindowRequested += OnCoreNewWindowRequested;
                core.NavigationCompleted += OnCoreNavigationCompleted;
                core.HistoryChanged += OnCoreHistoryChanged;

                HookControllerAccelerator();

                _webView.ZoomFactor = _settings.ZoomFactor;
                _webReady = true;

                string startUrl = Program.IsValidSoundCloudUrl(_pendingUrl) ? _pendingUrl : _settings.HomeUrl;
                _pendingUrl = null;
                core.Navigate(startUrl);
                UpdateNavButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to initialize WebView2 Runtime.\n\n" + ex.Message,
                    "SoundCloud Desktop",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void HookControllerAccelerator()
        {
            try
            {
                FieldInfo field = typeof(WebView2).GetField("_coreWebView2Controller", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    CoreWebView2Controller controller = field.GetValue(_webView) as CoreWebView2Controller;
                    if (controller != null)
                    {
                        _controller = controller;
                        _controller.AcceleratorKeyPressed += OnControllerAcceleratorKeyPressed;
                    }
                }
            }
            catch
            {
                // Fallback to WebView2.KeyDown
            }
        }

        private void OnControllerAcceleratorKeyPressed(object sender, CoreWebView2AcceleratorKeyPressedEventArgs e)
        {
            if (e.KeyEventKind != CoreWebView2KeyEventKind.KeyDown &&
                e.KeyEventKind != CoreWebView2KeyEventKind.SystemKeyDown)
            {
                return;
            }

            Keys key = (Keys)e.VirtualKey;
            Keys modifiers = Control.ModifierKeys;
            Keys combined = key | modifiers;

            if (HandleShortcut(combined))
            {
                e.Handled = true;
            }
        }

        private void OnWebViewKeyDown(object sender, KeyEventArgs e)
        {
            if (HandleShortcut(e.KeyData))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private bool HandleShortcut(Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.M))
            {
                if (PassDebounce()) ToggleMiniMode();
                return true;
            }
            if (keyData == (Keys.Control | Keys.H))
            {
                if (PassDebounce()) ToggleToolbar();
                return true;
            }
            if (keyData == Keys.F11)
            {
                if (PassDebounce()) ToggleFullscreen();
                return true;
            }
            if (keyData == Keys.F5 || keyData == (Keys.Control | Keys.R))
            {
                ReloadPage();
                return true;
            }
            if (keyData == (Keys.Alt | Keys.Left))
            {
                GoBack();
                return true;
            }
            if (keyData == (Keys.Alt | Keys.Right))
            {
                GoForward();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Oemplus) || keyData == (Keys.Control | Keys.Add))
            {
                AdjustZoom(0.1);
                return true;
            }
            if (keyData == (Keys.Control | Keys.OemMinus) || keyData == (Keys.Control | Keys.Subtract))
            {
                AdjustZoom(-0.1);
                return true;
            }
            if (keyData == (Keys.Control | Keys.D0) || keyData == (Keys.Control | Keys.NumPad0))
            {
                SetZoom(1.0);
                return true;
            }
            return false;
        }

        private bool PassDebounce()
        {
            int now = Environment.TickCount;
            if (unchecked(now - _lastToggleTick) < 160)
            {
                return false;
            }
            _lastToggleTick = now;
            return true;
        }

        private void OnCoreNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            ApplyPageModes();
            UpdateNavButtons();
        }

        private void OnCoreHistoryChanged(object sender, object e)
        {
            UpdateNavButtons();
        }

        private void OnCoreWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string raw = e.TryGetWebMessageAsString();
                if (string.IsNullOrWhiteSpace(raw)) return;

                Dictionary<string, object> msg = _json.Deserialize<Dictionary<string, object>>(raw);
                if (msg == null) return;

                string msgType = GetString(msg, "type");
                if (string.Equals(msgType, "playerState", StringComparison.Ordinal))
                {
                    HandlePlayerStateMessage(msg);
                }
                else if (string.Equals(msgType, "lyricsRequest", StringComparison.Ordinal))
                {
                    string lkey = GetString(msg, "key");
                    string lartist = GetString(msg, "artist");
                    string ltitle = GetString(msg, "title");
                    string prefProvider = _settings.LyricsProvider;
                    string ymToken = _settings.YandexMusicToken;
                    double ldur = 0;
                    try { if (msg.ContainsKey("duration") && msg["duration"] != null) ldur = Convert.ToDouble(msg["duration"], CultureInfo.InvariantCulture); } catch { }
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        string res;
                        try { res = LyricsService.Fetch(lartist, ltitle, ldur, prefProvider, ymToken); }
                        catch { res = "{\"found\":false}"; }
                        try
                        {
                            string js = "window.__scLyrics&&window.__scLyrics.onData(" + _json.Serialize(lkey) + "," + res + ");";
                            BeginInvoke((MethodInvoker)delegate { RunScript(js); });
                        }
                        catch { }
                    });
                }
                else if (string.Equals(msgType, "openExternal", StringComparison.Ordinal))
                {
                    string extUrl = GetString(msg, "url");
                    if (!string.IsNullOrWhiteSpace(extUrl) && extUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        OpenExternalUrl(extUrl);
                    }
                }
                else if (string.Equals(msgType, "openLyricsSettings", StringComparison.Ordinal))
                {
                    BeginInvoke((MethodInvoker)delegate { ShowYandexTokenDialog(); });
                }
                else if (string.Equals(msgType, "setLyricsProvider", StringComparison.Ordinal))
                {
                    string prov = GetString(msg, "provider");
                    BeginInvoke((MethodInvoker)delegate { SetLyricsProvider(prov); });
                }
                else if (string.Equals(msgType, "nav", StringComparison.Ordinal))
                {
                    string action = GetString(msg, "action");
                    if (string.Equals(action, "back", StringComparison.Ordinal))
                    {
                        GoBack();
                    }
                    else if (string.Equals(action, "forward", StringComparison.Ordinal))
                    {
                        GoForward();
                    }
                }
                else if (string.Equals(msgType, "shortcut", StringComparison.Ordinal))
                {
                    string action = GetString(msg, "action");
                    if (string.Equals(action, "toggleMini", StringComparison.Ordinal))
                    {
                        if (PassDebounce()) ToggleMiniMode();
                    }
                    else if (string.Equals(action, "toggleToolbar", StringComparison.Ordinal))
                    {
                        if (PassDebounce()) ToggleToolbar();
                    }
                    else if (string.Equals(action, "toggleFullscreen", StringComparison.Ordinal))
                    {
                        if (PassDebounce()) ToggleFullscreen();
                    }
                }
            }
            catch
            {
                // Ignore malformed messages from page
            }
        }

        private void HandlePlayerStateMessage(Dictionary<string, object> msg)
        {
            bool playing = GetBool(msg, "playing");
            string title = GetString(msg, "title");
            string artist = GetString(msg, "artist");
            string trackUrl = GetString(msg, "trackUrl");
            string pageUrl = GetString(msg, "pageUrl");

            _isPlaying = playing;
            if (!string.IsNullOrWhiteSpace(trackUrl))
            {
                _currentTrackUrl = trackUrl.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(pageUrl))
            {
                _currentTrackUrl = pageUrl.Trim();
            }

            string display;
            if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
            {
                display = artist.Trim() + " \u2014 " + title.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(title))
            {
                display = title.Trim();
            }
            else
            {
                display = "";
            }

            string prefix = _isPlaying ? "\u25B6 " : "";
            if (!string.IsNullOrEmpty(display))
            {
                Text = prefix + display + " \u2014 SoundCloud";
                _nowPlayingText = display;
                if (_trayIcon != null)
                {
                    _trayIcon.Text = Truncate(prefix + display, 63);
                }
            }
            else
            {
                Text = "SoundCloud";
                _nowPlayingText = "SoundCloud";
                if (_trayIcon != null)
                {
                    _trayIcon.Text = "SoundCloud";
                }
            }

            if (!_copiedFlashActive)
            {
                _lblNowPlaying.Text = _nowPlayingText;
                _lblNowPlaying.ForeColor = !string.IsNullOrEmpty(display) ? _activeTheme.TextPrimary : _activeTheme.TextMuted;
            }

            _btnPlayPause.Text = _isPlaying ? "\u275A\u275A" : "\u25B6";
            _btnPlayPause.ForeColor = _isPlaying ? _activeTheme.AccentColor : _activeTheme.TextPrimary;
            if (_miTrayPlayPause != null)
            {
                _miTrayPlayPause.Text = _isPlaying ? "Pause" : "Play";
            }
        }

        private void OnCoreNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            string uriText = e.Uri;
            Uri uri;
            if (string.IsNullOrWhiteSpace(uriText) || !Uri.TryCreate(uriText, UriKind.Absolute, out uri))
            {
                e.Handled = true;
                return;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                e.Handled = true;
                return;
            }

            if (IsInAppOrAuthHost(uri.Host) && _env != null)
            {
                e.Handled = true;
                CoreWebView2Deferral deferral = e.GetDeferral();
                bool completed = false;
                AuthPopupForm popup = new AuthPopupForm(_env, _appIcon, _activeTheme, delegate(CoreWebView2 core)
                {
                    if (completed) return;
                    completed = true;
                    try
                    {
                        e.NewWindow = core;
                    }
                    finally
                    {
                        deferral.Complete();
                    }
                });
                popup.FormClosed += delegate
                {
                    if (!completed)
                    {
                        completed = true;
                        try { deferral.Complete(); } catch { }
                    }
                };
                popup.Show(this);
            }
            else
            {
                e.Handled = true;
                OpenExternalUrl(uriText);
            }
        }

        private static bool IsInAppOrAuthHost(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            string[] allowedRoots = new string[]
            {
                "soundcloud.com",
                "sndcdn.com",
                "accounts.google.com",
                "appleid.apple.com",
                "facebook.com",
                "login.live.com",
                "login.microsoftonline.com",
                "twitter.com",
                "x.com",
                "github.com",
                "recaptcha.net",
                "challenges.cloudflare.com"
            };

            for (int i = 0; i < allowedRoots.Length; i++)
            {
                string root = allowedRoots[i];
                if (string.Equals(host, root, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + root, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void OpenExternalUrl(string url)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(url);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch
            {
            }
        }

        private void OnCoreWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (_settings == null || !_settings.BlockAds || _env == null) return;
            string rawUri = e.Request != null ? e.Request.Uri : null;
            if (string.IsNullOrEmpty(rawUri)) return;

            Uri uri;
            if (!Uri.TryCreate(rawUri, UriKind.Absolute, out uri)) return;

            bool isSoundCloudApi;
            if (!IsBlockedAdRequest(uri, out isSoundCloudApi)) return;

            try
            {
                if (isSoundCloudApi)
                {
                    byte[] payload = Encoding.UTF8.GetBytes("{\"collection\":[],\"ads\":[],\"next_href\":null}");
                    MemoryStream stream = new MemoryStream(payload);
                    e.Response = _env.CreateWebResourceResponse(
                        stream,
                        200,
                        "OK",
                        "Content-Type: application/json\r\nAccess-Control-Allow-Origin: https://soundcloud.com");
                }
                else
                {
                    e.Response = _env.CreateWebResourceResponse(null, 403, "Blocked", "");
                }
            }
            catch
            {
            }
        }

        private static bool IsBlockedAdRequest(Uri uri, out bool isSoundCloudApi)
        {
            isSoundCloudApi = false;
            string host = uri.Host ?? "";
            string pathAndQuery = (uri.AbsolutePath + uri.Query) ?? "";

            string[] blockedAdHosts = new string[]
            {
                "ads.soundcloud.com",
                "audio-ads.soundcloud.com",
                "ad-delivery.soundcloud.com",
                "ads-api.soundcloud.com",
                "promoted.soundcloud.com",
                "eventlogger.soundcloud.com",
                "adswizz.com",
                "2mdn.net",
                "doubleclick.net",
                "googlesyndication.com",
                "googleadservices.com",
                "googletagservices.com",
                "googletagmanager.com",
                "google-analytics.com",
                "imasdk.googleapis.com",
                "pagead2.googlesyndication.com",
                "adservice.google.com",
                "innovid.com",
                "serving-sys.com",
                "flashtalking.com",
                "adsrvr.org",
                "adnxs.com",
                "rubiconproject.com",
                "pubmatic.com",
                "openx.net",
                "criteo.com",
                "criteo.net",
                "taboola.com",
                "outbrain.com",
                "scorecardresearch.com",
                "quantserve.com",
                "moatads.com",
                "doubleverify.com",
                "adsafeprotected.com",
                "amazon-adsystem.com",
                "branch.io",
                "app.link",
                "krxd.net",
                "demdex.net",
                "omtrdc.net",
                "rlcdn.com",
                "casalemedia.com",
                "33across.com",
                "indexww.com",
                "sharethrough.com",
                "smartadserver.com",
                "yieldmo.com",
                "spotxchange.com",
                "teads.tv",
                "bidswitch.net",
                "ads-twitter.com",
                "analytics.twitter.com"
            };

            if (HostMatchesAny(host, blockedAdHosts))
            {
                isSoundCloudApi = host.EndsWith("soundcloud.com", StringComparison.OrdinalIgnoreCase);
                return true;
            }

            string[] soundCloudApiHosts = new string[]
            {
                "api-v2.soundcloud.com",
                "api.soundcloud.com",
                "api-mobi.soundcloud.com"
            };

            if (HostMatchesAny(host, soundCloudApiHosts))
            {
                string path = uri.AbsolutePath ?? "";
                if (path.IndexOf("/ads", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/audio-ads", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/video-ads", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/visual-ads", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/vast", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/promoted", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/commercials", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/monetization/ads", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/monetization/offers", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/me/upsell", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/upsell", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/sponsored", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pathAndQuery.IndexOf("ad_urn=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pathAndQuery.IndexOf("ad_id=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    pathAndQuery.IndexOf("promoted_by=", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isSoundCloudApi = true;
                    return true;
                }
            }

            return false;
        }

        private static bool HostMatchesAny(string host, string[] roots)
        {
            if (string.IsNullOrEmpty(host) || roots == null) return false;
            for (int i = 0; i < roots.Length; i++)
            {
                string root = roots[i];
                if (string.Equals(host, root, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + root, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void ApplyPageModes()
        {
            if (!_webReady || _webView == null || _webView.CoreWebView2 == null) return;
            string cleanArg = _settings.HideUpsellBanners ? "true" : "false";
            string adArg = _settings.BlockAds ? "true" : "false";
            string miniArg = _isMiniMode ? "true" : "false";
            string themeId = _activeTheme != null ? _activeTheme.Id : "original";
            string escapedThemeId = EscapeJsString(themeId);
            string aq = EscapeJsString(AppSettings.NormalizeAudioQuality(_settings.AudioQuality));
            string script = "if (window.__scDesktop) { window.__scDesktop.setCleanMode(" + cleanArg + "); if (window.__scDesktop.setAdBlock) window.__scDesktop.setAdBlock(" + adArg + "); window.__scDesktop.setMiniMode(" + miniArg + "); if (window.__scDesktop.setTheme) window.__scDesktop.setTheme(\"" + escapedThemeId + "\"); if (window.__scDesktop.setAudioQuality) window.__scDesktop.setAudioQuality(\"" + aq + "\"); }";
            RunScript(script);
        }

        private static string EscapeJsString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("'", "\\'")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private void RunScript(string script)
        {
            try
            {
                if (_webReady && _webView != null && _webView.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
            catch
            {
            }
        }

        private void UpdateNavButtons()
        {
            bool ready = _webReady && _webView != null && _webView.CoreWebView2 != null;
            _btnBack.Enabled = ready && _webView.CoreWebView2.CanGoBack;
            _btnForward.Enabled = ready && _webView.CoreWebView2.CanGoForward;
            Color primary = _activeTheme != null ? _activeTheme.TextPrimary : Color.FromArgb(232, 232, 232);
            Color muted = _activeTheme != null ? _activeTheme.TextMuted : Color.FromArgb(156, 156, 156);
            _btnBack.ForeColor = _btnBack.Enabled ? primary : muted;
            _btnForward.ForeColor = _btnForward.Enabled ? primary : muted;
        }

        private void GoBack()
        {
            if (_webReady && _webView != null && _webView.CoreWebView2 != null && _webView.CoreWebView2.CanGoBack)
            {
                _webView.CoreWebView2.GoBack();
            }
        }

        private void GoForward()
        {
            if (_webReady && _webView != null && _webView.CoreWebView2 != null && _webView.CoreWebView2.CanGoForward)
            {
                _webView.CoreWebView2.GoForward();
            }
        }

        private void ReloadPage()
        {
            if (_webReady && _webView != null && _webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Reload();
            }
        }

        private void NavigateTo(string url)
        {
            if (_webReady && _webView != null && _webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Navigate(url);
            }
            else
            {
                _pendingUrl = url;
            }
        }

        private void PrevTrack()
        {
            RunScript("if (window.__scDesktop) window.__scDesktop.prev();");
        }

        private void TogglePlayPause()
        {
            RunScript("if (window.__scDesktop) window.__scDesktop.togglePlay();");
        }

        private void NextTrack()
        {
            RunScript("if (window.__scDesktop) window.__scDesktop.next();");
        }

        private void ToggleToolbar()
        {
            if (_isMiniMode || _isFullscreen) return;
            _settings.ShowToolbar = !_settings.ShowToolbar;
            UpdateUiState();
            _settings.Save();
        }

        private void ToggleAlwaysOnTop()
        {
            _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
            if (!_isMiniMode)
            {
                TopMost = _settings.AlwaysOnTop;
            }
            UpdateUiState();
            _settings.Save();
        }

        private void ToggleCleanMode()
        {
            bool next = !(_settings.BlockAds || _settings.HideUpsellBanners);
            _settings.BlockAds = next;
            _settings.HideUpsellBanners = next;
            ApplyPageModes();
            UpdateUiState();
            _settings.Save();
        }

        private void ToggleCloseToTray()
        {
            _settings.CloseToTray = !_settings.CloseToTray;
            UpdateUiState();
            _settings.Save();
        }

        private void ToggleMiniMode()
        {
            if (_isFullscreen)
            {
                ToggleFullscreen();
            }

            if (!_isMiniMode)
            {
                _normalWindowState = WindowState;
                _normalBounds = (WindowState == FormWindowState.Normal) ? Bounds : RestoreBounds;

                _isMiniMode = true;
                if (WindowState == FormWindowState.Maximized)
                {
                    WindowState = FormWindowState.Normal;
                }

                MinimumSize = new Size(360, 110);
                MaximizeBox = false;
                TopMost = true;
                Size = new Size(480, 148);
                EnsureWindowOnScreen();
                RunScript("if (window.__scDesktop) window.__scDesktop.setMiniMode(true);");
            }
            else
            {
                _isMiniMode = false;
                RunScript("if (window.__scDesktop) window.__scDesktop.setMiniMode(false);");

                MinimumSize = new Size(420, 140);
                MaximizeBox = true;
                TopMost = _settings.AlwaysOnTop;
                Bounds = _normalBounds;
                WindowState = _normalWindowState;
            }

            UpdateUiState();
        }

        private void ToggleFullscreen()
        {
            if (_isMiniMode)
            {
                ToggleMiniMode();
            }

            if (!_isFullscreen)
            {
                _fsSavedWindowState = WindowState;
                _fsSavedBounds = (WindowState == FormWindowState.Normal) ? Bounds : RestoreBounds;
                _isFullscreen = true;

                FormBorderStyle = FormBorderStyle.None;
                if (WindowState == FormWindowState.Maximized)
                {
                    WindowState = FormWindowState.Normal;
                }
                WindowState = FormWindowState.Maximized;
            }
            else
            {
                _isFullscreen = false;
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = _fsSavedWindowState;
                if (_fsSavedWindowState == FormWindowState.Normal)
                {
                    Bounds = _fsSavedBounds;
                }
            }

            UpdateUiState();
        }

        private void EnsureWindowOnScreen()
        {
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(Left, wa.Right - Width));
            int y = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - Height));
            Location = new Point(x, y);
        }

        private void AdjustZoom(double delta)
        {
            SetZoom(_settings.ZoomFactor + delta);
        }

        private void SetZoom(double factor)
        {
            double clamped = Math.Max(0.5, Math.Min(2.0, Math.Round(factor, 2)));
            _settings.ZoomFactor = clamped;
            if (_webView != null)
            {
                _webView.ZoomFactor = clamped;
            }
            _settings.Save();
        }

        private void OnWebViewZoomFactorChanged(object sender, EventArgs e)
        {
            if (_webView != null)
            {
                double clamped = Math.Max(0.5, Math.Min(2.0, Math.Round(_webView.ZoomFactor, 2)));
                _settings.ZoomFactor = clamped;
            }
        }

        private void OnThemeButtonClick(object sender, EventArgs e)
        {
            UpdateThemeMenuChecks();
            _themeMenu.Show(_btnTheme, new Point(0, _btnTheme.Height));
        }

        private void OnSettingsButtonClick(object sender, EventArgs e)
        {
            UpdateUiState();
            _settingsMenu.Show(_btnSettings, new Point(0, _btnSettings.Height));
        }

        private void OnNowPlayingClick(object sender, EventArgs e)
        {
            CopyCurrentTrackUrl();
        }

        private void CopyCurrentTrackUrl()
        {
            string url = GetCurrentUrl();
            if (string.IsNullOrEmpty(url)) return;

            try
            {
                Clipboard.SetText(url);
                _copiedFlashActive = true;
                _lblNowPlaying.Text = "Copied!";
                _lblNowPlaying.ForeColor = _activeTheme.AccentColor;
                _copiedTimer.Stop();
                _copiedTimer.Start();
            }
            catch
            {
            }
        }

        private string GetCurrentUrl()
        {
            if (!string.IsNullOrWhiteSpace(_currentTrackUrl))
            {
                return _currentTrackUrl;
            }
            if (_webView != null && _webView.Source != null)
            {
                return _webView.Source.AbsoluteUri;
            }
            return "https://soundcloud.com";
        }

        private void OnCopiedTimerTick(object sender, EventArgs e)
        {
            _copiedTimer.Stop();
            _copiedFlashActive = false;
            _lblNowPlaying.Text = _nowPlayingText;
            UpdateNowPlayingColors();
        }

        private void SetTheme(string themeId)
        {
            ThemeDefinition theme = ThemeCatalog.GetById(themeId);
            _activeTheme = theme;
            _settings.ThemeId = theme.Id;
            _settings.Save();

            ApplyThemeToShell();
            ApplyPageModes();
        }

        private void ApplyThemeToShell()
        {
            ThemeDefinition theme = _activeTheme;
            if (theme == null) return;

            BackColor = theme.BodyBg;

            if (IsHandleCreated)
            {
                NativeMethods.EnableDarkTitleBar(Handle, theme.DarkTitleBar);
            }

            if (_toolbar != null)
            {
                _toolbar.BackColor = theme.ToolbarBg;
                _toolbar.Invalidate();
            }

            if (_sepLeft != null) _sepLeft.BackColor = theme.BorderColor;
            if (_sepRight != null) _sepRight.BackColor = theme.BorderColor;

            if (_toolbarButtons != null)
            {
                for (int i = 0; i < _toolbarButtons.Length; i++)
                {
                    Button btn = _toolbarButtons[i];
                    if (btn != null)
                    {
                        btn.ForeColor = theme.TextPrimary;
                        btn.FlatAppearance.MouseOverBackColor = theme.ButtonHover;
                        btn.FlatAppearance.MouseDownBackColor = theme.ButtonDown;
                    }
                }
            }

            if (_webView != null)
            {
                _webView.DefaultBackgroundColor = theme.BodyBg;
            }

            DarkMenuRenderer renderer = new DarkMenuRenderer(theme);
            if (_settingsMenu != null) _settingsMenu.Renderer = renderer;
            if (_trayMenu != null) _trayMenu.Renderer = renderer;
            if (_themeMenu != null) _themeMenu.Renderer = renderer;
            if (_miSettingsTheme != null && _miSettingsTheme.DropDown != null)
            {
                _miSettingsTheme.DropDown.Renderer = renderer;
            }
            if (_miSettingsLyrics != null && _miSettingsLyrics.DropDown != null)
            {
                _miSettingsLyrics.DropDown.Renderer = renderer;
            }
            if (_miSettingsAudioQuality != null && _miSettingsAudioQuality.DropDown != null)
            {
                _miSettingsAudioQuality.DropDown.Renderer = renderer;
            }
            if (_miTrayTheme != null && _miTrayTheme.DropDown != null)
            {
                _miTrayTheme.DropDown.Renderer = renderer;
            }

            UpdateUiState();
        }

        private void UpdateNowPlayingColors()
        {
            if (_lblNowPlaying == null || _activeTheme == null) return;
            if (_copiedFlashActive)
            {
                _lblNowPlaying.ForeColor = _activeTheme.AccentColor;
            }
            else
            {
                _lblNowPlaying.ForeColor = string.Equals(_nowPlayingText, "SoundCloud", StringComparison.Ordinal)
                    ? _activeTheme.TextMuted
                    : _activeTheme.TextPrimary;
            }
        }

        private void UpdateThemeMenuChecks()
        {
            string activeId = _activeTheme != null ? _activeTheme.Id : "original";
            for (int i = 0; i < _themeMenuItems.Count; i++)
            {
                ToolStripMenuItem item = _themeMenuItems[i];
                string itemId = item.Tag as string;
                item.Checked = string.Equals(itemId, activeId, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void UpdateUiState()
        {
            _toolbar.Visible = _settings.ShowToolbar && !_isMiniMode && !_isFullscreen;

            _btnPlayPause.ForeColor = _isPlaying ? _activeTheme.AccentColor : _activeTheme.TextPrimary;
            _btnTheme.ForeColor = _activeTheme.AccentColor;
            _btnMini.ForeColor = _isMiniMode ? _activeTheme.AccentColor : _activeTheme.TextPrimary;
            _btnTopMost.ForeColor = _settings.AlwaysOnTop ? _activeTheme.AccentColor : _activeTheme.TextPrimary;

            UpdateNavButtons();
            UpdateNowPlayingColors();
            UpdateThemeMenuChecks();

            if (_miSettingsToolbar != null) _miSettingsToolbar.Checked = _settings.ShowToolbar;
            if (_miSettingsMini != null) _miSettingsMini.Checked = _isMiniMode;
            if (_miSettingsFullscreen != null) _miSettingsFullscreen.Checked = _isFullscreen;
            if (_miSettingsTopMost != null) _miSettingsTopMost.Checked = _settings.AlwaysOnTop;
            if (_miSettingsCleanMode != null) _miSettingsCleanMode.Checked = (_settings.BlockAds || _settings.HideUpsellBanners);
            if (_miSettingsCloseToTray != null) _miSettingsCloseToTray.Checked = _settings.CloseToTray;
            bool isYandex = string.Equals(_settings.LyricsProvider, "yandex", StringComparison.OrdinalIgnoreCase);
            if (_miLyricsLrclib != null) _miLyricsLrclib.Checked = !isYandex;
            if (_miLyricsYandex != null) _miLyricsYandex.Checked = isYandex;

            string aq = AppSettings.NormalizeAudioQuality(_settings.AudioQuality);
            if (_miQualityHigh != null) _miQualityHigh.Checked = (aq == "high");
            if (_miQualityAuto != null) _miQualityAuto.Checked = (aq == "auto");
            if (_miQualityMp3 != null) _miQualityMp3.Checked = (aq == "mp3");

            if (_miTrayTopMost != null) _miTrayTopMost.Checked = _settings.AlwaysOnTop;
            if (_miTrayCloseToTray != null) _miTrayCloseToTray.Checked = _settings.CloseToTray;
        }

        private void SetAudioQuality(string mode)
        {
            _settings.AudioQuality = AppSettings.NormalizeAudioQuality(mode);
            _settings.Save();
            UpdateUiState();
            ApplyPageModes();
        }

        private void OpenLyricsPage()
        {
            if (!Visible || WindowState == FormWindowState.Minimized)
            {
                Show();
                WindowState = _normalWindowState;
            }
            Activate();
            RunScript("window.__scLyricsOpenPage && window.__scLyricsOpenPage();");
        }

        private void SetLyricsProvider(string providerId)
        {
            _settings.LyricsProvider = string.Equals(providerId, "yandex", StringComparison.OrdinalIgnoreCase) ? "yandex" : "lrclib";
            _settings.Save();
            bool isYandex = string.Equals(_settings.LyricsProvider, "yandex", StringComparison.OrdinalIgnoreCase);
            if (_miLyricsLrclib != null) _miLyricsLrclib.Checked = !isYandex;
            if (_miLyricsYandex != null) _miLyricsYandex.Checked = isYandex;

            string aq = AppSettings.NormalizeAudioQuality(_settings.AudioQuality);
            if (_miQualityHigh != null) _miQualityHigh.Checked = (aq == "high");
            if (_miQualityAuto != null) _miQualityAuto.Checked = (aq == "auto");
            if (_miQualityMp3 != null) _miQualityMp3.Checked = (aq == "mp3");
            RunScript("window.__scLyrics&&window.__scLyrics.reload();");
        }

        private void ShowYandexTokenDialog()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "Yandex Music Token";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.ClientSize = new Size(420, 140);
                dlg.BackColor = _activeTheme.ToolbarBg;
                dlg.ForeColor = _activeTheme.TextPrimary;

                Label lbl = new Label();
                lbl.Text = "Paste Yandex Music OAuth token (or set YANDEX_MUSIC_TOKEN env var):";
                lbl.AutoSize = false;
                lbl.Bounds = new Rectangle(14, 14, 392, 20);
                lbl.ForeColor = _activeTheme.TextPrimary;

                TextBox tb = new TextBox();
                tb.Bounds = new Rectangle(14, 42, 392, 24);
                tb.UseSystemPasswordChar = true;
                tb.Text = _settings.YandexMusicToken ?? "";
                tb.BackColor = _activeTheme.BodyBg;
                tb.ForeColor = _activeTheme.TextPrimary;

                Button btnClear = new Button();
                btnClear.Text = "Clear";
                btnClear.Bounds = new Rectangle(14, 86, 80, 30);
                btnClear.FlatStyle = FlatStyle.Flat;
                btnClear.Click += delegate { tb.Text = ""; };

                Button btnOk = new Button();
                btnOk.Text = "Save";
                btnOk.DialogResult = DialogResult.OK;
                btnOk.Bounds = new Rectangle(236, 86, 80, 30);
                btnOk.FlatStyle = FlatStyle.Flat;

                Button btnCancel = new Button();
                btnCancel.Text = "Cancel";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Bounds = new Rectangle(326, 86, 80, 30);
                btnCancel.FlatStyle = FlatStyle.Flat;

                dlg.Controls.Add(lbl);
                dlg.Controls.Add(tb);
                dlg.Controls.Add(btnClear);
                dlg.Controls.Add(btnOk);
                dlg.Controls.Add(btnCancel);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _settings.YandexMusicToken = (tb.Text ?? "").Trim();
                    _settings.Save();
                    RunScript("window.__scLyrics&&window.__scLyrics.reload();");
                }
            }
        }

        private void ToggleWindowVisibility()
        {
            if (Visible && WindowState != FormWindowState.Minimized)
            {
                Hide();
            }
            else
            {
                ShowAndFocusWindow();
            }
        }

        private void ShowAndFocusWindow()
        {
            if (!Visible)
            {
                Show();
            }
            if (WindowState == FormWindowState.Minimized)
            {
                NativeMethods.ShowWindow(Handle, NativeMethods.SW_RESTORE);
            }
            Activate();
            BringToFront();
            NativeMethods.SetForegroundWindow(Handle);
        }

        private void StartPipeServer()
        {
            Thread t = new Thread(PipeListenerLoop);
            t.IsBackground = true;
            t.Start();
        }

        private void PipeListenerLoop()
        {
            while (!_stopPipe)
            {
                try
                {
                    using (NamedPipeServerStream server = new NamedPipeServerStream(Program.PipeName, PipeDirection.In))
                    {
                        server.WaitForConnection();
                        if (_stopPipe) break;

                        using (StreamReader reader = new StreamReader(server, Encoding.UTF8))
                        {
                            string line = reader.ReadLine();
                            if (!_stopPipe && line != null)
                            {
                                string msg = line.Trim();
                                BeginInvoke(new Action(delegate
                                {
                                    ShowAndFocusWindow();
                                    if (!string.IsNullOrEmpty(msg) &&
                                        !string.Equals(msg, "FOCUS", StringComparison.OrdinalIgnoreCase) &&
                                        Program.IsValidSoundCloudUrl(msg))
                                    {
                                        NavigateTo(msg);
                                    }
                                }));
                            }
                        }
                    }
                }
                catch
                {
                    if (_stopPipe) break;
                    Thread.Sleep(200);
                }
            }
        }

        private void OnMainFormMove(object sender, EventArgs e)
        {
            CaptureNormalBounds();
        }

        private void OnMainFormResize(object sender, EventArgs e)
        {
            CaptureNormalBounds();
        }

        private void CaptureNormalBounds()
        {
            if (_isMiniMode || _isFullscreen) return;
            if (WindowState == FormWindowState.Normal)
            {
                _normalWindowState = FormWindowState.Normal;
                _normalBounds = Bounds;
            }
            else if (WindowState == FormWindowState.Maximized)
            {
                _normalWindowState = FormWindowState.Maximized;
                _normalBounds = RestoreBounds;
            }
        }

        private void SaveSettingsBounds()
        {
            Rectangle b = (_isMiniMode || _isFullscreen)
                ? _normalBounds
                : (WindowState == FormWindowState.Normal ? Bounds : RestoreBounds);

            if (b.Width >= 420 && b.Height >= 140)
            {
                _settings.WindowX = b.X;
                _settings.WindowY = b.Y;
                _settings.WindowWidth = b.Width;
                _settings.WindowHeight = b.Height;
            }

            _settings.IsMaximized = (_isMiniMode || _isFullscreen)
                ? (_normalWindowState == FormWindowState.Maximized)
                : (WindowState == FormWindowState.Maximized);

            _settings.Save();
        }

        private void ExitApplication()
        {
            _allowExit = true;
            Close();
        }

        private void OnMainFormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettingsBounds();

            if (!_allowExit && e.CloseReason == CloseReason.UserClosing && _settings.CloseToTray)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            _stopPipe = true;

            if (_controller != null)
            {
                try { _controller.AcceleratorKeyPressed -= OnControllerAcceleratorKeyPressed; } catch { }
            }

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            if (_copiedTimer != null)
            {
                _copiedTimer.Stop();
                _copiedTimer.Dispose();
            }
        }

        private static string GetString(Dictionary<string, object> map, string key)
        {
            object val;
            if (map != null && map.TryGetValue(key, out val) && val != null)
            {
                return Convert.ToString(val, CultureInfo.InvariantCulture);
            }
            return "";
        }

        private static bool GetBool(Dictionary<string, object> map, string key)
        {
            object val;
            if (map != null && map.TryGetValue(key, out val) && val != null)
            {
                if (val is bool) return (bool)val;
                bool parsed;
                if (bool.TryParse(Convert.ToString(val, CultureInfo.InvariantCulture), out parsed))
                {
                    return parsed;
                }
            }
            return false;
        }

        private static string Truncate(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Length <= maxLen) return value;
            return value.Substring(0, maxLen - 3) + "...";
        }
    }
}
