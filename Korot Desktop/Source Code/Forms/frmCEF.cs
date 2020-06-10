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
using HTAlt.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace Korot
{
    public partial class frmCEF : Form
    {
        public Settings Settings;
        private frmCollection ColMan;
        public bool closing;
        public ContextMenuStrip cmsCEF = null;
        private int updateProgress = 0;
        private bool isLoading = false;
        private readonly string loaduri = null;
        public bool _Incognito = false;
        public string userName;
        private readonly string profilePath;
        private readonly string userCache;
#pragma warning disable IDE0052 //(we don't need this for now)
        private int findIdentifier;
#pragma warning restore IDE0052
        private int findTotal;
        private int findCurrent;
        private bool findLast;
        private string defaultProxy = null;
        public ChromiumWebBrowser chromiumWebBrowser1;
        private readonly List<ToolStripMenuItem> favoritesFolders = new List<ToolStripMenuItem>();
        private readonly List<ToolStripMenuItem> favoritesNoIcon = new List<ToolStripMenuItem>();
        public bool NotificationListenerMode = false;
        public frmMain anaform => ((frmMain)ParentTabs);
        public frmCEF(Settings settings,bool isIncognito = false, string loadurl = "korot://newtab", string profileName = "user0",bool notifListenMode = false)
        {
            Settings = settings;
            NotificationListenerMode = notifListenMode;
            loaduri = loadurl;
            _Incognito = isIncognito;
            userName = profileName;
            profilePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\" + profileName + "\\";
            userCache = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\" + profileName + "\\cache\\";
            InitializeComponent();
            InitializeChromium();
            foreach (Control x in Controls)
            {
                try { x.KeyDown += tabform_KeyDown; x.MouseWheel += MouseScroll; x.Font = new Font("Ubuntu", x.Font.Size, x.Font.Style); } catch { continue; }
            }
        }
        public void LoadDynamicMenu()
        {
            mFavorites.Items.Clear();
            favoritesNoIcon.Clear();
            favoritesFolders.Clear();
            foreach (Folder folder in Settings.Favorites.Favorites)
            {
                if (folder is Favorite)
                {
                    var favorite = folder as Favorite;
                    ToolStripMenuItem menuItem = new ToolStripMenuItem
                    {
                        Name = favorite.Name,
                        Text = favorite.Text
                    };
                    menuItem.DropDown.RenderMode = ToolStripRenderMode.Professional;
                    menuItem.Tag = favorite;
                    if (!string.IsNullOrWhiteSpace(favorite.IconPath))
                    {
                        if (File.Exists(favorite.IconPath.Replace("{ICONSTORAGE}", iconStorage)))
                        {
                            Stream fs = HTAlt.Tools.ReadFile(favorite.IconPath.Replace("{ICONSTORAGE}", iconStorage));
                            menuItem.Image = Image.FromStream(fs);
                            fs.Close();
                        }
                        else
                        {
                            favoritesNoIcon.Add(menuItem);
                        }
                    }
                    else
                    {
                        favoritesNoIcon.Add(menuItem);
                    }
                    menuItem.MouseUp += menuStrip1_MouseUp;

                    mFavorites.Items.Add(menuItem);
                }
                else
                {
                    ToolStripMenuItem menuItem = new ToolStripMenuItem
                    {
                        Name = folder.Name,
                        Text = folder.Text,
                        Tag = folder
                    };
                    menuItem.MouseUp += menuStrip1_MouseUp;
                    menuItem.DropDown.RenderMode = ToolStripRenderMode.Professional;
                    favoritesFolders.Add(menuItem);

                    mFavorites.Items.Add(menuItem);
                    GenerateMenusFromXML(folder, (ToolStripMenuItem)mFavorites.Items[mFavorites.Items.Count - 1]);

                }
            }
            UpdateFavoriteColor();
            updateFavoritesImages();
        }
        private void GenerateMenusFromXML(Folder folder, ToolStripMenuItem menuItem)
        {
            ToolStripMenuItem item = null;

            foreach (Folder subfolder in folder.Favorites)
            {
                if (subfolder is Favorite)
                {
                    item = new ToolStripMenuItem
                    {
                        Name = subfolder.Name,
                        Text = subfolder.Text,
                        Tag = subfolder
                    };
                    item.DropDown.RenderMode = ToolStripRenderMode.Professional;
                    if (!string.IsNullOrWhiteSpace((subfolder as Favorite).IconPath))
                    {
                        if (File.Exists((subfolder as Favorite).IconPath.Replace("{ICONSTORAGE}", iconStorage)))
                        {
                            Stream fs = HTAlt.Tools.ReadFile((subfolder as Favorite).IconPath.Replace("{ICONSTORAGE}", iconStorage));
                            item.Image = Image.FromStream(fs);
                            fs.Close();
                        }
                        else
                        {
                            favoritesNoIcon.Add(item);
                        }
                    }
                    else
                    {
                        favoritesNoIcon.Add(item);
                    }
                    menuItem.DropDownItems.Add(item);


                    // add an event handler to the menu item added
                    item.MouseUp += menuStrip1_MouseUp;
                }
                else
                {
                    item = new ToolStripMenuItem
                    {
                        Name = subfolder.Name,
                        Text = subfolder.Text,
                        Tag = subfolder
                    };
                    item.DropDown.RenderMode = ToolStripRenderMode.Professional;
                    item.MouseUp += menuStrip1_MouseUp;
                    favoritesFolders.Add(item);
                    menuItem.DropDownItems.Add(item);
                    GenerateMenusFromXML(subfolder, (ToolStripMenuItem)menuItem.DropDownItems[menuItem.DropDownItems.Count - 1]);

                }
            }
        }
        public void InitializeChromium()
        {
            CefSettings settings = new CefSettings
            {
                UserAgent = "Mozilla/5.0 ( Windows "
                + Program.getOSInfo()
                + "; "
                + (Environment.Is64BitProcess ? "Win64" : "Win32NT")
                + ") AppleWebKit/537.36 (KHTML, like Gecko) Chrome/"
                + Cef.ChromiumVersion
                + " Safari/537.36 Korot/"
                + Application.ProductVersion.ToString()
                + (VersionInfo.IsPreRelease ? ("-pre" + VersionInfo.PreReleaseNumber) : "")
                + "(" + VersionInfo.CodeName + ")"
            };
            if (_Incognito) { settings.CachePath = null; settings.PersistSessionCookies = false; settings.RootCachePath = null; }
            else { settings.CachePath = userCache; settings.RootCachePath = userCache; }
            settings.RegisterScheme(new CefCustomScheme
            {
                SchemeName = "korot",
                SchemeHandlerFactory = new SchemeHandlerFactory(this)
                {
                    extKEM = "",
                    isExt = false,
                    extForm = null
                }
            });
            // Initialize cef with the provided settings
            if (Cef.IsInitialized == false) { Cef.Initialize(settings); }
            chromiumWebBrowser1 = new ChromiumWebBrowser(string.IsNullOrWhiteSpace(loaduri) ? "korot://newtab" : loaduri);
            pCEF.Controls.Add(chromiumWebBrowser1);
            chromiumWebBrowser1.ConsoleMessage += cef_consoleMessage;
            chromiumWebBrowser1.FindHandler = new FindHandler(this);
            chromiumWebBrowser1.KeyboardHandler = new KeyboardHandler(this);
            chromiumWebBrowser1.RequestHandler = new RequestHandlerKorot(this);
            chromiumWebBrowser1.DisplayHandler = new DisplayHandler(this);
            chromiumWebBrowser1.LoadingStateChanged += loadingstatechanged;
            chromiumWebBrowser1.TitleChanged += cef_TitleChanged;
            chromiumWebBrowser1.AddressChanged += cef_AddressChanged;
            chromiumWebBrowser1.LoadError += cef_onLoadError;
            chromiumWebBrowser1.KeyDown += tabform_KeyDown;
            chromiumWebBrowser1.LostFocus += cef_LostFocus;
            chromiumWebBrowser1.Enter += cef_GotFocus;
            chromiumWebBrowser1.GotFocus += cef_GotFocus;
            chromiumWebBrowser1.MenuHandler = new ContextMenuHandler(this);
            chromiumWebBrowser1.LifeSpanHandler = new BrowserLifeSpanHandler(this);
            chromiumWebBrowser1.DownloadHandler = new DownloadHandler(this);
            chromiumWebBrowser1.JsDialogHandler = new JsHandler(this);
            chromiumWebBrowser1.DialogHandler = new MyDialogHandler();
            chromiumWebBrowser1.JavascriptMessageReceived += OnBrowserJavascriptMessageReceived;
            chromiumWebBrowser1.FrameLoadEnd += OnFrameLoadEnd;
            chromiumWebBrowser1.MouseWheel += MouseScroll;
            chromiumWebBrowser1.Dock = DockStyle.Fill;
            chromiumWebBrowser1.Show();
            if (defaultProxy != null && Settings.RememberLastProxy && !string.IsNullOrWhiteSpace(Settings.LastProxy))
            {
                SetProxy(chromiumWebBrowser1, Settings.LastProxy);
            }
        }
        private bool startupScriptsExecuted = false;
        public void OnFrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if (e.Frame.IsMain && chromiumWebBrowser1.CanExecuteJavascriptInMainFrame)
            {
                if(!startupScriptsExecuted) 
                {
                    foreach (Extension y in Settings.Extensions.ExtensionList)
                    {
                        if (y.Settings.autoLoad)
                        {
                            chromiumWebBrowser1.ExecuteScriptAsync(@"  " + HTAlt.Tools.ReadFile(y.Startup.Replace("[EXTFOLDER]", new FileInfo(y.ManifestFile).Directory + "\\"), Encoding.UTF8));
                        }
                    }
                    startupScriptsExecuted = true;
                }
                chromiumWebBrowser1.ExecuteScriptAsync(@"  " + Properties.Resources.notificationDefault.Replace("[$]", getNotificationPermission(e.Frame.Url)));
                foreach (Extension y in Settings.Extensions.ExtensionList)
                {
                    if (y.Settings.autoLoad)
                    {
                        chromiumWebBrowser1.ExecuteScriptAsync(@"  " + HTAlt.Tools.ReadFile(y.Background.Replace("[EXTFOLDER]", new FileInfo(y.ManifestFile).Directory + "\\"), Encoding.UTF8));
                    }
                }
            }
        }
        public string getNotificationPermission(string url)
        {
            string x = HTAlt.Tools.GetBaseURL(url);
            if (Settings.GetSiteFromUrl(x).AllowNotifications)
            {
                return "granted";
            }
            return "denied";
        }
        public void PushNewNotification(Notification notification)
        {
            frmNotification notifyForm = new frmNotification(this, notification);
            anaform.notifications.Add(notifyForm);
            notifyForm.Show();
        }
        public frmNotification getNotificationByID(string ID)
        {
            List<frmNotification> foundList = new List<frmNotification>();
            foreach (frmNotification x in anaform.notifications)
            {
                if (x.notification.id == ID)
                {
                    foundList.Add(x);
                }
            }
            if (foundList.Count > 0) { return foundList[0]; }else { return null; }
        }
        public void requestNotificationPermission(string url)
        {
            Invoke(new Action(() =>
            {
                string baseUrl = HTAlt.Tools.GetBaseURL(url);
                if (anaform.notificationAsked.Contains(baseUrl)) { return; }
                else
                {
                    frmNotificationPermission newPerm = new frmNotificationPermission(this, baseUrl)
                    {
                        Location = new Point(pbPrivacy.Location.X, pNavigate.Height),
                        TopLevel = false,
                        Visible = true
                    };
                    Controls.Add(newPerm);
                    newPerm.Show();
                    newPerm.Focus();
                    newPerm.BringToFront();
                }
            }));
        }
        public void refreshPage()
        {
            chromiumWebBrowser1.Reload();
        }

        private void OnBrowserJavascriptMessageReceived(object sender, JavascriptMessageReceivedEventArgs e)
        {
            string message = (string)e.Message;
            ChromiumWebBrowser browser = (sender as ChromiumWebBrowser);
            if (string.Equals(message, "[Korot.Notification.RequestPermission]"))
            {
                requestNotificationPermission(browser.Address);
            }
            else
            {
                if (message.ToLower().StartsWith("[korot.notification "))
                {
                    if (Settings.GetSiteFromUrl(HTAlt.Tools.GetBaseURL(browser.Address)).AllowNotifications)
                    {
                        MemoryStream stream = new MemoryStream();
                        StreamWriter writer = new StreamWriter(stream);
                        writer.Write(message.Replace("[", "<").Replace("]", ">"));
                        writer.Flush();
                        stream.Position = 0;
                        XmlDocument document = new XmlDocument();
                        document.Load(stream);
                        XmlNode node = document.DocumentElement;
                        string id = node.Attributes["ID"] != null ? node.Attributes["ID"].Value : "";
                        string image = node.Attributes["Icon"] != null ? node.Attributes["Icon"].Value : "";
                        string body = node.Attributes["Body"] != null ? node.Attributes["Body"].Value : "";
                        string title = node.Attributes["Message"] != null ? node.Attributes["Message"].Value : "";
                        string onclick = node.Attributes["onClick"] != null ? node.Attributes["onClick"].Value : "";
                        Notification newnot = new Notification() { id = id, url = browser.Address, message = body, title = title, imageUrl = image, action = onclick };
                        frmNotification existingNot = getNotificationByID(id);
                        if (existingNot != null) { existingNot.notification = newnot; }
                        else
                        {
                            PushNewNotification(newnot);
                        }
                    }
                }
            }
        }
        private void tabform_Load(object sender, EventArgs e)
        {
            if (NotificationListenerMode) { timer1.Start(); }
            Uri testUri = new Uri("https://haltroy.com");
            Uri aUri = WebRequest.GetSystemWebProxy().GetProxy(testUri);
            if (aUri != testUri)
            {
                defaultProxy = aUri.AbsoluteUri;
            }

            if (defaultProxy == null) { DefaultProxyts.Visible = false; DefaultProxyts.Enabled = false; }
            else
            {
                if (Settings.RememberLastProxy && !string.IsNullOrWhiteSpace(Settings.LastProxy)) { SetProxy(chromiumWebBrowser1, Settings.LastProxy); }
            }
            frmCollection collectionManager = new frmCollection(this)
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill,
                Visible = true
            };
            ColMan = collectionManager;
            collectionManager.Show();
            panel3.Controls.Add(collectionManager);
            Updater();
            if (File.Exists(Settings.Theme.ThemeFile))
            {
                comboBox1.Text = new FileInfo(Settings.Theme.ThemeFile).Name.Replace(".ktf", "");
            }
            else
            {
                comboBox1.Text = "";
                Settings.Theme.Author = "";
                Settings.Theme.Name = "";
            }
            tbHomepage.Text = Settings.Homepage;
            tbSearchEngine.Text = Settings.SearchEngine;
            if (Settings.Homepage == "korot://newtab") { radioButton1.Enabled = true; }
            pbBack.BackColor = Settings.Theme.BackColor;
            pbOverlay.BackColor = Settings.Theme.OverlayColor;
            RefreshLangList();
            refreshThemeList();
            ChangeTheme();
            RefreshDownloadList();
            lbVersion.Text = Application.ProductVersion.ToString() + (VersionInfo.IsPreRelease ? "-pre" + VersionInfo.PreReleaseNumber : "") + " " + (Environment.Is64BitProcess ? "(64 bit)" : "(32 bit)") + " (" + VersionInfo.CodeName + ")";
            RefreshFavorites();
            LoadExt();
            RefreshProfiles();
            profilenameToolStripMenuItem.Text = userName;
            showCertificateErrorsToolStripMenuItem.Text = showCertError;
            chromiumWebBrowser1.Select();
            hsDoNotTrack.Checked = Settings.DoNotTrack;
            EasterEggs();
            RefreshTranslation();
            RefreshSizes();
            if (_Incognito)
            {
                foreach (Control x in tpSettings.Controls)
                {
                    if (x != btClose || x != btClose2 || x != btClose6 || x != btClose7 || x != btClose9) { x.Enabled = false; }
                }
                btInstall.Enabled = false;
                btUpdater.Enabled = false;
                btProfile.Enabled = false;
                cmsBStyle.Enabled = false;
                cmsSearchEngine.Enabled = false;
                btFav.Enabled = false;
                cmsHistory.Enabled = false;
                cmsProfiles.Enabled = false;
                removeSelectedToolStripMenuItem1.Enabled = false;
                clearToolStripMenuItem2.Enabled = false;
                removeSelectedToolStripMenuItem.Enabled = false;
                clearToolStripMenuItem.Enabled = false;
                disallowThisPageForCookieAccessToolStripMenuItem.Enabled = false;
                removeSelectedTSMI.Enabled = false;
                clearTSMI.Enabled = false;
                Settings.Theme.BackColor = Color.FromArgb(255, 64, 64, 64);
                Settings.Theme.OverlayColor = Color.DodgerBlue;
            }
            else
            {
                pbIncognito.Visible = false;
                tbAddress.Size = new Size(tbAddress.Size.Width + pbIncognito.Size.Width, tbAddress.Size.Height);
            }
            LoadLangFromFile(Settings.LanguageFile);
            cbLang.Text = Path.GetFileNameWithoutExtension(Settings.LanguageFile);
            Settings.Extensions.UpdateExtensions();
        }
        private void RefreshHistory()
        {
            int selectedValue = hlvHistory.SelectedIndices.Count > 0 ? hlvHistory.SelectedIndices[0] : 0;
            ListViewItem scroll = hlvHistory.TopItem;
            hlvHistory.Items.Clear();
            foreach(Site x in Settings.History)
            {
                ListViewItem listV = new ListViewItem(x.Date);
                listV.SubItems.Add(x.Name);
                listV.SubItems.Add(x.Url);
                hlvHistory.Items.Add(listV);
            }
            if (selectedValue <= (hlvHistory.Items.Count - 1))
            {
                hlvHistory.SelectedIndices.Clear();
                if (selectedValue < (hlvHistory.Items.Count - 1))
                {
                    hlvHistory.Items[selectedValue].Selected = true;
                    hlvHistory.TopItem = scroll;
                }
            }
            hlvHistory.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private readonly string iconStorage = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\IconStorage\\";

        private void updateFavoritesImages()
        {
            foreach (ToolStripMenuItem x in favoritesFolders)
            {
                x.Image = HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.folder : Properties.Resources.folder_w;
            }
            foreach (ToolStripMenuItem x in favoritesNoIcon)
            {
                x.Image = HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.web : Properties.Resources.web_w;
            }
        }
        
        private void menuStrip1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (((ToolStripMenuItem)sender).Tag != null)
                {
                    if (((ToolStripMenuItem)sender).Tag.ToString() != "korot://folder")
                    {
                        chromiumWebBrowser1.Load(((ToolStripMenuItem)sender).Tag.ToString());
                    }
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                if (((ToolStripMenuItem)sender).Tag != null)
                {
                    selectedFavorite = ((ToolStripMenuItem)sender);
                    tsopenInNewTab.Text = (((ToolStripMenuItem)sender).Tag.ToString() != "korot://folder") ? openInNewTab : openAllInNewTab;
                    openİnNewWindowToolStripMenuItem.Text = (((ToolStripMenuItem)sender).Tag.ToString() != "korot://folder") ? openInNewWindow : openAllInNewWindow;
                    openİnNewIncognitoWindowToolStripMenuItem.Text = (((ToolStripMenuItem)sender).Tag.ToString() != "korot://folder") ? openInNewIncWindow : openAllInNewIncWindow;
                    cmsFavorite.Show(MousePosition);
                }
            }
        }
        public void FindUpdate(int identifier, int count, int activeMatchOrdinal, bool finalUpdate)
        {
            findIdentifier = identifier;
            findTotal = count;
            findCurrent = activeMatchOrdinal;
            findLast = finalUpdate;
        }
        private void ListBox2_DoubleClick(object sender, EventArgs e)
        {
            if (listBox2.SelectedItem != null)
            {
                HTAlt.WinForms.HTMsgBox mesaj = new HTAlt.WinForms.HTMsgBox("Korot", listBox2.SelectedItem.ToString() + Environment.NewLine + ThemeMessage, new HTAlt.WinForms.HTDialogBoxContext() { Yes = true, No = true, Cancel = true }) {StartPosition = FormStartPosition.CenterParent,Yes = Yes,No = No,OK = OK, Cancel = Cancel,BackgroundColor = Settings.Theme.BackColor,Icon= Icon };
                if (mesaj.ShowDialog() == DialogResult.Yes)
                {
                    LoadTheme(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\" + Properties.Settings.Default.LastUser + "\\Themes\\" + listBox2.SelectedItem.ToString());
                    comboBox1.Text = listBox2.SelectedItem.ToString().Replace(".ktf", "");
                }
            }
        }
        #region "Translate"
        public string notificationPermission = "Allow [URL] for sending notifications?";
        public string allow = "Allow";
        public string deny = "Deny";
        public string ubuntuLicense = "Ubuntu Font License";
        public string newProfileInfo = "Please enter a name for the new profile.It should not contain: ";
        public string Yes = "Yes";
        public string No = "No";
        public string OK = "OK";
        public string Cancel = "Cancel";
        public string updateTitleTheme = "Korot Theme Updater";
        public string updateTitleExt = "Korot Extension Updater";
        public string updateExtInfo = "Updating [NAME]...[NEWLINE]Please wait...";
        public string openInNewWindow = "Open in New Window";
        public string openAllInNewWindow = "Open All in New Window(s)";
        public string openInNewIncWindow = "Open in New Incognito Window";
        public string openAllInNewIncWindow = "Open All in New Incognito Window(s)";
        public string openInNewTab = "Open in New Tab";
        public string openAllInNewTab = "Open All in New Tab(s)";
        public string newFavorite = "New Favorite";
        public string nametd = "Name :";
        public string urltd = "Url : ";
        public string add = "Add";
        public string newFolder = "New Folder";
        public string defaultFolderName = "New Folder";
        public string folderInfo = "Please enter a name for new Folder.";
        public string copyImage = "Copy Image";
        public string openLinkInNewWindow = "Open Link in a New Window";
        public string openLinkInNewIncWindow = "Open Link in a New Incognito Window";
        public string copyImageAddress = "Copy Image Address";
        public string saveLinkAs = "Save Link as...";
        public string goBack = "Go Back";
        public string goForward = "Go Forward";
        public string refresh = "Refresh";
        public string refreshNoCache = "Refresh (No Cache)";
        public string stop = "Stop";
        public string selectAll = "Select All";
        public string openLinkInNewTab = "Open Link in New Tab";
        public string copyLink = "Copy Link";
        public string saveImageAs = "Save Image as...";
        public string openImageInNewTab = "Open Image in New Tab";
        public string paste = "Paste";
        public string copy = "Copy";
        public string cut = "Cut";
        public string undo = "Undo";
        public string redo = "Redo";
        public string delete = "Delete";
        public string SearchOrOpenSelectedInNewTab = "Search/Open Selected in New Tab";
        public string developerTools = "Developer Tools";
        public string viewSource = "View Source";
        public string licenseTitle = "Licenses & Special Thanks Page";
        public string kLicense = "Korot License";
        public string vsLicense = "Microsoft Visual Studio 2019 Community License";
        public string chLicense = "Chromium License";
        public string cefLicense = "CefSharp License";
        public string etLicense = "EasyTabs License";
        public string specialThanks = "Special Thanks...";
        public string JSConfirm = "Confirm on page [TITLE]:";
        public string JSAlert = "A message from page [TITLE]:";
        public string selectAFolder = "Select a folder for downloads.";
        public string resetConfirm = "Do you want to reset Korot?" + Environment.NewLine + "(All of your personal datas are going to gone forever. This includes profiles, settings, downloads etc.)";
        public string findC = "Current: ";
        public string findT = "Total: ";
        public string findL = "(Last)";
        public string noSearch = "Not Searching or No Results";
        public string aboutInfo = "Korot uses Chromium by Google using CefSharp. [NEWLINE]Korot is written in C# using Visual Studio Community by Microsoft. [NEWLINE]Korot uses modified version of EasyTabs. [NEWLINE]Translation made by Haltroy. [NEWLINE][THEMENAME] theme made by [THEMEAUTHOR].";
        public string htmlFiles = "HTML File";
        public string print = "Print";
        public string IncognitoT = "Incognito";
        public string IncognitoTitle = "You are now in Incognito Mode!";
        public string IncognitoTitle1 = "Korot will not going to:";
        public string IncognitoT1M1 = "Record your history and downloads";
        public string IncognitoT1M2 = "Record cookies, sessions and form details";
        public string IncognitoT1M3 = "Record settings";
        public string IncognitoTitle2 = "But your activity can recorded by:";
        public string IncognitoT2M1 = "Websites";
        public string IncognitoT2M2 = "Your Internet service provider or your local network owner";
        public string IncognitoT2M3 = "Other viewers";
        public string disallowCookie = "Disallow this page using cookies";
        public string allowCookie = "Allow this page using cookie";
        public string imageFiles = "Image Files";
        public string allFiles = "All Files";
        public string selectBackImage = "Select a background image...";
        public string usingBC = "Using background color";
        public string settingstitle = "Settings";
        public string restoreOldSessions = "Restore last session";
        public string newWindow = "New Window";
        public string newincognitoWindow = "New Incognito Window";
        public string usesCookies = "This website uses cookies.";
        public string notUsesCookies = "This website does not use cookies.";
        public string showCertError = "Show Certificate Error";
        public string CertificateErrorMenuTitle = "Certificate Error Details";
        public string CertificateErrorTitle = "Not Safe";
        public string CertificateError = "This website is using a certificate that has errors.";
        public string CertificateOKTitle = "Safe";
        public string CertificateOK = "This website is using a certificate that has no errors.";
        public string ErrorTheme = "This theme file is corrupted or not suitable for this version.";
        public string ThemeMessage = "Do you want to change to this theme ?";
        public string UserAgentMessage = "Please enter an user agent.";
        public string installStatus = "Downloading Update...";
        public string StatusType = "[PERC]% | [CURRENT] KiB downloaded out of [TOTAL] KiB.";
        public string enterAValidCode = "Please enter a Valid Base64 Code.";
        public string enterAValidUrl = "Enter a Valid URL";
        public string goTotxt = "Go to \"[TEXT]\"";
        public string SearchOnWeb = "Search \"[TEXT]\"";
        public string defaultproxytext = "Default Proxy";
        public string SearchOnPage = "Search: ";
        //public string CaseSensitive = "Case Sensitive";
        public string privatemode = "Incognito";
        public string updateTitle = "Korot - Update";
        public string updateMessage = "Update available.Do you want to update?";
        public string updateError = "Error while checking for the updates.";
        public string checking = "Checking for updates...";
        public string uptodate = "Your Korot is up-to-date.";
        public string updateavailable = "Update available.";
        public string NewTabtitle = "New Tab";
        public string customSearchNote = "(Note: Searched text will be put after the url)";
        public string customSearchMessage = "Write Custom Search Url";
        public string korotdownloading = "Korot - Downloading";
        public string fromtwodot = "From : ";
        public string totwodot = "To : ";
        public string open = "Open";
        public string Search = "Search";
        public string run = "Run";
        public string startatstarup = "Run at startup";
        public string ErrorPageTitle = "Korot - Error";
        public string MonthNames = "\"January\",\"February\",\"March\",\"April\",\"May\",\"June\",\"July\",\"August\",\"September\",\"October\",\"November\",\"December\"";
        public string DayNames = "\"Sunday\",\"Monday\",\"Tuesday\",\"Wednesday\",\"Thursday\",\"Friday\",\"Saturday\"";
        public string SearchHelpText = "Search on web or enter an URL.";
        public string KT = "Korot can't display this page.";
        public string ET = "One possibility is because of one of these errors:";
        public string E1 = "1.  The URL is incorrect.";
        public string E2 = "2.  The website is not responding,too busy or too slow.";
        public string E3 = "3.  Machine disconnected from Internet or connection is too slow.";
        public string E4 = "4.  Antivirus program thinks this browser is a virus or the Website includes a virus.";
        public string RT = "We recommend:";
        public string R1 = "1.  Checking the URL for errors(like grammar errors). ";
        public string R2 = "2.  Connect the machine to Internet. ";
        public string R3 = "3.  Wait a few minutes and try again. ";
        public string R4 = "4.  Disable Antivirus or add this browser to whitelist of Antivirus.";
        public string switchTo = "Switch to:";
        public string deleteProfile = "Delete this profile";
        public string newprofile = "New Profile";
        public string CertErrorPageTitle = "This website is not secure";
        public string CertErrorPageMessage = "This website is using a certificate that has errors. Which means your information (credit cards,passwords,messages...) can be stolen by unknown people in this website.";
        public string CertErrorPageButton = "I understand these risks.";
        public string renderProcessDies = "Render Process Terminated. Closing application...";
        public string themeInfo = "[THEMENAME] theme made by [THEMEAUTHOR].";
        public string anon = "an unknown person";
        public string noname = "This unknown";
        public string text = "Text";
        public string image = "Image";
        public string link = "Link";
        //Editor
        public string catCommon = "Common";
        public string catText = "Text-based";
        public string catOnline = "Online";
        public string catPicture = "Picture";
        public string TitleID = "ID: ";
        public string TitleBackColor = "BackColor: ";
        public string TitleText = "Text: ";
        public string TitleFont = "Font: ";
        public string TitleSize = "FontSize: ";
        public string TitleProp = "FontProperties: ";
        public string TitleRegular = "Regular";
        public string TitleBold = "Bold";
        public string TitleItalic = "Italic";
        public string TitleUnderline = "Underline";
        public string TitleStrikeout = "Strikeout";
        public string TitleForeColor = "ForeColor: ";
        public string TitleSource = "Source: ";
        public string TitleWidth = "Width: ";
        public string TitleHeight = "Height: ";
        public string TitleDone = "Done";
        public string TitleEditItem = "Edit Item";
        public string importColItem = "Import item";
        public string importColItemInfo = "Enter a valid item code to import.";
        public string changeColID = "Change Collection ID";
        public string changeColIDInfo = "Enter a valid ID for this collection.";
        public string changeColText = "Change Collection Text";
        public string changeColTextInfo = "Enter a valid Text for this collection.";
        public string empty = "((empty))";
        public string SetToDefault = "Set to default";
        //Collection Manager
        public string newColInfo = "Enter a name for new collection";
        public string newColName = "New Collection";
        public string importColInfo = "Enter code for new collection";
        public string delColInfo = "Do you really want to delete $?";
        public string clearColInfo = "Do you really want to delete all?";
        public string okToClipboard = "Press OK to copy to clipboard.";
        public string newCollection = "New Collection";
        public string deleteCollection = "Delete this collection";
        public string clearCollection = "Clear";
        public string importCollection = "Import";
        public string exportCollection = "Export";
        public string deleteItem = "Delete this item";
        public string exportItem = "Export this item";
        public string editItem = "Edit this item";
        public string addToCollection = "Add to Collection";
        public string titleBackInfo = "Click the rectangle on top to change color.";
        public string MuteThisTab = "Mute this tab";
        public string UnmuteThisTab = "Unmute this tab";
        // CHANGE THIS WHEN YOU TOUCHED THE LANGUAGE SYSTEM EVEN BY ADDING SOMETHING OR DELETING
        public Version KLangVer = new Version("0.6.1.0");
        private void dummyCMS_Opening(object sender, CancelEventArgs e)
        {
            Process.Start(Application.StartupPath + "//Lang//");
        }
        public void LoadLangFromFile(string fileLocation)
        {
            string Playlist = HTAlt.Tools.ReadFile(fileLocation, Encoding.UTF8);
            char[] token = new char[] { Environment.NewLine.ToCharArray()[0] };
            string[] SF = Playlist.Split(token);
            while (SF.Length != 366) //ncı gün
            {
                Array.Resize<string>(ref SF, SF.Length + 1);
                SF[SF.Length - 1] = "mmisingno";
            }
            Version langVersion = new Version(SF[0].ToString().Replace(Environment.NewLine, "") != "mmissingno" ? SF[0].ToString().Replace(Environment.NewLine, "") : "0.0.0.0");
            int versionCompability = KLangVer.CompareTo(langVersion);
            if (versionCompability < 0)
            {
                if (!Settings.DisableLanguageError && !NotificationListenerMode)
                {
                    Settings.DisableLanguageError = true;
                    HTAlt.WinForms.HTMsgBox mesaj = new HTAlt.WinForms.HTMsgBox("Korot", "This language file is made for an older version of Korot. This can cause some problems. Do you wish to proceed?", new HTAlt.WinForms.HTDialogBoxContext() { Yes = true, No = true, Cancel = true }) { StartPosition = FormStartPosition.CenterParent, Yes = Yes, No = No, OK = OK, Cancel = Cancel, BackgroundColor = Settings.Theme.BackColor, Icon = Icon };
                    if (mesaj.ShowDialog() != DialogResult.Yes)
                    {
                        return;
                    }
                }
            }
            if (SF.Length >= 344)
            {
                MuteThisTab = SF[350].Substring(1).Replace(Environment.NewLine, "");
                UnmuteThisTab = SF[351].Substring(1).Replace(Environment.NewLine, "");
                MuteTS.Text = isMuted ? UnmuteThisTab : MuteThisTab;
                btNotification.ButtonText = SF[324].Substring(1).Replace(Environment.NewLine, "");
                lbNotifSetting.Text = SF[325].Substring(1).Replace(Environment.NewLine, "");
                tpNotification.Text = SF[325].Substring(1).Replace(Environment.NewLine, "");
                lbPlayNotifSound.Text = SF[326].Substring(1).Replace(Environment.NewLine, "");
                lbSilentMode.Text = SF[327].Substring(1).Replace(Environment.NewLine, "");
                lbSchedule.Text = SF[328].Substring(1).Replace(Environment.NewLine, "");
                scheduleFrom.Text = SF[329].Substring(1).Replace(Environment.NewLine, "");
                scheduleTo.Text = SF[330].Substring(1).Replace(Environment.NewLine, "");
                lb24HType.Text = SF[331].Substring(1).Replace(Environment.NewLine, "");
                scheduleEvery.Text = SF[332].Substring(1).Replace(Environment.NewLine, "");
                lbSunday.Text = SF[333].Substring(1).Replace(Environment.NewLine, "");
                lbMonday.Text = SF[334].Substring(1).Replace(Environment.NewLine, "");
                lbTuesday.Text = SF[335].Substring(1).Replace(Environment.NewLine, "");
                lbWednesday.Text = SF[336].Substring(1).Replace(Environment.NewLine, "");
                lbThursday.Text = SF[337].Substring(1).Replace(Environment.NewLine, "");
                lbFriday.Text = SF[338].Substring(1).Replace(Environment.NewLine, "");
                lbSaturday.Text = SF[339].Substring(1).Replace(Environment.NewLine, "");
                notificationPermission = SF[323].Substring(1).Replace(Environment.NewLine, "");
                deny = SF[322].Substring(1).Replace(Environment.NewLine, "");
                allow = SF[321].Substring(1).Replace(Environment.NewLine, "");
                changeColID = SF[317].Substring(1).Replace(Environment.NewLine, "");
                changeColIDInfo = SF[318].Substring(1).Replace(Environment.NewLine, "");
                changeColText = SF[319].Substring(1).Replace(Environment.NewLine, "");
                changeColTextInfo = SF[320].Substring(1).Replace(Environment.NewLine, "");
                importColItem = SF[316].Substring(1).Replace(Environment.NewLine, "");
                importColItemInfo = SF[315].Substring(1).Replace(Environment.NewLine, "");
                SetToDefault = SF[314].Substring(1).Replace(Environment.NewLine, "");
                tsChangeTitleBack.Text = SF[312].Substring(1).Replace(Environment.NewLine, "");
                titleBackInfo = SF[313].Substring(1).Replace(Environment.NewLine, "");
                addToCollection = SF[311].Substring(1).Replace(Environment.NewLine, "");
                newColInfo = SF[297].Substring(1).Replace(Environment.NewLine, "");
                newColName = SF[298].Substring(1).Replace(Environment.NewLine, "");
                importColInfo = SF[299].Substring(1).Replace(Environment.NewLine, "");
                delColInfo = SF[300].Substring(1).Replace(Environment.NewLine, "");
                clearColInfo = SF[301].Substring(1).Replace(Environment.NewLine, "");
                okToClipboard = SF[302].Substring(1).Replace(Environment.NewLine, "");
                newCollection = SF[303].Substring(1).Replace(Environment.NewLine, "");
                deleteCollection = SF[304].Substring(1).Replace(Environment.NewLine, "");
                clearCollection = SF[305].Substring(1).Replace(Environment.NewLine, "");
                importCollection = SF[306].Substring(1).Replace(Environment.NewLine, "");
                exportCollection = SF[307].Substring(1).Replace(Environment.NewLine, "");
                deleteItem = SF[308].Substring(1).Replace(Environment.NewLine, "");
                exportItem = SF[309].Substring(1).Replace(Environment.NewLine, "");
                editItem = SF[310].Substring(1).Replace(Environment.NewLine, "");
                catCommon = SF[276].Substring(1).Replace(Environment.NewLine, "");
                catText = SF[277].Substring(1).Replace(Environment.NewLine, "");
                catOnline = SF[278].Substring(1).Replace(Environment.NewLine, "");
                catPicture = SF[279].Substring(1).Replace(Environment.NewLine, "");
                TitleID = SF[280].Substring(1).Replace(Environment.NewLine, "");
                TitleBackColor = SF[281].Substring(1).Replace(Environment.NewLine, "");
                TitleText = SF[282].Substring(1).Replace(Environment.NewLine, "");
                TitleFont = SF[283].Substring(1).Replace(Environment.NewLine, "");
                TitleSize = SF[284].Substring(1).Replace(Environment.NewLine, "");
                TitleProp = SF[285].Substring(1).Replace(Environment.NewLine, "");
                TitleRegular = SF[286].Substring(1).Replace(Environment.NewLine, "");
                TitleBold = SF[287].Substring(1).Replace(Environment.NewLine, "");
                TitleItalic = SF[288].Substring(1).Replace(Environment.NewLine, "");
                TitleUnderline = SF[289].Substring(1).Replace(Environment.NewLine, "");
                TitleStrikeout = SF[290].Substring(1).Replace(Environment.NewLine, "");
                TitleForeColor = SF[291].Substring(1).Replace(Environment.NewLine, "");
                TitleSource = SF[292].Substring(1).Replace(Environment.NewLine, "");
                TitleWidth = SF[293].Substring(1).Replace(Environment.NewLine, "");
                TitleHeight = SF[294].Substring(1).Replace(Environment.NewLine, "");
                TitleDone = SF[295].Substring(1).Replace(Environment.NewLine, "");
                TitleEditItem = SF[296].Substring(1).Replace(Environment.NewLine, "");
                image = SF[273].Substring(1).Replace(Environment.NewLine, "");
                text = SF[274].Substring(1).Replace(Environment.NewLine, "");
                link = SF[275].Substring(1).Replace(Environment.NewLine, "");
                label9.Text = SF[272].Substring(1).Replace(Environment.NewLine, "");
                tsCollections.Text = SF[272].Substring(1).Replace(Environment.NewLine, "");
                tpCert.Text = SF[271].Substring(1).Replace(Environment.NewLine, "");
                tpAbout.Text = SF[19].Substring(1).Replace(Environment.NewLine, "");
                tpSettings.Text = SF[10].Substring(1).Replace(Environment.NewLine, "");
                tpSite.Text = SF[197].Substring(1).Replace(Environment.NewLine, "");
                tpCollection.Text = SF[272].Substring(1).Replace(Environment.NewLine, "");
                tpDownload.Text = SF[39].Substring(1).Replace(Environment.NewLine, "");
                tpHistory.Text = SF[11].Substring(1).Replace(Environment.NewLine, "");
                tpTheme.Text = SF[14].Substring(1).Replace(Environment.NewLine, "");
                lbautoRestore.Text = SF[270].Substring(1).Replace(Environment.NewLine, "");
                Properties.Settings.Default.KorotErrorTitle = SF[262].Substring(1).Replace(Environment.NewLine, "");
                Properties.Settings.Default.KorotErrorDesc = SF[263].Substring(1).Replace(Environment.NewLine, "");
                Properties.Settings.Default.KorotErrorTI = SF[264].Substring(1).Replace(Environment.NewLine, "");
                ubuntuLicense = SF[261].Substring(1).Replace(Environment.NewLine, "");
                tsFullscreen.Text = SF[257].Substring(1).Replace(Environment.NewLine, "");
                updateTitleTheme = SF[258].Substring(1).Replace(Environment.NewLine, "");
                updateTitleExt = SF[259].Substring(1).Replace(Environment.NewLine, "");
                updateExtInfo = SF[260].Substring(1).Replace(Environment.NewLine, "");
                openInNewWindow = SF[252].Substring(1).Replace(Environment.NewLine, "");
                openAllInNewWindow = SF[253].Substring(1).Replace(Environment.NewLine, "");
                openInNewIncWindow = SF[254].Substring(1).Replace(Environment.NewLine, "");
                openAllInNewIncWindow = SF[255].Substring(1).Replace(Environment.NewLine, "");
                openAllInNewTab = SF[251].Substring(1).Replace(Environment.NewLine, "");
                newFavorite = SF[244].Substring(1).Replace(Environment.NewLine, "");
                nametd = SF[245].Substring(1).Replace(Environment.NewLine, "");
                urltd = SF[246].Substring(1).Replace(Environment.NewLine, "");
                add = SF[247].Substring(1).Replace(Environment.NewLine, "");
                newFavoriteToolStripMenuItem.Text = SF[256].Substring(1).Replace(Environment.NewLine, "");
                newFolderToolStripMenuItem.Text = SF[248].Substring(1).Replace(Environment.NewLine, "");
                newFolder = SF[247 + 1].Substring(1).Replace(Environment.NewLine, "");
                defaultFolderName = SF[248 + 1].Substring(1).Replace(Environment.NewLine, "");
                folderInfo = SF[249 + 1].Substring(1).Replace(Environment.NewLine, "");
                copyImage = SF[238 + 1].Substring(1).Replace(Environment.NewLine, "");
                openLinkInNewWindow = SF[239 + 1].Substring(1).Replace(Environment.NewLine, "");
                openLinkInNewIncWindow = SF[240 + 1].Substring(1).Replace(Environment.NewLine, "");
                copyImageAddress = SF[241 + 1].Substring(1).Replace(Environment.NewLine, "");
                saveLinkAs = SF[242 + 1].Substring(1).Replace(Environment.NewLine, "");
                tsWebStore.Text = SF[237 + 1].Substring(1).Replace(Environment.NewLine, "");
                tsEmptyExt.Text = SF[233 + 1].Substring(1).Replace(Environment.NewLine, "");
                tsEmptyProfile.Text = SF[233 + 1].Substring(1).Replace(Environment.NewLine, "");
                empty = SF[233 + 1].Substring(1).Replace(Environment.NewLine, "");
                btCleanLog.ButtonText = SF[234 + 1].Substring(1).Replace(Environment.NewLine, "");
                label33.Text = SF[228 + 1].Substring(1).Replace(Environment.NewLine, "");
                label31.Text = SF[226 + 1].Substring(1).Replace(Environment.NewLine, "");
                label32.Text = SF[227 + 1].Substring(1).Replace(Environment.NewLine, "");
                rbNone.Text = SF[218 + 1].Substring(1).Replace(Environment.NewLine, "");
                rbTile.Text = SF[219 + 1].Substring(1).Replace(Environment.NewLine, "");
                rbCenter.Text = SF[220 + 1].Substring(1).Replace(Environment.NewLine, "");
                rbStretch.Text = SF[221 + 1].Substring(1).Replace(Environment.NewLine, "");
                rbZoom.Text = SF[222 + 1].Substring(1).Replace(Environment.NewLine, "");
                rbBackColor.Text = SF[223 + 1].Substring(1).Replace(Environment.NewLine, "");
                rbForeColor.Text = SF[224 + 1].Substring(1).Replace(Environment.NewLine, "");
                rbOverlayColor.Text = SF[225 + 1].Substring(1).Replace(Environment.NewLine, "");
                rbBackColor1.Text = SF[223 + 1].Substring(1).Replace(Environment.NewLine, "");
                rbForeColor1.Text = SF[224 + 1].Substring(1).Replace(Environment.NewLine, "");
                rbOverlayColor1.Text = SF[225 + 1].Substring(1).Replace(Environment.NewLine, "");
                licenseTitle = SF[211 + 1].Substring(1).Replace(Environment.NewLine, "");
                kLicense = SF[212 + 1].Substring(1).Replace(Environment.NewLine, "");
                vsLicense = SF[213 + 1].Substring(1).Replace(Environment.NewLine, "");
                chLicense = SF[214 + 1].Substring(1).Replace(Environment.NewLine, "");
                cefLicense = SF[215 + 1].Substring(1).Replace(Environment.NewLine, "");
                etLicense = SF[216 + 1].Substring(1).Replace(Environment.NewLine, "");
                specialThanks = SF[217 + 1].Substring(1).Replace(Environment.NewLine, "");
                JSAlert = SF[210 + 1].Substring(1).Replace(Environment.NewLine, "");
                JSConfirm = SF[209 + 1].Substring(1).Replace(Environment.NewLine, "");
                selectAFolder = SF[208 + 1].Substring(1).Replace(Environment.NewLine, "");
                showNewTabPageToolStripMenuItem.Text = SF[204 + 1].Substring(1).Replace(Environment.NewLine, "");
                showHomepageToolStripMenuItem.Text = SF[205 + 1].Substring(1).Replace(Environment.NewLine, "");
                showAWebsiteToolStripMenuItem.Text = SF[206 + 1].Substring(1).Replace(Environment.NewLine, "");
                lbDownloadFolder.Text = SF[203 + 1].Substring(1).Replace(Environment.NewLine, "");
                lbAutoDownload.Text = SF[202 + 1].Substring(1).Replace(Environment.NewLine, "");
                label28.Text = SF[201 + 1].Substring(1).Replace(Environment.NewLine, "");
                btReset.ButtonText = SF[200 + 1].Substring(1).Replace(Environment.NewLine, "");
                resetConfirm = SF[199 + 1].Substring(1).Replace(Environment.NewLine, "");
                label27.Text = SF[196 + 1].Substring(1).Replace(Environment.NewLine, "");
                allowSelectedToolStripMenuItem.Text = SF[197 + 1].Substring(1).Replace(Environment.NewLine, "");
                clearToolStripMenuItem1.Text = SF[17 + 1].Substring(1).Replace(Environment.NewLine, "");
                btCookie.ButtonText = SF[198 + 1].Substring(1).Replace(Environment.NewLine, "");
                ıncognitoModeToolStripMenuItem.Text = SF[193 + 1].Substring(1).Replace(Environment.NewLine, "");
                thisSessionİsNotGoingToBeSavedToolStripMenuItem.Text = SF[194 + 1].Substring(1).Replace(Environment.NewLine, "");
                clickHereToLearnMoreToolStripMenuItem.Text = SF[195 + 1].Substring(1).Replace(Environment.NewLine, "");
                chStatus.Text = SF[192 + 1].Substring(1).Replace(Environment.NewLine, "");
                lbLastProxy.Text = SF[186 + 1].Substring(1).Replace(Environment.NewLine, "");
                findC = SF[187 + 1].Substring(1).Replace(Environment.NewLine, "");
                findT = SF[188 + 1].Substring(1).Replace(Environment.NewLine, "");
                findL = SF[189 + 1].Substring(1).Replace(Environment.NewLine, "");
                noSearch = SF[190 + 1].Substring(1).Replace(Environment.NewLine, "");
                tsSearchNext.Text = SF[185 + 1].Substring(1).Replace(Environment.NewLine, "");
                anon = SF[183 + 1].Substring(1).Replace(Environment.NewLine, "");
                noname = SF[184 + 1].Substring(1).Replace(Environment.NewLine, "");
                themeInfo = SF[182 + 1].Substring(1).Replace(Environment.NewLine, "");
                extensionToolStripMenuItem1.Text = SF[181 + 1].Substring(1).Replace(Environment.NewLine, "");
                renderProcessDies = SF[178 + 1].Substring(1).Replace(Environment.NewLine, "");
                enterAValidCode = SF[177 + 1].Substring(1).Replace(Environment.NewLine, "");
                zoomInToolStripMenuItem.Text = SF[174 + 1].Substring(1).Replace(Environment.NewLine, "");
                resetZoomToolStripMenuItem.Text = SF[175 + 1].Substring(1).Replace(Environment.NewLine, "");
                zoomOutToolStripMenuItem.Text = SF[176 + 1].Substring(1).Replace(Environment.NewLine, "");
                htmlFiles = SF[171 + 1].Substring(1).Replace(Environment.NewLine, "");
                takeAScreenshotToolStripMenuItem.Text = SF[172 + 1].Substring(1).Replace(Environment.NewLine, "");
                saveThisPageToolStripMenuItem.Text = SF[173 + 1].Substring(1).Replace(Environment.NewLine, "");
                print = SF[170 + 1].Substring(1).Replace(Environment.NewLine, "");
                IncognitoT = SF[160 + 1].Substring(1).Replace(Environment.NewLine, "");
                IncognitoTitle = SF[160 + 1].Substring(1).Replace(Environment.NewLine, "");
                IncognitoTitle1 = SF[161 + 1].Substring(1).Replace(Environment.NewLine, "");
                IncognitoT1M1 = SF[163 + 1].Substring(1).Replace(Environment.NewLine, "");
                IncognitoT1M2 = SF[164 + 1].Substring(1).Replace(Environment.NewLine, "");
                IncognitoT1M3 = SF[165 + 1].Substring(1).Replace(Environment.NewLine, "");
                IncognitoTitle2 = SF[166 + 1].Substring(1).Replace(Environment.NewLine, "");
                IncognitoT2M1 = SF[167 + 1].Substring(1).Replace(Environment.NewLine, "");
                IncognitoT2M2 = SF[168 + 1].Substring(1).Replace(Environment.NewLine, "");
                IncognitoT2M3 = SF[169 + 1].Substring(1).Replace(Environment.NewLine, "");
                disallowCookie = SF[158 + 1].Substring(1).Replace(Environment.NewLine, "");
                allowCookie = SF[159 + 1].Substring(1).Replace(Environment.NewLine, "");
                if (chromiumWebBrowser1 != null)
                {
                    if (chromiumWebBrowser1.Address != null)
                    {
                        if (Settings.GetSiteFromUrl(HTAlt.Tools.GetBaseURL(chromiumWebBrowser1.Address)) != null)
                        {
                            if (!Settings.GetSiteFromUrl(HTAlt.Tools.GetBaseURL(chromiumWebBrowser1.Address)).AllowCookies)
                            {
                                disallowThisPageForCookieAccessToolStripMenuItem.Text = SF[158 + 1].Substring(1).Replace(Environment.NewLine, "");
                            }
                            else
                            {
                                disallowThisPageForCookieAccessToolStripMenuItem.Text = SF[159 + 1].Substring(1).Replace(Environment.NewLine, "");
                            }
                        }
                        else
                        {
                            disallowThisPageForCookieAccessToolStripMenuItem.Text = SF[159 + 1].Substring(1).Replace(Environment.NewLine, "");
                        }
                    }
                    else
                    {
                        disallowThisPageForCookieAccessToolStripMenuItem.Text = SF[159 + 1].Substring(1).Replace(Environment.NewLine, "");
                    }
                }
                else
                {
                    disallowThisPageForCookieAccessToolStripMenuItem.Text = SF[159 + 1].Substring(1).Replace(Environment.NewLine, "");
                }
                label25.Text = SF[139 + 1].Substring(1).Replace(Environment.NewLine, "");
                imageFiles = SF[136 + 1].Substring(1).Replace(Environment.NewLine, "");
                allFiles = SF[137 + 1].Substring(1).Replace(Environment.NewLine, "");
                selectBackImage = SF[138 + 1].Substring(1).Replace(Environment.NewLine, "");
                colorToolStripMenuItem.Text = SF[132 + 1].Substring(1).Replace(Environment.NewLine, "");
                usingBC = SF[133 + 1].Substring(1).Replace(Environment.NewLine, "");
                ımageFromURLToolStripMenuItem.Text = SF[134 + 1].Substring(1).Replace(Environment.NewLine, "");
                ımageFromLocalFileToolStripMenuItem.Text = SF[135 + 1].Substring(1).Replace(Environment.NewLine, "");
                lbDNT.Text = SF[129 + 1].Substring(1).Replace(Environment.NewLine, "");
                aboutInfo = SF[108 + 1].Substring(1).Replace(Environment.NewLine, "");
                if (string.IsNullOrWhiteSpace(Settings.Theme.Author) && string.IsNullOrWhiteSpace(Settings.Theme.Name))
                {
                    label21.Text = SF[108 + 1].Substring(1).Replace(Environment.NewLine, "").Replace("[NEWLINE]", Environment.NewLine);
                }
                else
                {
                    label21.Text = SF[108 + 1].Substring(1).Replace(Environment.NewLine, "").Replace("[NEWLINE]", Environment.NewLine) + Environment.NewLine + SF[182 + 1].Substring(1).Replace(Environment.NewLine, "").Replace("[THEMEAUTHOR]", string.IsNullOrWhiteSpace(Settings.Theme.Author) ? anon : Settings.Theme.Author).Replace("[THEMENAME]", string.IsNullOrWhiteSpace(Settings.Theme.Name) ? noname : Settings.Theme.Name);
                }
                linkLabel1.Text = SF[109 + 1].Substring(1).Replace(Environment.NewLine, "");
                lbSettings.Text = SF[9 + 1].Substring(1).Replace(Environment.NewLine, "");
                CertErrorPageButton = SF[107 + 1].Substring(1).Replace(Environment.NewLine, "");
                CertErrorPageMessage = SF[106 + 1].Substring(1).Replace(Environment.NewLine, "");
                CertErrorPageTitle = SF[105 + 1].Substring(1).Replace(Environment.NewLine, "");
                usesCookies = SF[103 + 1].Substring(1).Replace(Environment.NewLine, "");
                notUsesCookies = SF[104 + 1].Substring(1).Replace(Environment.NewLine, "");
                showCertError = SF[102 + 1].Substring(1).Replace(Environment.NewLine, "");
                CertificateErrorMenuTitle = SF[97 + 1].Substring(1).Replace(Environment.NewLine, "");
                CertificateErrorTitle = SF[98 + 1].Substring(1).Replace(Environment.NewLine, "");
                CertificateError = SF[99 + 1].Substring(1).Replace(Environment.NewLine, "");
                CertificateOKTitle = SF[100 + 1].Substring(1).Replace(Environment.NewLine, "");
                CertificateOK = SF[101 + 1].Substring(1).Replace(Environment.NewLine, "");
                ErrorTheme = SF[96 + 1].Substring(1).Replace(Environment.NewLine, "");
                ThemeMessage = SF[95 + 1].Substring(1).Replace(Environment.NewLine, "");
                btUpdater.ButtonText = SF[90 + 1].Substring(1).Replace(Environment.NewLine, "");
                btInstall.ButtonText = SF[91 + 1].Substring(1).Replace(Environment.NewLine, "");
                checking = SF[88 + 1].Substring(1).Replace(Environment.NewLine, "");
                uptodate = SF[89 + 1].Substring(1).Replace(Environment.NewLine, "");
                installStatus = SF[92 + 1].Substring(1).Replace(Environment.NewLine, "");
                StatusType = SF[93 + 1].Substring(1).Replace(Environment.NewLine, "");
                radioButton1.Text = SF[4 + 1].Substring(1).Replace(Environment.NewLine, "");
                if (updateProgress == 0)
                {
                    lbUpdateStatus.Text = SF[88 + 1].Substring(1).Replace(Environment.NewLine, "");
                }
                else if (updateProgress == 1)
                {
                    lbUpdateStatus.Text = SF[89 + 1].Substring(1).Replace(Environment.NewLine, "");
                }
                else if (updateProgress == 2)
                {
                    lbUpdateStatus.Text = SF[87 + 1].Substring(1).Replace(Environment.NewLine, "");
                }
                else if (updateProgress == 3)
                {
                    lbUpdateStatus.Text = SF[3 + 1].Substring(1).Replace(Environment.NewLine, "");
                }
                updateavailable = SF[87 + 1].Substring(1).Replace(Environment.NewLine, ""); ;
                privatemode = SF[0 + 1].Substring(1).Replace(Environment.NewLine, "");
                updateTitle = SF[1 + 1].Substring(1).Replace(Environment.NewLine, "");
                updateMessage = SF[2 + 1].Substring(1).Replace(Environment.NewLine, "");
                updateError = SF[3 + 1].Substring(1).Replace(Environment.NewLine, "");
                NewTabtitle = SF[4 + 1].Substring(1).Replace(Environment.NewLine, "");
                customSearchNote = SF[5 + 1].Substring(1).Replace(Environment.NewLine, "");
                customSearchMessage = SF[6 + 1].Substring(1).Replace(Environment.NewLine, "");
                label12.Text = SF[16 + 1].Substring(1).Replace(Environment.NewLine, "");
                newWindow = SF[7 + 1].Substring(1).Replace(Environment.NewLine, "");
                newincognitoWindow = SF[8 + 1].Substring(1).Replace(Environment.NewLine, "");
                label6.Text = SF[38 + 1].Substring(1).Replace(Environment.NewLine, "");
                downloadsToolStripMenuItem.Text = SF[38 + 1].Substring(1).Replace(Environment.NewLine, "");
                aboutToolStripMenuItem.Text = SF[18 + 1].Substring(1).Replace(Environment.NewLine, "");
                lbHomepage.Text = SF[11 + 1].Substring(1).Replace(Environment.NewLine, "");
                SearchOnPage = SF[58 + 1].Substring(1).Replace(Environment.NewLine, "");
                label26.Text = SF[13 + 1].Substring(1).Replace(Environment.NewLine, "");
                tsThemes.Text = SF[94 + 1].Substring(1).Replace(Environment.NewLine, "");
                caseSensitiveToolStripMenuItem.Text = SF[59 + 1].Substring(1).Replace(Environment.NewLine, "");
                customToolStripMenuItem.Text = SF[14 + 1].Substring(1).Replace(Environment.NewLine, "");
                removeSelectedToolStripMenuItem.Text = SF[16].Substring(1).Replace(Environment.NewLine, "");
                clearToolStripMenuItem.Text = SF[18].Substring(1).Replace(Environment.NewLine, "");
                settingstitle = SF[9 + 1].Substring(1).Replace(Environment.NewLine, "");
                historyToolStripMenuItem.Text = SF[10 + 1].Substring(1).Replace(Environment.NewLine, "");
                label4.Text = SF[10 + 1].Substring(1).Replace(Environment.NewLine, "");
                label22.Text = SF[50 + 1].Substring(1).Replace(Environment.NewLine, "");
                goBack = SF[19 + 1].Substring(1).Replace(Environment.NewLine, "");
                goForward = SF[20 + 1].Substring(1).Replace(Environment.NewLine, "");
                refresh = SF[21 + 1].Substring(1).Replace(Environment.NewLine, "");
                refreshNoCache = SF[22 + 1].Substring(1).Replace(Environment.NewLine, "");
                stop = SF[23 + 1].Substring(1).Replace(Environment.NewLine, "");
                selectAll = SF[24 + 1].Substring(1).Replace(Environment.NewLine, "");
                openLinkInNewTab = SF[25 + 1].Substring(1).Replace(Environment.NewLine, "");
                copyLink = SF[26 + 1].Substring(1).Replace(Environment.NewLine, "");
                saveImageAs = SF[27 + 1].Substring(1).Replace(Environment.NewLine, "");
                openImageInNewTab = SF[28 + 1].Substring(1).Replace(Environment.NewLine, "");
                paste = SF[29 + 1].Substring(1).Replace(Environment.NewLine, "");
                copy = SF[30 + 1].Substring(1).Replace(Environment.NewLine, "");
                cut = SF[31 + 1].Substring(1).Replace(Environment.NewLine, "");
                undo = SF[32 + 1].Substring(1).Replace(Environment.NewLine, "");
                redo = SF[33 + 1].Substring(1).Replace(Environment.NewLine, "");
                delete = SF[34 + 1].Substring(1).Replace(Environment.NewLine, "");
                SearchOrOpenSelectedInNewTab = SF[35 + 1].Substring(1).Replace(Environment.NewLine, "");
                developerTools = SF[36 + 1].Substring(1).Replace(Environment.NewLine, "");
                viewSource = SF[37 + 1].Substring(1).Replace(Environment.NewLine, "");
                restoreOldSessions = SF[82 + 1].Substring(1).Replace(Environment.NewLine, "");
                label14.Text = SF[39 + 1].Substring(1).Replace(Environment.NewLine, "");
                enterAValidUrl = SF[80 + 1].Substring(1).Replace(Environment.NewLine, "");
                label16.Text = SF[40 + 1].Substring(1).Replace(Environment.NewLine, "");
                chDate.Text = SF[45 + 1].Substring(1).Replace(Environment.NewLine, "");
                chDateHistory.Text = SF[45 + 1].Substring(1).Replace(Environment.NewLine, "");
                chTitle.Text = SF[235 + 1].Substring(1).Replace(Environment.NewLine, "");
                chURL.Text = SF[236 + 1].Substring(1).Replace(Environment.NewLine, "");
                fromtwodot = SF[47 + 1].Substring(1).Replace(Environment.NewLine, "");
                chFrom.Text = SF[46 + 1].Substring(1).Replace(Environment.NewLine, "");
                totwodot = SF[48 + 1].Substring(1).Replace(Environment.NewLine, "");
                korotdownloading = SF[41 + 1].Substring(1).Replace(Environment.NewLine, "");
                chTo.Text = SF[44 + 1].Substring(1).Replace(Environment.NewLine, "");
                lbOpen.Text = SF[42 + 1].Substring(1).Replace(Environment.NewLine, "");
                open = SF[43 + 1].Substring(1).Replace(Environment.NewLine, "");
                openLinkİnNewTabToolStripMenuItem.Text = SF[25 + 1].Substring(1).Replace(Environment.NewLine, "");
                openInNewTab = SF[55 + 1].Substring(1).Replace(Environment.NewLine, "");
                removeSelectedTSMI.Text = SF[15 + 1].Substring(1).Replace(Environment.NewLine, "");
                clearTSMI.Text = SF[17 + 1].Substring(1).Replace(Environment.NewLine, "");
                openFileToolStripMenuItem.Text = SF[56 + 1].Substring(1).Replace(Environment.NewLine, "");
                openFileİnExplorerToolStripMenuItem.Text = SF[57 + 1].Substring(1).Replace(Environment.NewLine, "");
                removeSelectedToolStripMenuItem1.Text = SF[15 + 1].Substring(1).Replace(Environment.NewLine, "");
                clearToolStripMenuItem2.Text = SF[17 + 1].Substring(1).Replace(Environment.NewLine, "");
                DefaultProxyts.Text = SF[53 + 1].Substring(1).Replace(Environment.NewLine, "");
                label13.Text = SF[51 + 1].Substring(1).Replace(Environment.NewLine, "");
                Yes = SF[83 + 1].Substring(1).Replace(Environment.NewLine, "");
                No = SF[84 + 1].Substring(1).Replace(Environment.NewLine, "");
                OK = SF[85 + 1].Substring(1).Replace(Environment.NewLine, "");
                Cancel = SF[86 + 1].Substring(1).Replace(Environment.NewLine, "");
                button10.Text = SF[52 + 1].Substring(1).Replace(Environment.NewLine, "");
                label15.Text = SF[54 + 1].Substring(1).Replace(Environment.NewLine, "");
                SearchOnWeb = SF[79 + 1].Substring(1).Replace(Environment.NewLine, "");
                goTotxt = SF[78 + 1].Substring(1).Replace(Environment.NewLine, "");
                newProfileInfo = SF[81 + 1].Substring(1).Replace(Environment.NewLine, "");
                lbSearchEngine.Text = SF[12 + 1].Substring(1).Replace(Environment.NewLine, "");
                MonthNames = SF[61 + 1].Substring(1).Replace(Environment.NewLine, "");
                DayNames = SF[60 + 1].Substring(1).Replace(Environment.NewLine, "");
                SearchHelpText = SF[62 + 1].Substring(1).Replace(Environment.NewLine, "");
                ErrorPageTitle = SF[49 + 1].Substring(1).Replace(Environment.NewLine, "");
                KT = SF[63 + 1].Substring(1).Replace(Environment.NewLine, "");
                ET = SF[64 + 1].Substring(1).Replace(Environment.NewLine, "");
                E1 = SF[65 + 1].Substring(1).Replace(Environment.NewLine, "");
                E2 = SF[66 + 1].Substring(1).Replace(Environment.NewLine, "");
                E3 = SF[67 + 1].Substring(1).Replace(Environment.NewLine, "");
                E4 = SF[68 + 1].Substring(1).Replace(Environment.NewLine, "");
                RT = SF[69 + 1].Substring(1).Replace(Environment.NewLine, "");
                R1 = SF[70 + 1].Substring(1).Replace(Environment.NewLine, "");
                R2 = SF[71 + 1].Substring(1).Replace(Environment.NewLine, "");
                R3 = SF[72 + 1].Substring(1).Replace(Environment.NewLine, "");
                R4 = SF[73 + 1].Substring(1).Replace(Environment.NewLine, "");
                Search = SF[74 + 1].Substring(1).Replace(Environment.NewLine, "");
                newprofile = SF[76 + 1].Substring(1).Replace(Environment.NewLine, "");
                switchTo = SF[75 + 1].Substring(1).Replace(Environment.NewLine, "");
                deleteProfile = SF[77 + 1].Substring(1).Replace(Environment.NewLine, "");
                RefreshTranslation();
                RefreshSizes();
            }
            else
            {
                HTAlt.WinForms.HTMsgBox mesaj = new HTAlt.WinForms.HTMsgBox(ErrorPageTitle, "This file does not suitable for this version of Korot.Please ask the creator of this language to update." + Environment.NewLine + " Error : Missing Line" + "[ Line Count: " + SF.Length + "]", new HTAlt.WinForms.HTDialogBoxContext() { OK = true}) { StartPosition = FormStartPosition.CenterParent, Yes = Yes, No = No, OK = OK, Cancel = Cancel, BackgroundColor = Settings.Theme.BackColor, Icon = Icon };
                DialogResult diyalog = mesaj.ShowDialog();
                Output.WriteLine(" [KOROT] Error at applying a language file : [ Line Count: " + SF.Length + "]");
            }
            Settings.LanguageFile = fileLocation;
        }
        public void RefreshLangList()
        {
            cbLang.Items.Clear();
            foreach (string foundfile in Directory.GetFiles(Application.StartupPath + "//Lang//", "*.lang", SearchOption.TopDirectoryOnly))
            {
                cbLang.Items.Add(Path.GetFileNameWithoutExtension(foundfile));
            }
            cbLang.Text = Path.GetFileNameWithoutExtension(Settings.LanguageFile);
        }
        #endregion
        private void customToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HTAlt.WinForms.HTInputBox inputb = new HTAlt.WinForms.HTInputBox(customSearchNote, customSearchMessage, Settings.SearchEngine) { Icon = Icon, SetToDefault = SetToDefault, StartPosition = FormStartPosition.CenterParent, OK = OK,Cancel = Cancel,BackgroundColor = Settings.Theme.BackColor};
            DialogResult diagres = inputb.ShowDialog();
            if (diagres == DialogResult.OK)
            {
                if (ValidHttpURL(inputb.TextValue) && !inputb.TextValue.StartsWith("korot://") && !inputb.TextValue.StartsWith("file://") && !inputb.TextValue.StartsWith("about"))
                {
                    Settings.SearchEngine = inputb.TextValue;
                    tbSearchEngine.Text = Settings.SearchEngine;
                }
                else
                {
                    customToolStripMenuItem_Click(null, null);
                }
            }
        }
        private void SearchEngineSelection_Click(object sender, EventArgs e)
        {
            Settings.SearchEngine = ((ToolStripMenuItem)sender).Tag.ToString();
            tbSearchEngine.Text = Settings.SearchEngine;
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            if (tbHomepage.Text.ToLower().StartsWith("korot://newtab"))
            {
                radioButton1.Checked = true;
                Settings.Homepage = tbHomepage.Text;
                if (!_Incognito) { Properties.Settings.Default.Save(); }
            }
            else
            {
                radioButton1.Checked = false;
                Settings.Homepage = tbHomepage.Text;
                if (!_Incognito) { Properties.Settings.Default.Save(); }
            }
        }
        private void textBox3_Click(object sender, EventArgs e)
        {
            cmsSearchEngine.Show(MousePosition);
        }
        private void openmytaginnewtab(object sender, LinkLabelLinkClickedEventArgs e)
        {
            anaform.Invoke(new Action(() => anaform.CreateTab(((LinkLabel)sender).Tag.ToString())));
        }
        private void ClearToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Settings.Favorites.Favorites.Clear();
        }

        private void PictureBox3_Click(object sender, EventArgs e)
        {
            ColorDialog colorpicker = new ColorDialog
            {
                AnyColor = true,
                AllowFullOpen = true,
                FullOpen = true
            };
            if (colorpicker.ShowDialog() == DialogResult.OK)
            {
                pbBack.BackColor = colorpicker.Color;
                Settings.Theme.BackColor = colorpicker.Color;
                pbBack.BackColor = colorpicker.Color;
                ChangeTheme();
            }
            
        }

        private void PictureBox4_Click(object sender, EventArgs e)
        {
            ColorDialog colorpicker = new ColorDialog
            {
                AnyColor = true,
                AllowFullOpen = true,
                FullOpen = true
            };
            if (colorpicker.ShowDialog() == DialogResult.OK)
            {
                pbOverlay.BackColor = colorpicker.Color;
                Settings.Theme.OverlayColor = colorpicker.Color;
                pbOverlay.BackColor = colorpicker.Color;
                ChangeTheme();
            }
            
        }

        public void RefreshDownloadList()
        {
            int selectedValue = hlvDownload.SelectedIndices.Count > 0 ? hlvDownload.SelectedIndices[0] : 0;
            ListViewItem scroll = hlvDownload.TopItem;
            hlvDownload.Items.Clear();
            if (anaform != null)
            {
                foreach (DownloadItem item in anaform.CurrentDownloads)
                {
                    ListViewItem listV = new ListViewItem
                    {
                        Text = item.PercentComplete + "%"
                    };
                    listV.SubItems.Add(DateTime.Now.ToString("dd/MM/yy hh:mm:ss"));
                    listV.SubItems.Add(item.FullPath);
                    listV.SubItems.Add(item.Url);
                    hlvDownload.Items.Add(listV);
                }
            }
            foreach (Site x in Settings.Downloads.Downloads)
            {
                ListViewItem listV = new ListViewItem
                {
                    Text = x.Name
                };
                listV.SubItems.Add(x.Date);
                listV.SubItems.Add(x.LocalUrl);
                listV.SubItems.Add(x.Url);
                listV.Tag = x;
                hlvDownload.Items.Add(listV);
            }
            hlvDownload.SelectedIndices.Clear();
            if (selectedValue < (hlvDownload.Items.Count - 1))
            {
                hlvDownload.Items[selectedValue].Selected = true;
                hlvDownload.TopItem = scroll;
            }
            hlvDownload.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }
        private void ListView2_DoubleClick(object sender, EventArgs e)
        {
            cmsDownload.Show(MousePosition);
        }
        private void OpenLinkİnNewTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            anaform.Invoke(new Action(() => anaform.CreateTab(hlvDownload.SelectedItems[0].SubItems[2].Text)));
        }

        private void OpenFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(hlvDownload.SelectedItems[0].SubItems[3].Text);
        }

        private void OpenFileİnExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", "/select," + hlvDownload.SelectedItems[0].SubItems[3].Text);
        }

        private void RemoveSelectedToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (hlvDownload.SelectedItems.Count > 0)
            {
                if (hlvDownload.SelectedItems[0].Text == "X" || hlvDownload.SelectedItems[0].Text == "✓")
                {
                    Settings.Downloads.Downloads.Remove(hlvDownload.SelectedItems[0].Tag as Site);
                }
                else { anaform.CancelledDownloads.Add(hlvDownload.SelectedItems[0].SubItems[3].Text); }
                if (!_Incognito) { Properties.Settings.Default.Save(); }
                RefreshDownloadList();
            }
        }

        private void ClearToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            Settings.Downloads.Downloads.Clear();
            if (!_Incognito) { Properties.Settings.Default.Save(); }
            RefreshDownloadList();
        }

        private void HlvHistory_DoubleClick(object sender, EventArgs e)
        {
            if (hlvHistory.SelectedItems.Count > 0)
            {
                anaform.Invoke(new Action(() => anaform.CreateTab(hlvHistory.SelectedItems[0].SubItems[2].Text)));
            }
        }

        private void NewWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(Application.ExecutablePath);
        }

        private void NewIncognitoWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(Application.ExecutablePath, "-incognito");
        }

        private void ColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Theme.BackgroundStyle = "BACKCOLOR";
            textBox4.Text = usingBC;
            colorToolStripMenuItem.Checked = true;
            
        }
        public static bool ValidHttpURL(string s)
        {
            string Pattern = @"^(?:about\:\/\/)|(?:about\:\/\/)|(?:file\:\/\/)|(?:https\:\/\/)|(?:korot\:\/\/)|(?:http:\/\/)|(?:\:\/\/)?[\w.-]+(?:\.[\w\.-]+)+[\w\-\._~:\/?#[\]@!\$&'\(\)\*\+,;=.]+$";
            Regex Rgx = new Regex(Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Regex Rgx2 = new Regex(@"\b(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return Rgx2.IsMatch(s) || Rgx.IsMatch(s);
        }
        private void FromURLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HTAlt.WinForms.HTInputBox inputbox = new HTAlt.WinForms.HTInputBox("Korot",
                                                                                            enterAValidCode,
                                                                                            "")
            { Icon = Icon, SetToDefault = SetToDefault, StartPosition = FormStartPosition.CenterParent, OK = OK, Cancel = Cancel, BackgroundColor = Settings.Theme.BackColor };
            if (inputbox.ShowDialog() == DialogResult.OK)
            {
                Settings.Theme.BackgroundStyle = inputbox.TextValue + ";";
                textBox4.Text = Settings.Theme.BackgroundStyle;
                colorToolStripMenuItem.Checked = false;
            }
            
        }

        private void FromLocalFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog filedlg = new OpenFileDialog
            {
                Filter = imageFiles + "|*.jpg;*.png;*.bmp;*.jpeg;*.jfif;*.gif;*.apng;*.ico;*.svg;*.webp|" + allFiles + "|*.*",
                Title = selectBackImage,
                Multiselect = false
            };
            if (filedlg.ShowDialog() == DialogResult.OK)
            {
                if (HTAlt.Tools.ImageToBase64(Image.FromFile(filedlg.FileName)).Length <= 131072)
                {
                    string imageType = Path.GetExtension(filedlg.FileName).Replace(".", "");
                    Settings.Theme.BackgroundStyle = "background-image: url('data:image/" + imageType + ";base64," + HTAlt.Tools.ImageToBase64(Image.FromFile(filedlg.FileName)) + "');";
                    textBox4.Text = Settings.Theme.BackgroundStyle;
                    colorToolStripMenuItem.Checked = false;
                }
                else
                {
                    FromLocalFileToolStripMenuItem_Click(sender, e);
                }
            }
            
        }

        private void RadioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            {
                Settings.Homepage = "korot://newtab";
                tbHomepage.Text = Settings.Homepage;
            }
        }
        public int fromH = -1;
        public int fromM = -1;
        public int toH = -1;
        public int toM = -1;
        public bool Nsunday = false;
        public bool Nmonday = false;
        public bool Ntuesday = false;
        public bool Nwednesday = false;
        public bool Nthursday = false;
        public bool Nfriday = false;
        public bool Nsaturday = false;
        public void RefreshScheduledSiletMode()
        {
            if (Settings.AutoSilent)
            {
                string Playlist = Settings.AutoSilentMode;
                string[] SplittedFase = Playlist.Split(':');
                if (SplittedFase.Length - 1 > 9)
                {

                    fromHour.Value = Convert.ToInt32(SplittedFase[0]);
                    fromMin.Value = Convert.ToInt32(SplittedFase[1]);
                    toHour.Value = Convert.ToInt32(SplittedFase[2]);
                    toMin.Value = Convert.ToInt32(SplittedFase[3]);
                    bool sunday = SplittedFase[4] == "1";
                    bool monday = SplittedFase[5] == "1";
                    bool tuesday = SplittedFase[6] == "1";
                    bool wednesday = SplittedFase[7] == "1";
                    bool thursday = SplittedFase[8] == "1";
                    bool friday = SplittedFase[9] == "1";
                    bool saturday = SplittedFase[10] == "1";
                    fromH = Convert.ToInt32(SplittedFase[0]);
                    fromM = Convert.ToInt32(SplittedFase[1]);
                    toH = Convert.ToInt32(SplittedFase[2]);
                    toM = Convert.ToInt32(SplittedFase[3]);
                    Nsunday = sunday;
                    Nmonday = monday;
                    Ntuesday = tuesday;
                    Nwednesday = wednesday;
                    Nthursday = thursday;
                    Nfriday = friday;
                    Nsaturday = saturday;
                    lbSunday.BackColor = sunday ? Settings.Theme.OverlayColor : Settings.Theme.BackColor;
                    lbMonday.BackColor = monday ? Settings.Theme.OverlayColor : Settings.Theme.BackColor;
                    lbTuesday.BackColor = tuesday ? Settings.Theme.OverlayColor : Settings.Theme.BackColor;
                    lbWednesday.BackColor = wednesday ? Settings.Theme.OverlayColor : Settings.Theme.BackColor;
                    lbThursday.BackColor = thursday ? Settings.Theme.OverlayColor : Settings.Theme.BackColor;
                    lbFriday.BackColor = friday ? Settings.Theme.OverlayColor : Settings.Theme.BackColor;
                    lbSaturday.BackColor = saturday ? Settings.Theme.OverlayColor : Settings.Theme.BackColor;
                    lbSunday.Tag = sunday ? "1" : "0";
                    lbMonday.Tag = monday ? "1" : "0";
                    lbTuesday.Tag = tuesday ? "1" : "0";
                    lbWednesday.Tag = wednesday ? "1" : "0";
                    lbThursday.Tag = thursday ? "1" : "0";
                    lbFriday.Tag = friday ? "1" : "0";
                    lbSaturday.Tag = saturday ? "1" : "0";
                }
            }
        }
        public void writeSchedules()
        {
            string ScheduleBuild = fromHour.Value + ":";
            ScheduleBuild += fromMin.Value + ":";
            ScheduleBuild += toHour.Value + ":";
            ScheduleBuild += toMin.Value + ":";
            ScheduleBuild += (lbSunday.Tag != null ? lbSunday.Tag.ToString() : "0") + ":";
            ScheduleBuild += (lbMonday.Tag != null ? lbMonday.Tag.ToString() : "0") + ":";
            ScheduleBuild += (lbTuesday.Tag != null ? lbTuesday.Tag.ToString() : "0") + ":";
            ScheduleBuild += (lbWednesday.Tag != null ? lbWednesday.Tag.ToString() : "0") + ":";
            ScheduleBuild += (lbThursday.Tag != null ? lbThursday.Tag.ToString() : "0") + ":";
            ScheduleBuild += (lbFriday.Tag != null ? lbFriday.Tag.ToString() : "0") + ":";
            ScheduleBuild += (lbSaturday.Tag != null ? lbSaturday.Tag.ToString() : "0") + ":";
            Settings.AutoSilentMode = ScheduleBuild;
        }
        private void tmrRefresher_Tick(object sender, EventArgs e)
        {
            hsDoNotTrack.Checked = Settings.DoNotTrack;
            tbHomepage.Text = Settings.Homepage;
            radioButton1.Checked = Settings.Homepage == "korot://newtab";
            tbSearchEngine.Text = Settings.SearchEngine;
            hsProxy.Checked = Settings.RememberLastProxy;
            hsNotificationSound.Checked = !Settings.QuietMode;
            hsSilent.Checked = Settings.Silent;
            hsSchedule.Checked = Settings.AutoSilent;
            panel1.Enabled = Settings.AutoSilent;
            RefreshScheduledSiletMode();
            RefreshLangList();
            refreshThemeList();
            RefreshDownloadList();
            RefreshHistory();
            RefreshSites();
            comboBox1.Text = !onThemeName ? (Settings.Theme.LoadedDefaults ? "((default))" : Settings.Theme.Name) : comboBox1.Text;
        }
        
        private void label15_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                refreshThemeList();
            }
            else if (e.Button == MouseButtons.Right)
            {
                Process.Start("explorer.exe", "\"" + Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\Themes\\" + "\"");
            }
        }
        private void RefreshSites()
        {
            Console.WriteLine(" [TODO] RefreshSites() ");
        }

        private void btInstall_Click(object sender, EventArgs e)
        {
            Process.Start(Application.ExecutablePath, "-update");
            Application.Exit();
        }

        private void btUpdater_Click(object sender, EventArgs e)
        {
            if (UpdateWebC.IsBusy) { UpdateWebC.CancelAsync(); }
            UpdateWebC.DownloadStringAsync(new Uri("https://haltroy.com/Update/Korot.htupdate"));
            updateProgress = 0;
        }
        private void Timer2_Tick(object sender, EventArgs e)
        {
            RefreshHistory();
            RefreshDownloadList();
        }
        private void removeSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.History.Remove(hlvHistory.SelectedItems[0].Tag as Site);
            RefreshHistory();
        }


        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.History.Clear();
            if (!_Incognito) { Properties.Settings.Default.Save(); }
            RefreshHistory();
        }



        private void Label2_Click(object sender, EventArgs e)
        {
            NewTab("https://haltroy.com/Korot.html");
        }

        public void LoadTheme(string ThemeFile)
        {
            Settings.Theme.LoadFromFile(ThemeFile);
        }
        public void refreshThemeList()
        {
            int savedValue = listBox2.SelectedIndex;
            int scroll = listBox2.TopIndex;
            listBox2.Items.Clear();
            foreach (string x in Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\" + Properties.Settings.Default.LastUser +"\\Themes\\"))
            {
                if (x.EndsWith(".ktf", StringComparison.OrdinalIgnoreCase))
                {
                    listBox2.Items.Add(new FileInfo(x).Name);
                }
            }
            if (savedValue <= (listBox2.Items.Count - 1))
            {
                listBox2.SelectedIndex = savedValue;
                listBox2.TopIndex = scroll;
            }
        }


        private void Button12_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\" + Properties.Settings.Default.LastUser + "\\Themes\\")) { Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\" + Properties.Settings.Default.LastUser + "\\Themes\\"); }
            string themeFile = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\" + Properties.Settings.Default.LastUser + "\\Themes\\" + comboBox1.Text + ".ktf";
            Theme saveTheme = new Theme("");
            saveTheme.ThemeFile = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\" + Properties.Settings.Default.LastUser + "\\Themes\\" + comboBox1.Text + ".ktf";
            saveTheme.BackColor = Settings.Theme.BackColor;
            saveTheme.OverlayColor = Settings.Theme.OverlayColor;
            saveTheme.MininmumKorotVersion = new Version(Application.ProductVersion);
            saveTheme.Version = new Version(Application.ProductVersion);
            saveTheme.Name = comboBox1.Text;
            saveTheme.Author = userName;
            saveTheme.BackgroundStyle = Settings.Theme.BackgroundStyle;
            saveTheme.BackgroundStyleLayout = Settings.Theme.BackgroundStyleLayout;
            saveTheme.CloseButtonColor = Settings.Theme.CloseButtonColor;
            saveTheme.NewTabColor = Settings.Theme.NewTabColor;
            saveTheme.SaveTheme();
            Settings.Theme.ThemeFile = themeFile;
            refreshThemeList();
        }

        private readonly WebClient UpdateWebC = new WebClient();
        public void Updater() 
        {
            UpdateWebC.DownloadStringCompleted += Updater_DownloadStringCompleted;
            UpdateWebC.DownloadProgressChanged += updater_checking;
            UpdateWebC.DownloadStringAsync(new Uri("https://haltroy.com/Update/Korot.htupdate"));
            updateProgress = 0;
        }

        private bool alreadyCheckedForUpdatesOnce = false;
        private void updater_checking(object sender, DownloadProgressChangedEventArgs e)
        {
            lbUpdateStatus.Text = checking;
            updateProgress = 0;
            btUpdater.Visible = false;
        }
        private void Updater_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled)
            {
                updateProgress = 3;
                btInstall.Visible = false;
                btUpdater.Visible = true;
                lbUpdateStatus.Text = updateError;
            }
            else
            {
                UpdateResult(e.Result);
            }
        }

        private void UpdateResult(string info)
        {
            char[] token = new char[] { Environment.NewLine.ToCharArray()[0] };
            string[] SplittedFase3 = info.Split(token);
            string preNo = SplittedFase3[5].Substring(1).Replace(Environment.NewLine, "");
            string preNewest = SplittedFase3[4].Substring(1).Replace(Environment.NewLine, "") + "-pre" + preNo;
            Version newest = new Version(SplittedFase3[0].Replace(Environment.NewLine, ""));
            Version current = new Version(Application.ProductVersion);
            if (VersionInfo.IsPreRelease)
            {
                if (preNo == "0") 
                {
                    if (alreadyCheckedForUpdatesOnce || Settings.DismissUpdate || _Incognito || NotificationListenerMode)
                    {
                        updateProgress = 2;
                        lbUpdateStatus.Text = updateavailable;
                        btUpdater.Visible = true;
                        btInstall.Visible = true;
                    }
                    else
                    {
                        alreadyCheckedForUpdatesOnce = true;
                        updateProgress = 2;
                        lbUpdateStatus.Text = updateavailable;
                        btInstall.Visible = true;
                        btUpdater.Visible = true;
                        HTAlt.WinForms.HTMsgBox mesaj = new HTAlt.WinForms.HTMsgBox(
                            updateTitle,
                            updateMessage,
                            new HTAlt.WinForms.HTDialogBoxContext() { Yes = true, No = true })
                        { StartPosition = FormStartPosition.CenterParent,Yes = Yes, No = No, OK = OK, Cancel = Cancel, BackgroundColor = Settings.Theme.BackColor, Icon = Icon };
                        DialogResult diagres = mesaj.ShowDialog();
                        if (diagres == DialogResult.Yes)
                        {
                            if (Application.OpenForms.OfType<Form1>().Count() < 1)
                            {
                                Process.Start(Application.ExecutablePath, "-update");
                            }
                            else
                            {
                                foreach (Form1 x in Application.OpenForms)
                                {
                                    x.Focus();
                                }
                            }
                        }
                        Settings.DismissUpdate = true;
                    }
                }
                else
                {
                    if (VersionInfo.PreReleaseNumber < Convert.ToInt32(preNo))
                    {
                        if (alreadyCheckedForUpdatesOnce || Settings.DismissUpdate || _Incognito || NotificationListenerMode)
                        {
                            updateProgress = 2;
                            lbUpdateStatus.Text = updateavailable;
                            btUpdater.Visible = true;
                            btInstall.Visible = true;
                        }
                        else
                        {
                            alreadyCheckedForUpdatesOnce = true;
                            updateProgress = 2;
                            lbUpdateStatus.Text = updateavailable;
                            btInstall.Visible = true;
                            btUpdater.Visible = true;
                            HTAlt.WinForms.HTMsgBox mesaj = new HTAlt.WinForms.HTMsgBox(
                                updateTitle,
                                updateMessage,
                                new HTAlt.WinForms.HTDialogBoxContext() { Yes = true, No = true })
                            { StartPosition = FormStartPosition.CenterParent,Yes = Yes, No = No, OK = OK, Cancel = Cancel, BackgroundColor = Settings.Theme.BackColor, Icon = Icon };
                            DialogResult diagres = mesaj.ShowDialog();
                            if (diagres == DialogResult.Yes)
                            {
                                if (Application.OpenForms.OfType<Form1>().Count() < 1)
                                {
                                    Process.Start(Application.ExecutablePath, "-update");
                                }
                                else
                                {
                                    foreach (Form1 x in Application.OpenForms)
                                    {
                                        x.Focus();
                                    }
                                }
                            }
                            Settings.DismissUpdate = true;
                        }
                    }
                }
            }
            else
            {
                if (newest > current)
                {
                    if (alreadyCheckedForUpdatesOnce || Settings.DismissUpdate || _Incognito)
                    {
                        updateProgress = 2;
                        lbUpdateStatus.Text = updateavailable;
                        btUpdater.Visible = true;
                        btInstall.Visible = true;
                    }
                    else
                    {
                        alreadyCheckedForUpdatesOnce = true;
                        updateProgress = 2;
                        lbUpdateStatus.Text = updateavailable;
                        btInstall.Visible = true;
                        btUpdater.Visible = true;
                        HTAlt.WinForms.HTMsgBox mesaj = new HTAlt.WinForms.HTMsgBox(
                            updateTitle,
                            updateMessage,
                            new HTAlt.WinForms.HTDialogBoxContext() { Yes = true, No = true })
                        { StartPosition = FormStartPosition.CenterParent,Yes = Yes, No = No, OK = OK, Cancel = Cancel, BackgroundColor = Settings.Theme.BackColor, Icon = Icon };
                        DialogResult diagres = mesaj.ShowDialog();
                        if (diagres == DialogResult.Yes)
                        {
                            if (Application.OpenForms.OfType<Form1>().Count() < 1)
                            {
                                Process.Start(Application.ExecutablePath, "-update");
                            }
                            else
                            {
                                foreach (Form1 x in Application.OpenForms)
                                {
                                    x.Focus();
                                }
                            }
                        }
                        Settings.DismissUpdate = true;
                    }
                }
                else
                {
                    btUpdater.Visible = true;
                    btInstall.Visible = false;
                    updateProgress = 1;
                    lbUpdateStatus.Text = uptodate;
                }
            }
        }
        public HTTitleTabs ParentTabs => (ParentForm as HTTitleTabs);
        public HTTitleTab ParentTab
        {
            get
            {
                List<int> tabIndexes = new List<int>();
                foreach (HTTitleTab x in ParentTabs.Tabs)
                {
                    if (x.Content == this) { tabIndexes.Add(ParentTabs.Tabs.IndexOf(x)); }
                }
                return (tabIndexes.Count > 0 ? ParentTabs.Tabs[tabIndexes[0]] : null);
            }
        }
        private async void SetProxy(ChromiumWebBrowser cwb, string Address)
        {
            if (Address == null) { }
            else
            {
                await Cef.UIThreadTaskFactory.StartNew(delegate
                {
                    IRequestContext rc = cwb.GetBrowser().GetHost().RequestContext;
                    Dictionary<string, object> v = new Dictionary<string, object>
                    {
                        ["mode"] = "fixed_servers",
                        ["server"] = Address
                    };
                    bool success = rc.SetPreference("proxy", v, out string error);
                });
            }
        }

        private void cef_GotFocus(object sender, EventArgs e)
        {
            Invoke(new Action(() =>
            {
                cmsPrivacy.Hide();
                cmsPrivacy.Close();
                cmsProfiles.Hide();
                cmsProfiles.Close();
                if (!doNotDestroyFind)
                {
                    cmsHamburger.Hide();
                    cmsHamburger.Close();
                }
            }));
        }
        private void cef_LostFocus(object sender, EventArgs e)
        {
            if (cmsCEF != null)
            {
                Invoke(new Action(() =>
                {
                    cmsCEF.Hide();
                    cmsCEF.Close();
                    cmsCEF = null;
                }));
            }
        }
        private void cef_consoleMessage(object sender, ConsoleMessageEventArgs e)
        {
            Output.WriteLine(" [Korot.ConsoleMessage] Message received: [Line: " + e.Line + " Level: " + e.Level + " Source: " + e.Source + " Message:" + e.Message + "]");
        }
        public void updateThemes()
        {
            foreach (string x in Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\" + Properties.Settings.Default.LastUser + "\\Themes\\", "*.*", SearchOption.AllDirectories))
            {
                if (x.EndsWith(".ktf", StringComparison.CurrentCultureIgnoreCase))
                {
                    string Playlist = HTAlt.Tools.ReadFile(x, Encoding.UTF8);
                    char[] token = new char[] { Environment.NewLine.ToCharArray()[0] };
                    string[] SplittedFase = Playlist.Split(token);
                    if (SplittedFase[9].Substring(1).Replace(Environment.NewLine, "") == "1")
                    {
                        frmUpdateExt extUpdate = new frmUpdateExt(x, true,Settings)
                        {
                            Text = updateTitleTheme
                        };
                        extUpdate.label1.Text = updateExtInfo
.Replace("[NAME]", SplittedFase[0].Substring(0).Replace(Environment.NewLine, ""))
.Replace("[NEWLINE]", Environment.NewLine);
                        extUpdate.infoTemp = StatusType;
                        extUpdate.Show();
                    }
                }
            }
        }

        public bool certError = false;
        public bool cookieUsage = false;
        public void ChangeStatus(string status)
        {
            label2.Text = status;
        }
        public void CreateNewCollection()
        {
            HTAlt.WinForms.HTInputBox mesaj = new HTAlt.WinForms.HTInputBox("Korot",newColInfo,newColName)
            { Icon = Icon, OK = OK, SetToDefault = SetToDefault, Cancel = Cancel, BackgroundColor = Settings.Theme.BackColor };
            DialogResult diagres = mesaj.ShowDialog();
            if (diagres == DialogResult.OK)
            {
                if (!string.IsNullOrWhiteSpace(mesaj.TextValue))
                {
                    Collection newCol = new Collection
                    {
                        ID = HTAlt.Tools.GenerateRandomText(12),
                        Text = mesaj.TextValue
                    };
                    Settings.CollectionManager.Collections.Add(newCol);
                }
                else { CreateNewCollection(); }
            }
        }
        public void loadingstatechanged(object sender, LoadingStateChangedEventArgs e)
        {
            if (!IsDisposed)
            {
                if (e.IsLoading)
                {
                    certError = false;
                    cookieUsage = false;
                    Invoke(new Action(() => pbPrivacy.Image = Properties.Resources.lockg));
                    Invoke(new Action(() => showCertificateErrorsToolStripMenuItem.Tag = null));
                    Invoke(new Action(() => showCertificateErrorsToolStripMenuItem.Visible = false));
                    Invoke(new Action(() => safeStatusToolStripMenuItem.Text = CertificateOKTitle));
                    Invoke(new Action(() => ınfoToolStripMenuItem.Text = CertificateOK));
                    Invoke(new Action(() => cookieInfoToolStripMenuItem.Text = notUsesCookies));
                    if (HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130)
                    {
                        btRefresh.Image = Korot.Properties.Resources.cancel;
                    }
                    else { btRefresh.Image = Korot.Properties.Resources.cancel_w; }
                }
                else
                {
                    if (_Incognito) { }
                    else
                    {
                        Invoke(new Action(() => Settings.History.Add(new Korot.Site() { Date = DateTime.Now.ToString("dd/MM/yy hh:mm:ss"), Name = Text, Url = tbAddress.Text })));

                    }
                    if (HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130)
                    {
                        btRefresh.Image = Korot.Properties.Resources.refresh;
                    }
                    else
                    { btRefresh.Image = Korot.Properties.Resources.refresh_w; }
                }
                if (onCEFTab)
                {
                    if (!e.Browser.IsDisposed)
                    {
                        btBack.Invoke(new Action(() => btBack.Enabled = canGoBack()));
                        btNext.Invoke(new Action(() => btNext.Enabled = canGoForward()));
                    }
                    else
                    {
                        btBack.Invoke(new Action(() => btBack.Enabled = false));
                        btNext.Invoke(new Action(() => btNext.Enabled = false));
                    }
                }
                isLoading = e.IsLoading;
                if (!Settings.GetSiteFromUrl(HTAlt.Tools.GetBaseURL(chromiumWebBrowser1.Address)).AllowCookies)
                {
                    disallowThisPageForCookieAccessToolStripMenuItem.Text = allowCookie;
                }
                else
                {
                    disallowThisPageForCookieAccessToolStripMenuItem.Text = disallowCookie;
                }
            }
        }

        public bool OnJSAlert(string url, string message)
        {
            if (!NotificationListenerMode)
            {
                HTAlt.WinForms.HTMsgBox mesaj = new HTAlt.WinForms.HTMsgBox(JSAlert.Replace("[TITLE]", Text).Replace("[URL]", url), message, new HTAlt.WinForms.HTDialogBoxContext() { OK = true, Cancel = true })
                { StartPosition = FormStartPosition.CenterParent, Yes = Yes, No = No, OK = OK, Cancel = Cancel, BackgroundColor = Settings.Theme.BackColor, Icon = Icon };
                mesaj.ShowDialog();
                return true;
            }
            else { return false; }
        }


        public bool OnJSConfirm(string url, string message, out bool returnval)
        {
            if (!NotificationListenerMode)
            {
                HTAlt.WinForms.HTMsgBox mesaj = new HTAlt.WinForms.HTMsgBox(JSConfirm.Replace("[TITLE]", Text).Replace("[URL]", url), message, new HTAlt.WinForms.HTDialogBoxContext() { OK = true, Cancel = true })
            { StartPosition = FormStartPosition.CenterParent, Yes = Yes, No = No, OK = OK, Cancel = Cancel, BackgroundColor = Settings.Theme.BackColor, Icon = Icon };
            if (mesaj.ShowDialog() == DialogResult.OK) { returnval = true; } else { returnval = false; }
            return true;
        }
            else { returnval = false; return false; }
        }


        public bool OnJSPrompt(string url, string message, string defaultValue, out bool returnval, out string textresult)
        {
            if (!NotificationListenerMode)
            {
                HTAlt.WinForms.HTInputBox mesaj = new HTAlt.WinForms.HTInputBox(url, message, defaultValue)
                { SetToDefault = SetToDefault, StartPosition = FormStartPosition.CenterParent, Icon = Icon, OK = OK, Cancel = Cancel, BackgroundColor = Settings.Theme.BackColor };
                if (mesaj.ShowDialog() == DialogResult.OK) { returnval = true; } else { returnval = false; }
                textresult = mesaj.TextValue;
                return true;
            }else { textresult = null; returnval = false; return false; }
        }
        public void NewTab(string url)
        {
            anaform.Invoke(new Action(() => { anaform.CreateTab(ParentTab, url); }));
        }

        private bool isFavMenuHidden = false;

        private void showFavMenu()
        {
            pNavigate.Height += mFavorites.Height;
            tabControl1.Height -= mFavorites.Height;
            tabControl1.Top += mFavorites.Height;
            mFavorites.Visible = true;
            isFavMenuHidden = false;
        }

        private void hideFavMenu()
        {
            pNavigate.Height -= mFavorites.Height;
            tabControl1.Height += mFavorites.Height;
            tabControl1.Top -= mFavorites.Height;
            mFavorites.Visible = false;
            isFavMenuHidden = true;
        }

        private ToolStripMenuItem selectedFavorite = null;
        public void RefreshFavorites()
        {
            mFavorites.Items.Clear();
            LoadDynamicMenu();
            if (mFavorites.Items.Count < 1)
            {
                if (!isFavMenuHidden)
                {
                    hideFavMenu();
                }
            }
            else
            {
                if (Settings.Favorites.ShowFavorites)
                {
                    if (isFavMenuHidden)
                    {
                        showFavMenu();
                    }
                }
                else
                {
                    if (!isFavMenuHidden)
                    {
                        hideFavMenu();
                    }
                }
            }
        }

        private void EasterEggs()
        {
            Random random = new Random();
            int randomNumber = random.Next(0, 100);
            if (randomNumber == 6)
            {
                lbKorot.Text = "Another Chromium-based web browser";
            }
            else if (randomNumber == 17)
            {
                lbKorot.Text = "StoneBrowser";
            }
            else if (randomNumber == 45)
            {
                lbKorot.Text = "null";
            }
            else if (randomNumber == 71)
            {
                lbKorot.Text = "web browser made by retarded";
            }
            else if (randomNumber == 3)
            {
                lbKorot.Text = "web browser designed to lag and eat ram";
            }
            else if (randomNumber == 9)
            {
                lbKorot.Text = "korot";
            }
            else if (randomNumber == 35)
            {
                lbKorot.Text = "StoneHomepage";
            }
            else if (randomNumber == 48)
            {
                lbKorot.Text = "ZStone";
            }
            else if (randomNumber == 7)
            {
                Random random2 = new Random();
                int randomNumber2 = random2.Next(1, 2);
                lbKorot.Text = (randomNumber2 == 1 ? "Pell" : "Kolme") + " Browser";
            }
            else if (randomNumber == 33)
            {
                Random random2 = new Random();
                int randomNumber2 = random2.Next(1, 2);
                lbKorot.Text = (randomNumber2 == 1 ? "Webtroy" : "Ninova");
            }
            else
            {
                lbKorot.Text = "Korot";
            }
        }
        private void miFavorite_MouseDown(object sender, MouseEventArgs e)
        {
            itemClicked = true;
            if (e.Button == MouseButtons.Left) //Open it
            {
                chromiumWebBrowser1.Load(((ToolStripMenuItem)sender).Tag.ToString());
            }
            else if (e.Button == MouseButtons.Right) //Show menu
            {
                selectedFavorite = (ToolStripMenuItem)sender;
                cmsFavorite.Show(MousePosition);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            allowSwitching = true;
            tabControl1.SelectedTab = tpCef;
            string urlLower = tbAddress.Text.ToLower();
            if (ValidHttpURL(urlLower))
            {
                loadPage(urlLower, Text);
            }

            else
            {
                loadPage(Settings.SearchEngine + urlLower, Text);

            }
        }
        private void button5_Click(object sender, EventArgs e)
        {
            allowSwitching = true;
            tabControl1.SelectedTab = tpCef;
            loadPage(Settings.Homepage);
        }

        private void loadPage(string url, string title = "")
        {
            redirectTo(url, title);
            lbURL_SelectedIndexChanged(null, null);
        }
        public void GoBack()
        {
            lbURL.SelectedIndex = lbURL.SelectedIndex == 0 ? 0 : lbURL.SelectedIndex - 1;
            lbTitle.SelectedIndex = lbURL.SelectedIndex;
        }


        public void button1_Click(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab == tpCef) //CEF
            {
                GoBack();
                //chromiumWebBrowser1.Back();
            }
            else if (tabControl1.SelectedTab == tpCert) //Certificate Error Menu
            {
                GoBack();
                resetPage();
            }
            else if (tabControl1.SelectedTab == tpSettings
                     || tabControl1.SelectedTab == tpHistory
                     || tabControl1.SelectedTab == tpDownload
                     || tabControl1.SelectedTab == tpAbout
                     || tabControl1.SelectedTab == tpTheme
                     || tabControl1.SelectedTab == tpCollection
                     || tabControl1.SelectedTab == tpSite
                     || tabControl1.SelectedTab == tpNotification) //Menu
            {
                resetPage();
            }
        }
        public void resetPage(bool doNotGoToCEFTab = false)
        {
            if (anaform.settingTab == ParentTab)
            {
                anaform.settingTab = null;
            }
            if (anaform.themeTab == ParentTab)
            {
                anaform.themeTab = null;
            }
            if (anaform.historyTab == ParentTab)
            {
                anaform.historyTab = null;
            }
            if (anaform.downloadTab == ParentTab)
            {
                anaform.downloadTab = null;
            }
            if (anaform.siteTab == ParentTab)
            {
                anaform.siteTab = null;
            }
            if (anaform.aboutTab == ParentTab)
            {
                anaform.aboutTab = null;
            }
            if (anaform.collectionTab == ParentTab)
            {
                anaform.collectionTab = null;
            }
            if (anaform.notificationTab == ParentTab)
            {
                anaform.notificationTab = null;
            }
            if (!doNotGoToCEFTab)
            {
                allowSwitching = true;
                tabControl1.SelectedTab = tpCef;
            }
        }
        public void button3_Click(object sender, EventArgs e)
        {
            resetPage();
            allowSwitching = true;
            tabControl1.SelectedTab = tpCef;
            lbURL.SelectedIndex = lbURL.SelectedIndex == lbURL.Items.Count - 1 ? lbURL.SelectedIndex : lbURL.SelectedIndex + 1;
            lbTitle.SelectedIndex = lbURL.SelectedIndex;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            resetPage();
            if (isLoading)
            {
                chromiumWebBrowser1.Stop();
            }
            else { chromiumWebBrowser1.Reload(); }
        }
        private void cef_AddressChanged(object sender, AddressChangedEventArgs e)
        {
            Invoke(new Action(() => tbAddress.Text = e.Address));
            Invoke(new Action(() =>
            {
                if (isPageFavorited(chromiumWebBrowser1.Address))
                {
                    btFav.ButtonImage = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.star_on_w : Properties.Resources.star_on;
                }
                else
                {
                    btFav.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.star : Properties.Resources.star_w;
                }
            }));
            if (!ValidHttpURL(e.Address))
            {
                chromiumWebBrowser1.Load(Settings.SearchEngine + e.Address);
            }
            if (lbURL.Items.Count != 0)
            {
                if (e.Address != lbURL.Items[lbURL.Items.Count - 1].ToString())
                {
                    Invoke(new Action(() => redirectTo(e.Address, Text)));
                }
            }
        }
        private void cef_onLoadError(object sender, LoadErrorEventArgs e)
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                if (e == null) //User Asked
                {
                    chromiumWebBrowser1.Load("korot://error/?e=TEST");
                }
                else
                {
                    if (e.Frame.IsMain)
                    {
                        chromiumWebBrowser1.Load("korot://error/?e=" + e.ErrorText);
                    }
                    else
                    {
                        e.Frame.LoadUrl("korot://error/?e=" + e.ErrorText);
                    }
                }
            }
        }


        private void cef_TitleChanged(object sender, TitleChangedEventArgs e)
        {
            Invoke(new Action(() =>
            {
                tpCef.Text = e.Title;
                int si = lbTitle.SelectedIndex;
                if (si != -1)
                {
                    if (lbURL.Items[si].ToString() != chromiumWebBrowser1.Address)
                    {
                        if (chromiumWebBrowser1.Address.ToLower().StartsWith("korot"))
                        {
                            if (chromiumWebBrowser1.Address.ToLower().StartsWith("korot://newtab") ||
chromiumWebBrowser1.Address.ToLower().StartsWith("korot://links") ||
chromiumWebBrowser1.Address.ToLower().StartsWith("korot://license") ||
chromiumWebBrowser1.Address.ToLower().StartsWith("korot://incognito"))
                            {
                                lbTitle.Items.RemoveAt(si);
                                lbTitle.Items.Insert(si, e.Title);
                                lbTitle.SelectedIndex = si;
                            }
                        }
                        else
                        {
                            lbTitle.Items.RemoveAt(si);
                            lbTitle.Items.Insert(si, e.Title);
                            lbTitle.SelectedIndex = si;
                        }
                    }
                }
            }));
        }

        public void showHideSearchMenu()
        {
            if (cmsHamburger.Visible)
            {
                cmsHamburger.Close();
                toolStripTextBox1.Text = SearchOnPage;
                chromiumWebBrowser1.StopFinding(true);
            }
            else
            {
                cmsHamburger.Show(btHamburger, 0, 0);
                toolStripTextBox1.Text = SearchOnPage;
                chromiumWebBrowser1.StopFinding(true);
            }
        }
        public void retrieveKey(int code)
        {
            if (code == 0) //BrowserBack
            {
                button1_Click(null, null);
            }
            else if (code == 1) //BrowserForward
            {
                button3_Click(null, null);
            }
            else if (code == 2) //BrowserRefresh
            {
                button2_Click(null, null);
            }
            else if (code == 3) //BrowserStop
            {
                button2_Click(null, null);
            }
            else if (code == 4) //BrowserHome
            {
                button5_Click(null, null);
            }
            else if (code == 5) //Fullscreen
            {
                tsFullscreen_Click(null, null);
            }
            else if (code == 6) //Mute
            {
                MuteTS_Click(null, null);
            }
        }

        public bool canGoForward()
        {
            return tabControl1.SelectedTab == tpCef ? (lbURL.SelectedIndex != lbURL.Items.Count - 1) : true;
        }
        public bool canGoBack()
        {
            return tabControl1.SelectedTab == tpCef ? (lbURL.SelectedIndex != 0) : true;
        }

        public bool isControlKeyPressed = false;
        public void tabform_KeyDown(object sender, KeyEventArgs e)
        {

            isControlKeyPressed = e.Control;
            if (e.KeyData == Keys.BrowserBack)
            {
                button1_Click(null, null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == Keys.BrowserForward)
            {
                button3_Click(null, null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == Keys.BrowserRefresh)
            {
                button2_Click(null, null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == Keys.BrowserStop)
            {
                button2_Click(null, null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == Keys.BrowserHome)
            {
                button5_Click(null, null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.N && isControlKeyPressed)
            {
                NewTab("korot://newtab");
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.N && isControlKeyPressed && e.Shift)
            {
                Process.Start(Application.ExecutablePath, "-incognito");
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.N && isControlKeyPressed && e.Alt)
            {
                Process.Start(Application.ExecutablePath);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F && isControlKeyPressed)
            {
                showHideSearchMenu();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == Keys.Enter)
            {
                button4_Click(null, null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if ((e.KeyData == Keys.Up || e.KeyData == Keys.PageUp) && isControlKeyPressed)
            {
                zoomInToolStripMenuItem_Click(sender, null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if ((e.KeyData == Keys.Down || e.KeyData == Keys.PageDown) && isControlKeyPressed)
            {
                zoomOutToolStripMenuItem_Click(sender, null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == Keys.PrintScreen && isControlKeyPressed)
            {
                takeScreenShot();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == Keys.S && isControlKeyPressed)
            {
                savePage();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (isControlKeyPressed && e.Shift && e.KeyData == Keys.N)
            {
                NewIncognitoWindowToolStripMenuItem_Click(null, null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (isControlKeyPressed && e.Alt && e.KeyData == Keys.N)
            {
                NewWindowToolStripMenuItem_Click(null, null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (isControlKeyPressed && e.KeyData == Keys.N)
            {
                NewTab("korot://newtab");
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == Keys.F11)
            {
                tsFullscreen_Click(null, null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == Keys.M && isControlKeyPressed)
            {
                MuteTS_Click(sender, e);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
        public void tsFullscreen_Click(object sender, EventArgs e)
        {
            Invoke(new Action(() => Fullscreenmode(!anaform.isFullScreen)));
        }
        private Image GetImageFromURL(string URL)
        {
            if (URL == "BACKCOLOR") { return null; }
            else
            {
                int virgulIndex = URL.IndexOf(',') + 1;
                string justB64Code = URL.Substring(virgulIndex);
                Output.WriteLine(string.Concat(justB64Code.Reverse().Skip(3).Reverse()));
                return HTAlt.Tools.Base64ToImage(string.Concat(justB64Code.Reverse().Skip(3).Reverse()));
            }
        }

        private void ChangeThemeForMenuItems(ToolStripMenuItem item)
        {
            item.BackColor = Settings.Theme.BackColor;
            item.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
            item.DropDown.BackColor = Settings.Theme.BackColor;
            item.DropDown.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
            foreach (ToolStripMenuItem x in item.DropDownItems)
            {
                ChangeThemeForMenuDDItems(x.DropDown);
            }
        }

        private void ChangeThemeForMenuDDItems(ToolStripDropDown itemDD)
        {
            itemDD.BackColor = Settings.Theme.BackColor;
            itemDD.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
            foreach (ToolStripMenuItem x in itemDD.Items)
            {
                x.BackColor = Settings.Theme.BackColor;
                x.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                ChangeThemeForMenuDDItems(x.DropDown);
            }
        }

        private void UpdateFavoriteColor()
        {
            foreach (ToolStripMenuItem y in mFavorites.Items)
            {
                ChangeThemeForMenuItems(y);
            }
        }
        private Color oldBackColor;
        private Color oldOverlayColor;
        private string oldStyle;

        private void ChangeTheme()
        {
            // It won't throw any exceptions when there's try.
            // This absoluletly does not makes sense.
            // It throws ArgumentException in System.Drawing and shutdown application
            // if there's no try {}
            // What the fuck?
            try
            {
                if (anaform != null)
                {
                    if (anaform.tabRenderer != null)
                    {
                        anaform.tabRenderer.ApplyColors(Settings.Theme.BackColor, HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White, Settings.Theme.OverlayColor, Settings.Theme.BackColor);
                        anaform.Update();
                    }
                }
                if (Settings.Theme.OverlayColor != oldOverlayColor)
                {
                    oldOverlayColor = Settings.Theme.OverlayColor;
                    pbOverlay.BackColor = Settings.Theme.OverlayColor;
                    pbProgress.BackColor = Settings.Theme.OverlayColor;
                    hsDownload.OverlayColor = Settings.Theme.OverlayColor;
                    hsDoNotTrack.OverlayColor = Settings.Theme.OverlayColor;
                    hsFav.OverlayColor = Settings.Theme.OverlayColor;
                    hsOpen.OverlayColor = Settings.Theme.OverlayColor;
                    hlvDownload.OverlayColor = Settings.Theme.OverlayColor;
                    hlvHistory.OverlayColor = Settings.Theme.OverlayColor;
                    if (Cef.IsInitialized)
                    {
                        if (chromiumWebBrowser1.IsBrowserInitialized)
                        {
                            if (chromiumWebBrowser1.Address.StartsWith("korot:")) { chromiumWebBrowser1.Reload(); }
                        }
                    }
                }

                if (Settings.Theme.BackColor != oldBackColor)
                {
                    UpdateFavoriteColor();
                    updateFavoritesImages();
                    label2.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    label2.BackColor = Settings.Theme.BackColor;
                    if (Cef.IsInitialized)
                    {
                        if (chromiumWebBrowser1.IsBrowserInitialized)
                        {
                            if (chromiumWebBrowser1.Address.StartsWith("korot:")) { chromiumWebBrowser1.Reload(); }
                        }
                    }
                    pbBack.BackColor = Settings.Theme.BackColor;
                    cmsFavorite.BackColor = Settings.Theme.BackColor;
                    cmsIncognito.BackColor = Settings.Theme.BackColor;
                    oldBackColor = Settings.Theme.BackColor;
                    cmsIncognito.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    cmsFavorite.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    tsCollections.Image = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.collection_w : Properties.Resources.collection;
                    button12.ButtonImage = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.collection_w : Properties.Resources.collection;
                    pbStore.Image = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.store_w : Properties.Resources.store;
                    tsWebStore.Image = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.store_w : Properties.Resources.store;
                    tsThemes.Image = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.theme_w : Properties.Resources.theme;
                    btClose6.ButtonImage = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.cancel_w : Properties.Resources.cancel;
                    btClose2.ButtonImage = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.cancel_w : Properties.Resources.cancel;
                    btClose7.ButtonImage = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.cancel_w : Properties.Resources.cancel;
                    btClose.ButtonImage = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.cancel_w : Properties.Resources.cancel;
                    btClose8.ButtonImage = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.cancel_w : Properties.Resources.cancel;
                    btClose9.ButtonImage = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.cancel_w : Properties.Resources.cancel;
                    btClose3.ButtonImage = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.cancel_w : Properties.Resources.cancel;
                    btClose10.ButtonImage = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.cancel_w : Properties.Resources.cancel;
                    lbSettings.BackColor = Color.Transparent;
                    lbSettings.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    hlvDownload.BackColor = Settings.Theme.BackColor;
                    hlvDownload.HeaderBackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    hlvDownload.HeaderForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    hlvHistory.BackColor = Settings.Theme.BackColor;
                    pbPrivacy.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    tbAddress.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    pbIncognito.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    fromHour.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    fromHour.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    fromMin.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    fromMin.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    toHour.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    toHour.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    toMin.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    toMin.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    hlvHistory.HeaderBackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    hlvHistory.HeaderForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    cmsDownload.BackColor = Settings.Theme.BackColor;
                    cmsHistory.BackColor = Settings.Theme.BackColor;
                    cmsSearchEngine.BackColor = Settings.Theme.BackColor;
                    profilenameToolStripMenuItem.DropDown.BackColor = Settings.Theme.BackColor;
                    profilenameToolStripMenuItem.DropDown.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    cmsDownload.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    listBox2.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    comboBox1.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    tbHomepage.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    tbSearchEngine.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    button10.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    hlvDownload.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    cbLang.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    hsNotificationSound.BackColor = Settings.Theme.BackColor;
                    hsNotificationSound.ButtonColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false), false);
                    hsNotificationSound.ButtonHoverColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 40, false), false);
                    hsNotificationSound.ButtonPressedColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 60, false), false);
                    hsSilent.BackColor = Settings.Theme.BackColor;
                    hsSilent.ButtonColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false), false);
                    hsSilent.ButtonHoverColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 40, false), false);
                    hsSilent.ButtonPressedColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 60, false), false);
                    hsSchedule.BackColor = Settings.Theme.BackColor;
                    hsSchedule.ButtonColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false), false);
                    hsSchedule.ButtonHoverColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 40, false), false);
                    hsSchedule.ButtonPressedColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 60, false), false);
                    hsAutoRestore.BackColor = Settings.Theme.BackColor;
                    hsAutoRestore.ButtonColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false), false);
                    hsAutoRestore.ButtonHoverColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 40, false), false);
                    hsAutoRestore.ButtonPressedColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 60, false), false);
                    hsDownload.BackColor = Settings.Theme.BackColor;
                    hsDownload.ButtonColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false), false);
                    hsDownload.ButtonHoverColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 40, false), false);
                    hsDownload.ButtonPressedColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 60, false), false);
                    hsDoNotTrack.BackColor = Settings.Theme.BackColor;
                    hsDoNotTrack.ButtonColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false), false);
                    hsDoNotTrack.ButtonHoverColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 40, false), false);
                    hsDoNotTrack.ButtonPressedColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 60, false), false);
                    hsProxy.BackColor = Settings.Theme.BackColor;
                    hsProxy.ButtonColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false), false);
                    hsProxy.ButtonHoverColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 40, false), false);
                    hsProxy.ButtonPressedColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 60, false), false);
                    hsFav.BackColor = Settings.Theme.BackColor;
                    hsFav.ButtonColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false), false);
                    hsFav.ButtonHoverColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 40, false), false);
                    hsFav.ButtonPressedColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 60, false), false);
                    hsOpen.BackColor = Settings.Theme.BackColor;
                    hsOpen.ButtonColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false), false);
                    hsOpen.ButtonHoverColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 40, false), false);
                    hsOpen.ButtonPressedColor = HTAlt.Tools.ReverseColor(HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 60, false), false);
                    hlvHistory.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    cbLang.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    cmsHistory.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    cmsSearchEngine.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    listBox2.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    comboBox1.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    btCookie.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    btInstall.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    btUpdater.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    tbHomepage.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    btCleanLog.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    tbFolder.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    tbStartup.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    cmsStartup.BackColor = Settings.Theme.BackColor;
                    cmsStartup.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    tbFolder.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    tbStartup.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black;
                    btReset.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    btDownloadFolder.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    button12.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    tbSearchEngine.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    btNotification.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    btNotification.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    panel1.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    panel1.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    button10.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    tbHomepage.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    tbSearchEngine.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    cbLang.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    cbLang.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    toolStripTextBox1.BackColor = HTAlt.Tools.ShiftBrightness(Settings.Theme.BackColor, 20, false);
                    flpLayout.BackColor = Settings.Theme.BackColor;
                    flpLayout.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    flpNewTab.BackColor = Settings.Theme.BackColor;
                    flpNewTab.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    flpClose.BackColor = Settings.Theme.BackColor;
                    flpClose.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    cmsProfiles.BackColor = Settings.Theme.BackColor;
                    cmsHamburger.BackColor = Settings.Theme.BackColor;
                    cmsPrivacy.BackColor = Settings.Theme.BackColor;
                    label2.BackColor = Settings.Theme.BackColor;
                    BackColor = Settings.Theme.BackColor;
                    extensionToolStripMenuItem1.DropDown.BackColor = Settings.Theme.BackColor;
                    aboutToolStripMenuItem.Image = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.about_w : Properties.Resources.about;
                    downloadsToolStripMenuItem.Image = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.download_w : Properties.Resources.download;
                    historyToolStripMenuItem.Image = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.history_w : Properties.Resources.history;
                    pbIncognito.Image = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.inctab_w : Properties.Resources.inctab;
                    tbAddress.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    cmsHamburger.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    cmsProfiles.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    label2.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    toolStripTextBox1.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    cmsPrivacy.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    extensionToolStripMenuItem1.DropDown.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    textBox4.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    if (isPageFavorited(chromiumWebBrowser1.Address)) { btFav.ButtonImage = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Properties.Resources.star_on_w : Properties.Resources.star_on; } else { btFav.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.star : Properties.Resources.star_w; }
                    mFavorites.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    settingsToolStripMenuItem.Image = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.Settings : Properties.Resources.Settings_w;
                    newWindowToolStripMenuItem.Image = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.newwindow : Properties.Resources.newwindow_w;
                    newIncognitoWindowToolStripMenuItem.Image = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.inctab : Properties.Resources.inctab_w;
                    btProfile.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.profiles : Properties.Resources.profiles_w;
                    btBack.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.leftarrow : Properties.Resources.leftarrow_w;
                    btRefresh.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.refresh : Properties.Resources.refresh_w;
                    btNext.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.rightarrow : Properties.Resources.rightarrow_w;
                    btNotifBack.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.leftarrow : Properties.Resources.leftarrow_w;
                    btCookieBack.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.leftarrow : Properties.Resources.leftarrow_w;
                    //button4.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.go : Properties.Resources.go_w;
                    btHome.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.home : Properties.Resources.home_w;
                    btHamburger.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.hamburger : Properties.Resources.hamburger_w;
                    tbAddress.BackColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.FromArgb(HTAlt.Tools.SubtractIfNeeded(Settings.Theme.BackColor.R, 20), HTAlt.Tools.SubtractIfNeeded(Settings.Theme.BackColor.G, 20), HTAlt.Tools.SubtractIfNeeded(Settings.Theme.BackColor.B, 20)) : Color.FromArgb(HTAlt.Tools.AddIfNeeded(Settings.Theme.BackColor.R, 20, 255), HTAlt.Tools.AddIfNeeded(Settings.Theme.BackColor.G, 20, 255), HTAlt.Tools.AddIfNeeded(Settings.Theme.BackColor.B, 20, 255));
                    textBox4.BackColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.FromArgb(HTAlt.Tools.SubtractIfNeeded(Settings.Theme.BackColor.R, 20), HTAlt.Tools.SubtractIfNeeded(Settings.Theme.BackColor.G, 20), HTAlt.Tools.SubtractIfNeeded(Settings.Theme.BackColor.B, 20)) : Color.FromArgb(HTAlt.Tools.AddIfNeeded(Settings.Theme.BackColor.R, 20, 255), HTAlt.Tools.AddIfNeeded(Settings.Theme.BackColor.G, 20, 255), HTAlt.Tools.AddIfNeeded(Settings.Theme.BackColor.B, 20, 255));
                    mFavorites.BackColor = Settings.Theme.BackColor;
                    extensionToolStripMenuItem1.Image = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.ext : Properties.Resources.ext_w;
                    cmsBStyle.BackColor = Settings.Theme.BackColor;
                    cmsBStyle.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    cmsCookie.BackColor = Settings.Theme.BackColor;
                    cmsCookie.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    cmsBack.BackColor = Settings.Theme.BackColor;
                    cmsBack.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    cmsForward.BackColor = Settings.Theme.BackColor;
                    cmsForward.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    switchToToolStripMenuItem.DropDown.BackColor = Settings.Theme.BackColor; switchToToolStripMenuItem.DropDown.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White;
                    foreach (ToolStripItem x in cmsForward.Items) { x.BackColor = Settings.Theme.BackColor; x.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White; }
                    foreach (ToolStripItem x in cmsBack.Items) { x.BackColor = Settings.Theme.BackColor; x.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White; }
                    foreach (ToolStripItem x in cmsProfiles.Items) { x.BackColor = Settings.Theme.BackColor; x.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White; }
                    foreach (ToolStripItem x in extensionToolStripMenuItem1.DropDownItems) { x.BackColor = Settings.Theme.BackColor; x.ForeColor = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Color.Black : Color.White; }
                    foreach (TabPage x in tabControl1.TabPages) { x.BackColor = Settings.Theme.BackColor; x.ForeColor = !HTAlt.Tools.IsBright(Settings.Theme.BackColor) ? Color.White : Color.Black; }
                    foreach (Control c in Controls)
                    {
                        c.Refresh();
                    }
                }
                if (Settings.Theme.BackgroundStyle != "BACKCOLOR")
                {
                    if (Settings.Theme.BackgroundStyle != oldStyle)
                    {
                        oldStyle = Settings.Theme.BackgroundStyle;
                        Image backStyle = GetImageFromURL(Settings.Theme.BackgroundStyle);
                        pNavigate.BackgroundImage = backStyle;
                        mFavorites.BackgroundImage = backStyle;
                        foreach (TabPage x in tabControl1.TabPages) { x.BackgroundImage = backStyle; }
                        tpSettings.BackgroundImage = backStyle;
                    }
                }
                else
                {
                    if (Settings.Theme.BackgroundStyle != oldStyle)
                    {
                        oldStyle = Settings.Theme.BackgroundStyle;
                        pNavigate.BackgroundImage = null;
                        mFavorites.BackgroundImage = null;
                        foreach (TabPage x in tabControl1.TabPages) { x.BackgroundImage = null; }
                        tpSettings.BackgroundImage = null;
                    }
                }
                if (Settings.Theme.BackgroundStyleLayout == 0) //NONE
                {
                    pNavigate.BackgroundImageLayout = ImageLayout.None;
                    mFavorites.BackgroundImageLayout = ImageLayout.None;
                    tpSettings.BackgroundImageLayout = ImageLayout.None;
                    foreach (TabPage x in tabControl1.TabPages) { x.BackgroundImageLayout = ImageLayout.None; }
                }
                else if (Settings.Theme.BackgroundStyleLayout == 1) //TILE
                {
                    pNavigate.BackgroundImageLayout = ImageLayout.Tile;
                    mFavorites.BackgroundImageLayout = ImageLayout.Tile;
                    tpSettings.BackgroundImageLayout = ImageLayout.Tile;
                    foreach (TabPage x in tabControl1.TabPages) { x.BackgroundImageLayout = ImageLayout.Tile; }
                }
                else if (Settings.Theme.BackgroundStyleLayout == 2) //CENTER
                {
                    pNavigate.BackgroundImageLayout = ImageLayout.Center;
                    mFavorites.BackgroundImageLayout = ImageLayout.Center;
                    tpSettings.BackgroundImageLayout = ImageLayout.Center;
                    foreach (TabPage x in tabControl1.TabPages) { x.BackgroundImageLayout = ImageLayout.Center; }
                }
                else if (Settings.Theme.BackgroundStyleLayout == 3) //STRETCH
                {
                    pNavigate.BackgroundImageLayout = ImageLayout.Stretch;
                    mFavorites.BackgroundImageLayout = ImageLayout.Stretch;
                    tpSettings.BackgroundImageLayout = ImageLayout.Stretch;
                    foreach (TabPage x in tabControl1.TabPages) { x.BackgroundImageLayout = ImageLayout.Stretch; }
                }
                else if (Settings.Theme.BackgroundStyleLayout == 4) //ZOOM
                {
                    pNavigate.BackgroundImageLayout = ImageLayout.Zoom;
                    mFavorites.BackgroundImageLayout = ImageLayout.Zoom;
                    tpSettings.BackgroundImageLayout = ImageLayout.Zoom;
                    foreach (TabPage x in tabControl1.TabPages) { x.BackgroundImageLayout = ImageLayout.Zoom; }
                }
            }catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private double websiteprogress;
        public void ChangeProgress(double value)
        {
            if (value == 1) { websiteprogress = value; pbProgress.Width = Width; }
            else
            {
                websiteprogress = value;
                pbProgress.Width = Convert.ToInt32(Convert.ToDouble(Width / 100) * Convert.ToDouble(value * 100));
            }
        }
        private void timer1_Tick(object sender, EventArgs e)
        {

            if (chromiumWebBrowser1.IsDisposed)
            {
                Close();
            }

            if (findLast)
            {
                tsSearchStatus.Text = findC + " " + findCurrent + " " + findL + " " + findT + " " + findTotal;
            }
            else if (findCurrent == 0 && findTotal == 0)
            {
                tsSearchStatus.Text = noSearch;
            }
            else
            {
                tsSearchStatus.Text = findC + " " + findCurrent + " " + findL + " " + findT + " " + findTotal;
            }

            onCEFTab = (tabControl1.SelectedTab == tpCef);
            ChangeTheme();
            if (Parent != null)
            {
                Parent.Text = Text;
            }
            RefreshTranslation();
            if (anaform != null)
            {
                if (anaform.OldSessions == "") { spRestorer.Visible = false; restoreLastSessionToolStripMenuItem.Visible = false; } else { spRestorer.Visible = true; restoreLastSessionToolStripMenuItem.Visible = true; }
            }
            Text = tabControl1.SelectedTab.Text;
            if (NotificationListenerMode)
            {
                isMuted = true;
                MuteTS.Text = "NOTIFICATION LISTENER MODE! MUTED";
                chromiumWebBrowser1.GetBrowserHost().SetAudioMuted(true);
            }
        }

        private void TestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            chromiumWebBrowser1.Load(((ToolStripMenuItem)sender).Tag.ToString());
        }
        private bool isPageFavorited(string url)
        {
            return Settings.Favorites.FavoritesWithNoFolders.Find(i => i.Url == url) != null;
        }

        private void Button7_Click(object sender, EventArgs e)
        {
            if (isPageFavorited(chromiumWebBrowser1.Address))
            {
                Settings.Favorites.DeleteFolder(Settings.Favorites.FavoritesWithNoFolders.Find(i => i.Url == chromiumWebBrowser1.Address));
                btFav.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.star : Properties.Resources.star_w;
            }
            else
            {
                frmNewFav newFav = new frmNewFav(Text, chromiumWebBrowser1.Address, this);
                newFav.ShowDialog();
            }
            RefreshFavorites();
        }

        private void RefreshProfiles()
        {
            switchToToolStripMenuItem.DropDownItems.Clear();
            foreach (string x in Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\"))
            {
                if (x != profilePath)
                {
                    DirectoryInfo info = new DirectoryInfo(x);
                    if (info.Name == userName) { }
                    else
                    {
                        ToolStripMenuItem profileItem = new ToolStripMenuItem
                        {
                            Text = info.Name
                        };
                        profileItem.Click += ProfilesToolStripMenuItem_Click;
                        switchToToolStripMenuItem.DropDownItems.Add(profileItem);
                    }
                }
            }
            if (switchToToolStripMenuItem.DropDownItems.Count == 0)
            {
                switchToToolStripMenuItem.DropDownItems.Add(tsEmptyProfile);
            }
        }
        public void RefreshSizes()
        {
            flpFrom.Location = new Point(scheduleFrom.Location.X + scheduleFrom.Width + 10, flpFrom.Location.Y);
            scheduleTo.Location = new Point(flpFrom.Location.X + flpFrom.Width + 10, scheduleTo.Location.Y);
            flpTo.Location = new Point(scheduleTo.Location.X + scheduleTo.Width + 10, flpTo.Location.Y);
            flpEvery.Location = new Point(scheduleEvery.Location.X + scheduleEvery.Width + 10, flpEvery.Location.Y);
            lbVersion.Location = new Point(lbKorot.Location.X + lbKorot.Width, lbVersion.Location.Y);
            flpClose.Location = new Point(label32.Location.X + label32.Width, flpClose.Location.Y);
            flpClose.Width = tpTheme.Width - (label32.Width + label32.Location.X + 25);
            flpNewTab.Location = new Point(label31.Location.X + label31.Width, flpNewTab.Location.Y);
            flpNewTab.Width = tpTheme.Width - (label31.Width + label31.Location.X + 25);
            hsAutoRestore.Location = new Point(lbautoRestore.Location.X + lbautoRestore.Width + 5, hsAutoRestore.Location.Y);
            hsFav.Location = new Point(label33.Location.X + label33.Width + 5, hsFav.Location.Y);
            hsDoNotTrack.Location = new Point(lbDNT.Location.X + lbDNT.Width + 5, hsDoNotTrack.Location.Y);
            hsOpen.Location = new Point(lbOpen.Location.X + lbOpen.Width + 5, hsOpen.Location.Y);
            hsDownload.Location = new Point(lbAutoDownload.Location.X + lbAutoDownload.Width + 5, hsDownload.Location.Y);
            hsProxy.Location = new Point(lbLastProxy.Location.X + lbLastProxy.Width + 5, hsProxy.Location.Y);
            linkLabel1.LinkArea = new LinkArea(0, linkLabel1.Text.Length);
            linkLabel1.Location = new Point(label21.Location.X, label21.Location.Y + label21.Size.Height);
            textBox4.Location = new Point(label12.Location.X + label12.Width, textBox4.Location.Y);
            textBox4.Width = tpTheme.Width - (label12.Width + label12.Location.X + 25);
            tbStartup.Location = new Point(label28.Location.X + label28.Width, tbStartup.Location.Y);
            tbStartup.Width = tpSettings.Width - (label28.Width + label28.Location.X + 15);
            flpLayout.Location = new Point(label25.Location.X + label25.Width, flpLayout.Location.Y);
            flpLayout.Width = tpTheme.Width - (label25.Width + label25.Location.X + 25);
            pbBack.Location = new Point(label14.Location.X + label14.Width, pbBack.Location.Y);
            pbOverlay.Location = new Point(label16.Location.X + label16.Width, pbOverlay.Location.Y);
            tbFolder.Location = new Point(lbDownloadFolder.Location.X + lbDownloadFolder.Width, tbFolder.Location.Y);
            tbFolder.Width = tpDownload.Width - (lbDownloadFolder.Location.X + lbDownloadFolder.Width + btDownloadFolder.Width + 25);
            btDownloadFolder.Location = new Point(tbFolder.Location.X + tbFolder.Width, btDownloadFolder.Location.Y);
            comboBox1.Location = new Point(label13.Location.X + label13.Width, comboBox1.Location.Y);
            comboBox1.Width = tpTheme.Width - (label13.Location.X + label13.Width + button12.Width + 25);
            button12.Location = new Point(comboBox1.Location.X + comboBox1.Width, button12.Location.Y);
            tbHomepage.Location = new Point(lbHomepage.Location.X + lbHomepage.Width + 5, tbHomepage.Location.Y);
            tbHomepage.Width = tpSettings.Width - (lbHomepage.Location.X + lbHomepage.Width + 25);
            radioButton1.Location = new Point(tbHomepage.Location.X, tbHomepage.Location.Y + tbHomepage.Height + 5);
            tbSearchEngine.Location = new Point(lbSearchEngine.Location.X + lbSearchEngine.Width + 5, tbSearchEngine.Location.Y);
            tbSearchEngine.Width = tpSettings.Width - (lbSearchEngine.Location.X + lbSearchEngine.Width + 25);
        }
        public void RefreshTranslation()
        {
            if (string.IsNullOrWhiteSpace(Settings.Theme.Author) && string.IsNullOrWhiteSpace(Settings.Theme.Name))
            {
                label21.Text = aboutInfo.Replace("[NEWLINE]", Environment.NewLine);
            }
            else
            {
                label21.Text = aboutInfo.Replace("[NEWLINE]", Environment.NewLine) + Environment.NewLine + themeInfo.Replace("[THEMEAUTHOR]", string.IsNullOrWhiteSpace(Settings.Theme.Author) ? anon : Settings.Theme.Author).Replace("[THEMENAME]", string.IsNullOrWhiteSpace(Settings.Theme.Name) ? noname : Settings.Theme.Name);
            }

            hsOpen.Checked = Settings.Downloads.OpenDownload;
            hsAutoRestore.Checked = Settings.AutoRestore;
            hsFav.Checked = Settings.Favorites.ShowFavorites;
            switch ((int)Settings.Theme.CloseButtonColor)
            {
                case 0:
                    rbBackColor1.Checked = true;
                    break;
                case 1:
                    rbForeColor1.Checked = true;
                    break;
                case 2:
                    rbOverlayColor1.Checked = true;
                    break;
            }
            switch ((int)Settings.Theme.NewTabColor)
            {
                case 0:
                    rbBackColor.Checked = true;
                    break;
                case 1:
                    rbForeColor.Checked = true;
                    break;
                case 2:
                    rbOverlayColor.Checked = true;
                    break;
            }
            switch (Settings.Theme.BackgroundStyleLayout)
            {
                case 0:
                    rbNone.Checked = true;
                    break;
                case 1:
                    rbTile.Checked = true;
                    break;
                case 2:
                    rbCenter.Checked = true;
                    break;
                case 3:
                    rbStretch.Checked = true;
                    break;
                case 4:
                    rbZoom.Checked = true;
                    break;
            }
            colorToolStripMenuItem.Checked = Settings.Theme.BackgroundStyle == "BACKCOLOR" ? true : false;
            switchToToolStripMenuItem.Text = switchTo;
            newProfileToolStripMenuItem.Text = newprofile;
            deleteThisProfileToolStripMenuItem.Text = deleteProfile;
            showCertificateErrorsToolStripMenuItem.Text = showCertError;
            textBox4.Text = Settings.Theme.BackgroundStyle == "BACKCOLOR" ? usingBC : Settings.Theme.BackgroundStyle;
            if (certError)
            {
                safeStatusToolStripMenuItem.Text = CertificateErrorTitle;
                ınfoToolStripMenuItem.Text = CertificateError;
            }
            else
            {
                safeStatusToolStripMenuItem.Text = CertificateOKTitle;
                ınfoToolStripMenuItem.Text = CertificateOK;
            }
            if (cookieUsage) { cookieInfoToolStripMenuItem.Text = usesCookies; } else { cookieInfoToolStripMenuItem.Text = notUsesCookies; }
            label7.Text = CertErrorPageTitle;
            label8.Text = CertErrorPageMessage;
            button10.Text = CertErrorPageButton;
            newWindowToolStripMenuItem.Text = newWindow;
            newIncognitoWindowToolStripMenuItem.Text = newincognitoWindow;
            settingsToolStripMenuItem.Text = settingstitle;
            restoreLastSessionToolStripMenuItem.Text = restoreOldSessions;
            if (Settings.Startup.ToLower() == "korot://newtab")
            {
                tbStartup.Text = showNewTabPageToolStripMenuItem.Text;
            }
            else if (Settings.Startup.ToLower() == "korot://homepage" || Settings.Startup.ToLower() == Settings.Homepage.ToLower())
            {
                tbStartup.Text = showHomepageToolStripMenuItem.Text;
            }
            else
            {
                tbStartup.Text = Settings.Startup;
            }
            hsDownload.Checked =Settings.Downloads.UseDownloadFolder;
            lbDownloadFolder.Enabled = hsDownload.Checked;
            tbFolder.Enabled = hsDownload.Checked;
            btDownloadFolder.Enabled = hsDownload.Checked;
            tbFolder.Text =Settings.Downloads.DownloadDirectory;
        }
        public void Fullscreenmode(bool fullscreen)
        {
            if (fullscreen != anaform.isFullScreen)
            {
                if (fullscreen)
                {
                    tabControl1.Location = new Point(tabControl1.Location.X, tabControl1.Location.Y - pNavigate.Height);
                    tabControl1.Height += pNavigate.Height;
                }
                else
                {
                    tabControl1.Location = new Point(tabControl1.Location.X, tabControl1.Location.Y + pNavigate.Height);
                    tabControl1.Height -= pNavigate.Height;
                }
                pNavigate.Visible = !fullscreen;
                anaform.Invoke(new Action(() => anaform.Fullscreenmode(fullscreen)));
            }
        }

        private void ProfilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProfileManagement.SwitchProfile(((ToolStripMenuItem)sender).Text, this);
        }

        private void NewProfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProfileManagement.NewProfile(this);
        }

        private void DeleteThisProfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProfileManagement.DeleteProfile(userName, this);
        }

        private void Button9_Click(object sender, EventArgs e)
        {
            cmsProfiles.Show(MousePosition);
        }
        private void DefaultProxyts_Click(object sender, EventArgs e)
        {
            SetProxy(chromiumWebBrowser1, defaultProxy);
            DefaultProxyts.Enabled = false;
        }
        public void applyExtension(string fileLocation)
        {
            string Playlist = HTAlt.Tools.ReadFile(fileLocation, Encoding.UTF8);
            char[] token = new char[] { Environment.NewLine.ToCharArray()[0] };
            string[] SplittedFase = Playlist.Split(token);
            if (SplittedFase.Length >= 11)
            {
                if (SplittedFase[4].Substring(1).Replace(Environment.NewLine, "").Substring(3, 1) == "1" && (new FileInfo(fileLocation).Length < 1048576) && (new FileInfo(SplittedFase[5].Substring(1).Replace(Environment.NewLine, "").Replace("[EXTFOLDER]", new FileInfo(fileLocation).Directory + "\\")).Length < 5242880))
                {
                    chromiumWebBrowser1.GetMainFrame().ExecuteJavaScriptAsync(HTAlt.Tools.ReadFile(SplittedFase[8].Substring(1).Replace(Environment.NewLine, "").Replace("[EXTFOLDER]", new FileInfo(fileLocation).Directory + "\\"), Encoding.UTF8));
                }
                if (SplittedFase[4].Substring(1).Replace(Environment.NewLine, "").Substring(4, 1) == "1" && !string.IsNullOrWhiteSpace(SplittedFase[7].Substring(1).Replace(Environment.NewLine, "")) && defaultProxy != null)
                {
                    SetProxy(chromiumWebBrowser1, SplittedFase[7].Substring(1).Replace(Environment.NewLine, ""));
                    DefaultProxyts.Enabled = true;
                    if (Settings.RememberLastProxy) { Settings.LastProxy = SplittedFase[7].Substring(1).Replace(Environment.NewLine, ""); }
                }
                bool allowWebContent = false;
                if (SplittedFase[4].Substring(1).Replace(Environment.NewLine, "").Substring(1, 1) == "1") { allowWebContent = true; }
                if (SplittedFase[4].Substring(1).Replace(Environment.NewLine, "").Substring(2, 1) == "1")
                {
                    frmExt formext = new frmExt(this, userName, fileLocation, SplittedFase[6].Substring(1).Replace(Environment.NewLine, "").Replace("[EXTFOLDER]", new FileInfo(fileLocation).Directory + "\\"), allowWebContent)
                    {
                        TopLevel = false,
                        FormBorderStyle = FormBorderStyle.None,
                        Size = new Size(Convert.ToInt32(SplittedFase[10].Substring(1).Replace(Environment.NewLine, "")), Convert.ToInt32(SplittedFase[9].Substring(1).Replace(Environment.NewLine, "")))
                    };
                    Controls.Add(formext);
                    formext.Visible = true;
                    formext.BringToFront();
                    formext.Show();
                }
            }
        }
        private void ExtensionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            applyExtension(((ToolStripMenuItem)sender).Tag.ToString());

        }
        public void showDevTools()
        {
            if (chromiumWebBrowser1.IsHandleCreated)
            {
                chromiumWebBrowser1.GetBrowser().ShowDevTools();
            }
        }
        public void LoadExt()
        {
            extensionToolStripMenuItem1.DropDownItems.Clear();
            foreach (Extension x in Settings.Extensions.ExtensionList)
            {
                ToolStripMenuItem extItem = new ToolStripMenuItem
                {
                    Text = x.Name,
                    Tag = x.ManifestFile
                };
                extItem.Click += ExtensionToolStripMenuItem_Click;
                extensionToolStripMenuItem1.DropDown.Items.Add(extItem);
            }
            if (extensionToolStripMenuItem1.DropDownItems.Count == 0)
            {
                extensionToolStripMenuItem1.DropDown.Items.Add(tsEmptyExt);
            }
            extensionToolStripMenuItem1.DropDown.Items.Add(tsExt);
            extensionToolStripMenuItem1.DropDown.Items.Add(tsWebStore);
        }

        private void TmrSlower_Tick(object sender, EventArgs e)
        {
            RefreshFavorites();
        }

        public void FrmCEF_SizeChanged(object sender, EventArgs e)
        {
            ChangeProgress(websiteprogress);
        }


        private void Panel1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            tabform_KeyDown(pCEF, new KeyEventArgs(e.KeyData));
        }
        private void pictureBox2_Click(object sender, EventArgs e)
        {
            cmsPrivacy.Show(pbPrivacy, 0, pbPrivacy.Size.Height);
        }

        private void xToolStripMenuItem_Click(object sender, EventArgs e)
        {
            cmsPrivacy.Close();
        }

        private void showCertificateErrorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (showCertificateErrorsToolStripMenuItem.Tag != null)
            {
                TextBox txtCertificate = new TextBox() { ScrollBars = ScrollBars.Both, Multiline = true, Dock = DockStyle.Fill, Text = showCertificateErrorsToolStripMenuItem.Tag.ToString() };
                Form frmCertificate = new Form() { Icon = Icon, Text = CertificateErrorMenuTitle, FormBorderStyle = FormBorderStyle.SizableToolWindow };
                frmCertificate.Controls.Add(txtCertificate);
                frmCertificate.ShowDialog();
            }
        }
        public List<string> CertAllowedUrls = new List<string>();
        private void button10_Click(object sender, EventArgs e)
        {
            CertAllowedUrls.Add(button10.Tag.ToString());
            chromiumWebBrowser1.Refresh();
            pnlCert.Visible = false;
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (anaform.settingTab != null)
            {
                anaform.SelectedTab = anaform.settingTab;
            }
            else
            {
                anaform.settingTab = ParentTab;
                btNext.Enabled = true;
                allowSwitching = true;
                tabControl1.SelectedTab = tpSettings;
            }
        }

        private void button11_Click(object sender, EventArgs e)
        {
            if (cmsHamburger.Visible) { cmsHamburger.Close(); } else { cmsHamburger.Show(btHamburger, 0, 0); }
            btHamburger.FlatAppearance.BorderSize = 0;
        }

        private void restoreLastSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            anaform.Invoke(new Action(() => anaform.ReadSession(anaform.OldSessions)));
            restoreLastSessionToolStripMenuItem.Visible = false;
        }

        private bool allowSwitching = false;
        private bool onCEFTab = true;
        private void tabControl1_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (allowSwitching == false) { e.Cancel = true; } else { toolStripTextBox1.Text = SearchOnPage; chromiumWebBrowser1.StopFinding(true); e.Cancel = false; allowSwitching = false; }
            onCEFTab = (tabControl1.SelectedTab == tpCef);
            if (tabControl1.SelectedTab == tpCef)
            {
                cef_GotFocus(sender, e);
                resetPage();
            }
            else if (tabControl1.SelectedTab == tpCert)
            {
                cef_GotFocus(sender, e);
                cef_LostFocus(sender, e);
                resetPage();
            }
            else
            {
                cef_LostFocus(sender, e);
            }
        }

        private void textBox4_Click(object sender, EventArgs e)
        {
            cmsBStyle.Show(textBox4, 0, 0);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            NewTab("korot://licenses");
        }
        private void contextMenuStrip4_Opening(object sender, CancelEventArgs e)
        {
            Process.Start("explorer.exe", "\"" + Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\Extensions\\\"");
            e.Cancel = true;
        }

        private void hsDoNotTrack_CheckedChanged(object sender, EventArgs e)
        {
            Settings.DoNotTrack = hsDoNotTrack.Checked;
        }

        private void disallowThisPageForCookieAccessToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Settings.GetSiteFromUrl(HTAlt.Tools.GetBaseURL(chromiumWebBrowser1.Address)) != null)
            {
                Settings.GetSiteFromUrl(HTAlt.Tools.GetBaseURL(chromiumWebBrowser1.Address)).AllowCookies = !Settings.GetSiteFromUrl(HTAlt.Tools.GetBaseURL(chromiumWebBrowser1.Address)).AllowCookies;
            }
            else
            {
                Site newSite = new Site()
                {
                    Url = HTAlt.Tools.GetBaseURL(chromiumWebBrowser1.Address),
                    AllowCookies = false,
                };
                Settings.Sites.Add(newSite);
            }
            chromiumWebBrowser1.Reload();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            cmsIncognito.Show(pbIncognito, 0, 0);
        }

        private void openInNewTab_Click(object sender, EventArgs e)
        {
            if (selectedFavorite != null)
            {
                if (selectedFavorite.Tag != null)
                {
                    if (selectedFavorite.Tag.ToString() != "korot://folder")
                    {
                        NewTab(selectedFavorite.Tag.ToString());
                    }
                    else
                    {
                        foreach (ToolStripItem item in selectedFavorite.DropDown.Items)
                        {
                            if (item.Tag.ToString() != "korot://folder") { NewTab(item.Tag.ToString()); }
                        }
                    }
                }
            }
        }
        private void removeSelectedTSMI_Click(object sender, EventArgs e)
        {
            if (selectedFavorite != null)
            {
                if (selectedFavorite.Tag.ToString() == chromiumWebBrowser1.Address)
                {
                    btFav.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.star : Properties.Resources.star_w; ;
                }
                Settings.Favorites.DeleteFolder(selectedFavorite.Tag as Folder);
                RefreshFavorites();
            }
        }

        private void clearTSMI_Click(object sender, EventArgs e)
        {
            Settings.Favorites.Favorites.Clear();
            btFav.ButtonImage = HTAlt.Tools.Brightness(Settings.Theme.BackColor) > 130 ? Properties.Resources.star : Properties.Resources.star_w; ;
            RefreshFavorites();
        }

        private void cmsFavorite_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason != ToolStripDropDownCloseReason.ItemClicked)
            {
                itemClicked = false;
                selectedFavorite = null;
            }
        }

        private void cmsFavorite_Opening(object sender, CancelEventArgs e)
        {
            if (selectedFavorite == null)
            {
                removeSelectedTSMI.Enabled = false;
                tsopenInNewTab.Enabled = false;
                openİnNewWindowToolStripMenuItem.Enabled = false;
                openİnNewIncognitoWindowToolStripMenuItem.Enabled = false;
                tsSepFav.Visible = false;
                removeSelectedTSMI.Visible = false;
                tsopenInNewTab.Visible = false;
                openİnNewWindowToolStripMenuItem.Visible = false;
                openİnNewIncognitoWindowToolStripMenuItem.Visible = false;
            }
            else
            {
                if (selectedFavorite.Tag != null)
                {
                    if (selectedFavorite.Tag.ToString() == "korot://folder")
                    {
                        tsopenInNewTab.Text = openAllInNewTab;
                        openİnNewIncognitoWindowToolStripMenuItem.Text = openAllInNewIncWindow;
                        openİnNewWindowToolStripMenuItem.Text = openAllInNewWindow;
                    }
                    else
                    {
                        tsopenInNewTab.Text = openInNewTab;
                        openİnNewIncognitoWindowToolStripMenuItem.Text = openInNewIncWindow;
                        openİnNewWindowToolStripMenuItem.Text = openInNewWindow;
                    }
                }
                removeSelectedTSMI.Enabled = !_Incognito;
                tsopenInNewTab.Enabled = true;
                openİnNewWindowToolStripMenuItem.Enabled = true;
                openİnNewIncognitoWindowToolStripMenuItem.Enabled = true;
                tsSepFav.Visible = true;
                removeSelectedTSMI.Visible = !_Incognito;
                tsopenInNewTab.Visible = true;
                openİnNewWindowToolStripMenuItem.Visible = true;
                openİnNewIncognitoWindowToolStripMenuItem.Visible = true;
            }
        }
        public void takeScreenShot()
        {
            takeAScreenshotToolStripMenuItem_Click(null, null);
        }

        public void savePage()
        {
            saveThisPageToolStripMenuItem_Click(null, null);
        }

        private void takeAScreenshotToolStripMenuItem_Click(object sender, EventArgs e)
        {
            allowSwitching = true;
            tabControl1.SelectedTab = tpCef;
            SaveFileDialog save = new SaveFileDialog()
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                FileName = "Korot Screenshot.png",
                Filter = imageFiles + "|*.png|" + allFiles + "|*.*"
            };
            if (save.ShowDialog() == DialogResult.OK)
            {
                HTAlt.Tools.WriteFile(save.FileName, TakeScrenshot.ImageToByte2(TakeScrenshot.Snapshot(chromiumWebBrowser1)));
            }
        }

        private void saveThisPageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            allowSwitching = true;
            tabControl1.SelectedTab = tpCef;
            SaveFileDialog save = new SaveFileDialog()
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                FileName = Text + ".html",
                Filter = htmlFiles + "|*.html;*.htm|" + allFiles + "|*.*"
            };
            if (save.ShowDialog() == DialogResult.OK)
            {
                Task<string> htmlText = chromiumWebBrowser1.GetSourceAsync();
                HTAlt.Tools.WriteFile(save.FileName, htmlText.Result, Encoding.UTF8);
            }
        }

        private void frmCEF_KeyUp(object sender, KeyEventArgs e)
        {
            isControlKeyPressed = !e.Control;
        }

        private void MouseScroll(object sender, MouseEventArgs e)
        {

            if (isControlKeyPressed)
            {
                allowSwitching = true;
                tabControl1.SelectedTab = tpCef;
                if (e.Delta > 0)
                {
                    zoomInToolStripMenuItem_Click(sender, null);
                }
                else if (e.Delta < 0)
                {
                    zoomOutToolStripMenuItem_Click(sender, null);
                }
            }
        }

        private void resetZoomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            allowSwitching = true;
            tabControl1.SelectedTab = tpCef;
            chromiumWebBrowser1.SetZoomLevel(0.0);
            cmsHamburger.Show(btHamburger, 0, 0);
            cmsHamburger.Show(btHamburger, 0, 0);
            doNotDestroyFind = true;
            toolStripTextBox1.Text = searchPrev;
            toolStripTextBox_TextChanged(null, e);
        }
        public void zoomIn()
        {
            Task<double> zoomLevel = chromiumWebBrowser1.GetZoomLevelAsync();
            if (zoomLevel.Result <= 8)
            {
                chromiumWebBrowser1.SetZoomLevel(zoomLevel.Result + 0.25);
            }
        }
        public void zoomOut()
        {
            Task<double> zoomLevel = chromiumWebBrowser1.GetZoomLevelAsync();
            if (zoomLevel.Result >= -0.75)
            {
                chromiumWebBrowser1.SetZoomLevel(zoomLevel.Result - 0.25);
            }
        }
        public void zoomInToolStripMenuItem_Click(object sender, EventArgs e)
        {
            allowSwitching = true;
            tabControl1.SelectedTab = tpCef;
            zoomIn();
            cmsHamburger.Show(btHamburger, 0, 0);
            doNotDestroyFind = true;
            toolStripTextBox1.Text = searchPrev;
            toolStripTextBox_TextChanged(null, e);

        }

        public void zoomOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            allowSwitching = true;
            tabControl1.SelectedTab = tpCef;
            zoomOut();
            cmsHamburger.Show(btHamburger, 0, 0);
            cmsHamburger.Show(btHamburger, 0, 0);
            doNotDestroyFind = true;
            toolStripTextBox1.Text = searchPrev;
            toolStripTextBox_TextChanged(null, e);
        }

        private bool doNotDestroyFind = false;
        private void cmsHamburger_Opening(object sender, CancelEventArgs e)
        {
            if (!doNotDestroyFind)
            {
                toolStripTextBox1.Text = SearchOnPage;
                chromiumWebBrowser1.StopFinding(true);
                doNotDestroyFind = false;
            }
            LoadExt();
            Task.Run(new Action(() => getZoomLevel()));
        }

        private async void getZoomLevel()
        {
            await Task.Run(() =>
            {
                Task<double> zoomLevel = chromiumWebBrowser1.GetZoomLevelAsync();
                zOOMLEVELToolStripMenuItem.Text = ((zoomLevel.Result * 100) + 100) + "%";
            });

        }

        private string searchPrev;
        private void toolStripTextBox_TextChanged(object sender, EventArgs e)
        {
            if ((!string.IsNullOrEmpty(toolStripTextBox1.Text)) & toolStripTextBox1.Text != SearchOnPage)
            {
                searchPrev = toolStripTextBox1.Text;
                chromiumWebBrowser1.Find(0, toolStripTextBox1.Text, true, caseSensitiveToolStripMenuItem.Checked, false);
            }
            else
            {
                doNotDestroyFind = false;
                toolStripTextBox1.Text = SearchOnPage;
                chromiumWebBrowser1.StopFinding(true);
            }
        }

        private void cmsHamburger_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (!doNotDestroyFind)
            {
                toolStripTextBox1.Text = SearchOnPage;
                chromiumWebBrowser1.StopFinding(true);
                doNotDestroyFind = false;
            }
        }

        private void caseSensitiveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            cmsHamburger.Show(btHamburger, 0, 0);
            doNotDestroyFind = true;
            toolStripTextBox1.Text = searchPrev;
            toolStripTextBox_TextChanged(null, e);
        }
        private void toolStripTextBox1_Click(object sender, EventArgs e)
        {
            if (!toolStripTextBox1.Selected)
            {
                toolStripTextBox1.SelectAll();
            }
        }

        private void historyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (anaform.historyTab != null)
            {
                anaform.SelectedTab = anaform.historyTab;
            }
            else
            {
                anaform.historyTab = ParentTab;
                btNext.Enabled = true;
                allowSwitching = true;
                tabControl1.SelectedTab = tpHistory;
            }
        }

        private void downloadsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (anaform.downloadTab != null)
            {
                anaform.SelectedTab = anaform.downloadTab;
            }
            else
            {
                anaform.downloadTab = ParentTab;
                btNext.Enabled = true;
                allowSwitching = true;
                tabControl1.SelectedTab = tpDownload;
            }
        }
        private void tsSearchNext_Click(object sender, EventArgs e)
        {
            chromiumWebBrowser1.Find(0, toolStripTextBox1.Text, true, caseSensitiveToolStripMenuItem.Checked, true);
            cmsHamburger.Show(btHamburger, 0, 0);
            doNotDestroyFind = true;
            toolStripTextBox1.Text = searchPrev;
            toolStripTextBox_TextChanged(null, e);
        }
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (anaform.aboutTab != null)
            {
                anaform.SelectedTab = anaform.aboutTab;
            }
            else
            {
                anaform.aboutTab = ParentTab;
                btNext.Enabled = true;
                allowSwitching = true;
                tabControl1.SelectedTab = tpAbout;
            }
        }

        private void hsProxy_CheckedChanged(object sender, EventArgs e)
        {
            Settings.RememberLastProxy = hsProxy.Checked;
        }

        private void tsThemes_Click(object sender, EventArgs e)
        {
            if (anaform.themeTab != null)
            {
                anaform.SelectedTab = anaform.themeTab;
            }
            else
            {
                anaform.themeTab = ParentTab;
                btNext.Enabled = true;
                allowSwitching = true;
                tabControl1.SelectedTab = tpTheme;
            }
        }

        private void clickHereToLearnMoreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NewTab("korot://incognito");
            anaform.Invoke(new Action(() => anaform.SelectedTabIndex = anaform.Tabs.Count - 1));
        }

        private void ıncognitoModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            cmsIncognito.Close();
        }

        private bool itemClicked = false;
        private void mFavorites_MouseClick(object sender, MouseEventArgs e)
        {
            if (!itemClicked) { if (e.Button == MouseButtons.Right) { selectedFavorite = null; cmsFavorite.Show(MousePosition); } }
        }

        private void cmsFavorite_Opened(object sender, EventArgs e)
        {
            cmsFavorite_Opening(null, null);
        }

        private void button15_Click(object sender, EventArgs e)
        {
            if (anaform.siteTab != null)
            {
                anaform.SelectedTab = anaform.siteTab;
            }
            else
            {
                resetPage(true);
                anaform.siteTab = ParentTab;
                btNext.Enabled = true;
                allowSwitching = true;
                tabControl1.SelectedTab = tpSite;
            }
        }
        private void button17_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog() { Description = selectAFolder };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                tbFolder.Text = dialog.SelectedPath;
               Settings.Downloads.DownloadDirectory = tbFolder.Text;
            }
        }

        private void tbFolder_TextChanged(object sender, EventArgs e)
        {
           Settings.Downloads.DownloadDirectory = tbFolder.Text;
        }

        private void hsDownload_CheckedChanged(object sender, EventArgs e)
        {
           Settings.Downloads.UseDownloadFolder = hsDownload.Checked;
            lbDownloadFolder.Enabled = hsDownload.Checked;
            tbFolder.Enabled = hsDownload.Checked;
            btDownloadFolder.Enabled = hsDownload.Checked;
        }

        private void showNewTabPageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tbStartup.Text = showNewTabPageToolStripMenuItem.Text;
            Settings.Startup = "korot://newtab";
        }

        private void showHomepageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tbStartup.Text = showHomepageToolStripMenuItem.Text;
            Settings.Startup = Settings.Homepage;
        }

        private void showAWebsiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HTAlt.WinForms.HTInputBox inputb = new HTAlt.WinForms.HTInputBox("Korot", enterAValidUrl, Settings.SearchEngine) { Icon = Icon, SetToDefault = SetToDefault, StartPosition = FormStartPosition.CenterParent, OK = OK, Cancel = Cancel, BackgroundColor = Settings.Theme.BackColor };
            DialogResult diagres = inputb.ShowDialog();
            if (diagres == DialogResult.OK)
            {
                if (string.IsNullOrWhiteSpace(inputb.TextValue) || (inputb.TextValue.ToLower() == "korot://newtab") || inputb.TextValue.ToLower() == Settings.Homepage.ToLower() || inputb.TextValue.ToLower() == "korot://homepage")
                {
                    showAWebsiteToolStripMenuItem_Click(sender, e);
                }
                else
                {
                    Settings.Startup = inputb.TextValue;
                    tbStartup.Text = inputb.TextValue;
                }
            }
        }

        private void tbStartup_Click(object sender, EventArgs e)
        {
            cmsStartup.Show(MousePosition);
        }

        private void button18_Click(object sender, EventArgs e)
        {
            HTAlt.WinForms.HTMsgBox mesaj = new HTAlt.WinForms.HTMsgBox("Korot", resetConfirm,
                                                                                      new HTAlt.WinForms.HTDialogBoxContext() { Yes = true, No = true, Cancel = true })
            { StartPosition = FormStartPosition.CenterParent,Yes = Yes, No = No, OK = OK, Cancel = Cancel, BackgroundColor = Settings.Theme.BackColor, Icon = Icon };
            if (mesaj.ShowDialog() == DialogResult.Yes)
            {
                Process.Start(Application.ExecutablePath, "-oobe");
                Application.Exit();

            }
        }

        private void hsFav_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Favorites.ShowFavorites = hsFav.Checked;
            RefreshFavorites();
        }

        private void btCleanLog_Click(object sender, EventArgs e)
        {
            int count = 0;
            foreach (string x in Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Korot\\Logs\\"))
            {
                File.Delete(x);
                count += 1;
            }
            Output.WriteLine(" [Korot.CleanLogs] " + count + " files deleted.");
        }

        private void hsOpen_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Downloads.OpenDownload = hsOpen.Checked;
        }

        private void tsWebStore_Click(object sender, EventArgs e)
        {
            NewTab("https://haltroy.com/store/Korot/Extensions/index.html");
        }

        private void pictureBox6_Click(object sender, EventArgs e)
        {
            NewTab("https://haltroy.com/store/Korot/Themes/index.html");
        }

        private void newFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmNewFav newFav = new frmNewFav(defaultFolderName, "korot://folder", this);
            newFav.ShowDialog();
        }

        private void newFavoriteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmNewFav newFav = new frmNewFav("", "", this);
            newFav.ShowDialog();
        }

        private void openİnNewWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (selectedFavorite != null)
            {
                if (selectedFavorite.Tag != null)
                {
                    if (selectedFavorite.Tag.ToString() != "korot://folder")
                    {
                        Process.Start(Application.ExecutablePath, selectedFavorite.Tag.ToString());
                    }
                    else
                    {
                        foreach (ToolStripItem item in selectedFavorite.DropDown.Items)
                        {
                            if (item.Tag.ToString() != "korot://folder") { Process.Start(Application.ExecutablePath, item.Tag.ToString()); }
                        }
                    }
                }
            }
        }

        private void openİnNewIncognitoWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (selectedFavorite != null)
            {
                if (selectedFavorite.Tag != null)
                {
                    if (selectedFavorite.Tag.ToString() != "korot://folder")
                    {
                        Process.Start(Application.ExecutablePath, "-incognito" + selectedFavorite.Tag.ToString());
                    }
                    else
                    {
                        foreach (ToolStripItem item in selectedFavorite.DropDown.Items)
                        {
                            if (item.Tag.ToString() != "korot://folder") { Process.Start(Application.ExecutablePath, "-incognito" + item.Tag.ToString()); }
                        }
                    }
                }
            }
        }

        private bool isLeftPressed, isRightPressed = false;

        private void cbLang_TextUpdate(object sender, EventArgs e)
        {
            if (File.Exists(Application.StartupPath + "\\Lang\\" + cbLang.Text + ".lang"))
            {
                LoadLangFromFile(Application.StartupPath + "\\Lang\\" + cbLang.Text + ".lang");
            }
        }

        private void frmCEF_FormClosing(object sender, FormClosingEventArgs e)
        {
            closing = true;
        }
        public void redirectTo(string url, string title)
        {
            if (bypassThisDeletion)
            {
                bypassThisDeletion = false;
            }
            else
            {
                if (lbURL.SelectedIndex != -1)
                {
                    if (lbURL.Items[lbURL.SelectedIndex].ToString() != url)
                    {
                        bypassThisIndexChange = true;
                        int selectedItem = lbURL.SelectedIndex;
                        if (lbURL.Items.Count - 1 > 0)
                        {
                            while (lbURL.Items.Count - 1 != selectedItem)
                            {
                                lbURL.Items.RemoveAt(selectedItem + 1);
                            }
                            while (lbTitle.Items.Count - 1 != selectedItem)
                            {
                                lbTitle.Items.RemoveAt(selectedItem + 1);
                            }
                        }
                        lbURL.Items.Add(url);
                        lbTitle.Items.Add(title);
                        lbURL.SelectedIndex = lbURL.Items.Count - 1;
                        lbTitle.SelectedIndex = lbURL.SelectedIndex;
                        return;
                    }
                }
                else
                {
                    lbURL.Items.Add(url);
                    lbTitle.Items.Add(title);
                    lbURL.SelectedIndex = lbURL.Items.Count - 1;
                    lbTitle.SelectedIndex = lbURL.SelectedIndex;
                }
            }
        }

        private bool bypassThisIndexChange = false;
        private bool bypassThisDeletion = false;
        public bool indexChanged = false;
        private void lbURL_SelectedIndexChanged(object sender, EventArgs e)
        {
            lbTitle.SelectedIndex = lbURL.SelectedIndex;
            if (bypassThisIndexChange) { bypassThisIndexChange = false; }
            else
            {
                bypassThisDeletion = true;
                indexChanged = true;
                chromiumWebBrowser1.Load(lbURL.SelectedItem.ToString());
            }
            btBack.Visible = lbURL.SelectedIndex != 0;
            btNext.Visible = lbURL.SelectedIndex != lbURL.Items.Count - 1;
        }

        private void cmsBack_Opening(object sender, CancelEventArgs e)
        {
            cmsBack.Items.Clear();
            if ((lbURL.SelectedIndex != -1) && lbURL.Items.Count > 0)
            {
                int i = 0;
                while (i != lbURL.SelectedIndex)
                {
                    ToolStripMenuItem item = new ToolStripMenuItem
                    {
                        Text = lbTitle.Items[i].ToString(),
                        ShortcutKeyDisplayString = i.ToString(),
                        Tag = lbURL.Items[i].ToString(),
                        ShowShortcutKeys = false
                    };
                    item.Click += backfrowardItemClick;
                    cmsBack.Items.Add(item);
                    i += 1;
                }
            }
        }

        private void backfrowardItemClick(object sender, EventArgs e)
        {
            int switchTo = Convert.ToInt32(((ToolStripMenuItem)sender).ShortcutKeyDisplayString);
            lbURL.SelectedIndex = switchTo;
        }

        private void cmsForward_Opening(object sender, CancelEventArgs e)
        {
            cmsForward.Items.Clear();
            if ((lbURL.SelectedIndex != -1) && lbURL.Items.Count > 0)
            {
                int i = lbURL.SelectedIndex + 1;
                while (i != lbURL.Items.Count - 1)
                {
                    ToolStripMenuItem item = new ToolStripMenuItem
                    {
                        Text = lbTitle.Items[i].ToString(),
                        ShortcutKeyDisplayString = i.ToString(),
                        Tag = lbURL.Items[i].ToString(),
                        ShowShortcutKeys = false
                    };
                    item.Click += backfrowardItemClick;
                    cmsForward.Items.Add(item);
                    i += 1;
                }
            }
        }

        private void hsAutoRestore_CheckedChanged(object sender, EventArgs e)
        {
           Settings.AutoRestore = hsAutoRestore.Checked;
        }

        private void tpCollection_Enter(object sender, EventArgs e)
        {
            ColMan.genColList();
        }


        private void tsCollections_Click(object sender, EventArgs e)
        {
            if (anaform.collectionTab != null)
            {
                anaform.SelectedTab = anaform.collectionTab;
            }
            else
            {
                anaform.collectionTab = ParentTab;
                btNext.Enabled = true;
                allowSwitching = true;
                tabControl1.SelectedTab = tpCollection;
            }
        }

        private void tsChangeTitleBack_Click(object sender, EventArgs e)
        {
            frmChangeTBTBack dialog = new frmChangeTBTBack(this);
            switch (dialog.ShowDialog())
            {
                case DialogResult.OK:
                    ParentTab.BackColor = dialog.Color;
                    ParentTab.UseDefaultBackColor = false;
                    break;
                case DialogResult.Abort:
                    ParentTab.BackColor = BackColor;
                    ParentTab.UseDefaultBackColor = true;
                    break;
            }
        }

        private void rbNone_CheckedChanged(object sender, EventArgs e)
        {
            if (rbNone.Checked)
            {
                rbTile.Checked = false;
                rbCenter.Checked = false;
                rbStretch.Checked = false;
                rbZoom.Checked = false;
                Settings.Theme.BackgroundStyleLayout = 0;
                ChangeTheme();
                
            }
        }

        private void rbTile_CheckedChanged(object sender, EventArgs e)
        {
            if (rbTile.Checked)
            {
                rbNone.Checked = false;
                rbCenter.Checked = false;
                rbStretch.Checked = false;
                rbZoom.Checked = false;
                Settings.Theme.BackgroundStyleLayout = 1;
                ChangeTheme();
                
            }
        }

        private void rbCenter_CheckedChanged(object sender, EventArgs e)
        {
            if (rbCenter.Checked)
            {
                rbTile.Checked = false;
                rbNone.Checked = false;
                rbStretch.Checked = false;
                rbZoom.Checked = false;
                Settings.Theme.BackgroundStyleLayout = 2;
                ChangeTheme();
                
            }
        }

        private void rbStretch_CheckedChanged(object sender, EventArgs e)
        {
            if (rbStretch.Checked)
            {
                rbTile.Checked = false;
                rbCenter.Checked = false;
                rbNone.Checked = false;
                rbZoom.Checked = false;
                Settings.Theme.BackgroundStyleLayout = 3;
                ChangeTheme();
                
            }
        }

        private void rbZoom_CheckedChanged(object sender, EventArgs e)
        {
            if (rbZoom.Checked)
            {
                rbTile.Checked = false;
                rbCenter.Checked = false;
                rbStretch.Checked = false;
                rbNone.Checked = false;
                Settings.Theme.BackgroundStyleLayout = 4;
                ChangeTheme();
                
            }
        }

        private void rbBackColor_CheckedChanged(object sender, EventArgs e)
        {
            if (rbBackColor.Checked)
            {
                rbForeColor.Checked = false;
                rbOverlayColor.Checked = false;
                Settings.Theme.NewTabColor = TabColors.BackColor;
                ChangeTheme();
                
            }
        }

        private void rbForeColor_CheckedChanged(object sender, EventArgs e)
        {
            if (rbForeColor.Checked)
            {
                rbBackColor.Checked = false;
                rbOverlayColor.Checked = false;
                Settings.Theme.NewTabColor = TabColors.ForeColor;
                ChangeTheme();
                
            }
        }

        private void rbOverlayColor_CheckedChanged(object sender, EventArgs e)
        {
            if (rbOverlayColor.Checked)
            {
                rbForeColor.Checked = false;
                rbBackColor.Checked = false;
                Settings.Theme.NewTabColor = TabColors.OverlayColor;
                ChangeTheme();
                
            }
        }

        private void rbBackColor1_CheckedChanged(object sender, EventArgs e)
        {
            if (rbBackColor1.Checked)
            {
                rbForeColor1.Checked = false;
                rbOverlayColor1.Checked = false;
                Settings.Theme.CloseButtonColor = TabColors.BackColor;
                ChangeTheme();
                
            }
        }

        private void rbForeColor1_CheckedChanged(object sender, EventArgs e)
        {
            if (rbForeColor1.Checked)
            {
                rbBackColor1.Checked = false;
                rbOverlayColor1.Checked = false;
                Settings.Theme.CloseButtonColor = TabColors.ForeColor;
                ChangeTheme();
                
            }
        }

        private void rbOverlayColor1_CheckedChanged(object sender, EventArgs e)
        {
            if (rbOverlayColor1.Checked)
            {
                rbForeColor1.Checked = false;
                rbBackColor1.Checked = false;
                Settings.Theme.CloseButtonColor = TabColors.OverlayColor;
                ChangeTheme();
                
            }
        }

        private void label2_TextChanged(object sender, EventArgs e)
        {
            label2.Visible = !string.IsNullOrWhiteSpace(label2.Text);
        }

        private void hsNotificationSound_CheckedChanged(object sender, EventArgs e)
        {
            Settings.QuietMode = !hsNotificationSound.Checked;
        }

        private void lbHaftaGunu_Click(object sender, EventArgs e)
        {
            Label myLabel = sender as Label;
            if (myLabel.Tag.ToString() == "1")
            {
                myLabel.Tag = "0";
                myLabel.BackColor = panel1.BackColor;
            }
            else if (myLabel.Tag.ToString() == "0")
            {
                myLabel.Tag = "1";
                myLabel.BackColor = Settings.Theme.OverlayColor;
            }
            writeSchedules();
            RefreshScheduledSiletMode();
        }

        private void fromHour_ValueChanged(object sender, EventArgs e)
        {
            writeSchedules();
        }

        private void hsSchedule_CheckedChanged(object sender, EventArgs e)
        {
            Settings.AutoSilent = hsSchedule.Checked;
            panel1.Enabled = hsSchedule.Checked;
        }

        private void hsSilent_CheckedChanged(object sender, EventArgs e)
        {
            Settings.DoNotPlaySound = hsSilent.Checked;
        }

        private void button19_Click_1(object sender, EventArgs e)
        {
            if (anaform.notificationTab != null)
            {
                anaform.SelectedTab = anaform.notificationTab;
            }
            else
            {
                resetPage(true);
                anaform.notificationTab = ParentTab;
                btNext.Enabled = true;
                allowSwitching = true;
                tabControl1.SelectedTab = tpNotification;
            }
        }

        private void btNotifBack_Click(object sender, EventArgs e)
        {
            if (anaform.settingTab != null)
            {
                anaform.SelectedTab = anaform.settingTab;
            }
            else
            {
                resetPage(true);
                anaform.settingTab = ParentTab;
                btNext.Enabled = true;
                allowSwitching = true;
                tabControl1.SelectedTab = tpSettings;
            }
        }

        private void btNBlockBack_Click(object sender, EventArgs e)
        {
            if (anaform.notificationTab != null)
            {
                anaform.SelectedTab = anaform.notificationTab;
            }
            else
            {
                resetPage(true);
                anaform.notificationTab = ParentTab;
                btNext.Enabled = true;
                allowSwitching = true;
                tabControl1.SelectedTab = tpNotification;
            }
        }

        private void tbAddress_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button4_Click(sender, e);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
        bool isMuted = false;
        private void MuteTS_Click(object sender, EventArgs e)
        {
            isMuted = isMuted ? false : true;
            chromiumWebBrowser1.GetBrowserHost().SetAudioMuted(isMuted);
            MuteTS.Text = isMuted ? UnmuteThisTab : MuteThisTab;
        }

        private void tmrNotifListener_Tick(object sender, EventArgs e)
        {
            if (NotificationListenerMode)
            {
                chromiumWebBrowser1.Refresh();
                chromiumWebBrowser1.GetBrowserHost().SetAudioMuted(true);
            }
        }
        bool onThemeName = false;
        private void comboBox1_Enter(object sender, EventArgs e)
        {
            onThemeName = true;
        }

        private void comboBox1_Leave(object sender, EventArgs e)
        {
            onThemeName = false;
        }

        private void label20_MouseClick(object sender, MouseEventArgs e)
        {
            isLeftPressed = e.Button == MouseButtons.Left ? true : isLeftPressed;
            isRightPressed = e.Button == MouseButtons.Right ? true : isRightPressed;
            if (isLeftPressed && isRightPressed)
            {
                NewTab("https://www.youtube.com/watch?v=KMU0tzLwhbE");
                isLeftPressed = false;
                isRightPressed = false;
            }
        }
    }
}
