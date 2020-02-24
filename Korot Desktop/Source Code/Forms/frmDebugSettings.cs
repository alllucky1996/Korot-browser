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
using System;
using System.Windows.Forms;

namespace Korot
{
    public partial class frmDebugSettings : Form
    {
        public frmDebugSettings()
        {
            InitializeComponent();
        }

        private void tbHomepage_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.Homepage = tbHomepage.Text;
        }

        private void nX_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.WindowPosX = Convert.ToInt32(nX.Value);
        }

        private void nY_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.WindowPosY = Convert.ToInt32(nY.Value);
        }

        private void nW_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.WindowSizeW = Convert.ToInt32(nW.Value);
        }

        private void nH_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.WindowSizeH = Convert.ToInt32(nH.Value);
        }

        private void tbSE_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.SearchURL = tbSE.Text;
        }

        private void cbOpen_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.downloadOpen = cbOpen.Checked;
        }

        private void cbDNT_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.DoNotTrack = cbDNT.Checked;
        }

        private void tbLang_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.LangFile = tbLang.Text;
        }

        private void nStyle_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.BStyleLayout = Convert.ToInt32(nStyle.Value);
        }

        private void tbStyle_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.BackStyle = tbStyle.Text;
        }

        private void tbTheme_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.ThemeFile = tbTheme.Text;
        }

        private void tbUser_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.LastUser = tbUser.Text;
        }

        private void tbFavorites_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.Favorites = tbFavorites.Text;
        }

        private void tbHistory_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.History = tbHistory.Text;
        }

        private void tbDownloads_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.DowloadHistory = tbDownloads.Text;
        }

        private void tbSession_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.LastSessionURIs = tbSession.Text;
        }

        private void lbCookie_MouseClick(object sender, MouseEventArgs e)
        {
            if (lbCookie.SelectedItem != null && e.Button == MouseButtons.Right)
            {
                Properties.Settings.Default.CookieDisallowList.Remove(lbCookie.SelectedItem.ToString());
                timer1_Tick(sender, null);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            int selected = lbCookie.SelectedIndex; lbCookie.Items.Clear(); foreach (String x in Properties.Settings.Default.CookieDisallowList) { lbCookie.Items.Add(x); }
            if (selected < lbCookie.Items.Count) { lbCookie.SelectedIndex = selected; }
            int selected2 = lbExt.SelectedIndex; lbExt.Items.Clear(); foreach (String x in Properties.Settings.Default.registeredExtensions) { lbExt.Items.Add(x); }
            if (selected2 < lbExt.Items.Count) { lbExt.SelectedIndex = selected2; }
            tbHomepage.Text = Properties.Settings.Default.Homepage;
            tbThemeName.Text = Properties.Settings.Default.ThemeName;
            tbThemeAuthor.Text = Properties.Settings.Default.ThemeAuthor;
            nX.Value = Properties.Settings.Default.WindowPosX;
            nY.Value = Properties.Settings.Default.WindowPosY;
            nW.Value = Properties.Settings.Default.WindowSizeW;
            nH.Value = Properties.Settings.Default.WindowSizeH;
            tbSE.Text = Properties.Settings.Default.SearchURL;
            cbOpen.Checked = Properties.Settings.Default.downloadOpen;
            cbDNT.Checked = Properties.Settings.Default.DoNotTrack;
            tbLang.Text = Properties.Settings.Default.LangFile;
            nStyle.Value = Properties.Settings.Default.BStyleLayout;
            tbStyle.Text = Properties.Settings.Default.BackStyle;
            tbTheme.Text = Properties.Settings.Default.ThemeFile;
            tbUser.Text = Properties.Settings.Default.LastUser;
            tbFavorites.Text = Properties.Settings.Default.Favorites;
            tbHistory.Text = Properties.Settings.Default.History;
            tbDownloads.Text = Properties.Settings.Default.DowloadHistory;
            tbSession.Text = Properties.Settings.Default.LastSessionURIs;
            pbBack.BackColor = Properties.Settings.Default.BackColor;
            pbOverlay.BackColor = Properties.Settings.Default.OverlayColor;
            cbProxy.Checked = Properties.Settings.Default.rememberLastProxy;
            tbDownload.Text = Properties.Settings.Default.DownloadFolder;
            tbStartup.Text = Properties.Settings.Default.StartupURL;
            cbDownload.Checked = Properties.Settings.Default.useDownloadFolder;
            numericUpDown1.Value = Properties.Settings.Default.newTabColor;
            numericUpDown2.Value = Properties.Settings.Default.closeColor;
            checkBox1.Checked = Properties.Settings.Default.showFav;
            checkBox2.Checked = Properties.Settings.Default.allowUnknownResources;
        }

        private void pbBack_Click(object sender, EventArgs e)
        {
            ColorDialog color = new ColorDialog() { AnyColor = true, AllowFullOpen = true, FullOpen = true };
            if (color.ShowDialog() == DialogResult.OK)
            {
                pbBack.BackColor = color.Color;
                Properties.Settings.Default.BackColor = color.Color;
            }
        }

        private void pbOverlay_Click(object sender, EventArgs e)
        {
            ColorDialog color = new ColorDialog() { AnyColor = true, AllowFullOpen = true, FullOpen = true };
            if (color.ShowDialog() == DialogResult.OK)
            {
                pbOverlay.BackColor = color.Color;
                Properties.Settings.Default.OverlayColor = color.Color;
            }
        }
        private void tbThemeName_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.ThemeName = tbThemeName.Text;
        }

        private void tbThemeAuthor_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.ThemeAuthor = tbThemeAuthor.Text;
        }

        private void checkBox1_MouseEnter(object sender, EventArgs e)
        {
            label17.Text = ((Control)sender).Tag.ToString();
        }

        private void checkBox1_MouseLeave(object sender, EventArgs e)
        {
            label17.Text = label17.Tag.ToString();
        }

        private void cbProxy_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.rememberLastProxy = cbProxy.Checked;
        }

        private void cbDownload_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.useDownloadFolder = cbDownload.Checked;
        }

        private void tbStartup_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.StartupURL = tbStartup.Text;
        }

        private void tbDownload_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.DownloadFolder = tbDownload.Text;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.newTabColor = Convert.ToInt32(numericUpDown1.Value);
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.closeColor = Convert.ToInt32(numericUpDown2.Value);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.showFav = checkBox1.Checked;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.allowUnknownResources = checkBox2.Checked;
        }

        private void lbExt_MouseClick(object sender, MouseEventArgs e)
        {
            if (lbExt.SelectedItem != null && e.Button == MouseButtons.Right)
            {
                Properties.Settings.Default.registeredExtensions.Remove(lbExt.SelectedItem.ToString());
                timer1_Tick(sender, null);
            }
        }
    }
}
