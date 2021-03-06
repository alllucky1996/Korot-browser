﻿/*

Copyright © 2020 Eren "Haltroy" Kanat

Use of this source code is governed by an MIT License that can be found in github.com/Haltroy/Korot/blob/master/LICENSE

*/

using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace Korot
{
    public partial class frmUpdateExt : Form
    {
        public Settings Settings;
        private Extension Extension;
        private Theme Theme;
        public bool isTheme = false;
        private readonly WebClient webC = new WebClient();
        private int currentVersion;
        private string fileLocation;
        private HTUpdateShim HTUPDATE;
        public string info = "Updating [NAME]..." + Environment.NewLine + "Please wait...";
        public string infoTemp = "[PERC]% | [CURRENT] KiB downloaded out of [TOTAL] KiB.";

        public frmUpdateExt(Extension ext, Settings settings)
        {
            Settings = settings;
            isTheme = false;
            Extension = ext;
            currentVersion = ext.Version;
            InitializeComponent();
            Lang();
            webC.DownloadStringCompleted += webC_DownloadStringComplete;
            foreach (Control x in Controls)
            {
                try { x.Font = new Font("Ubuntu", x.Font.Size, x.Font.Style); } catch { continue; }
            }
        }

        public frmUpdateExt(Theme theme, Settings settings)
        {
            Settings = settings;
            isTheme = true;
            Theme = theme;
            currentVersion = theme.Version;
            InitializeComponent();
            Lang();
            webC.DownloadStringCompleted += webC_DownloadStringComplete;
            foreach (Control x in Controls)
            {
                try { x.Font = new Font("Ubuntu", x.Font.Size, x.Font.Style); } catch { continue; }
            }
        }

        private void Lang()
        {
            Text = Settings.LanguageSystem.GetItemText("KorotExtensionUpdater");
            info = Settings.LanguageSystem.GetItemText("ExtensionUpdatingInfo");
            infoTemp = Settings.LanguageSystem.GetItemText("DownloadProgress");
        }

        private readonly string tempPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Korot\\DownloadTemp\\";

        private void Read()
        {
            if (Extension != null)
            {
                label1.Text = info.Replace("[NAME]", Extension.CodeName);
                fileLocation = tempPath + HTAlt.Tools.GenerateRandomText(12) + "\\" + Extension.CodeName + ".kef";
                verLocation = Extension.HTUpdate;
            }
            if (Theme != null)
            {
                label1.Text = info.Replace("[NAME]", Theme.CodeName);
                fileLocation = tempPath + HTAlt.Tools.GenerateRandomText(12) + "\\" + Theme.CodeName + ".ktf";
                verLocation = Theme.HTUpdate;
            }
            downloadString();
        }

        private string verLocation;

        private void frmUpdateExt_Load(object sender, EventArgs e)
        {
            Hide();
            Read();
        }

        private async void downloadString()
        {
            await Task.Run(() =>
            {
                webC.DownloadStringAsync(new Uri(verLocation));
            });
        }

        private async void downloadFile()
        {
            await Task.Run(() =>
            {
                if (File.Exists(fileLocation)) { File.Delete(fileLocation); }
                webC.DownloadFileAsync(new Uri(HTUPDATE.File), fileLocation);
            });
        }

        public void webC_DownloadStringComplete(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error != null)
            {
                downloadString();
            }
            else
            {
                HTUPDATE = new HTUpdateShim(e.Result);
                if (HTUPDATE.LatestVersion > currentVersion && HTUPDATE.CanInstall)
                {
                    startDownload();
                }else
                {
                    Close();
                }
            }
        }

        public void webC_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            htProgressBar1.Value = e.ProgressPercentage;
            label2.Text = infoTemp.Replace("[PERC]", e.ProgressPercentage.ToString())
                .Replace("[CURRENT]", (e.BytesReceived / 1024).ToString())
                .Replace("[TOTAL]", (e.TotalBytesToReceive / 1024).ToString());
        }

        public void webC_DownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error != null)
            { downloadFile(); }
            else
            {
                webC.Dispose();
                frmInstallExt installExt = new frmInstallExt(Settings, fileLocation, true);
                installExt.ShowDialog();
                Directory.Delete(new FileInfo(fileLocation).DirectoryName, true);
                Close();
            }
        }

        private void startDownload()
        {
            Show();
            downloadFile();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            BackColor = Settings.Theme.BackColor;
            ForeColor = Settings.NinjaMode ? Settings.Theme.BackColor : Settings.Theme.ForeColor;
            htProgressBar1.BarColor = Settings.NinjaMode ? Settings.Theme.BackColor : Settings.Theme.OverlayColor;
            htProgressBar1.BackColor = BackColor;
        }
    }
}