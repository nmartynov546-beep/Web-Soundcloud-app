using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SoundCloudDesktop
{
    internal sealed class AuthPopupForm : Form
    {
        private readonly WebView2 _webView;
        private readonly CoreWebView2Environment _env;
        private readonly ThemeDefinition _theme;
        private readonly Action<CoreWebView2> _onReady;

        public AuthPopupForm(CoreWebView2Environment env, Icon appIcon, Action<CoreWebView2> onReady)
            : this(env, appIcon, ThemeCatalog.GetById("original"), onReady)
        {
        }

        public AuthPopupForm(CoreWebView2Environment env, Icon appIcon, ThemeDefinition theme, Action<CoreWebView2> onReady)
        {
            _env = env;
            _theme = theme ?? ThemeCatalog.GetById("original");
            _onReady = onReady;

            Text = "SoundCloud — Вход";
            Width = 540;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = _theme.BodyBg;
            if (appIcon != null) Icon = appIcon;

            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            _webView.DefaultBackgroundColor = _theme.BodyBg;
            Controls.Add(_webView);

            Load += OnLoad;
            FormClosed += OnClosed;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.EnableDarkTitleBar(Handle, _theme.DarkTitleBar);
        }

        private async void OnLoad(object sender, EventArgs e)
        {
            try
            {
                await _webView.EnsureCoreWebView2Async(_env);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.WindowCloseRequested += delegate
                {
                    if (!IsDisposed) Close();
                };
                _webView.CoreWebView2.DocumentTitleChanged += delegate
                {
                    if (!IsDisposed && !string.IsNullOrWhiteSpace(_webView.CoreWebView2.DocumentTitle))
                        Text = _webView.CoreWebView2.DocumentTitle;
                };
                if (_onReady != null) _onReady(_webView.CoreWebView2);
            }
            catch
            {
                if (!IsDisposed) Close();
            }
        }

        private void OnClosed(object sender, FormClosedEventArgs e)
        {
            if (_webView != null)
            {
                _webView.Dispose();
            }
        }
    }
}
