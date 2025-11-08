using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LiveActivityApp
{
    internal sealed class EditScheduleForm : Form
    {
        private readonly ScheduleEntry _entry;

        private DateTimePicker dtpTime;
        private TextBox txtMessage;
        private ComboBox cbWindow;
        private ComboBox cbAction1;
        private ComboBox cbAction2;
        private ComboBox cbDelay;
        private Button btnOK;
        private Button btnCancel;

        public EditScheduleForm(
            ScheduleEntry entry,
            IReadOnlyList<string> allowedTokens,
            IReadOnlyList<WindowInfo> windows)
        {
            _entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Text = "Edit Schedule";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ClientSize = new Size(520, 270);

            // --- Controls ---
            var lblTime = new Label { Text = "Time:", AutoSize = true, Left = 12, Top = 16 };
            dtpTime = new DateTimePicker
            {
                Left = 120,
                Top = 12,
                Width = 370,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd HH:mm:ss",
                ShowUpDown = true,
                Value = _entry.ScheduledTime
            };

            var lblMsg = new Label { Text = "Message:", AutoSize = true, Left = 12, Top = 50 };
            txtMessage = new TextBox { Left = 120, Top = 46, Width = 370, Text = _entry.Message };

            var lblWin = new Label { Text = "Target Window:", AutoSize = true, Left = 12, Top = 84 };
            cbWindow = new ComboBox
            {
                Left = 120,
                Top = 80,
                Width = 370,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            // Populate window list (keep original first so it's selectable even if closed)
            var windowList = new List<WindowInfo>();
            if (_entry.TargetWindow != null)
                windowList.Add(_entry.TargetWindow);
            foreach (var w in windows ?? Array.Empty<WindowInfo>())
            {
                if (!windowList.Any(x => x.Handle == w.Handle))
                    windowList.Add(w);
            }
            cbWindow.Items.AddRange(windowList.ToArray());
            if (_entry.TargetWindow != null)
                cbWindow.SelectedItem = windowList.FirstOrDefault(x => x.Handle == _entry.TargetWindow.Handle)
                                        ?? windowList[0];
            else if (cbWindow.Items.Count > 0)
                cbWindow.SelectedIndex = 0;

            var lblAction1 = new Label { Text = "Action #1:", AutoSize = true, Left = 12, Top = 118 };
            cbAction1 = new ComboBox { Left = 120, Top = 114, Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblAction2 = new Label { Text = "Action #2:", AutoSize = true, Left = 300, Top = 118 };
            cbAction2 = new ComboBox { Left = 370, Top = 114, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };

            var tokens = allowedTokens ?? Array.Empty<string>();
            cbAction1.Items.AddRange(tokens.ToArray());
            cbAction2.Items.AddRange(tokens.ToArray());

            // Preselect actions from entry (uses first two, else "NONE")
            string a1 = "NONE", a2 = "NONE";
            if (_entry.Actions != null && _entry.Actions.Count > 0) a1 = _entry.Actions[0];
            if (_entry.Actions != null && _entry.Actions.Count > 1) a2 = _entry.Actions[1];
            cbAction1.SelectedItem = tokens.Contains(a1) ? a1 : "NONE";
            cbAction2.SelectedItem = tokens.Contains(a2) ? a2 : "NONE";

            var lblDelay = new Label { Text = "Action Delay (s):", AutoSize = true, Left = 12, Top = 152 };
            cbDelay = new ComboBox { Left = 120, Top = 148, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            if (cbDelay.Items.Count == 0)
            {
                for (double s = 0.5; s <= 5.0 + 1e-6; s += 0.5)
                    cbDelay.Items.Add(s.ToString("0.##"));
            }
            // Preselect delay
            string delayStr = _entry.ActionDelaySeconds.ToString("0.##");
            if (cbDelay.Items.Contains(delayStr)) cbDelay.SelectedItem = delayStr;
            else cbDelay.SelectedIndex = 1; // default ~1s

            btnOK = new Button { Text = "OK", Left = 324, Width = 80, Top = 208, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Left = 410, Width = 80, Top = 208, DialogResult = DialogResult.Cancel };

            btnOK.Click += (_, __) =>
            {
                ApplyEdits();
                DialogResult = DialogResult.OK;
                Close();
            };
            btnCancel.Click += (_, __) => Close();

            Controls.AddRange(new Control[]
            {
                lblTime, dtpTime,
                lblMsg, txtMessage,
                lblWin, cbWindow,
                lblAction1, cbAction1,
                lblAction2, cbAction2,
                lblDelay, cbDelay,
                btnOK, btnCancel
            });
        }

        private void ApplyEdits()
        {
            _entry.ScheduledTime = dtpTime.Value;
            _entry.Message = txtMessage.Text ?? string.Empty;

            if (cbWindow.SelectedItem is WindowInfo win)
                _entry.TargetWindow = win;

            // Rebuild actions from the two dropdowns, skipping "NONE"
            var actions = new List<string>();
            void addIfValid(ComboBox cb)
            {
                var t = cb.SelectedItem as string;
                if (!string.IsNullOrWhiteSpace(t) && !t.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                    actions.Add(t);
            }
            addIfValid(cbAction1);
            addIfValid(cbAction2);
            _entry.Actions = actions
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (double.TryParse((cbDelay.SelectedItem as string) ?? "1", out var d))
                _entry.ActionDelaySeconds = Math.Max(0, Math.Min(5.0, d));
        }
    }
}
