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
        static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool BlockInput(bool fBlockIt);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

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

        // ===== Focus/activation interop =====
        [DllImport("user32.dll")]
        static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        // --- Elevation helpers (place inside Form1, not in LowLevelInputSuppressor) ---
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, int TokenInformationLength, out int ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        const uint TOKEN_QUERY = 0x0008;
        const int TokenElevation = 20;

        // Simple logging helper
        private void Log(string msg)
        {
            try { Debug.WriteLine($"[LiveActivityApp] {msg}"); } catch { }
            lblStatus.Text = $"Status: {msg}";
        }

        // Send a key by virtual key code
        private bool TypeTextSmart(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;

            // 1) Try Unicode SendInput (best for arbitrary chars)
            var inputs = new List<INPUT>(text.Length * 2);
            foreach (var ch in text)
            {
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = (ushort)ch,
                            dwFlags = KEYEVENTF_UNICODE,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                });
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = (ushort)ch,
                            dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                });
            }

            var sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            if (sent == inputs.Count) return true;

            // 2) Fallback: SendKeys literal text for simple ASCII
            // This is a last resort for apps that ignore KEYEVENTF_UNICODE.
            try
            {
                SendKeys.SendWait(text);   // will type literally; no {ENTER} syntax unless you pass it
                return true;
            }
            catch (Exception ex)
            {
                Log($"Typing fallback failed: {ex.Message}");
                return false;
            }
        }

        // Check if the process owning the given window handle is elevated
        private static bool IsProcessElevatedByHwnd(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return false;

            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc == IntPtr.Zero) return false;

            try
            {
                if (!OpenProcessToken(hProc, TOKEN_QUERY, out var hTok) || hTok == IntPtr.Zero) return false;
                try
                {
                    int size;
                    GetTokenInformation(hTok, TokenElevation, IntPtr.Zero, 0, out size);
                    IntPtr buf = Marshal.AllocHGlobal(size);
                    try
                    {
                        if (!GetTokenInformation(hTok, TokenElevation, buf, size, out _)) return false;
                        int elevated = Marshal.ReadInt32(buf);
                        return elevated != 0;
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
                finally { CloseHandle(hTok); }
            }
            finally { CloseHandle(hProc); }
        }

        // Check if THIS process is elevated
        private static bool ThisProcessIsElevated()
        {
            using (var p = Process.GetCurrentProcess())
            {
                if (!OpenProcessToken(p.Handle, TOKEN_QUERY, out var hTok) || hTok == IntPtr.Zero) return false;
                try
                {
                    int size;
                    GetTokenInformation(hTok, TokenElevation, IntPtr.Zero, 0, out size);
                    IntPtr buf = Marshal.AllocHGlobal(size);
                    try
                    {
                        if (!GetTokenInformation(hTok, TokenElevation, buf, size, out _)) return false;
                        int elevated = Marshal.ReadInt32(buf);
                        return elevated != 0;
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
                finally { CloseHandle(hTok); }
            }
        }

        // --- SendInput helpers ---
        private const uint FLASHW_TRAY = 0x00000002;
        private const uint FLASHW_TIMERNOFG = 0x0000000C;


        // =========================
        // App state
        // =========================

        private readonly System.Windows.Forms.Timer _masterTimer = new System.Windows.Forms.Timer();
        private bool _isSchedulerRunning = false;

        // Tray-related fields
        private NotifyIcon? _trayIcon;
        private ContextMenuStrip? _trayMenu;
        private bool _exiting = false;

        // NEW: Thread-safe list for schedule entries to avoid collection-modification errors
        private readonly object _scheduleLock = new object();

        public Form1()
        {
            InitializeComponent();
            SetupApplication();
            this.FormClosed += (_, __) => _trayIcon?.Dispose();
            if (!IsDisposed && _trayIcon != null) RebuildTrayMenu();
        }

        /// <summary>
        /// Handles double-clicking a schedule entry to edit it.
        /// Now allows editing even while scheduler is running (but warns user).
        /// </summary>
        private void lvSchedule_DoubleClick(object? sender, EventArgs e)
        {
            // IMPROVED: Allow editing while running, but show warning
            if (_isSchedulerRunning)
            {
                var result = MessageBox.Show(
                    "The scheduler is currently running. Editing this entry may cause unexpected behavior.\n\nDo you want to continue?",
                    "Warning",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes) return;
            }

            if (lvSchedule.SelectedItems.Count == 0) return;

            var item = lvSchedule.SelectedItems[0];
            if (item.Tag is not ScheduleEntry entry) return;

            // Build a stable snapshot of current window candidates
            var windows = new List<WindowInfo>();
            foreach (var it in cmbWindowTitles.Items)
                if (it is WindowInfo w) windows.Add(w);

            using var dlg = new EditScheduleForm(entry, BuildAllowedTokens(), windows);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                // Thread-safe update to the entry
                lock (_scheduleLock)
                {
                    // Reflect changes back to the ListView row
                    item.Text = entry.ScheduledTime.ToString("HH:mm:ss");
                    if (item.SubItems.Count > 1) item.SubItems[1].Text = entry.Message;
                    if (item.SubItems.Count > 2) item.SubItems[2].Text = entry.TargetWindow?.Title ?? "";
                    if (item.SubItems.Count > 3) item.SubItems[3].Text = FormatActionsLog(entry.Actions, entry.ActionDelaySeconds);
                    lvSchedule.Refresh();
                }
                lblStatus.Text = "Status: Schedule updated.";
            }
        }

        // Tray mode helpers

        /// Force-release all keys that might be down so no stray chars leak in.
        private static void ReleaseAllKeys()
        {
            // Cover common virtual keys (0x08..0xFE is plenty)
            for (int vk = 0x08; vk <= 0xFE; vk++)
            {
                // If the high bit is set, Windows thinks the key is currently down.
                if ((GetAsyncKeyState(vk) & 0x8000) != 0)
                {
                    SendKeyVK((byte)vk, true); // send KEYUP
                                               // tiny breather helps with typematic buffers
                    Thread.Sleep(1);
                }
            }
        }
        // underlying SendInput for a single key by virtual key code
        private const uint KEYEVENTF_UNICODE = 0x0004;
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

            // button wiring
            btnRefresh.Click += btnRefresh_Click;
            lvSchedule.DoubleClick += lvSchedule_DoubleClick;
            btnAddSchedule.Click += btnAddSchedule_Click;
            btnDeleteSchedule.Click += btnDeleteSchedule_Click;
            btnAddAlarm.Click += btnAddAlarm_Click;
            btnDeleteAlarm.Click += btnDeleteAlarm_Click;

            // action dropdowns with static token list including "NONE"
            var tokens = BuildAllowedTokens();

            cbAction.DropDownStyle = ComboBoxStyle.DropDownList;
            cbAction.Items.Clear();
            cbAction.Items.AddRange(tokens.ToArray());
            if (cbAction.SelectedIndex < 0) cbAction.SelectedIndex = 0;

            cbAction2.DropDownStyle = ComboBoxStyle.DropDownList;
            cbAction2.Items.Clear();
            cbAction2.Items.AddRange(tokens.ToArray());
            if (cbAction2.SelectedIndex < 0) cbAction2.SelectedIndex = 0;

            // tray icon setup
            SetupTray();
            this.Resize += Form1_Resize;
            this.FormClosing += Form1_FormClosing;

            // delay dropdown (0.5..5.0 step 0.5)
            cbActionDelay.DropDownStyle = ComboBoxStyle.DropDownList;
            if (cbActionDelay.Items.Count == 0)
            {
                for (double s = 0.5; s <= 5.0 + 1e-6; s += 0.5)
                    cbActionDelay.Items.Add(s.ToString("0.##"));
            }
            if (cbActionDelay.SelectedIndex < 0) cbActionDelay.SelectedIndex = 1;

            EnsureScheduleColumns();

            // defensive: detach any auto-wired label3 click
            try { label3.Click -= label3_Click; } catch { /* ignore */ }

            // tray mode checkbox
            chkTrayMode.CheckedChanged += chkTrayMode_CheckedChanged;
            chkTrayMode.Checked = true;
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
        // Master tick - runs every second to check schedules and update alarms
        // =========================================================================
        private void MasterTimer_Tick(object? sender, EventArgs e)
        {
            UpdateAlarmsDisplay();
            if (_isSchedulerRunning) RunSchedulerCheck();
        }

        // =========================
        // SCHEDULER
        // =========================

        /// <summary>
        /// Checks all scheduled entries and executes any that are due.
        /// IMPROVED: Thread-safe iteration and better error handling.
        /// </summary>
        private void RunSchedulerCheck()
        {
            var now = DateTime.Now;
            var toRemove = new List<ListViewItem>();

            // 1) Take a snapshot of current rows so we can iterate safely
            List<ListViewItem> snapshot;
            lock (_scheduleLock)
            {
                snapshot = new List<ListViewItem>(lvSchedule.Items.Count);
                foreach (ListViewItem it in lvSchedule.Items) snapshot.Add(it);
            }

            // 2) Work over the snapshot; do NOT touch lvSchedule.Items inside this loop
            foreach (var item in snapshot)
            {
                if (item?.Tag is not ScheduleEntry entry) continue;

                // due within a 2-second window
                if (now >= entry.ScheduledTime && (now - entry.ScheduledTime) < TimeSpan.FromSeconds(2))
                {
                    if (!IsWindow(entry.TargetWindow.Handle))
                    {
                        item.BackColor = Color.LightYellow;
                        item.ToolTipText = "Window was closed before execution";
                        lblStatus.Text = $"Status: SKIPPED '{entry.Message}' - window closed.";
                    }
                    else
                    {
                        ExecuteScheduledAction(entry);
                        toRemove.Add(item);
                    }
                }
            }

            // 3) Remove executed rows by reference (only if still present)
            lock (_scheduleLock)
            {
                foreach (var item in toRemove)
                {
                    if (item.ListView == lvSchedule) lvSchedule.Items.Remove(item);
                }
            }

            // 4) Auto-stop when schedule is empty
            if (lvSchedule.Items.Count == 0)
            {
                btnStop_Click(this, EventArgs.Empty);
                lblStatus.Text = "Status: All scheduled tasks complete. Scheduler stopped.";
                RebuildTrayMenu();
            }
        }

        /// <summary>
        /// Adds a new schedule entry to the list.
        /// IMPROVED: Better validation and clearer error messages.
        /// Now allows adding even while scheduler is running.
        /// </summary>
        private void btnAddSchedule_Click(object sender, EventArgs e)
        {
            // VALIDATION: Check for required inputs
            if (cmbWindowTitles.SelectedIndex < 0)
            {
                MessageBox.Show(
                    "Please select a target window from the dropdown.\n\nClick 'Refresh List' to update available windows.",
                    "No Window Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                lblStatus.Text = "Status: Select a window first.";
                return;
            }

            if (string.IsNullOrWhiteSpace(txtScheduleMessage.Text))
            {
                MessageBox.Show(
                    "Please enter a message to send.\n\nThe message box cannot be empty.",
                    "No Message Entered",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                lblStatus.Text = "Status: Enter a message first.";
                return;
            }

            // VALIDATION: Check if scheduled time is in the past
            if (dtpScheduleTime.Value < DateTime.Now)
            {
                var result = MessageBox.Show(
                    $"The scheduled time ({dtpScheduleTime.Value:g}) is in the past.\n\nDo you want to add it anyway?",
                    "Past Schedule Time",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes) return;
            }

            var selectedWindow = (WindowInfo)cmbWindowTitles.Items[cmbWindowTitles.SelectedIndex];

            // VALIDATION: Check if window still exists
            if (!IsWindow(selectedWindow.Handle))
            {
                var result = MessageBox.Show(
                    $"The selected window '{selectedWindow.Title}' appears to be closed.\n\nDo you want to add it to the schedule anyway?",
                    "Window May Be Closed",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    lblStatus.Text = "Status: Window closed. Refresh the list or select another window.";
                    return;
                }
            }

            // Build actions from BOTH dropdowns, ignoring "NONE"
            var actions = new List<string>();
            AddActionIfValid(actions, cbAction);
            AddActionIfValid(actions, cbAction2);

            double delaySec = GetSelectedDelaySeconds();

            var newEntry = new ScheduleEntry(dtpScheduleTime.Value, txtScheduleMessage.Text, selectedWindow, actions)
            {
                ActionDelaySeconds = delaySec
            };

            // Thread-safe addition to the schedule
            lock (_scheduleLock)
            {
                // Row: Time | Message | Target Window | Logged Commands
                var row = new ListViewItem(newEntry.ScheduledTime.ToString("HH:mm:ss"));
                row.SubItems.Add(newEntry.Message);
                row.SubItems.Add(newEntry.TargetWindow.Title);
                row.SubItems.Add(FormatActionsLog(newEntry.Actions, newEntry.ActionDelaySeconds));
                row.Tag = newEntry;

                lvSchedule.Items.Add(row);
            }

            lblStatus.Text = $"Status: Added schedule for {newEntry.ScheduledTime:g}.";

            // IMPROVED: Clear the message box for next entry (optional convenience)
            // Uncomment if you want this behavior:
            // txtScheduleMessage.Clear();
        }

        /// <summary>
        /// Executes a scheduled action: focuses window, types message, then executes selected actions.
        /// IMPROVED: Better error handling and user feedback.
        /// </summary>
        private void ExecuteScheduledAction(ScheduleEntry entry)
        {
            if (entry?.TargetWindow == null)
            {
                lblStatus.Text = "Status: SKIPPED. No target window.";
                return;
            }

            // Validate window handle
            var hWnd = ResolveHandle(entry);
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
            {
                Log($"SKIPPED. Window '{entry.TargetWindow?.Title}' not found.");
                return;
            }

            // Elevation check
            bool targetElevated = IsProcessElevatedByHwnd(hWnd);
            bool meElevated = ThisProcessIsElevated();
            if (targetElevated && !meElevated)
            {
                Log("SKIPPED. Target app is Admin. Run this app as Admin to type into it.");
                return;
            }

            // 1) Just bring to front; no input blocking/suppression
            if (!BringToFrontReliable(hWnd))
            {
                Log("Could not bring target to foreground.");
                return;
            }

            // Small settle so focus completes
            Thread.Sleep(150);

            // 2) Type the message
            if (!TypeTextSmart(entry.Message))
            {
                Log("Typing failed (SendInput & SendKeys).");
                return;
            }

            Application.DoEvents();
            Thread.Sleep(150);  // tiny UI settle

            // 3) Run actions (ENTER, CTRL+ENTER, etc.) if any
            if (entry.Actions?.Count > 0)
            {
                if (entry.ActionDelaySeconds > 0)
                    Thread.Sleep((int)Math.Round(entry.ActionDelaySeconds * 1000.0));

                ExecuteActions(entry.Actions, entry.ActionDelaySeconds);
            }

            // 4) Status
            lblStatus.Text = (entry.Actions != null && entry.Actions.Count > 0)
                ? $"Status: EXECUTED '{entry.Message}' + {entry.Actions.Count} action(s) at {DateTime.Now:T}."
                : $"Status: Typed message only at {DateTime.Now:T}.";
        }

        // Reliable window focusing helper
        private static void PressEnter_VK()
        {
            SendKeyVK(VK_RETURN, false); // key down
            Thread.Sleep(5);
            SendKeyVK(VK_RETURN, true);  // key up
        }

        // =========================
        // ALARMS
        // =========================

        /// <summary>
        /// Updates the countdown display for all alarms.
        /// Changes color when alarm is close (< 10 seconds).
        /// </summary>
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
            // VALIDATION: Check for alarm message
            if (string.IsNullOrWhiteSpace(txtAlarmMessage.Text))
            {
                MessageBox.Show(
                    "Please enter a message for the alarm.",
                    "No Alarm Message",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                lblStatus.Text = "Status: Please enter an alarm message.";
                return;
            }

            // VALIDATION: Check if alarm time is in the past
            if (dtpAlarmTime.Value < DateTime.Now)
            {
                var result = MessageBox.Show(
                    $"The alarm time ({dtpAlarmTime.Value:g}) is in the past.\n\nDo you want to add it anyway?",
                    "Past Alarm Time",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes) return;
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

        private void Form1_Resize(object? sender, EventArgs e)
        {
            if (IsTrayMode && WindowState == FormWindowState.Minimized)
                HideToTray();
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (IsTrayMode && !_exiting)
            {
                e.Cancel = true;
                HideToTray();
            }
        }

        private void HideToTray()
        {
            if (_trayIcon != null) _trayIcon.Visible = true;
            ShowInTaskbar = false;
            Hide();
        }

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

        /// <summary>
        /// Deletes selected schedule entries.
        /// IMPROVED: Now allows deletion even while scheduler is running (with warning).
        /// </summary>
        private void btnDeleteSchedule_Click(object sender, EventArgs e)
        {
            if (lvSchedule.SelectedItems.Count == 0)
            {
                lblStatus.Text = "Status: Select a schedule entry to delete.";
                return;
            }

            // IMPROVED: Warn if deleting while scheduler is running
            if (_isSchedulerRunning)
            {
                var result = MessageBox.Show(
                    "The scheduler is currently running.\n\nAre you sure you want to delete the selected entry?",
                    "Confirm Deletion",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes) return;
            }

            // Thread-safe deletion
            lock (_scheduleLock)
            {
                foreach (ListViewItem it in lvSchedule.SelectedItems)
                    lvSchedule.Items.Remove(it);
            }

            lblStatus.Text = "Status: Selected schedule(s) deleted.";
        }

        /// <summary>
        /// Starts the scheduler.
        /// IMPROVED: Better validation before starting.
        /// </summary>
        private void btnStart_Click(object sender, EventArgs e)
        {
            if (lvSchedule.Items.Count == 0)
            {
                MessageBox.Show(
                    "The schedule is empty.\n\nAdd at least one scheduled task before starting the scheduler.",
                    "Cannot Start",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                lblStatus.Text = "Status: Cannot start. The schedule is empty.";
                return;
            }

            // VALIDATION: Check if any scheduled tasks have valid windows
            bool hasValidWindow = false;
            lock (_scheduleLock)
            {
                foreach (ListViewItem item in lvSchedule.Items)
                {
                    if (item.Tag is ScheduleEntry entry && IsWindow(entry.TargetWindow.Handle))
                    {
                        hasValidWindow = true;
                        break;
                    }
                }
            }

            if (!hasValidWindow)
            {
                var result = MessageBox.Show(
                    "Warning: None of the target windows in your schedule appear to be open.\n\nDo you want to start the scheduler anyway?",
                    "No Valid Windows",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes) return;
            }

            _isSchedulerRunning = true;
            LockSchedulerUI(true);
            Text = "Alarm and Auto Tool - RUNNING";
            lblStatus.Text = "Status: Scheduler is running...";
            RebuildTrayMenu();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _isSchedulerRunning = false;
            LockSchedulerUI(false);
            Text = "Alarm and Auto Tool - STOPPED";
            lblStatus.Text = "Status: Scheduler stopped.";
            RebuildTrayMenu();
        }

        /// <summary>
        /// Populates the window list with all currently running applications that have a visible window.
        /// </summary>
        private void PopulateRunningApps()
        {
            cmbWindowTitles.Items.Clear();

            try
            {
                foreach (Process p in Process.GetProcesses())
                {
                    // Only include processes with a main window and a non-empty title
                    if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                        cmbWindowTitles.Items.Add(new WindowInfo { Title = p.MainWindowTitle, Handle = p.MainWindowHandle });
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Status: Error refreshing windows: {ex.Message}";
            }

            if (cmbWindowTitles.Items.Count > 0)
                cmbWindowTitles.SelectedIndex = 0;
            else
                lblStatus.Text = "Status: No windows found. Make sure applications are running.";
        }

        /// <summary>
        /// Locks or unlocks the scheduler UI based on running state.
        /// IMPROVED: Now only locks controls that shouldn't be modified while running.
        /// Allows adding new entries even while running.
        /// </summary>
        private void LockSchedulerUI(bool isRunning)
        {
            btnStart.Enabled = !isRunning;
            btnStop.Enabled = isRunning;

            // IMPROVED: Keep these enabled so users can add entries while running
            btnRefresh.Enabled = true;  // Always allow refresh
            cmbWindowTitles.Enabled = true;  // Always allow window selection
            txtScheduleMessage.Enabled = true;  // Always allow message entry
            btnAddSchedule.Enabled = true;  // Always allow adding new entries
            btnDeleteSchedule.Enabled = true;  // Always allow deletion (with warning)

            // Action pickers remain available
            cbAction.Enabled = true;
            cbAction2.Enabled = true;
            cbActionDelay.Enabled = true;
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

        // === Replaced helpers: now return bool so we can detect failure/fallbacks ===

        // Scan-code sender (returns whether the single event was injected)
        private static bool SendKeyScan(ushort scanCode, bool keyUp, bool extended = false)
        {
            var input = new INPUT[1];
            input[0].type = INPUT_KEYBOARD;
            input[0].U.ki.wVk = 0;
            input[0].U.ki.wScan = scanCode;
            input[0].U.ki.dwFlags = KEYEVENTF_SCANCODE
                                   | (keyUp ? KEYEVENTF_KEYUP : 0)
                                   | (extended ? KEYEVENTF_EXTENDEDKEY : 0);
            input[0].U.ki.time = 0;
            input[0].U.ki.dwExtraInfo = IntPtr.Zero;

            var sent = SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
            return sent == 1;
        }

        // VK sender (returns whether the single event was injected)
        private static bool SendKeyVK(byte vk, bool keyUp)
        {
            var input = new INPUT[1];
            input[0].type = INPUT_KEYBOARD;
            input[0].U.ki.wVk = vk;
            input[0].U.ki.wScan = 0;
            input[0].U.ki.dwFlags = keyUp ? KEYEVENTF_KEYUP : 0;
            input[0].U.ki.time = 0;
            input[0].U.ki.dwExtraInfo = IntPtr.Zero;

            var sent = SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
            return sent == 1;
        }

        // Unicode character sender (returns whether both events were injected)
        private static bool SendUnicodeChar(ushort ch)
        {
            var inputs = new INPUT[2];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = 0;
            inputs[0].U.ki.wScan = ch;
            inputs[0].U.ki.dwFlags = KEYEVENTF_UNICODE;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = IntPtr.Zero;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = 0;
            inputs[1].U.ki.wScan = ch;
            inputs[1].U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = IntPtr.Zero;

            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            return sent == inputs.Length;
        }

        // Make the robust Enter try multiple paths in order.
        private static bool PressEnter_Robust()
        {
            // 1) Primary: main Enter by scancode (no extended flag)
            if (SendKeyScan(SC_ENTER, false, false) && SendKeyScan(SC_ENTER, true, false))
                return true;

            // 2) VK fallback
            if (SendKeyVK(VK_RETURN, false) && SendKeyVK(VK_RETURN, true))
                return true;

            // 3) Numpad-style (extended) scancode try
            if (SendKeyScan(SC_ENTER, false, true) && SendKeyScan(SC_ENTER, true, true))
                return true;

            // 4) Unicode CR
            if (SendUnicodeChar(0x000D))
                return true;

            // 5) Old-school SendKeys
            try { SendKeys.SendWait("{ENTER}"); return true; } catch { return false; }
        }



        // Send a key using virtual key code
        private static void PressEnter_ScanMain()
        {
            SendKeyScan(SC_ENTER, keyUp: false, extended: false);
            Thread.Sleep(5);
            SendKeyScan(SC_ENTER, keyUp: true, extended: false);
        }

        // Send a key using scan code (SC_*)
        private static void PressEnter_ScanNumpad()
        {
            SendKeyScan(SC_ENTER, false, true);
            Thread.Sleep(5);
            SendKeyScan(SC_ENTER, true, true);
        }

        /// Try to re-hydrate/refresh the target window handle right before execution.
        /// 1) Use the existing handle if still valid.
        /// 2) If stale, find a process whose MainWindowTitle matches the stored title.
        private static IntPtr FindWindowByExactTitle(string title)
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainWindowHandle != IntPtr.Zero &&
                        !string.IsNullOrEmpty(p.MainWindowTitle) &&
                        string.Equals(p.MainWindowTitle, title, StringComparison.Ordinal))
                    {
                        return p.MainWindowHandle;
                    }
                }
                catch { /* ignore processes we can't access */ }
            }
            return IntPtr.Zero;
        }

        private IntPtr ResolveHandle(ScheduleEntry entry)
        {
            if (entry?.TargetWindow == null) return IntPtr.Zero;

            // If the stored handle is still valid, use it.
            if (entry.TargetWindow.Handle != IntPtr.Zero && IsWindow(entry.TargetWindow.Handle))
                return entry.TargetWindow.Handle;

            // Otherwise, try to re-find by exact title.
            var fresh = FindWindowByExactTitle(entry.TargetWindow.Title ?? "");
            if (fresh != IntPtr.Zero)
                entry.TargetWindow.Handle = fresh;

            return entry.TargetWindow.Handle;
        }

        /// Tiny ALT tap to make Windows think "recent user input" happened.
        /// Makes SetForegroundWindow more likely to succeed.
        private void AltNudge()
        {
            // Uses your existing SendKeyVK helper (VK_MENU is ALT)
            SendKeyVK(VK_MENU, false); // ALT down
            Thread.Sleep(30);
            SendKeyVK(VK_MENU, true);  // ALT up
        }

        /// If the window is minimized, restore it before focusing.
        private void ShowWindowRestore(IntPtr hWnd)
        {
            ShowWindowAsync(hWnd, SW_SHOW);
            if (IsIconic(hWnd)) ShowWindowAsync(hWnd, SW_RESTORE);
        }

        /// Optional user cue if we still can’t take focus: flash the taskbar icon once.
        private void FlashOnce(IntPtr hWnd)
        {
            var fw = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                hwnd = hWnd,
                dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG,
                uCount = 3,
                dwTimeout = 0
            };
            FlashWindowEx(ref fw);
        }

        /// Temporarily attach this thread to the foreground thread and try SetForegroundWindow.
        private bool AttachThreadInputFocus(IntPtr hWnd)
        {
            var currentThread = GetCurrentThreadId();

            var fg = GetForegroundWindow();
            uint fgThread = 0;
            if (fg != IntPtr.Zero) fgThread = GetWindowThreadProcessId(fg, out _);

            uint targetThread = GetWindowThreadProcessId(hWnd, out _);

            if (fgThread == 0 || targetThread == 0) return false;

            try
            {
                // Attach to both the foreground thread and the target thread
                AttachThreadInput(currentThread, fgThread, true);
                AttachThreadInput(currentThread, targetThread, true);

                // Try to bring it forward now
                if (SetForegroundWindow(hWnd))
                    return true;
            }
            finally
            {
                // Always detach
                AttachThreadInput(currentThread, targetThread, false);
                AttachThreadInput(currentThread, fgThread, false);
            }
            return false;
        }

        /// Last resort “topmost flick” to make it visible to the user.
        private void TopMostFlick(IntPtr hWnd)
        {
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// One-stop focus shim: restore → alt nudge → SetForegroundWindow → attach-thread fallback → topmost flick.
        /// Returns true if we *believe* the focus landed on the target.
        private bool BringToFrontReliable(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return false;

            // 1) Make sure it's showing
            ShowWindowRestore(hWnd);
            Thread.Sleep(50);

            // 2) Cheap nudge
            AltNudge();
            if (SetForegroundWindow(hWnd)) return true;

            // 3) Thread-attach shim
            if (AttachThreadInputFocus(hWnd)) return true;

            // 4) Topmost flick + try again
            TopMostFlick(hWnd);
            Thread.Sleep(30);
            if (SetForegroundWindow(hWnd)) return true;

            // 5) Flash so the user can click if Windows still refuses
            FlashOnce(hWnd);
            return false;
        }


        /// <summary>
        /// Sends Enter key robustly using both SendInput and SendKeys as fallback.
        /// Some applications only respond to one method or the other.
        /// </summary>
        /// 
        // Keep the name the rest of your code calls:
        private static bool UseScancodeEnter = true; // set true to test

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

        /// <summary>
        /// Executes a list of keyboard actions with optional delays between each action.
        /// Supports: ENTER, CTRL+ENTER, TAB, ALT+TAB, and various letter combinations with modifiers.
        /// </summary>
        /// <param name="actions">List of action tokens to execute (e.g., "ENTER", "CTRL+A")</param>
        /// <param name="delaySeconds">Delay in seconds between actions</param>
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
                    SendKeyVK(VK_CONTROL, false);   // CTRL down
                    Thread.Sleep(5);
                    PressEnter_Robust();         // one Enter only
                    Thread.Sleep(5);
                    SendKeyVK(VK_CONTROL, true);    // CTRL up
                }
                else if (t == "TAB")
                {
                    if (!(SendKeyVK(VK_TAB, false) && SendKeyVK(VK_TAB, true)))
                    {
                        if (!SendUnicodeChar(0x0009))
                        {
                            try { SendKeys.SendWait("{TAB}"); } catch { /* ignore */ }
                        }
                    }
                }
                else if (t == "ALT+TAB")
                {
                    SendKeyVK(VK_MENU, false); // ALT down
                    Thread.Sleep(5);
                    if (!(SendKeyVK(VK_TAB, false) && SendKeyVK(VK_TAB, true)))
                        try { SendKeys.SendWait("%{TAB}"); } catch { /* ignore */ }
                    Thread.Sleep(5);
                    SendKeyVK(VK_MENU, true);  // ALT up
                }
                else if (t.StartsWith("SHIFT+") || t.StartsWith("CTRL+") || t.StartsWith("ALT+"))
                {
                    // Handle modifier+letter combinations (e.g., CTRL+C, SHIFT+A)
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

                // Apply delay between actions (but not after the last one)
                if (i != last && delaySeconds > 0)
                {
                    int ms = (int)Math.Round(delaySeconds * 1000.0);
                    Thread.Sleep(ms);
                }
                Debug.WriteLine($"[Actions] Running token: {t}");// For debugging
            }
        }

        // Virtual key codes
        private const byte VK_RETURN = 0x0D;


        private void cbAction_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Reserved for future use if needed
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

    /// <summary>
    /// Represents a scheduled task to be executed at a specific time.
    /// Includes the target window, message to send, and optional keyboard actions.
    /// </summary>
    public class ScheduleEntry
    {
        public DateTime ScheduledTime { get; set; }
        public string Message { get; set; }
        public WindowInfo TargetWindow { get; set; }

        /// <summary>
        /// List of keyboard actions to execute after typing the message.
        /// Examples: "ENTER", "CTRL+ENTER", "TAB", "CTRL+C"
        /// </summary>
        public List<string> Actions { get; set; } = new List<string>();

        /// <summary>
        /// Delay in seconds between each action execution.
        /// Valid range: 0.5 to 5.0 seconds
        /// </summary>
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

    /// <summary>
    /// Represents an alarm that displays a countdown and shows a reminder message.
    /// </summary>
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

    /// <summary>
    /// Represents a window with its title and handle for targeting keyboard input.
    /// </summary>
    public class WindowInfo
    {
        public string Title { get; set; } = "";
        public IntPtr Handle { get; set; }
        public override string ToString() => Title;
    }
}  