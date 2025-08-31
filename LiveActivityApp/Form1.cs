using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LiveActivityApp
{
    public partial class Form1 : Form
    {
        // --- WinAPI & SendInput definitions ---
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // --- Constants and Fields ---
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const ushort SC_ENTER = 0x1C;

        // The main timer now runs continuously to update alarms.
        private readonly System.Windows.Forms.Timer _masterTimer = new System.Windows.Forms.Timer();
        // This flag controls whether the scheduler part of the timer is active.
        private bool _isSchedulerRunning = false;

        public Form1()
        {
            InitializeComponent();
            SetupApplication();
        }

        private void SetupApplication()
        {
            // The master timer starts immediately and runs forever.
            _masterTimer.Interval = 1000;
            _masterTimer.Tick += MasterTimer_Tick;
            _masterTimer.Start();

            // Wire up ALL button click events here for reliability
            btnRefresh.Click += btnRefresh_Click;
            btnAddSchedule.Click += btnAddSchedule_Click;
            btnDeleteSchedule.Click += btnDeleteSchedule_Click;
            btnStart.Click += btnStart_Click;
            btnStop.Click += btnStop_Click;
            btnAddAlarm.Click += btnAddAlarm_Click;
            btnDeleteAlarm.Click += btnDeleteAlarm_Click;

            // Initial UI state
            this.Text = "Alarm and Auto Tool"; // SET INITIAL WINDOW TITLE
            btnStop.Enabled = false;
            lblStatus.Text = "Status: Idle.";
            txtScheduleMessage.Text = "Type your message here";
            txtAlarmMessage.Text = "Type Alarm message here";

            PopulateRunningApps();
        }

        // --- Main Timer Tick (Handles BOTH systems) ---
        private void MasterTimer_Tick(object? sender, EventArgs e)
        {
            // The alarm display updates every single second, unconditionally.
            UpdateAlarmsDisplay();

            // The scheduler logic ONLY runs if the user has clicked "Start".
            if (_isSchedulerRunning)
            {
                RunSchedulerCheck();
            }
        }

        // --- SCHEDULER Section ---

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
                // Auto-stop if the schedule becomes empty
                btnStop_Click(this, EventArgs.Empty);
                lblStatus.Text = "Status: All scheduled tasks complete. Scheduler stopped.";
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
            var newEntry = new ScheduleEntry(dtpScheduleTime.Value, txtScheduleMessage.Text, selectedWindow);
            var listViewItem = new ListViewItem(newEntry.ScheduledTime.ToString("HH:mm:ss"));
            listViewItem.SubItems.Add(newEntry.Message);
            listViewItem.SubItems.Add(newEntry.TargetWindow.Title);
            listViewItem.Tag = newEntry;
            lvSchedule.Items.Add(listViewItem);
            lblStatus.Text = $"Status: Added schedule for {newEntry.ScheduledTime:g}."; // Using "g" for short date/time
        }

        private void ExecuteScheduledAction(ScheduleEntry entry)
        {
            if (!IsWindow(entry.TargetWindow.Handle))
            {
                lblStatus.Text = $"Status: SKIPPED. Window '{entry.TargetWindow.Title}' was closed.";
                return;
            }
            SetForegroundWindow(entry.TargetWindow.Handle);
            Thread.Sleep(200);
            SendKeys.SendWait(entry.Message + "{ENTER}");
            lblStatus.Text = $"Status: EXECUTED '{entry.Message}' at {DateTime.Now:T}.";
        }

        // --- ALARM Section ---

        private void UpdateAlarmsDisplay()
        {
            // Iterates through the alarm list and updates countdowns in place.
            foreach (ListViewItem item in lvAlarms.Items)
            {
                if (item.Tag is AlarmEntry entry)
                {
                    TimeSpan timeRemaining = entry.AlarmTime - DateTime.Now;
                    if (timeRemaining.TotalSeconds > 0)
                    {
                        // Now handles days correctly
                        item.SubItems[1].Text = timeRemaining.ToString(@"d\.hh\:mm\:ss");
                        item.BackColor = timeRemaining.TotalSeconds < 10 ? Color.LightCoral : SystemColors.Window;
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
            alarmItem.SubItems.Add("Calculating..."); // Placeholder
            alarmItem.Tag = newEntry;
            lvAlarms.Items.Add(alarmItem);
            // Updated status message to show the full date and time for clarity
            lblStatus.Text = $"Status: Added alarm for {newEntry.AlarmTime:g}.";
        }

        private void btnDeleteAlarm_Click(object sender, EventArgs e)
        {
            if (lvAlarms.SelectedItems.Count == 0)
            {
                lblStatus.Text = "Status: Select an alarm from the list to delete.";
                return;
            }
            foreach (ListViewItem selectedItem in lvAlarms.SelectedItems)
                lvAlarms.Items.Remove(selectedItem);
            lblStatus.Text = "Status: Selected alarm(s) deleted.";
        }


        // --- General UI and Helper Methods ---

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            PopulateRunningApps();
            lblStatus.Text = "Status: Window list has been refreshed.";
        }

        private void btnDeleteSchedule_Click(object sender, EventArgs e)
        {
            if (lvSchedule.SelectedItems.Count == 0) return;
            foreach (ListViewItem selectedItem in lvSchedule.SelectedItems)
                lvSchedule.Items.Remove(selectedItem);
            lblStatus.Text = "Status: Selected schedule(s) deleted.";
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (lvSchedule.Items.Count == 0)
            {
                lblStatus.Text = "Status: Cannot start. The schedule is empty.";
                return;
            }
            _isSchedulerRunning = true; // Set the flag
            LockSchedulerUI(true);
            Text = "Alarm and Auto Tool - RUNNING"; // UPDATE WINDOW TITLE
            lblStatus.Text = "Status: Scheduler is running...";
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _isSchedulerRunning = false; // Clear the flag
            LockSchedulerUI(false);
            Text = "Alarm and Auto Tool - STOPPED"; // UPDATE WINDOW TITLE
            lblStatus.Text = "Status: Scheduler stopped.";
        }

        private void PopulateRunningApps()
        {
            cmbWindowTitles.Items.Clear();
            try
            {
                foreach (Process p in Process.GetProcesses())
                {
                    if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                    {
                        cmbWindowTitles.Items.Add(new WindowInfo { Title = p.MainWindowTitle, Handle = p.MainWindowHandle });
                    }
                }
            }
            catch { /* Ignore errors */ }
            if (cmbWindowTitles.Items.Count > 0) cmbWindowTitles.SelectedIndex = 0;
        }

        // This now ONLY locks the controls related to the scheduler.
        private void LockSchedulerUI(bool isRunning)
        {
            btnStart.Enabled = !isRunning;
            btnStop.Enabled = isRunning;
            // The following controls are part of the scheduler function
            btnRefresh.Enabled = !isRunning;
            cmbWindowTitles.Enabled = !isRunning;
            dtpScheduleTime.Enabled = !isRunning;
            txtScheduleMessage.Enabled = !isRunning;
            btnAddSchedule.Enabled = !isRunning;
            btnDeleteSchedule.Enabled = !isRunning;
        }

        // --- P/Invoke structs and Keystroke helpers ---
        private static void SendKeyScan(ushort scanCode, bool keyUp)
        {
            var input = new INPUT[1];
            input[0].type = INPUT_KEYBOARD;
            input[0].U.ki.wScan = scanCode;
            input[0].U.ki.dwFlags = KEYEVENTF_SCANCODE | (keyUp ? KEYEVENTF_KEYUP : 0);
            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT { public int type; public InputUnion U; }
        [StructLayout(LayoutKind.Explicit)]
        internal struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    }

    // --- Data Classes ---
    public class ScheduleEntry
    {
        public DateTime ScheduledTime { get; set; }
        public string Message { get; set; }
        public WindowInfo TargetWindow { get; set; }
        public ScheduleEntry(DateTime time, string message, WindowInfo target)
        {
            ScheduledTime = time; Message = message; TargetWindow = target;
        }
    }

    public class AlarmEntry
    {
        public DateTime AlarmTime { get; set; }
        public string Message { get; set; }
        public AlarmEntry(DateTime time, string message)
        {
            AlarmTime = time; Message = message;
        }
    }

    public class WindowInfo
    {
        public string Title { get; set; } = "";
        public IntPtr Handle { get; set; }
        public override string ToString() => Title;
    }
}

