using Microsoft.Win32; // FFFFFUUUUUUuuuuu
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;


// Welcome to the bunch of some useless and ugly .NET code
namespace Authorizer
{
    public partial class MainWindow : Form
    {
        private bool started = false;
        private Thread auth = null;
        // default server domain
        private string domain = "stats.linet.zp.ua";
        // default authorization refresh interval
        private int refreshInterval = 90000;
        // in some cases network interface gets loaded after startup programs are executed
        // windows is so windowish
        private int reconnectInterval = 5000;
        private System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));

        public MainWindow()
        {
            InitializeComponent();
            // using this mechanism is an overhead in such a program
            System.Windows.Forms.Form.CheckForIllegalCrossThreadCalls = false;
        }

        private void Window_onLoad(object sender, EventArgs e)
        {
            RegistryKey authKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Authorizer");
            if (authKey != null)
            {
                try
                {
                    textBoxUser.Text = (string)authKey.GetValue("user");
                    textBoxPasswd.Text = (string)authKey.GetValue("passwd");
                    checkBoxSave.Checked = true;
                }
                catch (Exception ex)
                {
                    return;
                }
                finally
                {
                    authKey.Close();
                }
            }
            RegistryKey startupKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            if (startupKey != null)
            {
                try
                {
                    string value = (string)startupKey.GetValue("Authorizer");
                    if (value != null && value == Process.GetCurrentProcess().MainModule.FileName)
                    {
                        checkBoxAutostart.Checked = true;
                    }
                }
                catch (Exception ex)
                {
                    return;
                }
                finally
                {
                    startupKey.Close();
                }
            }
            RegistryKey domainKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\AuthorizerDomain");
            if (domainKey != null)
            {
                try
                {
                    if (domainKey.GetValue("domain") != null && (string)domainKey.GetValue("domain") != "")
                    {
                        this.domain = (string)domainKey.GetValue("domain");
                    }
                }
                catch (Exception ex)
                {
                    return;
                }
                finally
                {
                    domainKey.Close();
                }
            }
            if (triggerButton() && checkBoxAutostart.Checked)
            {
                buttonAuth_Click(null, null);
                this.WindowState = FormWindowState.Minimized;
            }
        }

        // how's that in python?
        // oh, yeah - hashlib.md5(input).hexdigest()
        private string getMD5Hash(string input)
        {
            System.Security.Cryptography.MD5CryptoServiceProvider x = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] bs = System.Text.Encoding.UTF8.GetBytes(input);
            bs = x.ComputeHash(bs);
            System.Text.StringBuilder s = new System.Text.StringBuilder();
            foreach (byte b in bs)
            {
                s.Append(b.ToString("x2").ToLower());
            }
            string password = s.ToString();
            return password;
        }

        private bool triggerButton()
        {
            bool state = !(textBoxUser.Text.Trim().Equals("") || textBoxPasswd.Text.Trim().Equals(""));
            buttonAuth.Enabled = state;
            return state;
        }

        private string getDocument(string getString)
        {
            HttpWebRequest request = null;
            HttpWebResponse response = null;

            ServicePointManager.ServerCertificateValidationCallback +=
            delegate(
                object sender2,
                X509Certificate certificate,
                X509Chain chain,
                SslPolicyErrors sslPolicyErrors
            )
            {
                return true;
            };

            try
            {
                request = (HttpWebRequest)WebRequest.Create("https://" + this.domain + getString);
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex)
            {
                connected(false);
                return "no connection";
            }

            StreamReader responseReader = new StreamReader(response.GetResponseStream(), Encoding.ASCII);
            return responseReader.ReadToEnd();
        }

        private void connected(bool conn)
        {
            if (conn)
            {
                toolStripStatusLabel.Text = "Есть соединение";
                toolStripStatusLabel.ForeColor = Color.Green;
                notifyIconAuth.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
                notifyIconAuth.Text = "Линет - подключено";
            }
            else
            {
                toolStripStatusLabel.Text = "Нет соединения";
                toolStripStatusLabel.ForeColor = Color.Red;
                notifyIconAuth.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIconAuth.Icon")));
                notifyIconAuth.Text = "Линет - отключено";
            }
        }

        private void buttonAuth_Click(object sender, EventArgs e)
        {
            if (this.started)
            {
                this.started = false;
                this.auth.Abort();
                textBoxUser.Enabled = true;
                textBoxPasswd.Enabled = true;
                checkBoxAutostart.Enabled = true;
                checkBoxSave.Enabled = true;
                connected(false);
                buttonAuth.Text = "Дождитесь окончания сессии";
                buttonAuth.Enabled = false;
                Thread wait = new Thread(new ThreadStart(waitProc));
                wait.Start();
            }
            else
            {
                buttonAuth.Text = "Выключить Интернет"; // please, don't do that!
                textBoxUser.Enabled = false;
                textBoxPasswd.Enabled = false;
                checkBoxAutostart.Enabled = false;
                checkBoxSave.Enabled = false;
                this.auth = new Thread(new ThreadStart(threadProc));
                this.auth.Start();
                this.started = true;
            }
        }

        private void waitProc()
        {
            Thread.Sleep(refreshInterval);
            buttonAuth.Enabled = true;
            buttonAuth.Text = "Включить Интернет"; // lolwut
        }

        private void threadProc()
        {
            string ses = "";
            string doc = "";
            string user = textBoxUser.Text;
            string passwd = textBoxPasswd.Text;
            Regex regex = null;

            while (true)
            {
                while (true)
                {
                    doc = getDocument("/");
                    if (doc.Equals("no connection"))
                    {
                        Thread.Sleep(reconnectInterval);
                    }
                    else
                    {
                        break;
                    }
                }
                // God, I hate .NET
                regex = new Regex(@"<input type=hidden name=ses value=([\d]*)>");
                try
                {
                    // why the hell don't windows users get some perl distribution preinstalled
                    ses = regex.Match(doc).Groups[1].Value;
                }
                catch (IndexOutOfRangeException ex)
                {
                    return;
                }

                while (true)
                {
                    doc = getDocument("/cgi-bin/stat.pl?ses=" + ses + "&a=98&uu=" + user + "&pp=" + getMD5Hash(ses + " " + passwd));
                    regex = new Regex(@"<img src='/i/err.gif'>");
                    if (regex.IsMatch(doc))
                    {
                        textBoxUser.Enabled = true;
                        textBoxPasswd.Enabled = true;
                        checkBoxAutostart.Enabled = true;
                        checkBoxSave.Enabled = true;
                        buttonAuth.Text = "Включить Интернет";
                        connected(false);
                        this.started = false;
                        MessageBox.Show("Неверный логин или пароль!", "Линет - ошибка авторизации", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Thread.CurrentThread.Abort();
                        // just leave it here, in case .NET brakes or something
                        return;
                    }
                    if (doc.Equals("no connection"))
                    {
                        break;
                    }
                    connected(true);
                    Thread.Sleep(refreshInterval);
                }
            }
        }

        private void checkBoxSave_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxSave.Checked)
            {
                RegistryKey authKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Authorizer");
                if (authKey == null)
                {
                    authKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Authorizer");
                }
                if (authKey != null)
                {
                    try
                    {
                        authKey.SetValue("user", textBoxUser.Text);
                        authKey.SetValue("passwd", textBoxPasswd.Text);
                    }
                    catch (Exception ex)
                    {
                        return;
                    }
                    finally
                    {
                        authKey.Close();
                    }
                }
            }
            else
            {
                try
                {
                    Registry.LocalMachine.DeleteSubKey(@"SOFTWARE\Authorizer");
                }
                catch (Exception ex)
                {
                    return;
                }
            }
        }

        private void Window_onClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                this.auth.Abort();
            }
            catch (Exception ex)
            {
                return;
            }
        }

        private void Window_onResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        }

        private void Icon_onClick(object sender, MouseEventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
        }

        private void checkBoxAutostart_CheckedChanged(object sender, EventArgs e)
        {
            RegistryKey startupKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            if (startupKey == null)
            {
                startupKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            }
            if (startupKey != null)
            {
                if (checkBoxAutostart.Checked)
                {
                    try
                    {
                        startupKey.SetValue("Authorizer", Process.GetCurrentProcess().MainModule.FileName);
                    }
                    catch (Exception ex)
                    {
                        return;
                    }
                    finally
                    {
                        startupKey.Close();
                    }
                }
                else
                {
                    try
                    {
                        startupKey.DeleteValue("Authorizer");
                    }
                    catch (Exception ex)
                    {
                        return;
                    }
                }
            }
        }

        private void linkPing_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process ping = new Process();

                ping.StartInfo.FileName = "ping.exe";
                ping.StartInfo.Arguments = "-t " + this.domain;
                ping.Start();
            }
            catch (Exception ex)
            {
                return;
            }
        }

        // only in .NET menu items get stripped...
        private void ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DomainWindow domainDialog = new DomainWindow(this.domain);
            domainDialog.ShowDialog(this);
            if (domainDialog.DialogResult == DialogResult.OK)
            {
                RegistryKey authKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\AuthorizerDomain");
                if (authKey != null)
                {
                    try
                    {
                        this.domain = (string)authKey.GetValue("domain");
                    }
                    catch (Exception ex)
                    {
                        return;
                    }
                    finally
                    {
                        authKey.Close();
                    }
                }
            }
        }

        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            (new AboutBox()).Show();
        }

        private void doTriggerButton(object sender, EventArgs e)
        {
            triggerButton();
        }
    }
}
