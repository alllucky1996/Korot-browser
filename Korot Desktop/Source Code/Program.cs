﻿/*

Copyright © 2020 Eren "Haltroy" Kanat

Use of this source code is governed by an MIT License that can be found in github.com/Haltroy/Korot/blob/master/LICENSE

*/

using CefSharp;
using CefSharp.WinForms;
using EasyTabs;
using HTAlt;
using HTAlt.WinForms;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;

namespace Korot
{
    public static class VersionInfo
    {
        public static string CodeName => "Pergo";
        public static int VersionNumber = 54;
        public static string Version => isPreOut ? PreOutName : Application.ProductVersion.ToString();
        public static string PreOutName => "y20m12u01";
        public static bool isPreOut => true;
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Cef.EnableHighDPISupport();
            KorotTools.CreateThemes();
            KorotTools.createFolders();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (!File.Exists(Application.StartupPath + "\\Lang\\English.klf"))
            {
                KorotTools.FixDefaultLanguage();
            }
            Settings settings = new Settings(SafeFileSettingOrganizedClass.LastUser,args.Contains("-debug"));
            if (string.IsNullOrWhiteSpace(settings.Birthday) && !settings.CelebrateBirthday)
            {
                settings.BirthdayCount++;
            }
            if (settings.BirthdayCount > 10)
            {
                frmAskBirthday askBDay = new frmAskBirthday(settings);
                askBDay.ShowDialog();
            }
            List<frmNotification> notifications = new List<frmNotification>();
            try
            {
                if (args.Contains("--make-ext"))
                {
                    Application.Run(new frmMakeExt());
                    return;
                }
                else if (args.Contains("--error"))
                {
                    Application.Run(new frmError(settings));
                    return;
                }
                else if (args.Contains("-oobe") || settings.LoadedDefaults || !Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\"))
                {
                    Application.Run(new frmOOBE(settings));
                    return;
                }
                else if (args.Contains("--theme-wizard"))
                {
                    Application.Run(new frmThemeWizard(settings));
                    return;
                }
                else
                {
                    frmMain testApp = new frmMain(settings)
                    {
                        notifications = notifications,
                        isIncognito = args.Contains("-incognito")
                    };
                    settings.AllForms.Add(testApp);
                    bool isIncognito = args.Contains("-incognito");
                    if (SafeFileSettingOrganizedClass.LastUser == "") { SafeFileSettingOrganizedClass.LastUser = "user0"; }
                    for (int i = 0; i < args.Length; i++)
                    {
                        string x = args[i];
                        if (x == Application.ExecutablePath || x == "-oobe" || x == "-update") { }
                        else if (x == "-incognito")
                        {
                            frmCEF cefform = new frmCEF(testApp, settings, true, "korot://incognito", SafeFileSettingOrganizedClass.LastUser) { };
                            settings.AllForms.Add(cefform);
                            testApp.Tabs.Add(new TitleBarTab(testApp) { Content = cefform });
                        }
                        else if (x == "-debug")
                        {
                            settings.DebugMode = true;
                        }
                        else if (x.ToLowerInvariant().EndsWith(".kef") || x.ToLowerInvariant().EndsWith(".ktf"))
                        {
                            Application.Run(new frmInstallExt(settings, x));
                        }
                        else
                        {
                            testApp.CreateTab(x);
                        }
                    }
                    if (testApp.Tabs.Count < 1)
                    {
                        frmCEF cefform = new frmCEF(testApp, settings, isIncognito, settings.Startup, SafeFileSettingOrganizedClass.LastUser);
                        settings.AllForms.Add(cefform);
                        testApp.Tabs.Add(
new TitleBarTab(testApp)
{
    Content = cefform
});
                    }
                    testApp.SelectedTabIndex = 0;
                    TitleBarTabsApplicationContext applicationContext = new TitleBarTabsApplicationContext();
                    applicationContext.Start(testApp);
                    Application.Run(applicationContext);
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine(" [Korot] FATAL_ERROR: " + ex.ToString());
                SafeFileSettingOrganizedClass.ErrorMenu = "<root>" + Environment.NewLine 
                    + settings.GetSFOSCErrorMenu()
                    + "<Error Message=\""
                    + ex.Message.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;")
                    + "\">"
                    + Environment.NewLine
                    + ex.ToString().Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;")
                    + Environment.NewLine
                    + "</Error>"
                    + Environment.NewLine
                    + "</root>";
                Process.Start(Application.ExecutablePath, "--error");
                return;
            }
        }

        public static void RemoveDirectory(string directory, bool displayresult = true)
        {
            List<FileFolderError> errors = new List<FileFolderError>();
            foreach (string x in Directory.GetFiles(directory)) { try { File.Delete(x); } catch (Exception ex) { errors.Add(new FileFolderError(x, ex, false)); } }
            foreach (string x in Directory.GetDirectories(directory)) { try { Directory.Delete(x, true); } catch (Exception ex) { errors.Add(new FileFolderError(x, ex, true)); } }
            if (displayresult) { if (errors.Count == 0) { Output.WriteLine(" [RemoveDirectory] Removed \"" + directory + "\" with no errors."); } else { Output.WriteLine(" [RemoveDirectory] Removed \"" + directory + "\" with " + errors.Count + " error(s)."); foreach (FileFolderError x in errors) { Output.WriteLine(" [RemoveDirectory] " + (x.isDirectory ? "Directory" : "File") + " Error: " + x.Location + " [" + x.Error.ToString() + "]"); } } }
        }

        public static Stream ToStream(this Image image, ImageFormat format)
        {
            var stream = new System.IO.MemoryStream();
            image.Save(stream, format);
            stream.Position = 0;
            return stream;
        }

        public static Stream ToStream(this Bitmap bitmap, ImageFormat format)
        {
            var img = (Image)bitmap;
            return img.ToStream(format);
        }
    }

    public class Settings
    {
        public Themes Themes { get; set; }
        public bool DebugMode { get; set; } = false;
        public List<ThemeImage> Wallpapers { get; set; } = new List<ThemeImage>();
        public List<ThemeImage> UserWallpapers { get; set; } = new List<ThemeImage>();
        public Settings(string Profile,bool debug = false)
        {
            ProfileName = Profile;
            Extensions.Settings = this;
            LanguageSystem.Settings = this;
            Theme.Settings = this;
            DebugMode = debug;
            Themes = new Themes(this);
            if (string.IsNullOrWhiteSpace(Profile))
            {
                LoadedDefaults = true;
                Output.WriteLine(" [Settings] Loaded defaults because profile name was empty." + Environment.NewLine + " ProfileName: " + Profile);
                return;
            }
            if (!File.Exists(ProfileDirectory + "settings.kpf"))
            {
                LoadedDefaults = true;
                Output.WriteLine(" [Settings] Loaded defaults because can't find settings file." + Environment.NewLine + " at " + ProfileDirectory + "settings.kpf");
                return;
            }
            if (!Directory.Exists(ProfileDirectory))
            {
                LoadedDefaults = true;
                Output.WriteLine(" [Settings] Loaded defaults because can't find profile directory." + Environment.NewLine + " at " + ProfileDirectory);
                return;
            }
            string ManifestXML = HTAlt.Tools.ReadFile(ProfileDirectory + "settings.kpf", Encoding.Unicode);
            XmlDocument document = new XmlDocument();
            document.LoadXml(ManifestXML);
            List<string> loadedSettings = new List<string>();
            foreach (XmlNode node in document.FirstChild.NextSibling.ChildNodes)
            {
                if (loadedSettings.Contains(node.Name.ToLowerInvariant())) { return; } else { loadedSettings.Add(node.Name.ToLowerInvariant()); }
                if (node.Name.ToLowerInvariant() == "homepage")
                {
                    Homepage = node.InnerText.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'");
                }
                else if (node.Name.ToLowerInvariant().ToLowerInvariant() == "languagefile")
                {
                    string lf = node.InnerText.Replace("[KOROTPATH]", Application.StartupPath).Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'");
                    LanguageSystem.ReadFromFile(string.IsNullOrWhiteSpace(lf) ? Application.StartupPath + "\\Lang\\English.klf" : lf, true);
                }
                else if (node.Name.ToLowerInvariant() == "menusize")
                {
                    string w = node.InnerText.Substring(0, node.InnerText.IndexOf(";"));
                    string h = node.InnerText.Substring(node.InnerText.IndexOf(";"), node.InnerText.Length - node.InnerText.IndexOf(";"));
                    MenuSize = new Size(Convert.ToInt32(w.Replace(";", "")), Convert.ToInt32(h.Replace(";", "")));
                }
                else if (node.Name.ToLowerInvariant() == "menupoint")
                {
                    string x = node.InnerText.Substring(0, node.InnerText.IndexOf(";"));
                    string y = node.InnerText.Substring(node.InnerText.IndexOf(";"), node.InnerText.Length - node.InnerText.IndexOf(";"));
                    MenuPoint = new Point(Convert.ToInt32(x.Replace(";", "")), Convert.ToInt32(y.Replace(";", "")));
                }
                else if (node.Name.ToLowerInvariant() == "userwallpapers")
                {
                    for(int i = 0; i < node.ChildNodes.Count;i++)
                    {
                        XmlNode subnode = node.ChildNodes[i];
                        if (subnode.Name.ToLowerInvariant() == "wallpaper")
                        {
                            UserWallpapers.Add(new ThemeImage(subnode.InnerXml.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'")));
                        }
                    }
                }
                else if (node.Name.ToLowerInvariant() == "wallpapers")
                {
                    for (int i = 0; i < node.ChildNodes.Count; i++)
                    {
                        XmlNode subnode = node.ChildNodes[i];
                        if (subnode.Name.ToLowerInvariant() == "wallpaper")
                        {
                            Wallpapers.Add(new ThemeImage(subnode.InnerXml.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'")));
                        }
                    }
                }
                else if (node.Name.ToLowerInvariant() == "searchengine")
                {
                    SearchEngine = node.InnerText.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'");
                }
                else if (node.Name.ToLowerInvariant() == "startup")
                {
                    Startup = node.InnerText.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'");
                }
                else if (node.Name.ToLowerInvariant() == "lastproxy")
                {
                    LastProxy = node.InnerText.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'");
                }
                else if (node.Name.ToLowerInvariant() == "menuwasmaximized")
                {
                    MenuWasMaximized = node.InnerText == "true";
                }
                else if (node.Name.ToLowerInvariant() == "ninjamode")
                {
                    NinjaMode = node.InnerText == "true";
                }
                else if (node.Name.ToLowerInvariant() == "usedefaultsound")
                {
                    UseDefaultSound = node.InnerText == "true";
                }
                else if (node.Name.ToLowerInvariant() == "synth")
                {
                    if (node.Attributes["Volume"] != null && node.Attributes["Rate"] != null)
                    {
                        int rate = Convert.ToInt32(node.Attributes["Rate"].Value);
                        SynthRate = rate < -10 ? -10 : (rate > 10 ? 10 : rate);
                        int volume = Convert.ToInt32(node.Attributes["Volume"].Value);
                        SynthVolume = volume < 0 ? 0 : (volume > 100 ? 100 : volume);
                    }
                }
                else if (node.Name.ToLowerInvariant() == "soundlocation")
                {
                    if (!File.Exists(node.InnerText))
                    {
                        UseDefaultSound = true;
                    }
                    else
                    {
                        SoundLocation = node.InnerText;
                    }
                }
                else if (node.Name.ToLowerInvariant() == "donottrack")
                {
                    DoNotTrack = node.InnerText == "true";
                }
                else if (node.Name.ToLowerInvariant() == "birthday")
                {
                    if (node.Attributes["Celebrate"] != null && node.Attributes["Date"] != null && node.Attributes["Count"] != null)
                    {
                        CelebrateBirthday = node.Attributes["Celebrate"].Value == "true";
                        Birthday = node.Attributes["Date"].Value;
                        BirthdayCount = Convert.ToInt32(node.Attributes["Count"].Value);
                    }
                }
                else if (node.Name.ToLowerInvariant() == "autorestore")
                {
                    AutoRestore = node.InnerText == "true";
                }
                else if (node.Name.ToLowerInvariant() == "checkdefault")
                {
                    CheckIfDefault = node.InnerText == "true";
                }
                else if (node.Name.ToLowerInvariant() == "rememberlastproxy")
                {
                    RememberLastProxy = node.InnerText == "true";
                }
                else if (node.Name.ToLowerInvariant() == "screenshotfolder")
                {
                    ScreenShotFolder = node.InnerText;
                }
                else if (node.Name.ToLowerInvariant() == "savefolder")
                {
                    SaveFolder = node.InnerText;
                }
                else if (node.Name.ToLowerInvariant() == "theme")
                {
                    string themeFile = node.Attributes["File"] != null ? node.Attributes["File"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : "";
                    if (!File.Exists(themeFile)) { themeFile = ""; }
                    Theme = new Theme(themeFile, this);
                    foreach (XmlNode subnode in node.ChildNodes)
                    {
                        if (subnode.Name.ToLowerInvariant() == "name")
                        {
                            Theme.Name = subnode.InnerText.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'");
                        }
                        else if (subnode.Name.ToLowerInvariant() == "author")
                        {
                            Theme.Author = subnode.InnerText.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'");
                        }
                        else if (subnode.Name.ToLowerInvariant() == "backcolor")
                        {
                            Theme.BackColor = HTAlt.Tools.HexToColor(subnode.InnerText);
                        }
                        else if (subnode.Name.ToLowerInvariant() == "forecolor")
                        {
                            Theme.AutoForeColor = false;
                            Theme.ForeColor = HTAlt.Tools.HexToColor(subnode.InnerText);
                        }
                        else if (subnode.Name.ToLowerInvariant() == "overlaycolor")
                        {
                            Theme.OverlayColor = HTAlt.Tools.HexToColor(subnode.InnerText);
                        }
                        else if (subnode.Name.ToLowerInvariant() == "newtabcolor")
                        {
                            if (subnode.InnerText == "0")
                            {
                                Theme.NewTabColor = TabColors.BackColor;
                            }
                            else if (subnode.InnerText == "1")
                            {
                                Theme.NewTabColor = TabColors.ForeColor;
                            }
                            else if (subnode.InnerText == "2")
                            {
                                Theme.NewTabColor = TabColors.OverlayColor;
                            }
                            else if (subnode.InnerText == "3")
                            {
                                Theme.NewTabColor = TabColors.OverlayBackColor;
                            }
                        }
                        else if (subnode.Name.ToLowerInvariant() == "closebuttoncolor")
                        {
                            if (subnode.InnerText == "0")
                            {
                                Theme.CloseButtonColor = TabColors.BackColor;
                            }
                            else if (subnode.InnerText == "1")
                            {
                                Theme.CloseButtonColor = TabColors.ForeColor;
                            }
                            else if (subnode.InnerText == "2")
                            {
                                Theme.CloseButtonColor = TabColors.OverlayColor;
                            }
                            else if (subnode.InnerText == "3")
                            {
                                Theme.CloseButtonColor = TabColors.OverlayBackColor;
                            }
                        }
                    }
                }
                else if (node.Name.ToLowerInvariant() == "newtabmenu")
                {
                    NewTabSites = new NewTabSites(node.OuterXml);
                }
                else if (node.Name.ToLowerInvariant() == "autosilent")
                {
                    AutoSilent = node.InnerText == "true";
                }
                else if (node.Name.ToLowerInvariant() == "silent")
                {
                    Silent = node.InnerText == "true";
                }
                else if (node.Name.ToLowerInvariant() == "donotplaysound")
                {
                    DoNotPlaySound = node.InnerText == "true";
                }
                else if (node.Name.ToLowerInvariant() == "quietmode")
                {
                    QuietMode = node.InnerText == "true";
                }
                else if (node.Name.ToLowerInvariant() == "autosilentmode")
                {
                    AutoSilentMode = node.InnerText.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'");
                }
                else if (node.Name.ToLowerInvariant() == "sites")
                {
                    Sites = new List<Site>();
                    foreach (XmlNode sitenode in node.ChildNodes)
                    {
                        Site site = new Site
                        {
                            AllowCookies = sitenode.Attributes["AllowCookies"] != null ? (sitenode.Attributes["AllowCookies"].Value == "true") : false,
                            AllowNotifications = sitenode.Attributes["AllowNotifications"] != null ? (sitenode.Attributes["AllowNotifications"].Value == "true") : false,
                            Name = sitenode.Attributes["Name"] != null ? sitenode.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : "",
                            Url = sitenode.Attributes["Url"] != null ? sitenode.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : ""
                        };
                        Sites.Add(site);
                    }
                }
                else if (node.Name.ToLowerInvariant() == "extensions")
                {
                    Extensions = new Extensions(node.ChildNodes.Count > 0 ? node.OuterXml : "") { Settings = this };
                }
                else if (node.Name.ToLowerInvariant() == "collections")
                {
                    CollectionManager.readCollections(node.OuterXml, true);
                }
                else if (node.Name.ToLowerInvariant() == "autocleaner")
                {
                    AutoCleaner.LoadFromXML(node.OuterXml);
                }
                else if (node.Name.ToLowerInvariant() == "history")
                {
                    foreach (XmlNode subnode in node.ChildNodes)
                    {
                        if (subnode.Name.ToLowerInvariant() == "site")
                        {
                            Site newSite = new Site
                            {
                                Date = subnode.Attributes["Date"] != null ? subnode.Attributes["Date"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : "",
                                Name = subnode.Attributes["Name"] != null ? subnode.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : "",
                                Url = subnode.Attributes["Url"] != null ? subnode.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : ""
                            };
                            History.Add(newSite);
                        }
                    }
                }
                else if (node.Name.ToLowerInvariant() == "siteblocks")
                {
                    foreach (XmlNode subnode in node.ChildNodes)
                    {
                        if (subnode.Name.ToLowerInvariant() == "block")
                        {
                            if (subnode.Attributes["Level"] == null || subnode.Attributes["Filter"] == null || subnode.Attributes["Url"] == null) { }
                            else
                            {
                                BlockSite bs = new BlockSite() { Address = subnode.Attributes["Url"].Value, BlockLevel = Convert.ToInt32(subnode.Attributes["Level"].Value), Filter = subnode.Attributes["Filter"].Value };
                                Filters.Add(bs);
                            }
                        }
                    }
                }
                else if (node.Name.ToLowerInvariant() == "downloads")
                {
                    Downloads.DownloadDirectory = node.Attributes["directory"] != null ? node.Attributes["directory"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : "";
                    Downloads.OpenDownload = node.Attributes["open"] != null ? (node.Attributes["open"].Value == "true") : false;
                    Downloads.UseDownloadFolder = node.Attributes["usedownloadfolder"] != null ? (node.Attributes["usedownloadfolder"].Value == "true") : false;
                    foreach (XmlNode subnode in node.ChildNodes)
                    {
                        if (subnode.Name.ToLowerInvariant() == "site")
                        {
                            Site newSite = new Site
                            {
                                Name = subnode.Attributes["Name"] != null ? subnode.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : "",
                                Url = subnode.Attributes["Url"] != null ? subnode.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : "",
                                Date = subnode.Attributes["Date"] != null ? subnode.Attributes["Date"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : "",
                                LocalUrl = subnode.Attributes["LocalUrl"] != null ? subnode.Attributes["LocalUrl"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : ""
                            };
                            int status = Convert.ToInt32(subnode.Attributes["Status"] != null ? subnode.Attributes["Status"].Value : "0");
                            if (status == 0)
                            {
                                newSite.Status = DownloadStatus.None;
                            }
                            else if (status == 1)
                            {
                                newSite.Status = DownloadStatus.Cancelled;
                            }
                            else if (status == 2)
                            {
                                newSite.Status = DownloadStatus.Downloaded;
                            }
                            else if (status == 3)
                            {
                                newSite.Status = DownloadStatus.Error;
                            }
                            Downloads.Downloads.Add(newSite);
                        }
                    }
                }
                else if (node.Name.ToLowerInvariant() == "favorites")
                {
                    Favorites = new FavoritesSettings(node.ChildNodes.Count > 0 ? node.OuterXml : "")
                    {
                        ShowFavorites = node.Attributes["Show"] != null ? (node.Attributes["Show"].Value == "true") : false,
                    };
                }
            }
            if (string.IsNullOrWhiteSpace(Downloads.DownloadDirectory))
            {
                Downloads.DownloadDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads\\";
            }
            AutoCleaner.Settings = this;
            LoadRandomSites();
        }

        public ThemeImage GetRandomImageFromTheme()
        {
            if (Wallpapers.Count > 0)
            {
                var rnd = new Random();
                return Wallpapers[rnd.Next(0, Wallpapers.Count)];
            }
            else
            {
                return null;
            }
        }

        private void LoadRandomSites()
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(Properties.Resources.randomSites);
            XmlNode mainnode = doc.FirstChild.NextSibling;
            foreach(XmlNode node in mainnode.ChildNodes)
            {
                if (node.Name == "RandomSite" && node.Attributes["Url"] != null)
                {
                    RandomSites.Add(node.Attributes["Url"].Value);
                }
            }
            Output.WriteLine(" [INFO] [Settings] Loaded " + RandomSites.Count + " random sites.");
        }

        public string GiveRandomSite(bool completeRandom = false)
        {
            if (completeRandom)
            {
                int rndm = new Random().Next(0,(RandomSites.Count -1 ) * 2);
                if (rndm < RandomSites.Count)
                {
                    return "http://" + HTAlt.Tools.GenerateRandomText(new Random().Next(1,15)) + "." + HTAlt.Tools.GenerateRandomText(new Random().Next(1, 15));
                }else
                {
                    return RandomSites[rndm - RandomSites.Count];
                }
            }else
            {
                return RandomSites[new Random().Next(0, RandomSites.Count - 1)];
            }
        }

        #region Properties

        public bool CheckIfDefault { get; set; } = true;
        public bool LoadedDefaults = false;
        public string Birthday { get; set; } = "";
        public bool CelebrateBirthday { get; set; } = true;
        public int BirthdayCount { get; set; } = 0;

        public List<string> RandomSites { get; set; } = new List<string>();
        public AutoCleaner AutoCleaner { get; set; } = new AutoCleaner("");
        public List<frmCEF> UpdateFavorites { get; set; } = new List<frmCEF>();

        public void UpdateFavList()
        {
            for (int i = 0; i < AllForms.Count; i++)
            {
                if (AllForms[i] is frmCEF)
                {
                    UpdateFavorites.Add(AllForms[i] as frmCEF);
                }
            }
        }

        public bool UseDefaultSound { get; set; } = true;
        public string SoundLocation { get; set; } = "";
        public bool NinjaMode { get; set; } = false;
        public string ScreenShotFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        public string SaveFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public class BlockLevels
        {
            public static string Level0 = @"((http)|(https))\:\/\/§SITE§";
            public static string Level1 = @"((http)|(https))\:\/\/§SITE§";
            public static string Level2 = @"((http)|(https))\:\/\/[^\/]*?§SITE§";
            public static string Level3 = @"§SITE§";

            public static string Convert(string Url, string Level)
            {
                return Level.Replace("§SITE§", Url.Replace(".", @"\."));
            }

            public static string ConvertToLevel0(string Url)
            {
                return Convert(Url.Replace("https://", "").Replace("http://", ""), Level0);
            }

            public static string ConvertToLevel1(string Url)
            {
                return Convert(HTAlt.Tools.GetBaseURL(Url).Replace("https://", "").Replace("http://", ""), Level1);
            }

            public static string ConvertToLevel2(string Url)
            {
                return Convert(HTAlt.Tools.GetBaseURL(Url).Replace("https://", "").Replace("http://", ""), Level2);
            }

            public static string ConvertToLevel3(string Url)
            {
                return Convert(HTAlt.Tools.GetBaseURL(Url).Replace("https://", "").Replace("http://", ""), Level3);
            }
        }

        public List<BlockSite> Filters { get; set; } = new List<BlockSite>();

        public NewTabSites NewTabSites { get; set; } = new NewTabSites("");

        public bool Silent { get; set; } = false;

        public List<Site> Sites { get; set; } = new List<Site>();

        public bool AutoSilent { get; set; } = false;

        public bool DoNotPlaySound { get; set; } = false;

        public bool QuietMode { get; set; } = false;

        public string AutoSilentMode { get; set; } = "";

        public string ProfileName { get; set; } = "";

        public bool DismissUpdate { get; set; } = false;
        public int SynthVolume { get; set; } = 100;
        public int SynthRate { get; set; } = -2;

        public string Homepage { get; set; } = "korot://newtab";

        public Size MenuSize { get; set; } = new Size(720, 720);

        public Point MenuPoint { get; set; } = new Point(0, 0);

        public string SearchEngine { get; set; } = "https://www.google.com/search?q=";

        public bool RememberLastProxy { get; set; } = false;

        public string LastProxy { get; set; } = "";

        public bool DisableLanguageError { get; set; } = false;

        public bool MenuWasMaximized { get; set; } = true;

        public bool DoNotTrack { get; set; } = true;

        public Theme Theme { get; set; } = new Theme("", null);

        public DownloadSettings Downloads { get; set; } = new DownloadSettings() { DownloadDirectory = "", Downloads = new List<Site>(), OpenDownload = false, UseDownloadFolder = false };

        public LanguageSystem LanguageSystem { get; set; } = new LanguageSystem("", null);

        public CollectionManager CollectionManager { get; set; } = new CollectionManager("") { Collections = new List<Collection>() };

        public List<Site> History { get; set; } = new List<Site>();

        public FavoritesSettings Favorites { get; set; } = new FavoritesSettings("") { Favorites = new List<Folder>(), ShowFavorites = true };

        public Extensions Extensions { get; set; } = new Extensions("");

        public string Startup { get; set; } = "korot://newtab";

        public bool AutoRestore { get; set; } = false;

        #endregion Properties

        public List<Form> AllForms = new List<Form>();
        public List<Form> ThemeChangeForm = new List<Form>();

        public void JustChangedTheme()
        {
            for (int i = 0; i < AllForms.Count; i++) { ThemeChangeForm.Add(AllForms[i]); }
        }

        public bool IsQuietTime
        {
            get
            {
                string Playlist = AutoSilentMode;
                string[] SplittedFase = Playlist.Split(':');
                if (SplittedFase.Length - 1 > 9)
                {
                    bool sunday = SplittedFase[4] == "1";
                    bool monday = SplittedFase[5] == "1";
                    bool tuesday = SplittedFase[6] == "1";
                    bool wednesday = SplittedFase[7] == "1";
                    bool thursday = SplittedFase[8] == "1";
                    bool friday = SplittedFase[9] == "1";
                    bool saturday = SplittedFase[10] == "1";
                    int fromH = Convert.ToInt32(SplittedFase[0]);
                    int fromM = Convert.ToInt32(SplittedFase[1]);
                    int toH = Convert.ToInt32(SplittedFase[2]);
                    int toM = Convert.ToInt32(SplittedFase[3]);
                    bool Nsunday = sunday;
                    bool Nmonday = monday;
                    bool Ntuesday = tuesday;
                    bool Nwednesday = wednesday;
                    bool Nthursday = thursday;
                    bool Nfriday = friday;
                    bool Nsaturday = saturday;
                    if (AutoSilent)
                    {
                        DayOfWeek wk = DateTime.Today.DayOfWeek;
                        if ((Nsunday && wk == DayOfWeek.Sunday)
                            || (Nmonday && wk == DayOfWeek.Monday)
                            || (Ntuesday && wk == DayOfWeek.Tuesday)
                            || (Nwednesday && wk == DayOfWeek.Wednesday)
                            || (Nthursday && wk == DayOfWeek.Thursday)
                            || (Nfriday && wk == DayOfWeek.Friday)
                            || (Nsaturday && wk == DayOfWeek.Saturday))
                        {
                            //it passed the first test to be silent.
                            DateTime date = DateTime.Now;
                            int h = date.Hour;
                            int m = date.Minute;
                            if (fromH < h)
                            {
                                if (toH > h)
                                {
                                    QuietMode = true;
                                }
                                else if (toH == h)
                                {
                                    if (m >= toM)
                                    {
                                        QuietMode = true;
                                    }
                                    else
                                    {
                                        QuietMode = false;
                                    }
                                }
                                else
                                {
                                    QuietMode = false;
                                }
                            }
                            else if (fromH == h)
                            {
                                if (m >= fromM)
                                {
                                    QuietMode = true;
                                }
                                else
                                {
                                    QuietMode = false;
                                }
                            }
                            else
                            {
                                QuietMode = false;
                            }
                        }
                        else
                        {
                            QuietMode = false;
                        }
                    }
                    if (Silent) { QuietMode = true; }
                }
                return QuietMode;
            }
        }

        public string GetSFOSCErrorMenu()
        {
            return "<Translations>" + Environment.NewLine +
     "<Restart>" + LanguageSystem.GetItemText("ErrorRestart") + "</Restart>" + Environment.NewLine +
     "<Message1>" + LanguageSystem.GetItemText("ErrorDesc1") + "</Message1>" + Environment.NewLine +
     "<Message2>" + LanguageSystem.GetItemText("ErrorDesc2") + "</Message2>" + Environment.NewLine +
     "<Technical>" + LanguageSystem.GetItemText("ErrorTI") + "</Technical>" + Environment.NewLine +
     "</Translations>" + Environment.NewLine;
        }
        public bool IsUrlAllowed(string url)
        {
            bool allowed = true;
            foreach (BlockSite x in Filters)
            {
                Regex Rgx = new Regex(x.Filter, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                allowed = !Rgx.IsMatch(url);
            }
            return allowed;
        }

        public void Save()
        {
            string x =
                "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" + Environment.NewLine +
            "<Profile>" + Environment.NewLine +
            " <Homepage>" + Homepage.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "</Homepage>" + Environment.NewLine +
            "   <MenuSize>" + MenuSize.Width + ";" + MenuSize.Height + "</MenuSize>" + Environment.NewLine +
            "   <MenuPoint>" + MenuPoint.X + ";" + MenuPoint.Y + "</MenuPoint>" + Environment.NewLine +
            "   <ScreenShotFolder>" + ScreenShotFolder + "</ScreenShotFolder>" + Environment.NewLine +
            "   <SaveFolder>" + SaveFolder + "</SaveFolder>" + Environment.NewLine +
            "   <SearchEngine>" + SearchEngine.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "</SearchEngine>" + Environment.NewLine +
            "   <LanguageFile>" + LanguageSystem.LangFile.Replace(Application.StartupPath, "[KOROTPATH]").Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "</LanguageFile>" + Environment.NewLine +
            "   <Startup>" + Startup.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "</Startup>" + Environment.NewLine +
            "   <LastProxy>" + LastProxy.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "</LastProxy>" + Environment.NewLine +
            "   <MenuWasMaximized>" + (MenuWasMaximized ? "true" : "false") + "</MenuWasMaximized>" + Environment.NewLine +
            "   <Birthday Celebrate=\"" + (CelebrateBirthday ? "true" : "false") + "\" Date=\"" + Birthday + "\" Count=\"" + BirthdayCount + "\" />" + Environment.NewLine +
            "   <DoNotTrack>" + (DoNotTrack ? "true" : "false") + "</DoNotTrack>" + Environment.NewLine +
            "   <AutoRestore>" + (AutoRestore ? "true" : "false") + "</AutoRestore>" + Environment.NewLine +
            "   <RememberLastProxy>" + (RememberLastProxy ? "true" : "false") + "</RememberLastProxy>" + Environment.NewLine +
            "   <CheckDefault>" + (CheckIfDefault ? "true" : "false") + "</CheckDefault>" + Environment.NewLine +
            "   <Synth Volume=\"" + SynthVolume + "\" Rate=\"" + SynthRate + "\" />" + Environment.NewLine +
            "   <Silent>" + (Silent ? "true" : "false") + "</Silent>" + Environment.NewLine +
            "   <AutoSilent>" + (AutoSilent ? "true" : "false") + "</AutoSilent> " + Environment.NewLine +
            "   <DoNotPlaySound>" + (DoNotPlaySound ? "true" : "false") + "</DoNotPlaySound>" + Environment.NewLine +
            "   <QuietMode>" + (QuietMode ? "true" : "false") + "</QuietMode>" + Environment.NewLine +
            "   <AutoSilentMode>" + AutoSilentMode.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "</AutoSilentMode>" + Environment.NewLine + AutoCleaner.XMLOut() + Environment.NewLine +
            "   <Sites>" + Environment.NewLine;
            foreach (Site site in Sites)
            {
                x += "     <Site Name=\""
                     + site.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;")
                     + "\" Url=\""
                     + site.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;")
                     + "\" AllowNotifications=\""
                     + (site.AllowNotifications ? "true" : "false")
                     + "\" AllowCookies=\""
                     + (site.AllowCookies ? "true" : "false")
                     + "\" />"
                     + Environment.NewLine;
            }
            x += "   </Sites>" + Environment.NewLine + "   <SiteBlocks>" + Environment.NewLine;
            foreach (BlockSite block in Filters)
            {
                // .Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;")
                x += "     <Block Level=\"" + block.BlockLevel + "\" Url=\"" + block.Address + "\" Filter=\"" + block.Filter + "\" />" + Environment.NewLine;
            }
            x += "   </SiteBlocks>" + Environment.NewLine + "   <Theme File=\"" + (!string.IsNullOrWhiteSpace(Theme.ThemeFile) ? Theme.ThemeFile.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") : "") + "\">" + Environment.NewLine +
            "     <Name>" + Theme.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "</Name>" + Environment.NewLine +
            "     <Author>" + Theme.Author.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "</Author>" + Environment.NewLine +
            "     <BackColor>" + HTAlt.Tools.ColorToHex(Theme.BackColor) + "</BackColor>" + Environment.NewLine +
            (Theme.AutoForeColor ? "": ("<ForeColor>" + HTAlt.Tools.ColorToHex(Theme.ForeColor) + "</ForeColor>" + Environment.NewLine)) +
            "     <OverlayColor>" + HTAlt.Tools.ColorToHex(Theme.OverlayColor) + "</OverlayColor>" + Environment.NewLine +
            "     <NewTabColor>" + (int)Theme.NewTabColor + "</NewTabColor>" + Environment.NewLine +
            "     <CloseButtonColor>" + (int)Theme.CloseButtonColor + "</CloseButtonColor>" + Environment.NewLine +
            "     </Theme>" + Environment.NewLine + NewTabSites.XMLOut + Environment.NewLine + Extensions.ExtractList + CollectionManager.writeCollections + "   <History>" + Environment.NewLine;
            foreach (Site site in History)
            {
                x += "     <Site Name=\""
                     + site.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;")
                     + "\" Url=\""
                     + site.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;")
                     + "\" Date=\""
                     + site.Date.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;")
                     + "\" />"
                     + Environment.NewLine;
            }
            x += "   </History>" + Environment.NewLine +
                "   <Downloads Directory=\"" + Downloads.DownloadDirectory.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" Open=\"" + (Downloads.OpenDownload ? "true" : "false") + "\" UseDownloadFolder=\"" + (Downloads.UseDownloadFolder ? "true" : "false") + "\">" + Environment.NewLine;
            foreach (Site site in Downloads.Downloads)
            {
                x += "     <Site Name=\""
                     + site.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;")
                     + "\" Url=\""
                     + site.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;")
                     + "\" Status=\""
                     + (int)site.Status
                     + "\" Date=\""
                     + site.Date.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;")
                     + "\" LocalUrl=\""
                     + site.LocalUrl.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;")
                     + "\" />"
                     + Environment.NewLine;
            }
            x += "   </Downloads>" + Environment.NewLine + Favorites.outXml + "   </Profile>" + Environment.NewLine;
            HTAlt.Tools.WriteFile(ProfileDirectory + "settings.kpf", x, Encoding.Unicode);
        }

        public string ProfileDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + ProfileName + "\\";

        public Site GetSiteFromUrl(string Url)
        {
            return Sites.Find(i => i.Url == Url);
        }
    }



    public class DownloadSettings
    {
        public bool OpenDownload { get; set; }
        public string DownloadDirectory { get; set; }
        public bool UseDownloadFolder { get; set; }
        public List<Site> Downloads { get; set; }
    }

    public class Site
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string LocalUrl { get; set; }
        public bool AllowCookies { get; set; }
        public string Date { get; set; }
        public bool AllowNotifications { get; set; }
        public DownloadStatus Status { get; set; }
    }

    public enum DownloadStatus
    {
        None,
        Cancelled,
        Downloaded,
        Error,
        Downloading
    }

    public class FavoritesSettings
    {
        public void DeleteFolder(Folder folder)
        {
            if (folder == null) { return; }
            if (folder.IsTopFavorite)
            {
                Favorites.Remove(folder);
            }
            else
            {
                if (folder is Favorite)
                {
                    folder.ParentFolder.Favorites.Remove(folder);
                }
                else
                {
                    folder.Favorites.Clear();
                    folder.ParentFolder.Favorites.Remove(folder);
                }
            }
        }

        public FavoritesSettings(string xmlString)
        {
            Favorites = new List<Folder>();
            if (string.IsNullOrWhiteSpace(xmlString))
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(xmlString))
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(xmlString);
                foreach (XmlNode node in document.FirstChild.ChildNodes)
                {
                    if (node.Name == "Folder")
                    {
                        Folder folder = new Folder()
                        {
                            Name = node.Attributes["Name"] != null ? node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : HTAlt.Tools.GenerateRandomText(),
                            Text = node.Attributes["Text"] != null ? node.Attributes["Text"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : HTAlt.Tools.GenerateRandomText(),
                        };
                        folder.ParentFolder = null;
                        GenerateMenusFromXML(node, folder);
                        Favorites.Add(folder);
                    }
                    else if (node.Name == "Favorite")
                    {
                        Favorite favorite = new Favorite()
                        {
                            Name = node.Attributes["Name"] != null ? node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : HTAlt.Tools.GenerateRandomText(),
                            Text = node.Attributes["Text"] != null ? node.Attributes["Text"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : HTAlt.Tools.GenerateRandomText(),
                            Url = node.Attributes["Url"] == null ? "" : node.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'"),
                            IconPath = node.Attributes["Icon"] == null ? "" : node.Attributes["Icon"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'")
                        };
                        favorite.ParentFolder = null;
                        Favorites.Add(favorite);
                    }
                }
            }
        }

        private void GenerateMenusFromXML(XmlNode rootNode, Folder folder)
        {
            foreach (XmlNode node in rootNode.ChildNodes)
            {
                if (node.Name == "Folder")
                {
                    Folder subfolder = new Folder()
                    {
                        Name = node.Attributes["Name"] != null ? node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : HTAlt.Tools.GenerateRandomText(),
                        Text = node.Attributes["Text"] != null ? node.Attributes["Text"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : HTAlt.Tools.GenerateRandomText(),
                    };
                    subfolder.ParentFolder = folder;
                    GenerateMenusFromXML(node, subfolder);
                    folder.Favorites.Add(subfolder);
                }
                else if (node.Name == "Favorite")
                {
                    Favorite favorite = new Favorite()
                    {
                        Name = node.Attributes["Name"] != null ? node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : HTAlt.Tools.GenerateRandomText(),
                        Text = node.Attributes["Text"] != null ? node.Attributes["Text"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'") : HTAlt.Tools.GenerateRandomText(),
                        Url = node.Attributes["Url"] == null ? "" : node.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'"),
                        IconPath = node.Attributes["Icon"] == null ? "" : node.Attributes["Icon"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'")
                    };
                    favorite.ParentFolder = folder;
                    folder.Favorites.Add(favorite);
                }
            }
        }

        public string outXml
        {
            get
            {
                string x = "   <Favorites Show=\"" + (ShowFavorites ? "true" : "false") + "\">" + Environment.NewLine;
                foreach (Folder y in Favorites)
                {
                    x += y.outXml + Environment.NewLine;
                }
                x += "   </Favorites>" + Environment.NewLine;
                return x;
            }
        }

        private void RecursiveFWNF(Folder folder, List<Favorite> list)
        {
            foreach (Folder x in folder.Favorites)
            {
                if (x is Favorite)
                {
                    list.Add(x as Favorite);
                }
                else
                {
                    RecursiveFWNF(x, list);
                }
            }
        }

        public List<Favorite> FavoritesWithNoFolders
        {
            get
            {
                List<Favorite> fav = new List<Favorite>();
                foreach (Folder x in Favorites)
                {
                    if (x is Favorite)
                    {
                        fav.Add(x as Favorite);
                    }
                    else
                    {
                        RecursiveFWNF(x, fav);
                    }
                }
                return fav;
            }
        }

        public List<Folder> Favorites { get; set; }
        public bool ShowFavorites { get; set; }
    }

    public class Folder
    {
        private List<Folder> _Fav = new List<Folder>();
        public Folder ParentFolder { get; set; }
        public bool IsTopFavorite => ParentFolder == null;
        public string Name { get; set; }
        public string Text { get; set; }
        public List<Folder> Favorites { get => _Fav; set => _Fav = value; }

        public string outXml
        {
            get
            {
                bool isNotFolder = (this is Favorite);
                string x = "<" + (isNotFolder ? "Favorite" : "Folder") + " Name=\"" + Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" Text=\"" + Text.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\"";
                if (isNotFolder)
                {
                    Favorite favorite = this as Favorite;
                    x += " Url=\"" + favorite.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" IconPath=\"" + favorite.IconPath.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" />";
                }
                else
                {
                    x += ">" + Environment.NewLine;
                    foreach (Folder y in Favorites)
                    {
                        x += y.outXml + Environment.NewLine;
                    }
                    x += "</Folder>" + Environment.NewLine;
                }
                return x;
            }
        }
    }

    public class Favorite : Folder
    {
        public new List<Folder> Favorites => null;
        public string Url { get; set; }
        public string IconPath { get; set; }
        public Image Icon => HTAlt.Tools.ReadFile(IconPath, "ignored");
    }

    public class FileFolderError
    {
        public FileFolderError(string _Location, Exception _Error, bool IsDirectory)
        {
            isDirectory = IsDirectory;
            Location = _Location;
            Error = _Error;
        }

        public bool isDirectory { get; set; }
        public string Location { get; set; }
        public Exception Error { get; set; }
    }

    /// <summary>
    /// Global variable used in <see cref="LanguageItem"/>.
    /// </summary>
    public class LanguageGlobalVar
    {
        /// <summary>
        /// Creates a new <see cref="LanguageGlobalVar"/>.
        /// </summary>
        /// <param name="name">Name of new <see cref="LanguageGlobalVar"/>.</param>
        /// <param name="textOrCondition">Text or Condition of new <see cref="LanguageGlobalVar"/>.</param>
        /// <param name="isCondition"><c>true</c> if new <see cref="LanguageGlobalVar"/> is a <see cref="VarType.Conditioned"/>, otherwise <c>false</c>.</param>
        public LanguageGlobalVar(string name, string textOrCondition, bool isCondition = false)
        {
            Name = name;
            if (isCondition)
            {
                ConditionString = textOrCondition;
                Type = VarType.Conditioned;
            }
            else
            {
                Type = VarType.Normal;
                Text = textOrCondition;
            }
        }
        /// <summary>
        /// Types of <see cref="LanguageGlobalVar"/>.
        /// </summary>
        public enum VarType
        {
            Normal,
            Changeable,
            Conditioned
        }
        /// <summary>
        /// Creates a new <see cref="LanguageGlobalVar"/> with <see cref="Type"/> being <see cref="VarType.Changeable"/>.
        /// </summary>
        /// <param name="name">Name of <see cref="LanguageGlobalVar"/>.</param>
        /// <param name="defaultVal">Default value of <see cref="LanguageGlobalVar"/>.</param>
        public LanguageGlobalVar(string name, string defaultVal = "")
        {
            Type = VarType.Changeable;
            Text = defaultVal;
            Name = name;
        }
        /// <summary>
        /// Type of <see cref="LanguageGlobalVar"/>.
        /// </summary>
        public VarType Type { get; set; }

        /// <summary>
        /// nbame of <see cref="LanguageGlobalVar"/>.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Text or value of <see cref="LanguageGlobalVar"/>.
        /// </summary>
        public string Text { get; set; }
        /// <summary>
        /// Condition of <see cref="LanguageGlobalVar"/>. Used by <see cref="Condition"/>.
        /// </summary>
        public string ConditionString { get; set; }
        /// <summary>
        /// Returns a <see cref="string"/> result of <see cref="ConditionString"/>.
        /// </summary>
        public string Condition
        {
            get 
            {
                return "[EXPERIMENTAL] CONDITION: " + ConditionString;
            }
        }
    }
    public class LanguageSystem
    {
        public List<LanguageGlobalVar> GlobalVars { get; set; } = new List<LanguageGlobalVar>();
        public List<LanguageItem> LanguageItems { get; set; } = new List<LanguageItem>();

        public string GetItemText(string ID)
        {
            LanguageItem item = LanguageItems.Find(i => i.ID.Trim() == ID.Trim());
            if (item == null)
            {
                Output.WriteLine(" [Language] Missing Item [ID=\"" + ID + "\" LangFile=\"" + LangFile + "\" ItemCount=\"" + LanguageItems.Count + "\"]");
                return "[MI] " + ID;
            }
            else
            {
                string itemText = item.Text;
                for(int i = 0; i < GlobalVars.Count;i++)
                {
                    LanguageGlobalVar globalVar = GlobalVars[i];
                    if (globalVar.Type == LanguageGlobalVar.VarType.Conditioned) 
                    {
                        itemText = KorotTools.RuleifyString(itemText, globalVar.Name, globalVar.Condition);
                    }
                    else
                    {
                        itemText = KorotTools.RuleifyString(itemText, globalVar.Name, string.IsNullOrEmpty(globalVar.Text) ? "" : globalVar.Text);
                    }
                }
                return itemText;
            }
        }

        public int ItemCount => LanguageItems.Count;
        public string LangFile { get; private set; } = Application.StartupPath + "\\Lang\\English.klf";
        public Settings Settings { get; set; } = null;
        public string Name { get; set; }

        public LanguageSystem(string fileLoc, Settings settings)
        {
            Settings = settings;
            ReadFromFile(!string.IsNullOrWhiteSpace(fileLoc) ? fileLoc : LangFile, true);
        }

        public void ForceReadFromFile(string fileLoc, bool clear = true)
        {
            LangFile = fileLoc;
            string code = HTAlt.Tools.ReadFile(fileLoc, Encoding.Unicode);
            ReadCode(code, clear);
        }

        public void ReadFromFile(string fileLoc, bool clear = true)
        {
            if (LangFile != fileLoc || LanguageItems.Count == 0)
            {
                ForceReadFromFile(fileLoc, clear);
            }
        }

        private void LoadDefaultGlobalVars()
        {
            GlobalVars.Add(new LanguageGlobalVar("NEWLINE", Environment.NewLine, false));
            GlobalVars.Add(new LanguageGlobalVar("CODENAME", VersionInfo.CodeName, false));
            GlobalVars.Add(new LanguageGlobalVar("VERSIONNO", "" + VersionInfo.VersionNumber, false));
            GlobalVars.Add(new LanguageGlobalVar("VERSION", Application.ProductVersion, false));
        }
        public void ReadCode(string xmlCode, bool clear = true)
        {
            if (clear) 
            { 
                LanguageItems.Clear(); 
                GlobalVars.Clear();
                LoadDefaultGlobalVars();
            }
            XmlDocument document = new XmlDocument();
            document.LoadXml(xmlCode);
            XmlNode rootNode = document.FirstChild;
            if (rootNode.Name == "Language")
            {
                Name = rootNode.Attributes["Name"] != null ? rootNode.Attributes["Name"].Value : Path.GetFileNameWithoutExtension(LangFile);
                if (rootNode.Attributes["CompatibleVersion"] != null)
                {
                    int compVersion = Convert.ToInt32(rootNode.Attributes["CompatibleVersion"].Value);
                    if (compVersion > VersionInfo.VersionNumber && LangFile != Application.StartupPath + "\\Lang\\English.klf")
                    {
                        HTMsgBox msgbox = new HTMsgBox("Korot", "This language file is not compatible with your Korot version."
                            + Environment.NewLine
                            + Environment.NewLine
                            + "Language File Compatible Version: "
                            + rootNode.Attributes["CompatibleVersion"].Value
                            + Environment.NewLine
                            + "Your Korot Version: "
                            + Application.ProductVersion.ToString()
                            + Environment.NewLine
                            + Environment.NewLine
                            + "Would you still want to continue?", new HTDialogBoxContext(MessageBoxButtons.YesNoCancel))
                        {
                            BackColor = (Settings != null ? Settings.Theme.BackColor : Color.White),
                            ForeColor = (Settings != null ? Settings.Theme.ForeColor : Color.Black),
                            Yes = "Yes",
                            No = "No",
                            Cancel = "Cancel",
                            AutoForeColor = false,
                            Icon = Properties.Resources.KorotIcon
                        };
                        DialogResult result = msgbox.ShowDialog();
                        if (result != DialogResult.Yes)
                        {
                            return;
                        }
                    }
                    foreach (XmlNode node in rootNode.ChildNodes)
                    {
                        if (node.Name.ToLowerInvariant() == "globalvariables")
                        {
                            foreach(XmlNode subnode in node.ChildNodes)
                            {
                                if (subnode.Name.ToLowerInvariant() == "globalvar")
                                {
                                    if (subnode.Attributes["ID"] != null && subnode.Attributes["Text"] != null && subnode.Attributes["Condition"] == null && subnode.Attributes["Default"] == null)
                                    {
                                        GlobalVars.Add(new LanguageGlobalVar(subnode.Attributes["ID"].Value, subnode.Attributes["Text"].Value, false));
                                    }
                                    else if (subnode.Attributes["ID"] != null && subnode.Attributes["Condition"] != null && subnode.Attributes["Default"] == null && subnode.Attributes["Text"] == null)
                                    {
                                        GlobalVars.Add(new LanguageGlobalVar(subnode.Attributes["ID"].Value, subnode.Attributes["Condition"].Value, true));
                                    }
                                    else if (subnode.Attributes["ID"] != null && subnode.Attributes["Condition"] == null && subnode.Attributes["Default"] != null && subnode.Attributes["Text"] == null)
                                    {
                                        GlobalVars.Add(new LanguageGlobalVar(subnode.Attributes["ID"].Value, subnode.Attributes["Default"].Value));
                                    }
                                }
                            }
                        }
                        else if (node.Name.ToLowerInvariant() == "translate")
                        {
                            string id = node.Attributes["ID"] != null ? node.Attributes["ID"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\"") : HTAlt.Tools.GenerateRandomText(12);
                            string text = node.Attributes["Text"] != null ? node.Attributes["Text"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\"") : id;
                            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(text))
                            {
                                LanguageItems.Add(new LanguageItem() { ID = id, Text = text });
                            }
                        }
                    }
                }
            }
        }
    }

    public class LanguageItem
    {
        public string ID { get; set; }
        public string Text { get; set; }
    }

    public class NewTabSites
    {
        public string XMLOut => "<NewTabMenu>" + Environment.NewLine +
                   (FavoritedSite0 != null ? "<Attached0 Name=\"" + FavoritedSite0.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" Url=\"" + FavoritedSite0.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" />" + Environment.NewLine : "") +
                   (FavoritedSite1 != null ? "<Attached1 Name=\"" + FavoritedSite1.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" Url=\"" + FavoritedSite1.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" />" + Environment.NewLine : "") +
                   (FavoritedSite2 != null ? "<Attached2 Name=\"" + FavoritedSite2.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" Url=\"" + FavoritedSite2.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" />" + Environment.NewLine : "") +
                   (FavoritedSite3 != null ? "<Attached3 Name=\"" + FavoritedSite3.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" Url=\"" + FavoritedSite3.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" />" + Environment.NewLine : "") +
                   (FavoritedSite4 != null ? "<Attached4 Name=\"" + FavoritedSite4.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" Url=\"" + FavoritedSite4.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" />" + Environment.NewLine : "") +
                   (FavoritedSite5 != null ? "<Attached5 Name=\"" + FavoritedSite5.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" Url=\"" + FavoritedSite5.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" />" + Environment.NewLine : "") +
                   (FavoritedSite6 != null ? "<Attached6 Name=\"" + FavoritedSite6.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" Url=\"" + FavoritedSite6.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" />" + Environment.NewLine : "") +
                   (FavoritedSite7 != null ? "<Attached7 Name=\"" + FavoritedSite7.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" Url=\"" + FavoritedSite7.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" />" + Environment.NewLine : "") +
                   (FavoritedSite8 != null ? "<Attached8 Name=\"" + FavoritedSite8.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" Url=\"" + FavoritedSite8.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" />" + Environment.NewLine : "") +
                   (FavoritedSite9 != null ? "<Attached9 Name=\"" + FavoritedSite9.Name.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" Url=\"" + FavoritedSite9.Url.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" />" + Environment.NewLine : "") +
                    "</NewTabMenu>";

        public NewTabSites(string xmlCode)
        {
            if (string.IsNullOrWhiteSpace(xmlCode))
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(xmlCode))
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(xmlCode);
                foreach (XmlNode node in document.FirstChild.ChildNodes)
                {
                    if (node.Name.ToLowerInvariant() == "attached0")
                    {
                        if (node.Attributes["Name"] == null || node.Attributes["Url"] == null) { return; }
                        FavoritedSite0 = new Site
                        {
                            Name = node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\""),
                            Url = node.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\"")
                        };
                    }
                    else if (node.Name.ToLowerInvariant() == "attached1")
                    {
                        if (node.Attributes["Name"] == null || node.Attributes["Url"] == null) { return; }
                        FavoritedSite1 = new Site
                        {
                            Name = node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\""),
                            Url = node.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\"")
                        };
                    }
                    else if (node.Name.ToLowerInvariant() == "attached2")
                    {
                        if (node.Attributes["Name"] == null || node.Attributes["Url"] == null) { return; }
                        FavoritedSite2 = new Site
                        {
                            Name = node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\""),
                            Url = node.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\"")
                        };
                    }
                    else if (node.Name.ToLowerInvariant() == "attached3")
                    {
                        if (node.Attributes["Name"] == null || node.Attributes["Url"] == null) { return; }
                        FavoritedSite3 = new Site
                        {
                            Name = node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\""),
                            Url = node.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\"")
                        };
                    }
                    else if (node.Name.ToLowerInvariant() == "attached4")
                    {
                        if (node.Attributes["Name"] == null || node.Attributes["Url"] == null) { return; }
                        FavoritedSite4 = new Site
                        {
                            Name = node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\""),
                            Url = node.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\"")
                        };
                    }
                    else if (node.Name.ToLowerInvariant() == "attached5")
                    {
                        if (node.Attributes["Name"] == null || node.Attributes["Url"] == null) { return; }
                        FavoritedSite5 = new Site
                        {
                            Name = node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\""),
                            Url = node.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\"")
                        };
                    }
                    else if (node.Name.ToLowerInvariant() == "attached6")
                    {
                        if (node.Attributes["Name"] == null || node.Attributes["Url"] == null) { return; }
                        FavoritedSite6 = new Site
                        {
                            Name = node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\""),
                            Url = node.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\"")
                        };
                    }
                    else if (node.Name.ToLowerInvariant() == "attached7")
                    {
                        if (node.Attributes["Name"] == null || node.Attributes["Url"] == null) { return; }
                        FavoritedSite7 = new Site
                        {
                            Name = node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\""),
                            Url = node.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\"")
                        };
                    }
                    else if (node.Name.ToLowerInvariant() == "attached8")
                    {
                        if (node.Attributes["Name"] == null || node.Attributes["Url"] == null) { return; }
                        FavoritedSite8 = new Site
                        {
                            Name = node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\""),
                            Url = node.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\"")
                        };
                    }
                    else if (node.Name.ToLowerInvariant() == "attached9")
                    {
                        if (node.Attributes["Name"] == null || node.Attributes["Url"] == null) { return; }
                        FavoritedSite9 = new Site
                        {
                            Name = node.Attributes["Name"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\""),
                            Url = node.Attributes["Url"].Value.Replace("&amp;", "&").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&apos;", "'").Replace("&quot;", "\"")
                        };
                    }
                }
            }
        }

        public string SiteToHTMLData(Site site)
        {
            string x = "<a href=\"" + site.Url + "\" style=\"background-color: §BACKCOLOR2§; color: §FORECOLOR§;\">" + site.Name + "</a>" +
    "</br>" +
   "<a href=\"" + site.Url + "\" style=\"background-color: §BACKCOLOR2§; color: §FORECOLOR§;font-size: 0.5em;\">" + (site.Url.Length > 30 ? site.Url.Substring(0, 30) : site.Url) + "</a>";
            return x;
        }

        public Site FavoritedSite0 { get; set; }
        public Site FavoritedSite1 { get; set; }
        public Site FavoritedSite2 { get; set; }
        public Site FavoritedSite3 { get; set; }
        public Site FavoritedSite4 { get; set; }
        public Site FavoritedSite5 { get; set; }
        public Site FavoritedSite6 { get; set; }
        public Site FavoritedSite7 { get; set; }
        public Site FavoritedSite8 { get; set; }
        public Site FavoritedSite9 { get; set; }
    }

    public class Proxy
    {
        public string ID { get; set; }
        public string Address { get; set; }
        public Exception Exception { get; set; }
    }

    public class BlockSite
    {
        public string Address { get; set; }
        public string Filter { get; set; }
        public int BlockLevel { get; set; }
    }

    public class KorotTools
    {
        public static void CreateThemes()
        {
            Theme dark = DefaultThemes.KorotDark(null);
            Theme light = DefaultThemes.KorotLight(null);
            Theme mnight = DefaultThemes.KorotMidnight(null);
            dark.SaveTheme();
            light.SaveTheme();
            mnight.SaveTheme();
            Properties.Resources.kdark.Save(dark.PreviewLocation);
            Properties.Resources.klight.Save(light.PreviewLocation);
            Properties.Resources.kmidnight.Save(mnight.PreviewLocation);
        }
        public static string RuleifyString(string main, string rule,string replaceWith)
        {
            string ignored = "§IGNORED_" + HTAlt.Tools.GenerateRandomText(12) + "§";
            main = main.Replace("![" + rule.ToUpper() + "]", ignored);
            main = main.Replace("[" + rule.ToUpper() + "]", replaceWith);
            main = main.Replace(ignored,"[" + rule.ToUpper() + "]");
            return main;
        }
        public static bool isKorotDefaultBrowser()
        {
            const string userChoice = @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice";
            string progId;
            using (RegistryKey userChoiceKey = Registry.CurrentUser.OpenSubKey(userChoice))
            {
                if (userChoiceKey == null)
                {
                    return false;
                }
                object progIdValue = userChoiceKey.GetValue("Progid");
                if (progIdValue == null)
                {
                    return false;
                }
                progId = progIdValue.ToString();
                return progId.ToLowerInvariant() == "korot";
            }
        }
        public static string getOSInfo()
        {
            string fullName = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            //We only need the version number or name like 7,Vista,10
            //Remove any other unnecesary thing.
            fullName = fullName.Replace("Microsoft Windows", "")
                .Replace(" (PRODUCT) RED", "")
                .Replace(" Business", "")
                .Replace(" Education", "")
                .Replace(" Embedded", "")
                .Replace(" Enterprise LTSC", "")
                .Replace(" Enterprise", "")
                .Replace(" Home Basic", "")
                .Replace(" Home Premium", "")
                .Replace(" Home", "")
                .Replace(" Insider", "")
                .Replace(" IoT Core", "")
                .Replace(" IoT", "")
                .Replace(" KN", "")
                .Replace(" Media Center 2002", "")
                .Replace(" Media Center 2004", "")
                .Replace(" Media Center 2005", "")
                .Replace(" Mobile Enterprise", "")
                .Replace(" Mobile", "")
                .Replace(" N", "")
                .Replace(" Pro Education", "")
                .Replace(" Pro for Workstations", "")
                .Replace(" Professional x64", "")
                .Replace(" Professional", "")
                .Replace(" Pro", "")
                .Replace(" Signature Edition", "")
                .Replace(" Single Language", "")
                .Replace(" Starter", "")
                .Replace(" S", "")
                .Replace(" Tablet PC", "")
                .Replace(" Team", "")
                .Replace(" Ultimate", "")
                .Replace(" VL", "")
                .Replace(" X", "")
                .Replace(" with Bing", "")
                .Replace(" ", "");

            switch (fullName)
            {
                case "XP":
                    return "NT 5.1";

                case "Vista":
                    return "NT 6.0";

                case "7":
                    return "NT 6.1";

                default:
                    return "NT " + fullName;
            }
        }

        public static long GetDirectorySize(string p)
        {
            // 1.
            // Get array of all file names.
            string[] a = Directory.GetFiles(p, "*.*");

            // 2.
            // Calculate total bytes of all files in a loop.
            long b = 0;
            foreach (string name in a)
            {
                // 3.
                // Use FileInfo to get length of each file.
                FileInfo info = new FileInfo(name);
                b += info.Length;
            }
            // 4.
            // Return total size
            return b;
        }

        public static bool isNonRedirectKorotPage(string Url)
        {
            return (Url.ToLowerInvariant().StartsWith("korot://newtab")
                || Url.ToLowerInvariant().StartsWith("korot://links")
                || Url.ToLowerInvariant().StartsWith("korot://license")
                || Url.ToLowerInvariant().StartsWith("korot://incognito")
                || Url.ToLowerInvariant().StartsWith("korot://command")
                || Url.ToLowerInvariant().StartsWith("korot://test")
                || Url.ToLowerInvariant().StartsWith("korot://technical"));
        }

        public static bool createFolders()
        {
            if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\")) { Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\"); }
            if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\")) { Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\"); }
            if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\Themes\\")) { Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\Themes\\"); }
            if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\Extensions\\")) { Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\Extensions\\"); }
            if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\Logs\\")) { Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\Logs\\"); }
            if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\IconStorage\\")) { Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\IconStorage\\"); }
            if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\Scripts\\")) { Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\Scripts\\"); }
            return true;
        }

        public static bool ValidHttpURL(string s)
        {
            //string Pattern = @"^((http(s)?|korot|file|pop|smtp|ftp|chrome|about):(\/\/)?)|(^([\w.-]+(?:\.[\w\.-]+)+[\w\-\._~:/?#[\]@!\$&'\(\)\*\+,;=.]+$))|(.{1,4}\:.{1,4}\:.{1,4}\:.{1,4}\:.{1,4}\:.{1,4}\:.{1,4}\:.{1,4})";
            //Regex Rgx = new Regex(Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            //return Rgx.IsMatch(s);
            return HTAlt.Tools.ValidUrl(s, new string[] { "korot" }, false);
        }



        public static string GetUserAgent()
        {
            return "Mozilla/5.0 ( Windows "
                + KorotTools.getOSInfo()
                + "; "
                + (Environment.Is64BitProcess ? "Win64" : "Win32NT") + "; " + (Environment.Is64BitProcess ? "x64" : "x86")
                + ") AppleWebKit/537.36 (KHTML, like Gecko) Chrome/"
                + Cef.ChromiumVersion
                + " Safari/537.36 Korot/"
                + VersionInfo.Version
                + " [" + VersionInfo.CodeName + "]";
        }

        public static bool FixDefaultLanguage()
        {
            if (!Directory.Exists(Application.StartupPath + "\\Lang\\"))
            {
                Directory.CreateDirectory(Application.StartupPath + "\\Lang\\");
            }
            HTAlt.Tools.WriteFile(Application.StartupPath + "\\Lang\\English.klf", Properties.Resources.English);
            Tools.WriteFile(Application.StartupPath + "\\Lang\\Türkçe.klf", Properties.Resources.Türkçe);
            return true;
        }
    }

    public class SessionSystem
    {
        public SessionSystem(string XMLCode)
        {
            if (!string.IsNullOrWhiteSpace(XMLCode))
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(XMLCode);
                XmlNode workNode = document.FirstChild;
                if (document.FirstChild.Name.ToLowerInvariant() == "xml") { workNode = document.FirstChild.NextSibling; }
                if (workNode.Attributes["Index"] != null)
                {
                    int si = Convert.ToInt32(workNode.Attributes["Index"].Value);
                    foreach (XmlNode node in workNode.ChildNodes)
                    {
                        if (node.Name.ToLowerInvariant() == "sessionsite")
                        {
                            if (node.Attributes["Url"] != null && node.Attributes["Title"] != null)
                            {
                                Sessions.Add(new Session(node.Attributes["Url"].Value, node.Attributes["Tİtle"].Value));
                            }
                        }
                    }
                    SelectedIndex = si;
                    SelectedSession = Sessions[si];
                }
            }
        }

        public SessionSystem() : this("")
        {
        }

        public string XmlOut()
        {
            string x = "<Session Index=\"" + SelectedIndex + "\" >" + Environment.NewLine;
            for (int i = 0; i < Sessions.Count; i++)
            {
                x += "<SessionSite Url=\"" + Sessions[i].Url + "\" Title=\"" + Sessions[i].Title + "\" >" + Environment.NewLine;
            }
            return x + "</Session>";
        }

        public List<Session> Sessions { get; set; } = new List<Session>();

        public bool SkipAdd = false;

        public void GoBack(ChromiumWebBrowser browser)
        {
            if (CanGoBack())
            {
                SkipAdd = true;
                SelectedIndex -= 1;
                SelectedSession = Sessions[SelectedIndex];
                browser.Invoke(new Action(() => browser.Load(SelectedSession.Url)));
            }
        }

        public void GoForward(ChromiumWebBrowser browser)
        {
            if (CanGoForward())
            {
                SkipAdd = true;
                SelectedIndex += 1;
                SelectedSession = Sessions[SelectedIndex];
                browser.Invoke(new Action(() => browser.Load(SelectedSession.Url)));
            }
        }

        public Session SessionInIndex(int Index)
        {
            return Sessions[Index];
        }

        public Session SelectedSession { get; set; }
        public int SelectedIndex { get; set; }

        public void MoveTo(int i, ChromiumWebBrowser browser)
        {
            if (browser is null)
            {
                throw new ArgumentNullException("\"browser\" was null");
            }
            if (i >= 0 && i < Sessions.Count)
            {
                SkipAdd = true;
                SelectedIndex = i;
                SelectedSession = Sessions[i];
                browser.Load(SelectedSession.Url);
            }
            else
            {
                throw new ArgumentOutOfRangeException("\"i\" was bigger than Sessions.Count or smaller than 0. [i=\"" + i + "\" Count=\"" + Sessions.Count + "\"]");
            }
        }

        public void Add(string url, string title)
        {
            Add(new Session(url, title));
        }

        public void Add(Session Session)
        {
            if (Session is null)
            {
                throw new ArgumentNullException("\"Session\" was null.");
            }
            if (Session.Url.ToLowerInvariant().StartsWith("korot") && (!KorotTools.isNonRedirectKorotPage(Session.Url)))
            {
                return;
            }
            if (SkipAdd) { SkipAdd = false; return; }
            if (CanGoForward() && SelectedIndex + 1 < Sessions.Count)
            {
                if (!Session.Equals(Sessions[SelectedIndex]))
                {
                    Console.WriteLine("Session Not Equal: 1:" + Session.Url + " 2:" + Sessions[SelectedIndex].Url);
                    Session[] RemoveThese = After();
                    for (int i = 0; i < RemoveThese.Length; i++)
                    {
                        Sessions.Remove(RemoveThese[i]);
                    }
                    if (Sessions.Count > 0)
                    {
                        if (Sessions[Sessions.Count - 1].Url != Session.Url)
                        {
                            Sessions.Add(Session);
                        }
                    }
                    else
                    {
                        Sessions.Add(Session);
                    }
                }
            }
            else
            {
                if (Sessions.Count > 0)
                {
                    if (Sessions[Sessions.Count - 1].Url != Session.Url)
                    {
                        Sessions.Add(Session);
                    }
                }
                else
                {
                    Sessions.Add(Session);
                }
            }
            if (Sessions.Count > 0)
            {
                if (Sessions[Sessions.Count - 1].Url != Session.Url)
                {
                    SelectedSession = Session;
                    SelectedIndex = Sessions.IndexOf(Session);
                }
                else
                {
                    SelectedSession = Sessions[Sessions.Count - 1];
                    SelectedIndex = Sessions.Count - 1;
                }
            }
            else
            {
                Sessions.Add(Session);
            }
        }

        public bool CanGoBack()
        {
            return CanGoBack(SelectedSession);
        }

        public bool CanGoBack(Session Session)
        {
            if (Session is null)
            {
                return false;
            }
            if (!Sessions.Contains(Session))
            {
                throw new ArgumentOutOfRangeException("Cannot find Session[Url=\"" + (Session.Url == null ? "null" : Session.Url) + "\" Title=\"" + (Session.Title == null ? "null" : Session.Title) + "\"].");
            }
            int current = Sessions.IndexOf(Session);
            return current > 0;
        }

        public bool CanGoForward()
        {
            return CanGoForward(SelectedSession);
        }

        public bool CanGoForward(Session Session)
        {
            if (Session is null)
            {
                return false;
            }
            if (!Sessions.Contains(Session))
            {
                throw new ArgumentOutOfRangeException("Cannot find Session[Url=\"" + (Session.Url == null ? "null" : Session.Url) + "\" Title=\"" + (Session.Title == null ? "null" : Session.Title) + "\"].");
            }

            int current = Sessions.IndexOf(Session) + 1;
            return current < Sessions.Count;
        }

        public Session[] Before()
        {
            return Before(SelectedSession);
        }

        public Session[] Before(Session Session)
        {
            if (Session is null)
            {
                return new Session[] { };
            }
            if (!Sessions.Contains(Session))
            {
                throw new ArgumentOutOfRangeException("Cannot find Session[Url=\"" + (Session.Url == null ? "null" : Session.Url) + "\" Title=\"" + (Session.Title == null ? "null" : Session.Title) + "\"].");
            }
            int current = Sessions.IndexOf(Session);
            List<Session> fs = new List<Session>();
            for (int i = 0; i < current; i++)
            {
                fs.Add(Sessions[i]);
            }
            return fs.ToArray();
        }

        public Session[] After()
        {
            return After(SelectedSession);
        }

        public Session[] After(Session Session)
        {
            if (Session is null)
            {
                return new Session[] { };
            }
            if (!Sessions.Contains(Session))
            {
                throw new ArgumentOutOfRangeException("Cannot find Session[Url=\"" + (Session.Url == null ? "null" : Session.Url) + "\" Title=\"" + (Session.Title == null ? "null" : Session.Title) + "\"].");
            }
            int current = Sessions.IndexOf(Session) + 1;
            List<Session> fs = new List<Session>();
            for (int i = current; i < Sessions.Count; i++)
            {
                fs.Add(Sessions[i]);
            }
            return fs.ToArray();
        }
    }

    public class Session
    {
        public override bool Equals(object obj)
        {
            return obj is Session session && Url == session.Url;
        }

        public override int GetHashCode()
        {
            return -1915121810 + EqualityComparer<string>.Default.GetHashCode(Url);
        }

        public Session(string _Url, string _Title)
        {
            Url = _Url;
            Title = _Title;
        }

        public Session() : this("", "")
        {
        }

        public Session(string _Url) : this(_Url, _Url)
        {
        }

        public string Url { get; set; }
        public string Title { get; set; }
    }

    public class AutoCleaner
    {
        public AutoCleaner(string XMLCode)
        {
            //Defaults
            CleanCache = false;
            CleanDownloads = false;
            CleanHistory = false;
            CleanLogs = false;
            CleanCacheFile = false;
            CacheFileSize = 5;
            CleanCacheDaily = false;
            CleanCacheDay = 30;
            LatestCacheCleanup = DateTime.Now.ToString("dd/MM/yyyy");
            CleanHistoryDaily = false;
            CleanHistoryDay = 30;
            CleanHistoryFile = false;
            HistoryFileSize = 5;
            CleanOldHistory = false;
            OldHistoryDay = 30;
            LatestHistoryCleanup = DateTime.Now.ToString("dd/MM/yyyy");
            CleanLogsDaily = false;
            CleanLogsDay = 30;
            CleanLogsFile = false;
            LogsFileSize = 5;
            CleanOldLogs = false;
            OldLogsDay = 30;
            LatestLogsCleanup = DateTime.Now.ToString("dd/MM/yyyy");
            CleanDownloadsDaily = false;
            CleanDownloadsDay = 30;
            CleanDownloadsFile = false;
            DownloadsFileSize = 5;
            CleanOldDownloads = false;
            OldDownloadsDay = 30;
            LatestDownloadsCleanup = DateTime.Now.ToString("dd/MM/yyyy");
            //Load XML
            if (!string.IsNullOrWhiteSpace(XMLCode))
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(XMLCode);
                foreach (XmlNode node in doc.FirstChild.ChildNodes)
                {
                    if (node.Name == "Cache")
                    {
                        if (node.Attributes["Clean"] != null && node.Attributes["FileSize"] != null && node.Attributes["Size"] != null && node.Attributes["Daily"] != null && node.Attributes["Day"] != null && node.Attributes["Latest"] != null)
                        {
                            CleanCache = node.Attributes["Clean"].Value == "true";
                            CleanCacheFile = node.Attributes["FileSize"].Value == "true";
                            CacheFileSize = Convert.ToInt32(node.Attributes["Size"].Value);
                            CleanCacheDaily = node.Attributes["Daily"].Value == "true";
                            CleanCacheDay = Convert.ToInt32(node.Attributes["Day"].Value);
                            LatestCacheCleanup = node.Attributes["Latest"].Value;
                        }
                    }
                    else if (node.Name == "Logs")
                    {
                        if (node.Attributes["Clean"] != null && node.Attributes["FileSize"] != null && node.Attributes["Size"] != null && node.Attributes["Daily"] != null && node.Attributes["Day"] != null && node.Attributes["Latest"] != null && node.Attributes["Old"] != null && node.Attributes["OldDay"] != null)
                        {
                            CleanLogs = node.Attributes["Clean"].Value == "true";
                            CleanLogsFile = node.Attributes["FileSize"].Value == "true";
                            LogsFileSize = Convert.ToInt32(node.Attributes["Size"].Value);
                            CleanLogsDaily = node.Attributes["Daily"].Value == "true";
                            CleanLogsDay = Convert.ToInt32(node.Attributes["Day"].Value);
                            CleanOldLogs = node.Attributes["Old"].Value == "true";
                            OldLogsDay = Convert.ToInt32(node.Attributes["OldDays"].Value);
                            LatestLogsCleanup = node.Attributes["Latest"].Value;
                        }
                    }
                    else if (node.Name == "History")
                    {
                        if (node.Attributes["Clean"] != null && node.Attributes["FileSize"] != null && node.Attributes["Size"] != null && node.Attributes["Daily"] != null && node.Attributes["Day"] != null && node.Attributes["Latest"] != null && node.Attributes["Old"] != null && node.Attributes["OldDay"] != null)
                        {
                            CleanHistory = node.Attributes["Clean"].Value == "true";
                            CleanHistoryFile = node.Attributes["FileSize"].Value == "true";
                            HistoryFileSize = Convert.ToInt32(node.Attributes["Size"].Value);
                            CleanHistoryDaily = node.Attributes["Daily"].Value == "true";
                            CleanHistoryDay = Convert.ToInt32(node.Attributes["Day"].Value);
                            CleanOldHistory = node.Attributes["Old"].Value == "true";
                            OldHistoryDay = Convert.ToInt32(node.Attributes["OldDays"].Value);
                            LatestHistoryCleanup = node.Attributes["Latest"].Value;
                        }
                    }
                    else if (node.Name == "Downloads")
                    {
                        if (node.Attributes["Clean"] != null && node.Attributes["FileSize"] != null && node.Attributes["Size"] != null && node.Attributes["Daily"] != null && node.Attributes["Day"] != null && node.Attributes["Latest"] != null && node.Attributes["Old"] != null && node.Attributes["OldDay"] != null)
                        {
                            CleanDownloads = node.Attributes["Clean"].Value == "true";
                            CleanDownloadsFile = node.Attributes["FileSize"].Value == "true";
                            DownloadsFileSize = Convert.ToInt32(node.Attributes["Size"].Value);
                            CleanDownloadsDaily = node.Attributes["Daily"].Value == "true";
                            CleanDownloadsDay = Convert.ToInt32(node.Attributes["Day"].Value);
                            CleanOldDownloads = node.Attributes["Old"].Value == "true";
                            OldDownloadsDay = Convert.ToInt32(node.Attributes["OldDays"].Value);
                            LatestDownloadsCleanup = node.Attributes["Latest"].Value;
                        }
                    }
                }
            }
        }

        public void LoadFromXML(string XMLCode)
        {
            //Defaults
            CleanCache = false;
            CleanDownloads = false;
            CleanHistory = false;
            CleanLogs = false;
            CleanCacheFile = false;
            CacheFileSize = 5;
            CleanCacheDaily = false;
            CleanCacheDay = 30;
            LatestCacheCleanup = DateTime.Now.ToString("dd/MM/yyyy");
            CleanHistoryDaily = false;
            CleanHistoryDay = 30;
            CleanHistoryFile = false;
            HistoryFileSize = 5;
            CleanOldHistory = false;
            OldHistoryDay = 30;
            LatestHistoryCleanup = DateTime.Now.ToString("dd/MM/yyyy");
            CleanLogsDaily = false;
            CleanLogsDay = 30;
            CleanLogsFile = false;
            LogsFileSize = 5;
            CleanOldLogs = false;
            OldLogsDay = 30;
            LatestLogsCleanup = DateTime.Now.ToString("dd/MM/yyyy");
            CleanDownloadsDaily = false;
            CleanDownloadsDay = 30;
            CleanDownloadsFile = false;
            DownloadsFileSize = 5;
            CleanOldDownloads = false;
            OldDownloadsDay = 30;
            LatestDownloadsCleanup = DateTime.Now.ToString("dd/MM/yyyy");
            //Load XML
            if (!string.IsNullOrWhiteSpace(XMLCode))
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(XMLCode);
                foreach (XmlNode node in doc.FirstChild.ChildNodes)
                {
                    if (node.Name == "Cache")
                    {
                        if (node.Attributes["Clean"] != null && node.Attributes["FileSize"] != null && node.Attributes["Size"] != null && node.Attributes["Daily"] != null && node.Attributes["Day"] != null && node.Attributes["Latest"] != null)
                        {
                            CleanCache = node.Attributes["Clean"].Value == "true";
                            CleanCacheFile = node.Attributes["FileSize"].Value == "true";
                            CacheFileSize = Convert.ToInt32(node.Attributes["Size"].Value);
                            CleanCacheDaily = node.Attributes["Daily"].Value == "true";
                            CleanCacheDay = Convert.ToInt32(node.Attributes["Day"].Value);
                            LatestCacheCleanup = node.Attributes["Latest"].Value;
                        }
                    }
                    else if (node.Name == "Logs")
                    {
                        if (node.Attributes["Clean"] != null && node.Attributes["FileSize"] != null && node.Attributes["Size"] != null && node.Attributes["Daily"] != null && node.Attributes["Day"] != null && node.Attributes["Latest"] != null && node.Attributes["Old"] != null && node.Attributes["OldDay"] != null)
                        {
                            CleanLogs = node.Attributes["Clean"].Value == "true";
                            CleanLogsFile = node.Attributes["FileSize"].Value == "true";
                            LogsFileSize = Convert.ToInt32(node.Attributes["Size"].Value);
                            CleanLogsDaily = node.Attributes["Daily"].Value == "true";
                            CleanLogsDay = Convert.ToInt32(node.Attributes["Day"].Value);
                            CleanOldLogs = node.Attributes["Old"].Value == "true";
                            OldLogsDay = Convert.ToInt32(node.Attributes["OldDays"].Value);
                            LatestLogsCleanup = node.Attributes["Latest"].Value;
                        }
                    }
                    else if (node.Name == "History")
                    {
                        if (node.Attributes["Clean"] != null && node.Attributes["FileSize"] != null && node.Attributes["Size"] != null && node.Attributes["Daily"] != null && node.Attributes["Day"] != null && node.Attributes["Latest"] != null && node.Attributes["Old"] != null && node.Attributes["OldDay"] != null)
                        {
                            CleanHistory = node.Attributes["Clean"].Value == "true";
                            CleanHistoryFile = node.Attributes["FileSize"].Value == "true";
                            HistoryFileSize = Convert.ToInt32(node.Attributes["Size"].Value);
                            CleanHistoryDaily = node.Attributes["Daily"].Value == "true";
                            CleanHistoryDay = Convert.ToInt32(node.Attributes["Day"].Value);
                            CleanOldHistory = node.Attributes["Old"].Value == "true";
                            OldHistoryDay = Convert.ToInt32(node.Attributes["OldDays"].Value);
                            LatestHistoryCleanup = node.Attributes["Latest"].Value;
                        }
                    }
                    else if (node.Name == "Downloads")
                    {
                        if (node.Attributes["Clean"] != null && node.Attributes["FileSize"] != null && node.Attributes["Size"] != null && node.Attributes["Daily"] != null && node.Attributes["Day"] != null && node.Attributes["Latest"] != null && node.Attributes["Old"] != null && node.Attributes["OldDay"] != null)
                        {
                            CleanDownloads = node.Attributes["Clean"].Value == "true";
                            CleanDownloadsFile = node.Attributes["FileSize"].Value == "true";
                            DownloadsFileSize = Convert.ToInt32(node.Attributes["Size"].Value);
                            CleanDownloadsDaily = node.Attributes["Daily"].Value == "true";
                            CleanDownloadsDay = Convert.ToInt32(node.Attributes["Day"].Value);
                            CleanOldDownloads = node.Attributes["Old"].Value == "true";
                            OldDownloadsDay = Convert.ToInt32(node.Attributes["OldDays"].Value);
                            LatestDownloadsCleanup = node.Attributes["Latest"].Value;
                        }
                    }
                }
            }
        }

        public string XMLOut()
        {
            return "<AutoUpdate>" + Environment.NewLine +
                "<Cache Clean=\"" + (CleanCache ? "true" : "false") + "\" FileSize=\"" + (CleanCacheFile ? "true" : "false") + "\" Size=\"" + CacheFileSize + "\" Daily=\"" + (CleanCacheDaily ? "true" : "false") + "\" Day=\"" + CleanCacheDay + "\" Latest=\"" + LatestCacheCleanup + "\" />" + Environment.NewLine +
                "<History Clean=\"" + (CleanHistory ? "true" : "false") + "\" FileSize=\"" + (CleanHistoryFile ? "true" : "false") + "\" Size=\"" + HistoryFileSize + "\" Daily=\"" + (CleanHistoryDaily ? "true" : "false") + "\" Day=\"" + CleanHistoryDay + "\" Latest=\"" + LatestHistoryCleanup + "\" Old=\"" + (CleanOldHistory ? "true" : "false") + "\" OldDay=\"" + OldHistoryDay + "\" />" + Environment.NewLine +
                "<Downloads Clean=\"" + (CleanDownloads ? "true" : "false") + "\" FileSize=\"" + (CleanDownloadsFile ? "true" : "false") + "\" Size=\"" + DownloadsFileSize + "\" Daily=\"" + (CleanDownloadsDaily ? "true" : "false") + "\" Day=\"" + CleanDownloadsDay + "\" Latest=\"" + LatestDownloadsCleanup + "\" Old=\"" + (CleanOldDownloads ? "true" : "false") + "\" OldDay=\"" + OldDownloadsDay + "\" />" + Environment.NewLine +
                "<Logs Clean=\"" + (CleanLogs ? "true" : "false") + "\" FileSize=\"" + (CleanLogsFile ? "true" : "false") + "\" Size=\"" + LogsFileSize + "\" Daily=\"" + (CleanLogsDaily ? "true" : "false") + "\" Day=\"" + CleanLogsDay + "\" Latest=\"" + LatestLogsCleanup + "\" Old=\"" + (CleanOldLogs ? "true" : "false") + "\" OldDay=\"" + OldLogsDay + "\" />" + Environment.NewLine +
                "</AutoUpdate>";
        }

        public Settings Settings;
        private readonly string DirPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Korot\\" + SafeFileSettingOrganizedClass.LastUser + "\\";
        public bool CleanCache { get; set; }
        public bool CleanDownloads { get; set; }
        public bool CleanHistory { get; set; }
        public bool CleanLogs { get; set; }

        public void DoForceCleanup()
        {
            Directory.Delete(DirPath + "cache\\", true);
            LatestCacheCleanup = DateTime.Now.ToString("dd/MM/yyyy");
            string[] files = Directory.GetFiles(DirPath + "Logs\\", "*.txt", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                if (LogsSiteIsOld(files[i])) { File.Delete(files[i]); }
            }
            LatestLogsCleanup = DateTime.Now.ToString("dd/MM/yyyy");
            for (int i = 0; i < Settings.Downloads.Downloads.Count; i++)
            {
                if (DownloadsSiteIsOld(Settings.Downloads.Downloads[i])) { Settings.Downloads.Downloads.Remove((Settings.Downloads.Downloads[i])); }
            }
            LatestDownloadsCleanup = DateTime.Now.ToString("dd/MM/yyyy");
            for (int i = 0; i < Settings.History.Count; i++)
            {
                if (HistorySiteIsOld(Settings.History[i])) { Settings.History.Remove((Settings.History[i])); }
            }
            LatestHistoryCleanup = DateTime.Now.ToString("dd/MM/yyyy");
        }

        public void DoCleanup()
        {
            if (CleanCache)
            {
                if (CleanCacheFile)
                {
                    if (KorotTools.GetDirectorySize(DirPath + "cache\\") > (CacheFileSize * 1048576))
                    {
                        Directory.Delete(DirPath + "cache\\", true);
                        LatestCacheCleanup = DateTime.Now.ToString("dd/MM/yyyy");
                    }
                }
                if (CacheCleanToday)
                {
                    Directory.Delete(DirPath + "cache\\", true);
                    LatestCacheCleanup = DateTime.Now.ToString("dd/MM/yyyy");
                }
            }
            if (CleanLogs)
            {
                if (CleanLogsFile)
                {
                    if (KorotTools.GetDirectorySize(DirPath + "Logs\\") > (LogsFileSize * 1048576))
                    {
                        string[] files = Directory.GetFiles(DirPath + "Logs\\", "*.txt", SearchOption.TopDirectoryOnly);
                        for (int i = 0; i < files.Length; i++)
                        {
                            if (LogsSiteIsOld(files[i])) { File.Delete(files[i]); }
                        }
                        LatestLogsCleanup = DateTime.Now.ToString("dd/MM/yyyy");
                    }
                }
                if (LogsCleanToday)
                {
                    string[] files = Directory.GetFiles(DirPath + "Logs\\", "*.txt", SearchOption.TopDirectoryOnly);
                    for (int i = 0; i < files.Length; i++)
                    {
                        if (LogsSiteIsOld(files[i])) { File.Delete(files[i]); }
                    }
                    LatestLogsCleanup = DateTime.Now.ToString("dd/MM/yyyy");
                }
            }
            if (CleanDownloads)
            {
                if (CleanDownloadsFile)
                {
                    if (KorotTools.GetDirectorySize(DirPath + "Downloads\\") > (DownloadsFileSize * 1048576))
                    {
                        for (int i = 0; i < Settings.Downloads.Downloads.Count; i++)
                        {
                            if (DownloadsSiteIsOld(Settings.Downloads.Downloads[i])) { Settings.Downloads.Downloads.Remove((Settings.Downloads.Downloads[i])); }
                        }
                        LatestDownloadsCleanup = DateTime.Now.ToString("dd/MM/yyyy");
                    }
                }
                if (DownloadsCleanToday)
                {
                    for (int i = 0; i < Settings.Downloads.Downloads.Count; i++)
                    {
                        if (DownloadsSiteIsOld(Settings.Downloads.Downloads[i])) { Settings.Downloads.Downloads.Remove((Settings.Downloads.Downloads[i])); }
                    }
                    LatestDownloadsCleanup = DateTime.Now.ToString("dd/MM/yyyy");
                }
            }
            if (CleanHistory)
            {
                if (CleanHistoryFile)
                {
                    if (KorotTools.GetDirectorySize(DirPath + "History\\") > (HistoryFileSize * 1048576))
                    {
                        for (int i = 0; i < Settings.History.Count; i++)
                        {
                            if (HistorySiteIsOld(Settings.History[i])) { Settings.History.Remove((Settings.History[i])); }
                        }
                        LatestHistoryCleanup = DateTime.Now.ToString("dd/MM/yyyy");
                    }
                }
                if (HistoryCleanToday)
                {
                    for (int i = 0; i < Settings.History.Count; i++)
                    {
                        if (HistorySiteIsOld(Settings.History[i])) { Settings.History.Remove((Settings.History[i])); }
                    }
                    LatestHistoryCleanup = DateTime.Now.ToString("dd/MM/yyyy");
                }
            }
        }

        #region Cache

        public bool CleanCacheFile { get; set; }
        public int CacheFileSize { get; set; }
        public bool CleanCacheDaily { get; set; }
        public int CleanCacheDay { get; set; }
        public string LatestCacheCleanup { get; set; }

        public string NextCacheCleanup
        {
            get
            {
                DateTime.TryParseExact(LatestCacheCleanup, "dd/MM/yy", null, System.Globalization.DateTimeStyles.None, out DateTime latest);
                latest = latest.AddDays(CleanCacheDay);
                return latest.ToString("dd/MM/yy");
            }
        }

        public bool CacheCleanToday => (DateTime.Now.ToString("dd/MM/yy") == NextCacheCleanup) && CleanCache && CleanCacheDaily;

        #endregion Cache

        #region History

        public bool CleanHistoryFile { get; set; }
        public int HistoryFileSize { get; set; }
        public bool CleanHistoryDaily { get; set; }
        public int CleanHistoryDay { get; set; }
        public string LatestHistoryCleanup { get; set; }
        public bool CleanOldHistory { get; set; }
        public int OldHistoryDay { get; set; }

        public bool HistorySiteIsOld(Site Site)
        {
            DateTime.TryParseExact(Site.Date, "dd/MM/yy HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out DateTime sitedate);
            return DateTime.Now < sitedate.AddDays(CleanOldHistory ? OldHistoryDay : 0);
        }

        public string NextHistoryCleanup
        {
            get
            {
                DateTime.TryParseExact(LatestHistoryCleanup, "dd/MM/yy", null, System.Globalization.DateTimeStyles.None, out DateTime latest);
                latest = latest.AddDays(CleanHistoryDay);
                return latest.ToString("dd/MM/yy");
            }
        }

        public bool HistoryCleanToday => (DateTime.Now.ToString("dd/MM/yy") == NextHistoryCleanup) && CleanHistory;

        #endregion History

        #region Downloads

        public bool CleanDownloadsFile { get; set; }
        public int DownloadsFileSize { get; set; }
        public bool CleanDownloadsDaily { get; set; }
        public int CleanDownloadsDay { get; set; }
        public string LatestDownloadsCleanup { get; set; }
        public bool CleanOldDownloads { get; set; }
        public int OldDownloadsDay { get; set; }

        public bool DownloadsSiteIsOld(Site Site)
        {
            DateTime.TryParseExact(Site.Date, "dd/MM/yy HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out DateTime sitedate);
            return DateTime.Now < sitedate.AddDays(CleanOldDownloads ? OldDownloadsDay : 0);
        }

        public string NextDownloadsCleanup
        {
            get
            {
                DateTime.TryParseExact(LatestDownloadsCleanup, "dd/MM/yy", null, System.Globalization.DateTimeStyles.None, out DateTime latest);
                latest = latest.AddDays(CleanDownloadsDay);
                return latest.ToString("dd/MM/yy");
            }
        }

        public bool DownloadsCleanToday => (DateTime.Now.ToString("dd/MM/yy") == NextDownloadsCleanup) && CleanDownloads;

        #endregion Downloads

        #region Logs

        public bool CleanLogsFile { get; set; }
        public int LogsFileSize { get; set; }
        public bool CleanLogsDaily { get; set; }
        public int CleanLogsDay { get; set; }
        public string LatestLogsCleanup { get; set; }
        public bool CleanOldLogs { get; set; }
        public int OldLogsDay { get; set; }

        public bool LogsSiteIsOld(string FileName)
        {
            DateTime.TryParseExact(Path.GetFileNameWithoutExtension(FileName), "yyyy-MM-dd-HH-mm-ss", null, System.Globalization.DateTimeStyles.None, out DateTime sitedate);
            return DateTime.Now < sitedate.AddDays(OldLogsDay * -1);
        }

        public string NextLogsCleanup
        {
            get
            {
                DateTime.TryParseExact(LatestLogsCleanup, "dd/MM/yy", null, System.Globalization.DateTimeStyles.None, out DateTime latest);
                latest = latest.AddDays(CleanOldLogs ? OldLogsDay : 0);
                return latest.ToString("dd/MM/yy");
            }
        }

        public bool LogsCleanToday => (DateTime.Now.ToString("dd/MM/yy") == NextLogsCleanup) && CleanLogs;

        #endregion Logs
    }

}