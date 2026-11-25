using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Net;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NvDriverDownloader
{
    public class MainForm : Form
    {
        private ComboBox cboGpu;
        private ComboBox cboVersion;
        private GreenRadioButton rbDesktop;
        private GreenRadioButton rbNotebook;
        private Button btnDetect;
        private Button btnRefresh;
        private Button btnDownload;
        private Button btnNvCpl;
        private Button btnNvApp;
        private Panel progressTrack;
        private Panel progressFill;
        private Label lblStatus;
        private RichTextBox rtbDetected;

        private const string TpuUrl = "https://www.techpowerup.com/download/nvidia-geforce-graphics-drivers/";
        private const string FileSuffix = "-win10-win11-64bit-international-dch-whql.exe";
        private static readonly string[] Hosts = new string[] {
            "https://us.download.nvidia.com",
            "https://international.download.nvidia.com"
        };

        private const string NvAppPageUrl = "https://www.nvidia.com/en-us/software/nvidia-app/";
        private const string NvCplStoreId = "9NF8H0H7WMLT";
        private const string NvCplStoreWebUrl = "https://apps.microsoft.com/detail/9nf8h0h7wmlt";

        private static readonly Color FormBg = Color.FromArgb(30, 30, 30);
        private static readonly Color PanelBg = Color.FromArgb(45, 45, 48);
        private static readonly Color TextColor = Color.FromArgb(230, 230, 230);
        private static readonly Color Accent = Color.FromArgb(118, 185, 0);
        private static readonly Color AccentDark = Color.FromArgb(90, 140, 0);
        private static readonly Color ButtonBg = Color.FromArgb(51, 51, 51);
        private static readonly Color ButtonBorder = Color.FromArgb(70, 70, 70);
        private static readonly Color HighlightBg = Color.FromArgb(70, 70, 70);

        private Color FrameColor = Color.FromArgb(74, 84, 89);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private WebClient client;
        private bool busy;
        private int hostIndex;
        private string pendingFile;
        private string pendingDest;
        private string pendingVer;
        private string pendingUrl;
        private bool pendingIsApp;
        private Dictionary<string, string> versionFileNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private Timer marqueeTimer;
        private int marqueePos;
        private int marqueeDir = 1;

        public MainForm()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            FrameColor = ReadSystemAccent();

            Text = "NVIDIA Driver Downloader";
            ClientSize = new Size(540, 255);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = FormBg;
            ForeColor = TextColor;

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            BuildUi();
            PopulateGpus();
            Load += OnLoadHandler;
        }

        private Label MakeLabel(string text, int x, int y, int w, int h)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Location = new Point(x, y);
            lbl.Size = new Size(w, h);
            lbl.BackColor = FormBg;
            lbl.ForeColor = TextColor;
            return lbl;
        }

        private Button MakeButton(string text, int x, int y, int w, int h)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = ButtonBorder;
            b.FlatAppearance.MouseOverBackColor = Accent;
            b.FlatAppearance.MouseDownBackColor = AccentDark;
            b.BackColor = ButtonBg;
            b.ForeColor = TextColor;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        private ComboBox MakeCombo(int x, int y, int w)
        {
            ComboBox c = new ComboBox();
            c.Location = new Point(x, y);
            c.Size = new Size(w, 24);
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = PanelBg;
            c.ForeColor = TextColor;
            c.DrawMode = DrawMode.OwnerDrawFixed;
            c.ItemHeight = 20;
            c.DrawItem += ComboDrawItem;
            return c;
        }

        private void BuildUi()
        {
            Controls.Add(MakeLabel("Видеокарта:", 12, 18, 80, 20));

            cboGpu = MakeCombo(95, 15, 330);
            cboGpu.SelectedIndexChanged += OnGpuChanged;
            Controls.Add(cboGpu);

            btnDetect = MakeButton("Определить", 435, 14, 95, 26);
            btnDetect.Click += OnDetectClick;
            Controls.Add(btnDetect);

            rbDesktop = new GreenRadioButton();
            rbDesktop.Text = "Desktop";
            rbDesktop.Location = new Point(95, 48);
            rbDesktop.Size = new Size(90, 22);
            rbDesktop.FlatStyle = FlatStyle.Flat;
            rbDesktop.BackColor = FormBg;
            rbDesktop.ForeColor = TextColor;
            rbDesktop.Checked = true;
            Controls.Add(rbDesktop);

            rbNotebook = new GreenRadioButton();
            rbNotebook.Text = "Notebook";
            rbNotebook.Location = new Point(195, 48);
            rbNotebook.Size = new Size(100, 22);
            rbNotebook.FlatStyle = FlatStyle.Flat;
            rbNotebook.BackColor = FormBg;
            rbNotebook.ForeColor = TextColor;
            Controls.Add(rbNotebook);

            Controls.Add(MakeLabel("Версия:", 12, 82, 80, 20));

            cboVersion = MakeCombo(95, 79, 330);
            Controls.Add(cboVersion);

            btnRefresh = MakeButton("Обновить", 435, 78, 95, 26);
            btnRefresh.Click += OnRefreshClick;
            Controls.Add(btnRefresh);

            btnDownload = MakeButton("Скачать", 95, 115, 120, 30);
            btnDownload.Click += OnDownloadClick;
            Controls.Add(btnDownload);

            btnNvCpl = MakeButton("Панель NVIDIA", 223, 115, 140, 30);
            btnNvCpl.Click += OnNvCplClick;
            Controls.Add(btnNvCpl);

            btnNvApp = MakeButton("NVIDIA App", 371, 115, 159, 30);
            btnNvApp.Click += OnNvAppClick;
            Controls.Add(btnNvApp);

            progressTrack = new Panel();
            progressTrack.Location = new Point(95, 153);
            progressTrack.Size = new Size(435, 18);
            progressTrack.BackColor = PanelBg;
            progressTrack.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(progressTrack);

            progressFill = new Panel();
            progressFill.Location = new Point(1, 1);
            progressFill.Size = new Size(0, 14);
            progressFill.BackColor = Accent;
            progressTrack.Controls.Add(progressFill);

            lblStatus = MakeLabel("Готово.", 12, 182, 518, 20);
            Controls.Add(lblStatus);

            rtbDetected = new RichTextBox();
            rtbDetected.Location = new Point(12, 205);
            rtbDetected.Size = new Size(518, 40);
            rtbDetected.BackColor = FormBg;
            rtbDetected.ForeColor = TextColor;
            rtbDetected.BorderStyle = BorderStyle.None;
            rtbDetected.ReadOnly = true;
            rtbDetected.TabStop = false;
            rtbDetected.ScrollBars = RichTextBoxScrollBars.None;
            rtbDetected.Padding = Padding.Empty;
            rtbDetected.Text = "Определение видеокарты...";
            Controls.Add(rtbDetected);
        }

        private void ComboDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }
            ComboBox c = (ComboBox)sender;
            string text = Convert.ToString(c.Items[e.Index]);
            bool isEdit = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = (selected && !isEdit) ? HighlightBg : PanelBg;
            Color fore = (selected && !isEdit) ? Color.White : TextColor;
            using (SolidBrush brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            TextRenderer.DrawText(e.Graphics, text, c.Font, e.Bounds, fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private sealed class GzipWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                HttpWebRequest http = request as HttpWebRequest;
                if (http != null)
                {
                    http.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                }
                return request;
            }
        }

        private void PopulateGpus()
        {
            string[] models = new string[] {
                "GeForce RTX 5090 Laptop", "GeForce RTX 5080 Laptop", "GeForce RTX 5070 Ti Laptop", "GeForce RTX 5070 Laptop", "GeForce RTX 5060 Laptop", "GeForce RTX 5050 Laptop",
                "GeForce RTX 5090", "GeForce RTX 5080", "GeForce RTX 5070 Ti", "GeForce RTX 5070", "GeForce RTX 5060 Ti", "GeForce RTX 5060", "GeForce RTX 5050",
                "GeForce RTX 4090 Laptop", "GeForce RTX 4080 Laptop", "GeForce RTX 4070 Laptop", "GeForce RTX 4060 Laptop", "GeForce RTX 4050 Laptop",
                "GeForce RTX 4090", "GeForce RTX 4080 SUPER", "GeForce RTX 4080", "GeForce RTX 4070 Ti SUPER", "GeForce RTX 4070 Ti", "GeForce RTX 4070 SUPER", "GeForce RTX 4070", "GeForce RTX 4060 Ti", "GeForce RTX 4060",
                "GeForce RTX 3090 Ti", "GeForce RTX 3090", "GeForce RTX 3080 Ti", "GeForce RTX 3080", "GeForce RTX 3070 Ti", "GeForce RTX 3070", "GeForce RTX 3060 Ti", "GeForce RTX 3060", "GeForce RTX 3050",
                "GeForce RTX 3080 Ti Laptop", "GeForce RTX 3080 Laptop", "GeForce RTX 3070 Ti Laptop", "GeForce RTX 3070 Laptop", "GeForce RTX 3060 Laptop", "GeForce RTX 3050 Ti Laptop", "GeForce RTX 3050 Laptop",
                "GeForce RTX 2080 Ti", "GeForce RTX 2080 SUPER", "GeForce RTX 2080", "GeForce RTX 2070 SUPER", "GeForce RTX 2070", "GeForce RTX 2060 SUPER", "GeForce RTX 2060",
                "GeForce GTX 1660 Ti", "GeForce GTX 1660 SUPER", "GeForce GTX 1660", "GeForce GTX 1650 SUPER", "GeForce GTX 1650", "GeForce GTX 1630",
                "GeForce GTX 1080 Ti", "GeForce GTX 1080", "GeForce GTX 1070 Ti", "GeForce GTX 1070", "GeForce GTX 1060", "GeForce GTX 1050 Ti", "GeForce GTX 1050", "GeForce GT 1030"
            };
            foreach (string m in models)
            {
                cboGpu.Items.Add(m);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDarkTitleBar();
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                int on = 1;
                int r = DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int));
                if (r != 0)
                {
                    DwmSetWindowAttribute(Handle, 19, ref on, sizeof(int));
                }
                int caption = ToColorRef(FrameColor);
                DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
                int border = ToColorRef(FrameColor);
                DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int));
                int text = ToColorRef(Color.White);
                DwmSetWindowAttribute(Handle, 36, ref text, sizeof(int));
            }
            catch
            {
            }
        }

        private static Color ReadSystemAccent()
        {
            Color fallback = Color.FromArgb(74, 84, 89);
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AccentColor");
                        if (value != null)
                        {
                            uint v = unchecked((uint)Convert.ToInt32(value));
                            int r = (int)(v & 0xFF);
                            int g = (int)((v >> 8) & 0xFF);
                            int b = (int)((v >> 16) & 0xFF);
                            return Color.FromArgb(r, g, b);
                        }
                    }
                }
            }
            catch
            {
            }
            return fallback;
        }

        private static int ToColorRef(Color c)
        {
            return c.R | (c.G << 8) | (c.B << 16);
        }

        private async void OnLoadHandler(object sender, EventArgs e)
        {
            Task versionsTask = LoadVersionsAsync();
            await DetectAndApplyAsync();
            await versionsTask;
        }

        private async void OnDetectClick(object sender, EventArgs e)
        {
            await DetectAndApplyAsync();
        }

        private async void OnRefreshClick(object sender, EventArgs e)
        {
            await LoadVersionsAsync();
        }

        private void OpenStorePage()
        {
            try
            {
                Process.Start("ms-windows-store://pdp/?productid=" + NvCplStoreId);
                SetStatus("Открываю Microsoft Store: Панель управления NVIDIA.");
            }
            catch
            {
                try
                {
                    Process.Start(NvCplStoreWebUrl);
                    SetStatus("Открываю страницу Microsoft Store в браузере.");
                }
                catch (Exception ex)
                {
                    DarkMessageBox.Show("Не удалось открыть Microsoft Store:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static bool IsNvCplInstalled()
        {
            try
            {
                string sub = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
                RegistryHive[] hives = new RegistryHive[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
                foreach (RegistryHive hive in hives)
                {
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                        using (RegistryKey key = baseKey.OpenSubKey(sub))
                        {
                            if (key != null)
                            {
                                foreach (string name in key.GetSubKeyNames())
                                {
                                    if (name.IndexOf("NVIDIAControlPanel", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            try
            {
                if (File.Exists(@"C:\Program Files\NVIDIA Corporation\Control Panel Client\nvcplui.exe"))
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static string FindWinget()
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("PATH");
                if (path != null)
                {
                    foreach (string dir in path.Split(';'))
                    {
                        string d = dir.Trim();
                        if (d.Length == 0)
                        {
                            continue;
                        }
                        try
                        {
                            string cand = Path.Combine(d, "winget.exe");
                            if (File.Exists(cand))
                            {
                                return cand;
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
            try
            {
                string alt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\winget.exe");
                if (File.Exists(alt))
                {
                    return alt;
                }
            }
            catch
            {
            }
            return null;
        }

        private async void OnNvCplClick(object sender, EventArgs e)
        {
            if (busy)
            {
                return;
            }

            if (IsNvCplInstalled())
            {
                SetStatus("Панель управления NVIDIA уже установлена.");
                DarkMessageBox.Show("Панель управления NVIDIA уже установлена.", "Уже установлено", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string winget = FindWinget();
            if (winget == null)
            {
                OpenStorePage();
                return;
            }

            SetBusy(true, "Установка Панели управления NVIDIA (winget)...");
            StartMarquee();
            int exitCode = -1;
            string output = "";
            await Task.Run(delegate
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(winget,
                        "install --id " + NvCplStoreId + " -e --source msstore --accept-package-agreements --accept-source-agreements --silent");
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;

                    System.Text.StringBuilder collected = new System.Text.StringBuilder();
                    using (Process p = new Process())
                    {
                        p.StartInfo = psi;
                        DataReceivedEventHandler onData = delegate(object os, DataReceivedEventArgs ea)
                        {
                            if (ea.Data != null)
                            {
                                string clean = CleanWingetLine(ea.Data);
                                if (clean.Length > 0)
                                {
                                    collected.AppendLine(clean);
                                    ShowInstallLine(clean);
                                }
                            }
                        };
                        p.OutputDataReceived += onData;
                        p.ErrorDataReceived += onData;
                        p.Start();
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                        p.WaitForExit();
                        exitCode = p.ExitCode;
                    }
                    output = collected.ToString();
                }
                catch (Exception ex)
                {
                    output = ex.Message;
                }
            });
            StopMarquee();
            SetBusy(false, null);

            if (exitCode == 0 || output.IndexOf("already installed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (output.IndexOf("already installed", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SetStatus("Панель управления NVIDIA уже установлена.");
                    DarkMessageBox.Show("Панель управления NVIDIA уже установлена.", "Уже установлено", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    SetStatus("Панель управления NVIDIA установлена.");
                    DarkMessageBox.Show("Готово: Панель управления NVIDIA установлена.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                SetStatus("winget не смог установить.");
                DialogResult r = DarkMessageBox.Show(
                    "Не удалось установить Панель управления NVIDIA через winget." + Environment.NewLine + Environment.NewLine +
                    "Открыть страницу в Microsoft Store?",
                    "winget", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r == DialogResult.Yes)
                {
                    OpenStorePage();
                }
            }
        }

        private void ShowInstallLine(string line)
        {
            try
            {
                if (IsDisposed || lblStatus == null || lblStatus.IsDisposed)
                {
                    return;
                }
                if (lblStatus.InvokeRequired)
                {
                    lblStatus.BeginInvoke((MethodInvoker)delegate { ShowInstallLine(line); });
                    return;
                }
                if (line.Length > 80)
                {
                    line = line.Substring(0, 80) + "...";
                }
                SetStatus("Установка Панели управления NVIDIA: " + line);
            }
            catch
            {
            }
        }

        private static string CleanWingetLine(string raw)
        {
            if (raw == null)
            {
                return "";
            }
            string s = Regex.Replace(raw, "\x1B\\[[0-9;?]*[ -/]*[@-~]", "");
            s = s.Replace("\r", " ").Replace("\n", " ");
            s = Regex.Replace(s, "\\s+", " ");
            return s.Trim();
        }

        private void StartMarquee()
        {
            marqueePos = 0;
            marqueeDir = 1;
            if (marqueeTimer == null)
            {
                marqueeTimer = new Timer();
                marqueeTimer.Interval = 30;
                marqueeTimer.Tick += MarqueeTick;
            }
            MarqueeTick(null, EventArgs.Empty);
            marqueeTimer.Start();
        }

        private void StopMarquee()
        {
            if (marqueeTimer != null)
            {
                marqueeTimer.Stop();
            }
            SetProgress(0);
        }

        private void MarqueeTick(object sender, EventArgs e)
        {
            int track = progressTrack.ClientSize.Width - 2;
            if (track <= 0)
            {
                return;
            }
            int chunk = track / 4;
            if (chunk < 20)
            {
                chunk = 20;
            }
            if (chunk > track)
            {
                chunk = track;
            }
            int range = track - chunk;
            if (range < 0)
            {
                range = 0;
            }
            marqueePos += marqueeDir * Math.Max(1, range / 20);
            if (marqueePos >= range)
            {
                marqueePos = range;
                marqueeDir = -1;
            }
            else if (marqueePos <= 0)
            {
                marqueePos = 0;
                marqueeDir = 1;
            }
            progressFill.Location = new Point(1 + marqueePos, 1);
            progressFill.Width = chunk;
        }

        private async void OnNvAppClick(object sender, EventArgs e)
        {
            if (busy)
            {
                return;
            }
            string ver = null;
            string url = null;
            SetBusy(true, "Поиск версии NVIDIA App...");
            try
            {
                string html;
                using (WebClient wc = new GzipWebClient())
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
                    html = await wc.DownloadStringTaskAsync(new Uri(NvAppPageUrl));
                }
                Match m = Regex.Match(html,
                    "https://(?:us|international)\\.download\\.nvidia\\.com/nvapp/client/[^\"'<>\\s]+/NVIDIA_app_v([\\d.]+)_x64\\.exe");
                if (!m.Success)
                {
                    m = Regex.Match(html,
                        "https://(?:us|international)\\.download\\.nvidia\\.com/nvapp/client/[^\"'<>\\s]+/NVIDIA_app_v([\\d.]+)(?:_[a-z0-9]+)?\\.exe");
                }
                if (m.Success)
                {
                    url = m.Value;
                    ver = m.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                SetBusy(false, null);
                SetStatus("Не удалось получить версию NVIDIA App.");
                DialogResult open = DarkMessageBox.Show(
                    "Не удалось получить ссылку на NVIDIA App:" + Environment.NewLine + ex.Message +
                    Environment.NewLine + Environment.NewLine + "Открыть страницу загрузки в браузере?",
                    "Ошибка", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                if (open == DialogResult.Yes)
                {
                    try
                    {
                        Process.Start(NvAppPageUrl);
                    }
                    catch
                    {
                    }
                }
                return;
            }

            if (url == null)
            {
                SetBusy(false, null);
                SetStatus("Ссылка на NVIDIA App не найдена.");
                DialogResult open = DarkMessageBox.Show(
                    "Не удалось найти ссылку на установщик NVIDIA App." + Environment.NewLine +
                    "Открыть страницу загрузки в браузере?",
                    "Не найдено", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (open == DialogResult.Yes)
                {
                    try
                    {
                        Process.Start(NvAppPageUrl);
                    }
                    catch
                    {
                    }
                }
                return;
            }

            string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NVIDIA_Drivers");
            Directory.CreateDirectory(outDir);
            string dest = Path.Combine(outDir, "NVIDIA_app_v" + (ver == null ? "latest" : ver) + ".exe");
            if (File.Exists(dest))
            {
                DialogResult r = DarkMessageBox.Show("Файл уже существует:\n" + dest + "\n\nПерезаписать?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes)
                {
                    SetBusy(false, null);
                    SetStatus("Готово.");
                    return;
                }
            }

            pendingUrl = url;
            pendingIsApp = true;
            pendingVer = ver == null ? "NVIDIA App" : ver;
            pendingFile = null;
            pendingDest = dest;
            hostIndex = 0;
            SetProgress(0);

            client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
            client.DownloadProgressChanged += OnProgress;
            client.DownloadFileCompleted += OnCompleted;
            StartDownload();
        }

        private void SetDetectedText(string gpuName, string publicVer, string internalVer)
        {
            if (rtbDetected == null || rtbDetected.IsDisposed)
            {
                return;
            }
            rtbDetected.Clear();
            Font normalFont = rtbDetected.Font;
            rtbDetected.SelectionFont = normalFont;
            rtbDetected.SelectionColor = TextColor;
            if (gpuName == null)
            {
                rtbDetected.AppendText("Видеокарта NVIDIA не обнаружена. Выберите модель вручную.");
                return;
            }
            rtbDetected.AppendText("Определено: " + gpuName + Environment.NewLine + "Версия драйвера: ");
            rtbDetected.SelectionFont = normalFont;
            rtbDetected.SelectionColor = TextColor;
            rtbDetected.AppendText(publicVer == null ? "н/д" : publicVer);
            rtbDetected.SelectionFont = normalFont;
            rtbDetected.SelectionColor = TextColor;
            if (internalVer != null && internalVer != publicVer)
            {
                rtbDetected.AppendText(" (" + internalVer + ")");
            }
        }

        private async Task DetectAndApplyAsync()
        {
            string[] info = await Task.Run(new Func<string[]>(DetectGpuInfo));
            if (info == null)
            {
                SetDetectedText(null, null, null);
                return;
            }
            string gpuName = info[0];
            string driverVer = info[1];
            string publicVer = info.Length > 2 ? info[2] : null;
            if (publicVer == null)
            {
                publicVer = ToFriendlyDriverVersion(driverVer);
            }
            SetDetectedText(gpuName, publicVer, driverVer);

            if (gpuName.ToLower().IndexOf("laptop") >= 0 || gpuName.ToLower().IndexOf("notebook") >= 0)
            {
                rbNotebook.Checked = true;
            }
            else
            {
                rbDesktop.Checked = true;
            }

            int best = -1;
            int bestLen = -1;
            for (int i = 0; i < cboGpu.Items.Count; i++)
            {
                string item = cboGpu.Items[i].ToString();
                if (gpuName.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0 && item.Length > bestLen)
                {
                    best = i;
                    bestLen = item.Length;
                }
            }
            if (best >= 0)
            {
                cboGpu.SelectedIndex = best;
            }
        }

        private static string ToFriendlyDriverVersion(string raw)
        {
            if (raw == null)
            {
                return null;
            }
            string[] parts = raw.Split('.');
            if (parts.Length != 4)
            {
                return raw;
            }
            int build;
            int revision;
            if (!int.TryParse(parts[2], out build) || !int.TryParse(parts[3], out revision))
            {
                return raw;
            }
            if (build < 10 || revision < 0)
            {
                return raw;
            }
            int major = (build - 10) * 100 + revision / 100;
            return major.ToString() + "." + (revision % 100).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string TryGetNvidiaSmiVersion()
        {
            try
            {
                string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (string.IsNullOrEmpty(systemDir))
                {
                    return null;
                }
                string exe = Path.Combine(systemDir, "nvidia-smi.exe");
                if (!File.Exists(exe))
                {
                    return null;
                }

                ProcessStartInfo psi = new ProcessStartInfo(exe,
                    "--query-gpu=driver_version --format=csv,noheader");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { };
                    if (!p.Start())
                    {
                        return null;
                    }
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(3000))
                    {
                        try
                        {
                            p.Kill();
                        }
                        catch
                        {
                        }
                        return null;
                    }
                    string output = p.StandardOutput.ReadToEnd();
                    using (StringReader reader = new StringReader(output))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            Match m = Regex.Match(line, @"^\s*(\d+\.\d+)");
                            if (m.Success)
                            {
                                return m.Groups[1].Value;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private string[] DetectGpuInfo()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController"))
                {
                    foreach (ManagementBaseObject obj in searcher.Get())
                    {
                        string name = Convert.ToString(obj["Name"]);
                        if (name != null && name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string internalVer = Convert.ToString(obj["DriverVersion"]);
                            return new string[] { name, internalVer, TryGetNvidiaSmiVersion() };
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private void OnGpuChanged(object sender, EventArgs e)
        {
            if (cboGpu.SelectedItem == null)
            {
                return;
            }
            string item = cboGpu.SelectedItem.ToString().ToLower();
            if (item.IndexOf("laptop") >= 0 || item.IndexOf("notebook") >= 0)
            {
                rbNotebook.Checked = true;
            }
            else
            {
                rbDesktop.Checked = true;
            }
        }

        private async Task LoadVersionsAsync()
        {
            if (busy)
            {
                return;
            }
            SetBusy(true, "Загрузка списка версий...");
            try
            {
                string html;
                using (WebClient wc = new GzipWebClient())
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
                    html = await wc.DownloadStringTaskAsync(new Uri(TpuUrl));
                }
                List<string> versions = ParseVersions(html);
                cboVersion.Items.Clear();
                foreach (string v in versions)
                {
                    cboVersion.Items.Add(v);
                }
                if (cboVersion.Items.Count > 0)
                {
                    cboVersion.SelectedIndex = 0;
                }
                SetStatus("Найдено версий: " + versions.Count + ". Выбрана " + cboVersion.SelectedItem + ".");
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка загрузки списка версий.");
                MessageBox.Show("Не удалось получить список версий:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private List<string> ParseVersions(string html)
        {
            List<string> result = new List<string>();
            versionFileNames.Clear();
            Regex re = new Regex(@"(\d+\.\d+)-desktop-win10-win11-64bit-international-dch-whql\.exe");
            foreach (Match m in re.Matches(html))
            {
                string v = m.Groups[1].Value;
                if (!result.Contains(v))
                {
                    result.Add(v);
                    versionFileNames[v] = m.Value;
                }
            }
            return result;
        }

        private void OnDownloadClick(object sender, EventArgs e)
        {
            if (busy)
            {
                return;
            }
            if (cboVersion.SelectedItem == null)
            {
                MessageBox.Show("Сначала выберите версию драйвера.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string ver = cboVersion.SelectedItem.ToString();
            string family = rbNotebook.Checked ? "notebook" : "desktop";
            pendingVer = ver;
            string exact = null;
            if (!versionFileNames.TryGetValue(ver, out exact) || string.IsNullOrEmpty(exact))
            {
                exact = ver + "-desktop" + FileSuffix;
            }
            if (family == "notebook")
            {
                exact = exact.Replace("-desktop-", "-notebook-");
            }
            pendingFile = exact;

            string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NVIDIA_Drivers");
            Directory.CreateDirectory(outDir);
            pendingDest = Path.Combine(outDir, "GeForce_GameReady_" + ver + ".exe");

            if (File.Exists(pendingDest))
            {
                DialogResult r = DarkMessageBox.Show("Файл уже существует:\n" + pendingDest + "\n\nПерезаписать?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes)
                {
                    return;
                }
            }

            pendingUrl = null;
            pendingIsApp = false;
            hostIndex = 0;
            SetProgress(0);
            SetBusy(true, "Подготовка...");

            client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
            client.DownloadProgressChanged += OnProgress;
            client.DownloadFileCompleted += OnCompleted;
            StartDownload();
        }

        private static bool UrlExists(string url)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.UserAgent = "Mozilla/5.0";
                req.Timeout = 10000;
                req.ReadWriteTimeout = 10000;
                req.AddRange(0, 0);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    int code = (int)resp.StatusCode;
                    return code == 200 || code == 206;
                }
            }
            catch
            {
                return false;
            }
        }

        private List<string> BuildDriverFileCandidates()
        {
            List<string> result = new List<string>();
            if (!string.IsNullOrEmpty(pendingFile))
            {
                result.Add(pendingFile);
            }
            string family = rbNotebook.Checked ? "notebook" : "desktop";
            string classic = pendingVer + "-" + family + FileSuffix;
            if (!result.Contains(classic))
            {
                result.Add(classic);
            }
            return result;
        }

        private void StartDownload()
        {
            string url = pendingUrl;
            if (url == null)
            {
                SetStatus("Проверка ссылки...");
                List<string> files = BuildDriverFileCandidates();
                for (int h = hostIndex; h < Hosts.Length; h++)
                {
                    foreach (string file in files)
                    {
                        string candidate = Hosts[h] + "/Windows/" + pendingVer + "/" + file;
                        if (UrlExists(candidate))
                        {
                            hostIndex = h;
                            url = candidate;
                            break;
                        }
                    }
                    if (url != null)
                    {
                        break;
                    }
                }
                if (url == null)
                {
                    OnDownloadError("Не удалось найти подходящую ссылку на драйвер " + pendingVer + ".");
                    return;
                }
            }
            SetStatus("Скачивание: " + url);
            try
            {
                client.DownloadFileAsync(new Uri(url), pendingDest);
            }
            catch (Exception ex)
            {
                OnDownloadError(ex.Message);
            }
        }

        private void OnProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            SetProgress(e.ProgressPercentage);
            SetStatus("Скачивание версии " + pendingVer + ": " + e.ProgressPercentage + "% (" + (e.BytesReceived / 1048576) + " МБ)");
        }

        private void OnCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                SetBusy(false, null);
                SetStatus("Скачивание отменено.");
                return;
            }
            if (e.Error != null)
            {
                if (pendingUrl == null)
                {
                    hostIndex++;
                    if (hostIndex < Hosts.Length)
                    {
                        StartDownload();
                        return;
                    }
                }
                OnDownloadError(e.Error.Message);
                return;
            }
            SetBusy(false, null);
            SetProgress(100);
            SetStatus("Готово: " + pendingDest);
            DarkMessageBox.Show((pendingIsApp ? "NVIDIA App скачан:\n" : "Драйвер скачан:\n") + pendingDest, "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnDownloadError(string message)
        {
            SetBusy(false, null);
            SetStatus("Ошибка скачивания.");
            DarkMessageBox.Show("Не удалось скачать " + (pendingIsApp ? "NVIDIA App" : "драйвер") + ":\n" + message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void SetProgress(int pct)
        {
            if (pct < 0)
            {
                pct = 0;
            }
            if (pct > 100)
            {
                pct = 100;
            }
            int max = progressTrack.ClientSize.Width - 2;
            if (max < 0)
            {
                max = 0;
            }
            progressFill.Location = new Point(1, 1);
            progressFill.Width = (int)(max * pct / 100.0);
        }

        private void SetStatus(string text)
        {
            lblStatus.Text = text == null ? "" : text;
        }

        private void SetBusy(bool value, string status)
        {
            busy = value;
            btnDownload.Enabled = !value;
            btnRefresh.Enabled = !value;
            btnDetect.Enabled = !value;
            btnNvCpl.Enabled = !value;
            btnNvApp.Enabled = !value;
            cboGpu.Enabled = !value;
            cboVersion.Enabled = !value;
            rbDesktop.Enabled = !value;
            rbNotebook.Enabled = !value;
            if (status != null)
            {
                SetStatus(status);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (client != null && busy)
            {
                DialogResult r = MessageBox.Show("Идёт скачивание. Прервать и выйти?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                try
                {
                    client.CancelAsync();
                }
                catch
                {
                }
            }
            base.OnFormClosing(e);
        }
    }

    public class GreenRadioButton : RadioButton
    {
        private static readonly Color DotColor = Color.FromArgb(118, 185, 0);
        private static readonly Color RingIdle = Color.FromArgb(150, 150, 150);

        public GreenRadioButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            Invalidate();
            base.OnCheckedChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int d = 14;
            int cy = (Height - d) / 2;
            Rectangle circle = new Rectangle(0, cy, d, d);

            using (Pen ring = new Pen(Checked ? DotColor : RingIdle, 1.6f))
            {
                g.DrawEllipse(ring, circle);
            }

            if (Checked)
            {
                int inner = 6;
                Rectangle dot = new Rectangle(circle.X + (d - inner) / 2, circle.Y + (d - inner) / 2, inner, inner);
                using (Brush b = new SolidBrush(DotColor))
                {
                    g.FillEllipse(b, dot);
                }
            }

            int textLeft = d + 6;
            Rectangle textRect = new Rectangle(textLeft, 0, Width - textLeft, Height);
            Color textColor = Enabled ? ForeColor : Color.FromArgb(140, 140, 140);
            TextRenderer.DrawText(g, Text, Font, textRect, textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }
    }

    public static class DarkMessageBox
    {
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            IWin32Window owner = Form.ActiveForm;
            if (owner == null && Application.OpenForms.Count > 0)
            {
                owner = Application.OpenForms[0];
            }

            using (DarkDialog dlg = new DarkDialog(text, caption, buttons, icon))
            {
                if (owner != null)
                {
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    return dlg.ShowDialog(owner);
                }
                dlg.StartPosition = FormStartPosition.CenterScreen;
                return dlg.ShowDialog();
            }
        }
    }

    public class DarkDialog : Form
    {
        private static readonly Color DFormBg = Color.FromArgb(30, 30, 30);
        private static readonly Color DText = Color.FromArgb(230, 230, 230);
        private static readonly Color DAccent = Color.FromArgb(118, 185, 0);
        private static readonly Color DAccentDark = Color.FromArgb(90, 140, 0);
        private static readonly Color DButtonBg = Color.FromArgb(51, 51, 51);
        private static readonly Color DButtonBorder = Color.FromArgb(70, 70, 70);
        private static readonly Color DWarn = Color.FromArgb(214, 158, 0);
        private static readonly Color DErr = Color.FromArgb(200, 60, 60);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private readonly string message;
        private readonly MessageBoxButtons buttons;
        private readonly MessageBoxIcon icon;
        private readonly Color frameColor;

        public DarkDialog(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            this.message = text == null ? "" : text;
            this.buttons = buttons;
            this.icon = icon;
            this.frameColor = ReadAccentColor();

            Text = caption == null ? "" : caption;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = DFormBg;
            ForeColor = DText;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(380, 150);

            BuildLayout();
        }

        private void BuildLayout()
        {
            int textLeft = (icon == MessageBoxIcon.None) ? 18 : 64;
            int textWidth = ClientSize.Width - textLeft - 18;
            if (textWidth < 60)
            {
                textWidth = 60;
            }

            Size measured = TextRenderer.MeasureText(message, Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            int textHeight = measured.Height;
            if (textHeight < 20)
            {
                textHeight = 20;
            }

            Label lbl = new Label();
            lbl.Text = message;
            lbl.AutoSize = false;
            lbl.Location = new Point(textLeft, 20);
            lbl.Size = new Size(textWidth, textHeight);
            lbl.BackColor = DFormBg;
            lbl.ForeColor = DText;
            Controls.Add(lbl);

            int btnTop = 20 + textHeight + 18;
            int btnH = 32;
            int btnW = 90;

            if (buttons == MessageBoxButtons.YesNo)
            {
                Button no = MakeDialogButton("Нет", DialogResult.No);
                no.Location = new Point(ClientSize.Width - 18 - btnW, btnTop);
                Button yes = MakeDialogButton("Да", DialogResult.Yes);
                yes.Location = new Point(no.Left - 12 - btnW, btnTop);
                Controls.Add(yes);
                Controls.Add(no);
                AcceptButton = yes;
                CancelButton = no;
            }
            else
            {
                Button ok = MakeDialogButton("OK", DialogResult.OK);
                ok.Location = new Point((ClientSize.Width - btnW) / 2, btnTop);
                Controls.Add(ok);
                AcceptButton = ok;
                CancelButton = ok;
            }

            ClientSize = new Size(ClientSize.Width, btnTop + btnH + 18);
        }

        private Button MakeDialogButton(string text, DialogResult result)
        {
            Button b = new Button();
            b.Text = text;
            b.Size = new Size(90, 32);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = DButtonBorder;
            b.FlatAppearance.MouseOverBackColor = DAccent;
            b.FlatAppearance.MouseDownBackColor = DAccentDark;
            b.BackColor = DButtonBg;
            b.ForeColor = DText;
            b.UseVisualStyleBackColor = false;
            b.DialogResult = result;
            return b;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                int r = DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int));
                if (r != 0)
                {
                    DwmSetWindowAttribute(Handle, 19, ref on, sizeof(int));
                }
                int caption = ToColorRef(frameColor);
                DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
                int border = ToColorRef(frameColor);
                DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int));
                int text = ToColorRef(Color.White);
                DwmSetWindowAttribute(Handle, 36, ref text, sizeof(int));
            }
            catch
            {
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (icon == MessageBoxIcon.None)
            {
                return;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color c = DAccent;
            string sym = "i";
            if (icon == MessageBoxIcon.Warning || icon == MessageBoxIcon.Exclamation)
            {
                c = DWarn;
                sym = "!";
            }
            else if (icon == MessageBoxIcon.Error || icon == MessageBoxIcon.Hand || icon == MessageBoxIcon.Stop)
            {
                c = DErr;
                sym = "x";
            }
            else if (icon == MessageBoxIcon.Question)
            {
                c = DAccent;
                sym = "?";
            }

            Rectangle r = new Rectangle(20, 22, 32, 32);
            using (Brush b = new SolidBrush(c))
            {
                g.FillEllipse(b, r);
            }
            using (Font f = new Font("Segoe UI", 14f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, sym, f, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private static Color ReadAccentColor()
        {
            Color fallback = Color.FromArgb(74, 84, 89);
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AccentColor");
                        if (value != null)
                        {
                            uint v = unchecked((uint)Convert.ToInt32(value));
                            int r = (int)(v & 0xFF);
                            int g = (int)((v >> 8) & 0xFF);
                            int b = (int)((v >> 16) & 0xFF);
                            return Color.FromArgb(r, g, b);
                        }
                    }
                }
            }
            catch
            {
            }
            return fallback;
        }

        private static int ToColorRef(Color c)
        {
            return c.R | (c.G << 8) | (c.B << 16);
        }
    }

    public static class Program
    {
        private const int Net48Release = 528040;
        private const string Net48WebInstallerUrl = "https://go.microsoft.com/fwlink/?LinkId=2085155";
        private const string Net48PageUrl = "https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48";

        [STAThread]
        public static void Main()
        {
            if (!IsNet48Installed() && !PromptAndInstallNet48())
            {
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static bool IsNet48Installed()
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    if (key == null)
                    {
                        return false;
                    }
                    object release = key.GetValue("Release");
                    if (release == null)
                    {
                        return false;
                    }
                    return Convert.ToInt32(release) >= Net48Release;
                }
            }
            catch
            {
                return true;
            }
        }

        private static bool PromptAndInstallNet48()
        {
            DialogResult answer = MessageBox.Show(
                "Для работы программы требуется .NET Framework 4.8." + Environment.NewLine + Environment.NewLine +
                "Скачать и установить сейчас?",
                "Необходим .NET Framework 4.8",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
            {
                return false;
            }

            string installerPath = Path.Combine(Path.GetTempPath(), "ndp48-web.exe");
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile(Net48WebInstallerUrl, installerPath);
                }
            }
            catch (Exception ex)
            {
                DialogResult open = MessageBox.Show(
                    "Не удалось скачать установщик .NET Framework 4.8:" + Environment.NewLine + ex.Message +
                    Environment.NewLine + Environment.NewLine + "Открыть страницу загрузки в браузере?",
                    "Ошибка загрузки",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error);
                if (open == DialogResult.Yes)
                {
                    try
                    {
                        Process.Start(Net48PageUrl);
                    }
                    catch
                    {
                    }
                }
                return false;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(installerPath, "/passive /norestart");
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось запустить установщик .NET Framework 4.8:" + Environment.NewLine + ex.Message,
                    "Ошибка установки",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            if (IsNet48Installed())
            {
                try
                {
                    Process.Start(Application.ExecutablePath);
                }
                catch
                {
                }
                return false;
            }

            MessageBox.Show(
                "Установка завершена. Возможно, потребуется перезагрузка компьютера," + Environment.NewLine +
                "после чего запустите программу снова.",
                "Завершено",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return false;
        }
    }
}
