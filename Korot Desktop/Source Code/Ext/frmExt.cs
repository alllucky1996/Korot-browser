﻿//MIT License
//
//Copyright (c) 2020 Eren "Haltroy" Kanat
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
using CefSharp;
using CefSharp.WinForms;
using System;
using System.Windows.Forms;

namespace Korot
{
    public partial class frmExt : Form
    {
        private readonly string ExtensionPopupPath;
        private readonly frmCEF tabform;
        private readonly string userCache;

        //frmMain anaform;
        private readonly bool allowWebContent;
        private readonly string ExtManifestFile;
        private ChromiumWebBrowser chromiumWebBrowser1;
        public frmExt(frmCEF CefForm, string profileName, string manifestFile, string popupHTML, bool _allowWebContent)
        {
            InitializeComponent();
            tabform = CefForm;
            ExtManifestFile = manifestFile;
            //anaform = rmmain;
            userCache = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\Users\\" + profileName + "\\cache\\";
            Text = "Korot";
            allowWebContent = _allowWebContent;
            ExtensionPopupPath = popupHTML;
            InitializeChromium();
        }
        private static bool IsLocalPath(string p)
        {
            if (p.ToLower().StartsWith("http:\\") | p.ToLower().StartsWith("https:\\") | p.ToLower().StartsWith("ftp:\\"))
            {
                return false;
            }
            else if (p.ToLower().StartsWith("file:\\"))
            {
                return true;
            }

            return new Uri(p).IsFile;
        }

        private void FrmExt_Load(object sender, EventArgs e) { }
        public void InitializeChromium()
        {
            CefSettings settings = new CefSettings
            {
                UserAgent = "Mozilla/5.0 ( Windows "
                + Program.getOSInfo()
                + "; "
                + (Environment.Is64BitProcess ? "WOW64" : "Win32NT")
                + ") AppleWebKit/537.36 (KHTML, like Gecko) Chrome/"
                + Cef.ChromiumVersion
                + " Safari/537.36 Korot/"
                + Application.ProductVersion.ToString()
                + (VersionInfo.IsPreRelease ? ("-pre" + VersionInfo.PreReleaseNumber) : "")
                + "(" + VersionInfo.CodeName + ")"
            };
            if (tabform._Incognito) { settings.CachePath = null; settings.PersistSessionCookies = false; settings.RootCachePath = null; }
            else { settings.CachePath = userCache; settings.RootCachePath = userCache; }
            settings.RegisterScheme(new CefCustomScheme
            {
                SchemeName = "korot",
                SchemeHandlerFactory = new SchemeHandlerFactory(tabform)
                {
                    extKEM = ExtManifestFile,
                    isExt = true,
                    extForm = this
                }

            });
            // Initialize cef with the provided settings
            if (Cef.IsInitialized == false) { Cef.Initialize(settings); }
            chromiumWebBrowser1 = new ChromiumWebBrowser(ExtensionPopupPath);
            Controls.Add(chromiumWebBrowser1);
            chromiumWebBrowser1.RequestHandler = new RequestHandlerKorot(tabform);
            chromiumWebBrowser1.DisplayHandler = new DisplayHandler(tabform);
            chromiumWebBrowser1.TitleChanged += cef_TitleChanged;
            chromiumWebBrowser1.LoadError += cef_onLoadError;
            chromiumWebBrowser1.MenuHandler = new ContextMenuHandler(tabform);
            chromiumWebBrowser1.LifeSpanHandler = new BrowserLifeSpanHandler(tabform);
            chromiumWebBrowser1.DownloadHandler = new DownloadHandler(tabform);
            chromiumWebBrowser1.JsDialogHandler = new JsHandler(tabform);
            chromiumWebBrowser1.DialogHandler = new MyDialogHandler();
            chromiumWebBrowser1.Dock = DockStyle.Fill;
            chromiumWebBrowser1.Show();
        }
        private void cef_TitleChanged(object sender, TitleChangedEventArgs e)
        {
            Invoke(new Action(() => Text = e.Title));
        }
        private void cef_onLoadError(object sender, LoadErrorEventArgs e)
        {
            if (e == null) //User Asked
            {
                chromiumWebBrowser1.Load("http://korot://error?e=TEST");
            }
            else
            {
                if (e.Frame.IsMain)
                {
                    chromiumWebBrowser1.LoadHtml("http://korot://error?e=" + e.ErrorText);
                }
                else
                {
                    e.Frame.LoadUrl("http://korot://error?e=" + e.ErrorText);
                }
            }
        }

        private void frmExt_Leave(object sender, EventArgs e)
        {

            Close();
        }
    }
}