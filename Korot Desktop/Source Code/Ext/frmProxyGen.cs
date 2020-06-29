﻿using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace Korot
{
    public partial class frmProxyGen : Form
    {
        public frmProxyGen()
        {
            InitializeComponent();
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                tbID.Text = listView1.SelectedItems[0].Text;
                tbIP.Text = listView1.SelectedItems[0].SubItems[1].Text;
                tbID.Enabled = true;
                tbIP.Enabled = true;
            }
            else
            {
                tbID.Text = "";
                tbIP.Text = "";
                tbID.Enabled = false;
                tbIP.Enabled = false;
            }
        }

        private void addToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListViewItem item = new ListViewItem
            {
                Text = tbID.Text
            };
            ListViewItem.ListViewSubItem subitem = new ListViewItem.ListViewSubItem
            {
                Text = tbIP.Text
            };
            item.SubItems.Insert(1, subitem);
            listView1.Items.Add(item);
        }

        private void tbID_TextChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                listView1.SelectedItems[0].Text = tbID.Text;
            }
        }

        private void tbIP_TextChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                listView1.SelectedItems[0].SubItems[1].Text = tbIP.Text;
            }
        }
        private string buildProxyFile
        {
            get
            {
                string proxyFile = "<KorotExtension.ProxyList>" + Environment.NewLine + "<!-- Auto-generated by Korot Extension Maker. -->" + Environment.NewLine;
                foreach (ListViewItem item in listView1.Items)
                {
                    proxyFile += "<Proxy ID=\"" + item.Text.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" IP=\"" + item.SubItems[1].Text.Replace("&", "&amp;").Replace(">", "&gt;").Replace("<", "&lt;").Replace("'", "&apos;") + "\" />" + Environment.NewLine;
                }
                proxyFile += "</KorotExtension.ProxyList>" + Environment.NewLine;
                return proxyFile;
            }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = "Load proxy file...",
                Filter = "Korot Proxy File|*.kpf",
            };
            DialogResult res = dialog.ShowDialog();
            if (res == DialogResult.OK)
            {

            }
        }
        private void LoadKPF(string kpfloc)
        {
            // Read the file
            string ManifestXML = HTAlt.Tools.ReadFile(kpfloc, Encoding.UTF8);
            // Write XML to Stream so we don't need to load the same file again.
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(ManifestXML); //Writes our XML file
            writer.Flush();
            stream.Position = 0;
            XmlDocument document = new XmlDocument();
            document.Load(stream); //Loads our XML Stream
            // Make sure that this is an extension manifest.
            if (document.FirstChild.Name.ToLower() != "korotextension.proxylist") { return; }
            // This is the part where hell starts. Looking at this code for a small amount
            // of time might cause turning skin to red, puking blood and hair loss. 
            foreach (XmlNode node in document.FirstChild.ChildNodes)
            {
                if (node.Name.ToLower() == "proxy")
                {
                    string id = node.Attributes["ID"] != null ? node.Attributes["ID"].Value : "";
                    string ip = node.Attributes["IP"] != null ? node.Attributes["IP"].Value : "";
                    ListViewItem item = new ListViewItem
                    {
                        Text = id
                    };
                    ListViewItem.ListViewSubItem subitem = new ListViewItem.ListViewSubItem
                    {
                        Text = ip
                    };
                    item.SubItems.Insert(1, subitem);
                    listView1.Items.Add(item);
                }
            }
        }
        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog()
            {
                Title = "Save proxy file to...",
                Filter = "Korot Proxy File|*.kpf",
            };
            DialogResult res = dialog.ShowDialog();
            if (res == DialogResult.OK)
            {
                savelocation = dialog.FileName;
                safelocation = Path.GetFileName(savelocation);
                HTAlt.Tools.WriteFile(dialog.FileName, buildProxyFile, Encoding.UTF8);
            }
        }
        public string savelocation = "";
        public string safelocation = "";

        private void htButton1_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            saveToolStripMenuItem_Click(sender, e);
            Close();
        }
    }
}