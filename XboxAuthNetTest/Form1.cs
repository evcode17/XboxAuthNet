﻿using Microsoft.Web.WebView2.Core;
using Org.BouncyCastle.Math;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;
using XboxAuthNet.OAuth;
using XboxAuthNet.Utils;
using XboxAuthNet.XboxLive;
using XboxAuthNet.XboxLive.Entity;

namespace XboxAuthNetTest
{
    public partial class Form1 : Form
    {
        private readonly HttpClient httpClient;

        public Form1()
        {
            httpClient = new HttpClient();
            //oauth = new MicrosoftOAuth("00000000402B5328", XboxAuth.XboxScope, httpClient);
            //oauth = new MicrosoftOAuth("00000000441cc96b", XboxAuth.XboxScope, httpClient);
            oauth = new MicrosoftOAuth("499c8d36-be2a-4231-9ebd-ef291b7bb64c", XboxAuth.XboxScope, httpClient);
            InitializeComponent();
        }

        MicrosoftOAuth oauth;

        string sessionFilePath = "auth.json";

        private void Form1_Load(object sender, EventArgs e)
        {
            var res = readSession();
            showResponse(res);
        }

        private MicrosoftOAuthResponse readSession()
        {
            if (!File.Exists(sessionFilePath))
                return null;

            var file = File.ReadAllText(sessionFilePath);
            var response = JsonSerializer.Deserialize<MicrosoftOAuthResponse>(file);

            return response;
        }

        private void writeSession(MicrosoftOAuthResponse response)
        {
            var json = JsonSerializer.Serialize(response);
            File.WriteAllText(sessionFilePath, json);
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;

            try
            {
                // try login using refresh token
                MicrosoftOAuthResponse res = readSession();
                if (!string.IsNullOrEmpty(res?.RefreshToken))
                {
                    res = await oauth.RefreshToken(res?.RefreshToken);
                    log("refresh login success");
                    loginSuccess(res);
                    return;
                }

                var url = oauth.CreateUrlForOAuth();
                log("CreateUrlForOAuth(): " + url);
                webView21.Source = new Uri(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                button1.Enabled = true;
            }
        }

        private async void webView21_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            log("nav " + e.Uri + ", " + e.IsRedirected);

            if (e.IsRedirected && oauth.CheckOAuthCodeResult(new Uri(e.Uri), out var authCode)) // login success
            {
                if (!authCode.IsSuccess)
                {
                    MessageBox.Show($"{authCode.Error}\n{authCode.ErrorDescription}");
                    return;
                }

                try
                {
                    log("browser login succses");

                    var res = await oauth.GetTokens(authCode); // get token
                    loginSuccess(res);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private async void btnXboxLive_Click(object sender, EventArgs e)
        {
            try
            {
                this.Enabled = false;
                var relyingParty = txtXboxRelyingParty.Text;

                var xbox = new XboxAuth(httpClient);
                var ex = await xbox.ExchangeRpsTicketForUserToken(textBox1.Text);
                var res = await xbox.ExchangeTokensForXstsIdentity(ex.Token, null, null, relyingParty, null);
                showResponse(res);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                this.Enabled = true;
            }
        }

        private async void btnXboxLiveFull_Click(object sender, EventArgs e)
        {
            try
            {
                this.Enabled = false;
                var relyingParty = txtXboxRelyingParty.Text;
                var keyPairGenerator = KeyPairGeneratorFactory.CreateDefaultAsymmetricKeyPair();

                var sisu = XboxSecureAuth.CreateFromKeyGenerator(httpClient, keyPairGenerator);
                var userToken = await sisu.RequestUserToken(textBox1.Text, XboxSecureAuth.XboxTokenPrefix);
                var deviceToken = await sisu.RequestDeviceToken(XboxDeviceTypes.Nintendo, "0.0.0");
                var titleToken = await sisu.RequestTitleToken(textBox1.Text, deviceToken.Token);

                var xbox = new XboxAuth(httpClient);
                var xsts = await xbox.ExchangeTokensForXstsIdentity(userToken.Token, deviceToken.Token, titleToken.Token, relyingParty, null);
                showResponse(xsts);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                this.Enabled = true;
            }
        }

        private async void btnXboxSisu_Click(object sender, EventArgs e)
        {
            try
            {
                this.Enabled = false;
                var relyingParty = txtXboxRelyingParty.Text;
                var keyPairGenerator = KeyPairGeneratorFactory.CreateDefaultAsymmetricKeyPair();

                var sisu = XboxSecureAuth.CreateFromKeyGenerator(httpClient, keyPairGenerator);
                var deviceToken = await sisu.RequestDeviceToken(XboxDeviceTypes.Win32, "0.0.0");
                var xsts = await sisu.SisuAuth(textBox1.Text, XboxGameTitles.MinecraftJava, deviceToken.Token, relyingParty);
                showResponse(xsts.AuthorizationToken);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                this.Enabled = true;
            }
        }

        private void btnMSSignout_Click(object sender, EventArgs e)
        {
            webView21.Source = new Uri(MicrosoftOAuth.GetSignOutUrl());
            writeSession(null);
        }

        private void showResponse(MicrosoftOAuthResponse res)
        {
            if (res == null)
                return;

            textBox1.Text = res.AccessToken;
            textBox2.Text = res.ExpireIn.ToString();
            textBox3.Text = res.RefreshToken;
            textBox4.Text = res.Scope;
            textBox5.Text = res.TokenType;
            textBox6.Text = res.UserId;
        }

        private void showResponse(XboxAuthResponse res)
        {
            txtXboxAccessToken.Text = res.Token;
            txtXboxExpire.Text = res.ExpireOn;
            txtXboxUserXUID.Text = res.UserXUID;
            txtXboxUserHash.Text = res.UserHash;
        }

        private void loginSuccess(MicrosoftOAuthResponse res)
        {
            showResponse(res);

            writeSession(res);
            MessageBox.Show("SUCCESS");
            btnXboxLive.Enabled = true;
            button1.Enabled = false;
        }

        private void log(string msg)
        {
            richTextBox1.AppendText(msg + "\n");
        }
    }
}
