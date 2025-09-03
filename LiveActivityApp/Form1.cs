using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

namespace LiveActivityApp
{
    public partial class Form1 : Form
    {
        // =========================
        // WinAPI & SendInput interop
        // =========================

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const ushort SC_ENTER = 0x1C;

        private const byte VK_CONTROL = 0x11;
        private const byte VK_SHIFT = 0x10;
        private const byte VK_MENU = 0x12; // ALT
        private const byte VK_TAB = 0x09;

        // =========================
        // App state
        // =========================

        private readonly System.Windows.Forms.Timer _masterTimer = new System.Windows.Forms.Timer();
        private bool _isSchedulerRunning = false;

        // ↓↓↓ move these here (inside Form1)
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private bool _exiting = false;

        // true if the app is closing (to avoid hiding to tray)
        public Form1()
        {
            InitializeComponent();
            SetupApplication();
            this.FormClosed += (_, __) => _trayIcon?.Dispose();
            if (!IsDisposed && _trayIcon != null) RebuildTrayMenu();
        }

        private void lvSchedule_DoubleClick(object? sender, EventArgs e)
        {
            if (_isSchedulerRunning) return; // don't edit while running
            if (lvSchedule.SelectedItems.Count == 0) return;

            var item = lvSchedule.SelectedItems[0];
            if (item.Tag is not ScheduleEntry entry) return;

            // Build a stable snapshot of current window candidates (use the combo contents)
            var windows = new List<WindowInfo>();
            foreach (var it in cmbWindowTitles.Items)
                if (it is WindowInfo w) windows.Add(w);

            using var dlg = new EditScheduleForm(entry, BuildAllowedTokens(), windows);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                // Reflect changes back to the ListView row
                item.Text = entry.ScheduledTime.ToString("HH:mm:ss");
                if (item.SubItems.Count > 1) item.SubItems[1].Text = entry.Message;
                if (item.SubItems.Count > 2) item.SubItems[2].Text = entry.TargetWindow?.Title ?? "";
                if (item.SubItems.Count > 3) item.SubItems[3].Text = FormatActionsLog(entry.Actions, entry.ActionDelaySeconds);
                lvSchedule.Refresh();
                lblStatus.Text = "Status: Schedule updated.";
            }
        }

        // Tray mode helpers
        private bool IsTrayMode => chkTrayMode?.Checked == true;

        private void UpdateTrayModeUI()
        {
            if (chkTrayMode == null) return;

            if (chkTrayMode.Checked)
            {
                chkTrayMode.Text = "ON";
                chkTrayMode.BackColor = Color.SeaGreen;
                chkTrayMode.ForeColor = Color.White;
            }
            else
            {
                chkTrayMode.Text = "OFF";
                chkTrayMode.BackColor = Color.Firebrick;
                chkTrayMode.ForeColor = Color.White;

                // When tray mode is OFF, make sure the icon doesn't linger
                if (_trayIcon != null) _trayIcon.Visible = false;
            }
        }


        private void SetupApplication()
        {
            // master timer
            _masterTimer.Interval = 1000;
            _masterTimer.Tick += MasterTimer_Tick;
            _masterTimer.Start();

            // button wiring (Start/Stop wired by Designer)
            btnRefresh.Click += btnRefresh_Click;
            lvSchedule.DoubleClick += lvSchedule_DoubleClick;
            btnAddSchedule.Click += btnAddSchedule_Click;
            btnDeleteSchedule.Click += btnDeleteSchedule_Click;
            btnAddAlarm.Click += btnAddAlarm_Click;
            btnDeleteAlarm.Click += btnDeleteAlarm_Click;

            // action dropdowns (now two) + static token list with "NONE"
            var tokens = BuildAllowedTokens(); // includes "NONE"

            cbAction.DropDownStyle = ComboBoxStyle.DropDownList;
            cbAction.Items.Clear();
            cbAction.Items.AddRange(tokens.ToArray());
            if (cbAction.SelectedIndex < 0) cbAction.SelectedIndex = 0; // NONE

            cbAction2.DropDownStyle = ComboBoxStyle.DropDownList;
            cbAction2.Items.Clear();
            cbAction2.Items.AddRange(tokens.ToArray());
            if (cbAction2.SelectedIndex < 0) cbAction2.SelectedIndex = 0; // NONE

            // tray icon
            SetupTray();
            this.Resize += Form1_Resize;
            this.FormClosing += Form1_FormClosing;


            // delay dropdown (0.5..5.0 step 0.5). If prefilled in Designer, this is a no-op.
            cbActionDelay.DropDownStyle = ComboBoxStyle.DropDownList;
            if (cbActionDelay.Items.Count == 0)
            {
                for (double s = 0.5; s <= 5.0 + 1e-6; s += 0.5)
                    cbActionDelay.Items.Add(s.ToString("0.##"));
            }
            if (cbActionDelay.SelectedIndex < 0) cbActionDelay.SelectedIndex = 1; // ~1s default

            EnsureScheduleColumns();

            // defensive: detach any auto-wired label3 click that throws
            try { label3.Click -= label3_Click; } catch { /* ignore if not connected */ }

            // tray mode checkbox
            chkTrayMode.CheckedChanged += chkTrayMode_CheckedChanged;
            chkTrayMode.Checked = true;   // default ON; change to false if you want it OFF by default
            UpdateTrayModeUI();

            // initial UI
            this.Text = "Alarm and Auto Tool";
            btnStop.Enabled = false;
            lblStatus.Text = "Status: Idle.";
            txtScheduleMessage.Text = "Type your message here";
            txtAlarmMessage.Text = "Type Alarm message here";

            PopulateRunningApps();

        }

        // =========================================================================
        // Master tick
        // =========================================================================
        private void MasterTimer_Tick(object? sender, EventArgs e)
        {
            UpdateAlarmsDisplay();
            if (_isSchedulerRunning) RunSchedulerCheck();
        }

        // =========================
        // SCHEDULER
        // =========================

        private void RunSchedulerCheck()
        {
            var now = DateTime.Now;

            for (int i = lvSchedule.Items.Count - 1; i >= 0; i--)
            {
                if (lvSchedule.Items[i].Tag is ScheduleEntry entry)
                {
                    if (now >= entry.ScheduledTime && (now - entry.ScheduledTime) < TimeSpan.FromSeconds(2))
                    {
                        ExecuteScheduledAction(entry);
                        lvSchedule.Items.RemoveAt(i);
                    }
                }
            }

            if (lvSchedule.Items.Count == 0)
            {
                btnStop_Click(this, EventArgs.Empty);
                lblStatus.Text = "Status: All scheduled tasks complete. Scheduler stopped.";
                RebuildTrayMenu();
            }
        }

        private void btnAddSchedule_Click(object sender, EventArgs e)
        {
            if (cmbWindowTitles.SelectedIndex < 0 || string.IsNullOrWhiteSpace(txtScheduleMessage.Text))
            {
                lblStatus.Text = "Status: Select a window and enter a message for the schedule.";
                return;
            }

            var selectedWindow = (WindowInfo)cmbWindowTitles.Items[cmbWindowTitles.SelectedIndex];

            // Build actions from BOTH dropdowns, ignoring "NONE"
            var actions = new List<string>();
            AddActionIfValid(actions, cbAction);
            AddActionIfValid(actions, cbAction2);

            double delaySec = GetSelectedDelaySeconds();

            var newEntry = new ScheduleEntry(dtpScheduleTime.Value, txtScheduleMessage.Text, selectedWindow, actions)
            {
                ActionDelaySeconds = delaySec
            };

            // Row: Time | Message | Target Window | Logged Commands
            var row = new ListViewItem(newEntry.ScheduledTime.ToString("HH:mm:ss"));
            row.SubItems.Add(newEntry.Message);
            row.SubItems.Add(newEntry.TargetWindow.Title);
            row.SubItems.Add(FormatActionsLog(newEntry.Actions, newEntry.ActionDelaySeconds));
            row.Tag = newEntry;

            lvSchedule.Items.Add(row);

            lblStatus.Text = $"Status: Added schedule for {newEntry.ScheduledTime:g}.";
        }

        /// <summary>
        /// Focus → type message → (optional initial delay) → execute ONLY the selected actions
        /// (e.g., ENTER / CTRL+ENTER / TAB / ALT+TAB / SHIFT+X / CTRL+X / ALT+X).
        /// No automatic Enter is sent anymore.
        /// </summary>
        private void ExecuteScheduledAction(ScheduleEntry entry)
        {
            if (!IsWindow(entry.TargetWindow.Handle))
            {
                lblStatus.Text = $"Status: SKIPPED. Window '{entry.TargetWindow.Title}' was closed.";
                return;
            }

            // Bring target to foreground so it receives keystrokes
            SetForegroundWindow(entry.TargetWindow.Handle);
            Thread.Sleep(200);

            try
            {
                // Type the message text only
                SendKeys.SendWait(entry.Message);
                Application.DoEvents();
                Thread.Sleep(30);

                // If you selected actions (e.g., ENTER/CTRL+ENTER), run them.
                if (entry.Actions != null && entry.Actions.Count > 0)
                {
                    // Optional initial delay before starting actions
                    if (entry.ActionDelaySeconds > 0)
                    {
                        int initialMs = (int)Math.Round(entry.ActionDelaySeconds * 1000.0);
                        Thread.Sleep(initialMs);
                    }

                    ExecuteActions(entry.Actions, entry.ActionDelaySeconds);
                    lblStatus.Text = $"Status: EXECUTED '{entry.Message}' + {entry.Actions.Count} action(s) at {DateTime.Now:T}.";
                }
                else
                {
                    // Nothing else done — no Enter, no combos — by design.
                    lblStatus.Text = $"Status: Typed message only (no actions selected) at {DateTime.Now:T}.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Status: Send failed: {ex.Message}";
            }
        }


        private static void PressEnter_SendInput()
        {
            SendKeyScan(SC_ENTER, keyUp: false, extended: false);
            Thread.Sleep(5);
            SendKeyScan(SC_ENTER, keyUp: true, extended: false);

            Thread.Sleep(5);
            SendKeyScan(SC_ENTER, keyUp: false, extended: true);
            Thread.Sleep(5);
            SendKeyScan(SC_ENTER, keyUp: true, extended: true);
        }

        // =========================
        // ALARMS
        // =========================

        private void UpdateAlarmsDisplay()
        {
            foreach (ListViewItem item in lvAlarms.Items)
            {
                if (item.Tag is AlarmEntry entry)
                {
                    TimeSpan remaining = entry.AlarmTime - DateTime.Now;

                    if (remaining.TotalSeconds > 0)
                    {
                        item.SubItems[1].Text = remaining.ToString(@"d\.hh\:mm\:ss");
                        item.BackColor = remaining.TotalSeconds < 10 ? Color.LightCoral : SystemColors.Window;
                    }
                    else
                    {
                        item.SubItems[1].Text = "Finished";
                        item.BackColor = Color.LightGray;
                    }
                }
            }
        }

        private void btnAddAlarm_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtAlarmMessage.Text))
            {
                lblStatus.Text = "Status: Please enter a message for the alarm.";
                return;
            }

            var newEntry = new AlarmEntry(dtpAlarmTime.Value, txtAlarmMessage.Text);

            var alarmItem = new ListViewItem(newEntry.Message);
            alarmItem.SubItems.Add("Calculating...");
            alarmItem.Tag = newEntry;

            lvAlarms.Items.Add(alarmItem);
            lblStatus.Text = $"Status: Added alarm for {newEntry.AlarmTime:g}.";
        }

        private void btnDeleteAlarm_Click(object sender, EventArgs e)
        {
            if (lvAlarms.SelectedItems.Count == 0)
            {
                lblStatus.Text = "Status: Select an alarm from the list to delete.";
                return;
            }

            foreach (ListViewItem it in lvAlarms.SelectedItems)
                lvAlarms.Items.Remove(it);

            lblStatus.Text = "Status: Selected alarm(s) deleted.";
        }

        // =========================
        // General UI helpers
        // =========================
        private void SetupTray()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = this.Icon ?? SystemIcons.Application,
                Text = "Alarm and Auto Tool",
                Visible = false
            };

            _trayMenu = new ContextMenuStrip();
            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.DoubleClick += (_, __) => RestoreFromTray();

            RebuildTrayMenu();
        }

        private void RebuildTrayMenu()
        {
            _trayMenu.Items.Clear();

            var miShow = new ToolStripMenuItem("Show", null, (_, __) => RestoreFromTray());
            var miStartStop = new ToolStripMenuItem(_isSchedulerRunning ? "Stop" : "Start", null, (_, __) =>
            {
                if (_isSchedulerRunning) btnStop_Click(this, EventArgs.Empty);
                else btnStart_Click(this, EventArgs.Empty);
                RebuildTrayMenu();
            });
            var miExit = new ToolStripMenuItem("Exit", null, (_, __) =>
            {
                _exiting = true;
                if (_trayIcon != null) { _trayIcon.Visible = false; _trayIcon.Dispose(); }
                Close();
            });

            _trayMenu.Items.Add(miShow);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(miStartStop);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(miExit);
        }

        // Minimize to tray if in tray mode
        private void Form1_Resize(object? sender, EventArgs e)
        {
            if (IsTrayMode && WindowState == FormWindowState.Minimized)
                HideToTray();
        }

        // Intercept close (X) button to go to tray instead of exiting
        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (IsTrayMode && !_exiting)
            {
                e.Cancel = true;   // don’t close, go to tray instead
                HideToTray();
            }
        }
        // Hide the main window and show the tray icon
        private void HideToTray()
        {
            if (_trayIcon != null) _trayIcon.Visible = true;
            ShowInTaskbar = false;
            Hide();
        }

        // Restore the main window and hide the tray icon
        private void RestoreFromTray()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Activate();
            if (_trayIcon != null) _trayIcon.Visible = false;
        }

        //ACTION BUTTONS
        private void btnRefresh_Click(object sender, EventArgs e)
        {
            PopulateRunningApps();
            lblStatus.Text = "Status: Window list has been refreshed.";
        }

        private void btnDeleteSchedule_Click(object sender, EventArgs e)
        {
            if (lvSchedule.SelectedItems.Count == 0) return;

            foreach (ListViewItem it in lvSchedule.SelectedItems)
                lvSchedule.Items.Remove(it);

            lblStatus.Text = "Status: Selected schedule(s) deleted.";
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (lvSchedule.Items.Count == 0)
            {
                lblStatus.Text = "Status: Cannot start. The schedule is empty.";
                return;
            }

            _isSchedulerRunning = true;
            LockSchedulerUI(true);
            Text = "Alarm and Auto Tool - RUNNING";
            lblStatus.Text = "Status: Scheduler is running...";
            RebuildTrayMenu();
        }
        // Stop button clicked
        private void btnStop_Click(object sender, EventArgs e)
        {
            _isSchedulerRunning = false;
            LockSchedulerUI(false);
            Text = "Alarm and Auto Tool - STOPPED";
            lblStatus.Text = "Status: Scheduler stopped.";
            RebuildTrayMenu();   // <— add this
        }
        // Populate cmbWindowTitles with currently running apps that have a main window
        private void PopulateRunningApps()
        {
            cmbWindowTitles.Items.Clear();

            try
            {
                foreach (Process p in Process.GetProcesses())
                {
                    if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                        cmbWindowTitles.Items.Add(new WindowInfo { Title = p.MainWindowTitle, Handle = p.MainWindowHandle });
                }
            }
            catch
            {
                // ignore
            }

            if (cmbWindowTitles.Items.Count > 0)
                cmbWindowTitles.SelectedIndex = 0;
        }

        private void LockSchedulerUI(bool isRunning)
        {
            btnStart.Enabled = !isRunning;
            btnStop.Enabled = isRunning;

            btnRefresh.Enabled = !isRunning;
            cmbWindowTitles.Enabled = !isRunning;
            txtScheduleMessage.Enabled = !isRunning;
            btnAddSchedule.Enabled = !isRunning;
            btnDeleteSchedule.Enabled = !isRunning;

            // freeze action pickers while running
            cbAction.Enabled = !isRunning;
            cbAction2.Enabled = !isRunning;
            cbActionDelay.Enabled = !isRunning;
        }

        // ============================================
        // lvSchedule-as-editor helpers
        // ============================================

        private static List<string> BuildAllowedTokens()
        {
            // "NONE" first so it's the default / safe state
            var list = new List<string> { "NONE", "ENTER", "CTRL+ENTER", "TAB", "ALT+TAB" };
            for (char c = 'A'; c <= 'Z'; c++)
            {
                list.Add($"SHIFT+{c}");
                list.Add($"CTRL+{c}");
                list.Add($"ALT+{c}");
            }
            return list;
        }

        private void EnsureScheduleColumns()
        {
            if (lvSchedule.Columns.Count < 4)
            {
                lvSchedule.Columns.Add("Logged Commands", 220, HorizontalAlignment.Left);
            }
        }

        private static string FormatActionsLog(List<string> actions, double delaySec)
        {
            if (actions == null || actions.Count == 0) return "—";
            var delay = delaySec <= 0 ? "" : $" @ {delaySec:0.##}s";
            return string.Join(" | ", actions) + delay;
        }

        private double GetSelectedDelaySeconds()
        {
            if (cbActionDelay.SelectedItem is string s && double.TryParse(s.Replace("s", "").Trim(), out var val))
                return Math.Max(0.0, Math.Min(5.0, val));
            return 1.0;
        }

        private static void AddActionIfValid(List<string> actions, ComboBox cb)
        {
            var token = cb.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(token)) return;
            if (token.Equals("NONE", StringComparison.OrdinalIgnoreCase)) return;
            actions.Add(token);
        }

        // ============================================
        // SendInput helpers & interop types
        // ============================================

        private static void SendKeyScan(ushort scanCode, bool keyUp, bool extended = false)
        {
            var input = new INPUT[1];
            input[0].type = INPUT_KEYBOARD;
            input[0].U.ki.wVk = 0; // using scan codes
            input[0].U.ki.wScan = scanCode;
            input[0].U.ki.dwFlags = KEYEVENTF_SCANCODE
                                    | (keyUp ? KEYEVENTF_KEYUP : 0)
                                    | (extended ? KEYEVENTF_EXTENDEDKEY : 0);
            input[0].U.ki.time = 0;
            input[0].U.ki.dwExtraInfo = IntPtr.Zero;

            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
        }

        /// Send Enter robustly: SendInput + SendKeys fallback.
        /// If you ever see a double-send in a specific app, comment out the SendKeys line.
        private static void PressEnter_Robust()
        {
            PressEnter_SendInput();          // scan-code enter (some apps need this)
            Thread.Sleep(10);
            SendKeys.SendWait("{ENTER}");    // fallback (other apps only react to this)
        }


        private static void SendKeyVK(byte vk, bool keyUp)
        {
            var input = new INPUT[1];
            input[0].type = INPUT_KEYBOARD;
            input[0].U.ki.wVk = vk;
            input[0].U.ki.wScan = 0;
            input[0].U.ki.dwFlags = keyUp ? KEYEVENTF_KEYUP : 0;
            input[0].U.ki.time = 0;
            input[0].U.ki.dwExtraInfo = IntPtr.Zero;

            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
        }

        private static void PressVK(byte vk)
        {
            SendKeyVK(vk, false);
            Thread.Sleep(5);
            SendKeyVK(vk, true);
        }

        private static void PressCombo(byte modifierVk, byte keyVk)
        {
            SendKeyVK(modifierVk, false); // modifier down
            Thread.Sleep(5);
            PressVK(keyVk);               // tap
            Thread.Sleep(5);
            SendKeyVK(modifierVk, true);  // modifier up
        }

        private static byte VkFromLetter(char c)
        {
            char upper = char.ToUpperInvariant(c);
            if (upper < 'A' || upper > 'Z') return 0;
            return (byte)upper; // ASCII == VK for letters
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            public int type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // =========================
        // ACTION EXECUTION (with delay)
        // =========================

        private static void ExecuteActions(IReadOnlyList<string> actions, double delaySeconds)
        {
            if (actions == null || actions.Count == 0) return;
            int last = actions.Count - 1;

            for (int i = 0; i < actions.Count; i++)
            {
                var token = actions[i];
                if (string.IsNullOrWhiteSpace(token)) continue;
                var t = token.ToUpperInvariant();

                if (t == "ENTER")
                {
                    PressEnter_Robust();
                }
                else if (t == "CTRL+ENTER")
                {
                    // Try with SendInput while CTRL is held…
                    SendKeyVK(VK_CONTROL, false);
                    Thread.Sleep(5);
                    PressEnter_SendInput();
                    Thread.Sleep(5);
                    SendKeyVK(VK_CONTROL, true);

                    // …then a compatibility fallback using SendKeys’ own modifier handling.
                    Thread.Sleep(10);
                    SendKeys.SendWait("^{ENTER}");
                }
                else if (t == "TAB")
                {
                    PressVK(VK_TAB);
                }
                else if (t == "ALT+TAB")
                {
                    SendKeyVK(VK_MENU, false);
                    Thread.Sleep(30);
                    PressVK(VK_TAB);
                    Thread.Sleep(30);
                    SendKeyVK(VK_MENU, true);
                }
                else if (t.StartsWith("SHIFT+") || t.StartsWith("CTRL+") || t.StartsWith("ALT+"))
                {
                    if (t.Length == 7)
                    {
                        char ch = t[6];
                        if (char.IsLetter(ch))
                        {
                            byte vk = VkFromLetter(ch);
                            if (vk != 0)
                            {
                                if (t.StartsWith("SHIFT+")) PressCombo(VK_SHIFT, vk);
                                else if (t.StartsWith("CTRL+")) PressCombo(VK_CONTROL, vk);
                                else PressCombo(VK_MENU, vk);
                            }
                        }
                    }
                }

                // delay between actions
                if (i != last && delaySeconds > 0)
                {
                    int ms = (int)Math.Round(delaySeconds * 1000.0);
                    Thread.Sleep(ms);
                }
            }
        }

        private void cbAction_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        //TRAY MODE HANDLING
        private void chkTrayMode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateTrayModeUI();
        }

    }
    
    // =========================
    // Data models
    // =========================

    public class ScheduleEntry
    {
        public DateTime ScheduledTime { get; set; }
        public string Message { get; set; }
        public WindowInfo TargetWindow { get; set; }

        // Per-row actions (what shows in "Logged Commands")
        public List<string> Actions { get; set; } = new List<string>();

        // Action delay (seconds) used between actions
        public double ActionDelaySeconds { get; set; } = 1.0;

        public ScheduleEntry(DateTime time, string message, WindowInfo target)
        {
            ScheduledTime = time;
            Message = message;
            TargetWindow = target;
        }

        public ScheduleEntry(DateTime time, string message, WindowInfo target, List<string> actions)
            : this(time, message, target)
        {
            Actions = actions ?? new List<string>();
        }
    }

    public class AlarmEntry
    {
        public DateTime AlarmTime { get; set; }
        public string Message { get; set; }

        public AlarmEntry(DateTime time, string message)
        {
            AlarmTime = time;
            Message = message;
        }
    }

    public class WindowInfo
    {
        public string Title { get; set; } = "";
        public IntPtr Handle { get; set; }
        public override string ToString() => Title;
    }
}
