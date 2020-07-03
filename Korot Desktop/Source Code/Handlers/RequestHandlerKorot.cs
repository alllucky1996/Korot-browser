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
using System;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace Korot
{
    internal class RequestHandlerKorot : IRequestHandler
    {
        public frmMain anaform()
        {
            return ((frmMain)cefform.ParentTabs);
        }

        private readonly frmCEF cefform;
        public RequestHandlerKorot(frmCEF _frmCEF)
        {
            cefform = _frmCEF;
        }

        public bool GetAuthCredentials(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
        {
            callback.Dispose();
            return false;
        }

        public IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            return new ResReqHandler(cefform);
        }

        public bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect)
        {
            if (!(request.TransitionType == TransitionType.AutoSubFrame
                || request.TransitionType == TransitionType.SourceMask
                || request.TransitionType == TransitionType.ForwardBack
                || request.TransitionType == TransitionType.Reload))
            {
                if (request.Url.ToLower().StartsWith("korot"))
                {
                    if (request.Url.ToLower().StartsWith("korot://newtab")
                          || request.Url.ToLower().StartsWith("korot://links")
                          || request.Url.ToLower().StartsWith("korot://license")
                          || request.Url.ToLower().StartsWith("korot://incognito"))
                    {
                        cefform.Invoke(new Action(() => cefform.redirectTo(request.Url, request.Url)));
                    }
                    else
                    {
                        // lol no
                    }
                }
                else
                {
                    if (request.Url.ToLower().StartsWith("devtools")) { return false; }
                    cefform.Invoke(new Action(() => cefform.redirectTo(request.Url, request.Url)));
                }
            }
            return false;
        }

        public bool OnCertificateError(IWebBrowser chromiumWebBrowser, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
        {
            string certError = "CefErrorCode: "
                + errorCode
                + Environment.NewLine
                + "Url: "
                + requestUrl
                + Environment.NewLine
                + "SSLInfo: "
                + Environment.NewLine
                + "CertStatus: "
                + sslInfo.CertStatus
                + Environment.NewLine
                + "X509Certificate: "
                + sslInfo.X509Certificate.ToString();
            cefform.Invoke(new Action(() =>
            {
                cefform.safeStatusToolStripMenuItem.Text = cefform.CertificateErrorTitle;
                cefform.ınfoToolStripMenuItem.Text = cefform.CertificateError;
                cefform.showCertificateErrorsToolStripMenuItem.Tag = certError;
                cefform.certError = true;
                cefform.showCertificateErrorsToolStripMenuItem.Visible = true;
                cefform.pbPrivacy.Image = Properties.Resources.lockr;
            }));
            if (cefform.CertAllowedUrls.Contains(requestUrl))
            {
                callback.Continue(true);
                return true;
            }
            else
            {
                cefform.Invoke(new Action(() =>
                {
                    cefform.pnlCert.Visible = true;
                    cefform.btCertError.Tag = requestUrl;
                    cefform.tabControl1.SelectedTab = cefform.tpCert;
                }));
                callback.Cancel();
                return false;
            }
        }

        public bool OnOpenUrlFromTab(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
        {
            if (userGesture)
            {
                anaform().Invoke(new Action(() => anaform().CreateTab(targetUrl)));
                return true;
            }
            else { return false; }
        }

        public void OnPluginCrashed(IWebBrowser chromiumWebBrowser, IBrowser browser, string pluginPath) { }
        public bool OnQuotaRequest(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, long newSize, IRequestCallback callback)
        {
            callback.Dispose();
            return false;
        }

        public void OnRenderProcessTerminated(IWebBrowser chromiumWebBrowser, IBrowser browser, CefTerminationStatus status)
        {
            anaform().Invoke(new Action(() => anaform().Hide()));
            HTAlt.WinForms.HTMsgBox mesaj = new HTAlt.WinForms.HTMsgBox("Korot", cefform.renderProcessDies, new HTAlt.WinForms.HTDialogBoxContext() { OK = true }) { Yes = cefform.Yes, No = cefform.No, OK = cefform.OK, Cancel = cefform.Cancel, BackgroundColor = cefform.Settings.Theme.BackColor, Icon = cefform.anaform.Icon };
            if (mesaj.ShowDialog() == DialogResult.OK || mesaj.ShowDialog() == DialogResult.Cancel)
            {
                Application.Exit();
            }
        }

        public void OnRenderViewReady(IWebBrowser chromiumWebBrowser, IBrowser browser) { }

        public bool OnSelectClientCertificate(IWebBrowser chromiumWebBrowser, IBrowser browser, bool isProxy, string host, int port, X509Certificate2Collection certificates, ISelectClientCertificateCallback callback)
        {
            Control control = (Control)chromiumWebBrowser;

            control.Invoke(new Action(delegate ()
            {
                X509Certificate2Collection selectedCertificateCollection = X509Certificate2UI.SelectFromCollection(certificates, "Certificates Dialog", "Select Certificate for authentication", X509SelectionFlag.SingleSelection);
                if (selectedCertificateCollection.Count > 0)
                {
                    //X509Certificate2UI.SelectFromCollection returns a collection, we've used SingleSelection, so just take the first
                    //The underlying CEF implementation only accepts a single certificate
                    callback.Select(selectedCertificateCollection[0]);
                }
                else
                {
                    //User canceled no certificate should be selected.
                    callback.Select(null);
                }
            }));

            return true;
        }

        public void OnDocumentAvailableInMainFrame(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            // absolutely do nothing
        }
    }
}
