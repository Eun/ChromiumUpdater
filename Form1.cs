using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Threading;

namespace ChromiumUpdater
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public String sURL = "";
        public String sVersionFile = "";
        public String sSetupFile = "";

        public Form1 g_frm;
        public bool silent = false;

        public String AppPath;
        public String sIniFilePath;
        public String sCurVersion = "Unknown";
        public String sLatestVersion;
        public Thread thread;
        IniFile pIniFile;

        [DllImport("wininet.dll", CharSet = CharSet.Auto)]
        public static extern bool InternetGetConnectedState(ref ConnectionState lpdwFlags, int dwReserved);
        [Flags]
        public enum ConnectionState : int
        {
            INTERNET_CONNECTION_MODEM = 0x1,
            INTERNET_CONNECTION_LAN = 0x2,
            INTERNET_CONNECTION_PROXY = 0x4,
            INTERNET_RAS_INSTALLED = 0x10,
            INTERNET_CONNECTION_OFFLINE = 0x20,
            INTERNET_CONNECTION_CONFIGURED = 0x40
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            AppPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChromiumUpdater");
            if (!Directory.Exists(AppPath))
            {
                try
                {
                    Directory.CreateDirectory(AppPath);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("Error: Could not create directory \"" + AppPath + "\".\n\nError Description:\n" + ex.Message, "ChromiumUpdater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
            }
            sIniFilePath = Path.Combine(AppPath, "ChromiumUpdater.ini");
            pIniFile = new IniFile();
            if (File.Exists(sIniFilePath))
            {
                pIniFile.Load(sIniFilePath);
            }




            sURL = pIniFile.GetKeyValue("GENERAL", "UpdateURL");
            if (sURL == null || sURL.Length == 0)
            {
                sURL = "http://commondatastorage.googleapis.com/chromium-browser-continuous/Win/";
            }

            sVersionFile = pIniFile.GetKeyValue("GENERAL", "VersionFile");
            if (sVersionFile == null || sVersionFile.Length == 0)
            {
                sVersionFile = "LAST_CHANGE";
            }

            sSetupFile = pIniFile.GetKeyValue("GENERAL", "SetupFile");
            if (sSetupFile == null || sSetupFile.Length == 0)
            {
                sSetupFile = "mini_installer.exe";
            }

            sCurVersion = pIniFile.GetKeyValue("UPDATE", "InstalledVersion");
            if (sCurVersion == null || sCurVersion.Length == 0)
            {
                sCurVersion = "Unknown / Not Installed";
            }

            if (Environment.GetCommandLineArgs().Length > 1)
            {
                  silent = true;
            }

            g_frm = this;

            if (!silent)
            {
                label1.Text = "Your Version: " + sCurVersion;
                label2.Text = "Latest Version: Refreshing...";
                button2.Enabled = false;
            }
            else
            {
                g_frm.ShowInTaskbar = false;
                g_frm.Size = new Size(0,0);
                g_frm.Left = -100000;
                g_frm.Top = -100000;
                g_frm.Visible = false;
            }

            try
            {
                thread = new Thread(new ThreadStart(UpdateThread));
                thread.Start();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Error: Could not start update thread.\n\nError Description:\n" + ex.Message, "ChromiumUpdater", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Environment.Exit(0);
        }




        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                thread = new Thread(new ThreadStart(DownloadThread));
                thread.Start();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Error: Could not start download thread.\n\nError Description:\n" + ex.Message, "ChromiumUpdater", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

 
        public void UpdateThread()
        {
            MethodInvoker uicall;
            uicall = delegate { progressBar1.Style = ProgressBarStyle.Marquee; label2.Text = "Latest Version: Waiting for connection..."; };
            Invoke(uicall);

            ConnectionState Description = 0;
            InternetGetConnectedState(ref Description, 0);
            while ((Description & ConnectionState.INTERNET_CONNECTION_LAN) != ConnectionState.INTERNET_CONNECTION_LAN)
            {
                Thread.Sleep(1000);
                InternetGetConnectedState(ref Description, 0);
            }

            uicall = delegate { label2.Text = "Latest Version: Refreshing..."; };
            Invoke(uicall);
           

            String sLatest = Path.Combine(AppPath, "LATEST");
            WebClient webClient = new WebClient();

            try
            {
                webClient.DownloadFile(new Uri(sURL + sVersionFile), sLatest);

                StreamReader tr = new StreamReader(sLatest);
                sLatestVersion = tr.ReadToEnd();
                tr.Close();
                File.Delete(sLatest);

                if (sLatestVersion.Length != 0)
                {
                    uicall = delegate { progressBar1.Style = ProgressBarStyle.Continuous; };
                    Invoke(uicall);
                    if (sLatestVersion != sCurVersion)
                    {
                        uicall = delegate { button2.Enabled = true; };
                        Invoke(uicall);
                        if (silent)
                        {
                            thread = null;
                            uicall = delegate { button2_Click(null, null); };
                            Invoke(uicall);
                        }
                    }
                    else if (g_frm.silent)
                    {
                        Application.Exit();
                    }
                }
                else
                {
                    uicall = delegate { progressBar1.Style = ProgressBarStyle.Blocks; };
                    Invoke(uicall);
                    sLatestVersion = "Unknown";
                }
                uicall = delegate { label2.Text = "Latest Version: " + sLatestVersion; };
                Invoke(uicall);
                thread = null;
            }
            catch (Exception e)
            {
                
                thread = null;
                uicall = delegate { label2.Text = e.Message; progressBar1.Style = ProgressBarStyle.Blocks; };
                Invoke(uicall);
                if (g_frm.silent)
                {
                    Application.Exit();
                }
                return;
            }
        }


        public void DownloadThread()
        {
            MethodInvoker uicall;
            String sLatestDir = Path.Combine(AppPath, sLatestVersion);
            uicall = delegate { button2.Enabled = false; progressBar1.Style = ProgressBarStyle.Continuous; };
            Invoke(uicall);
            if (Directory.Exists(sLatestDir))
            {
                try
                {
                    Directory.Delete(sLatestDir, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not delete directory \"" + AppPath + "\".\n\nError Description:\n" + ex.Message, "ChromiumUpdater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            try
            {
                Directory.CreateDirectory(sLatestDir);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Error: Could not create directory \"" + sLatestDir + "\".\n\nError Description:\n" + ex.Message, "ChromiumUpdater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            WebClient webClient = new WebClient();
            webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(Completed);
            webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(ProgressChanged);
            try
            {
                webClient.DownloadFileAsync(new Uri(sURL + sLatestVersion + "/" + sSetupFile), Path.Combine(sLatestDir, sSetupFile));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Error: Could download file.\n\nError Description:\n" + ex.Message, "ChromiumUpdater", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            MethodInvoker uicall = delegate
            {
                progressBar1.Value = e.ProgressPercentage;
            };
            Invoke(uicall);
        }

        private void Completed(object sender, AsyncCompletedEventArgs e)
        {

            MethodInvoker uicall;
            uicall = delegate
            {
                progressBar1.Value = 0;
                progressBar1.Style = ProgressBarStyle.Marquee;
                label1.Text = "Your Version: Installing";
            };
            Invoke(uicall);

            try
            {
                Process process = Process.Start(Path.Combine(Path.Combine(AppPath, sLatestVersion), sSetupFile));
                process.EnableRaisingEvents = true;
                process.Exited += process_Exited;
            }
            catch (SystemException err)
            {
                uicall = delegate
                {
                    progressBar1.Style = ProgressBarStyle.Blocks;
                    label1.Text = "Your Version: Error:" + err.Message;
                    sCurVersion = "";
                };
                Invoke(uicall);
            }
        }

        
        public void process_Exited(object sender, EventArgs e)
        {

            MethodInvoker uicall = delegate
            {
                progressBar1.Style = ProgressBarStyle.Continuous;
                label1.Text = "Your Version: " + sLatestVersion;
                pIniFile.SetKeyValue("UPDATE", "InstalledVersion", sLatestVersion);
                pIniFile.Save(sIniFilePath);
                if (silent)
                {
                    Application.Exit();
                }
            };
            Invoke(uicall);
        }



        private void button1_Click(object sender, EventArgs e)
        {
            panel1.Visible = false;
            panel2.Visible = true;
            try
            {
                RegistryKey rk = Registry.CurrentUser;
                RegistryKey sk = rk.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
                if (sk.GetValue("ChromiumUpdater", "").ToString().Length > 0)
                {
                    checkBox1.Checked = true;
                }
                else
                {
                    checkBox1.Checked = false;
                }
                rk.Close();
            }
            catch
            {
                checkBox1.Checked = false;
            }
            textBox1.Text = sURL;
            textBox2.Text = sVersionFile;
            textBox3.Text = sSetupFile;

        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                try
                {
                    RegistryKey rk = Registry.CurrentUser;
                    RegistryKey sk = rk.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                    sk.SetValue("ChromiumUpdater", "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" --silent");
                    rk.Close();
                }
                catch (SystemException err)
                {
                    MessageBox.Show("Error: Could not enable autoupdate.\n\nError Description:\n" + err.Message, "ChromiumUpdater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    checkBox1.Checked = false;
                }
            }
            else
            {
                try
                {

                    RegistryKey rk = Registry.CurrentUser;
                    RegistryKey sk = rk.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                    if (sk.GetValue("ChromiumUpdater") != null)
                    {
                        sk.DeleteValue("ChromiumUpdater");
                    }
                    rk.Close();
                }
                catch (SystemException err)
                {
                    MessageBox.Show("Error: Could not disable autoupdate.\n\nError Description:\n" + err.Message, "ChromiumUpdater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    checkBox1.Checked = true;
                }
            }

            if (sURL != textBox1.Text)
            {
                sURL = textBox1.Text;
                if (sURL.Length == 0)
                {
                    sURL = "http://commondatastorage.googleapis.com/chromium-browser-continuous/Win/";
                }
                if (!sURL.EndsWith("/"))
                {
                    sURL += "/";
                }
                if (!isValidUrl(ref sURL))
                {
                    MessageBox.Show("Error: This is not an valid URL, please make sure it starts with http://, https:// or ftp://\n", "ChromiumUpdater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                pIniFile.SetKeyValue("GENERAL", "UpdateURL", sURL);
                pIniFile.Save(sIniFilePath);

                label2.Text = "Latest Version: Refreshing...";
                try
                {
                    thread = new Thread(new ThreadStart(UpdateThread));
                    thread.Start();
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("Error: Could not start update thread.\n\nError Description:\n" + ex.Message, "ChromiumUpdater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                button2.Enabled = false;
            }

            if (sVersionFile != textBox2.Text)
            {
                sVersionFile = textBox2.Text;
                pIniFile.SetKeyValue("GENERAL", "VersionFile", sVersionFile);
                pIniFile.Save(sIniFilePath);
                label2.Text = "Latest Version: Refreshing...";
                try
                {
                    thread = new Thread(new ThreadStart(UpdateThread));
                    thread.Start();
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("Error: Could not start update thread.\n\nError Description:\n" + ex.Message, "ChromiumUpdater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                button2.Enabled = false;
            }

            if (sSetupFile != textBox3.Text)
            {
                sSetupFile = textBox3.Text;
                pIniFile.SetKeyValue("GENERAL", "SetupFile", sSetupFile);
                pIniFile.Save(sIniFilePath);
                label2.Text = "Latest Version: Refreshing...";
                try
                {
                    thread = new Thread(new ThreadStart(UpdateThread));
                    thread.Start();
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("Error: Could not start update thread.\n\nError Description:\n" + ex.Message, "ChromiumUpdater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                button2.Enabled = false;
            }

            panel1.Visible = true;
            panel2.Visible = false;
        }
        public bool isValidUrl(ref string url)
        {
            string pattern = @"^(http|https|ftp)\://[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,3}(:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$#\=~])*[^\.\,\)\(\s]$";
            Regex reg = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return reg.IsMatch(url);
        }

    


    }
}
