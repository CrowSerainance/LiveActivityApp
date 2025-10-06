
namespace LiveActivityApp
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label2 = new Label();
            btnStart = new Button();
            btnStop = new Button();
            cmbWindowTitles = new ComboBox();
            btnRefresh = new Button();
            lblStatus = new Label();
            lvSchedule = new ListView();
            columnHeader1 = new ColumnHeader();
            columnHeader2 = new ColumnHeader();
            columnHeader3 = new ColumnHeader();
            columnHeader6 = new ColumnHeader();
            dtpScheduleTime = new DateTimePicker();
            txtScheduleMessage = new TextBox();
            btnAddSchedule = new Button();
            btnDeleteSchedule = new Button();
            label1 = new Label();
            label3 = new Label();
            lvAlarms = new ListView();
            columnHeader4 = new ColumnHeader();
            columnHeader5 = new ColumnHeader();
            label4 = new Label();
            label5 = new Label();
            txtAlarmMessage = new TextBox();
            label6 = new Label();
            dtpAlarmTime = new DateTimePicker();
            btnAddAlarm = new Button();
            btnDeleteAlarm = new Button();
            cbAction = new ComboBox();
            cbActionDelay = new ComboBox();
            cbAction2 = new ComboBox();
            label7 = new Label();
            label8 = new Label();
            label9 = new Label();
            chkTrayMode = new CheckBox();
            lblTrayMode = new Label();
            SuspendLayout();
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(541, 156);
            label2.Name = "label2";
            label2.Size = new Size(113, 15);
            label2.TabIndex = 2;
            label2.Text = "Target Window Title";
            // 
            // btnStart
            // 
            btnStart.Location = new Point(374, 233);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(75, 23);
            btnStart.TabIndex = 6;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(695, 233);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(75, 23);
            btnStop.TabIndex = 7;
            btnStop.Text = "Stop";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // cmbWindowTitles
            // 
            cmbWindowTitles.FormattingEnabled = true;
            cmbWindowTitles.Location = new Point(504, 183);
            cmbWindowTitles.Name = "cmbWindowTitles";
            cmbWindowTitles.Size = new Size(181, 23);
            cmbWindowTitles.TabIndex = 8;
            // 
            // btnRefresh
            // 
            btnRefresh.Location = new Point(695, 183);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(75, 23);
            btnRefresh.TabIndex = 10;
            btnRefresh.Text = "Refresh List";
            btnRefresh.UseVisualStyleBackColor = true;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(12, 101);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(39, 15);
            lblStatus.TabIndex = 11;
            lblStatus.Text = "Status";
            // 
            // lvSchedule
            // 
            lvSchedule.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader2, columnHeader3, columnHeader6 });
            lvSchedule.Location = new Point(374, 302);
            lvSchedule.Name = "lvSchedule";
            lvSchedule.Size = new Size(396, 136);
            lvSchedule.TabIndex = 12;
            lvSchedule.UseCompatibleStateImageBehavior = false;
            lvSchedule.View = View.Details;
            // 
            // columnHeader1
            // 
            columnHeader1.Text = "Time";
            columnHeader1.Width = 50;
            // 
            // columnHeader2
            // 
            columnHeader2.Text = "Message";
            columnHeader2.Width = 110;
            // 
            // columnHeader3
            // 
            columnHeader3.Text = "Target Window";
            columnHeader3.Width = 100;
            // 
            // columnHeader6
            // 
            columnHeader6.Text = "Logged Commands";
            columnHeader6.Width = 132;
            // 
            // dtpScheduleTime
            // 
            dtpScheduleTime.CustomFormat = "dddd, MMMM d, yyyy h:mm:ss tt";
            dtpScheduleTime.Format = DateTimePickerFormat.Custom;
            dtpScheduleTime.Location = new Point(509, 72);
            dtpScheduleTime.Name = "dtpScheduleTime";
            dtpScheduleTime.Size = new Size(279, 23);
            dtpScheduleTime.TabIndex = 13;
            // 
            // txtScheduleMessage
            // 
            txtScheduleMessage.Location = new Point(12, 30);
            txtScheduleMessage.Name = "txtScheduleMessage";
            txtScheduleMessage.Size = new Size(267, 23);
            txtScheduleMessage.TabIndex = 15;
            // 
            // btnAddSchedule
            // 
            btnAddSchedule.Location = new Point(522, 233);
            btnAddSchedule.Name = "btnAddSchedule";
            btnAddSchedule.Size = new Size(132, 23);
            btnAddSchedule.TabIndex = 16;
            btnAddSchedule.Text = "Add to Schedule";
            btnAddSchedule.UseVisualStyleBackColor = true;
            // 
            // btnDeleteSchedule
            // 
            btnDeleteSchedule.Location = new Point(526, 272);
            btnDeleteSchedule.Name = "btnDeleteSchedule";
            btnDeleteSchedule.Size = new Size(128, 23);
            btnDeleteSchedule.TabIndex = 17;
            btnDeleteSchedule.Text = "Delete Selected";
            btnDeleteSchedule.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(13, 9);
            label1.Name = "label1";
            label1.Size = new Size(111, 15);
            label1.TabIndex = 18;
            label1.Text = "SET MESSAGE HERE";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(609, 50);
            label3.Name = "label3";
            label3.Size = new Size(93, 15);
            label3.TabIndex = 19;
            label3.Text = "DATE AND TIME";
            label3.Click += label3_Click;
            // 
            // lvAlarms
            // 
            lvAlarms.Columns.AddRange(new ColumnHeader[] { columnHeader4, columnHeader5 });
            lvAlarms.Location = new Point(12, 302);
            lvAlarms.Name = "lvAlarms";
            lvAlarms.Size = new Size(304, 136);
            lvAlarms.TabIndex = 20;
            lvAlarms.UseCompatibleStateImageBehavior = false;
            lvAlarms.View = View.Details;
            // 
            // columnHeader4
            // 
            columnHeader4.Text = "Reminder";
            columnHeader4.Width = 150;
            // 
            // columnHeader5
            // 
            columnHeader5.Text = "Countdown";
            columnHeader5.Width = 150;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(91, 280);
            label4.Name = "label4";
            label4.Size = new Size(103, 15);
            label4.TabIndex = 21;
            label4.Text = "Upcoming Alarms";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(13, 156);
            label5.Name = "label5";
            label5.Size = new Size(88, 15);
            label5.TabIndex = 22;
            label5.Text = "Alarm Message";
            // 
            // txtAlarmMessage
            // 
            txtAlarmMessage.Location = new Point(12, 174);
            txtAlarmMessage.Name = "txtAlarmMessage";
            txtAlarmMessage.Size = new Size(303, 23);
            txtAlarmMessage.TabIndex = 23;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(12, 200);
            label6.Name = "label6";
            label6.Size = new Size(69, 15);
            label6.TabIndex = 24;
            label6.Text = "Alarm Time";
            // 
            // dtpAlarmTime
            // 
            dtpAlarmTime.CustomFormat = "MM/dd/yyyy hh:mm tt";
            dtpAlarmTime.Format = DateTimePickerFormat.Custom;
            dtpAlarmTime.Location = new Point(12, 218);
            dtpAlarmTime.Name = "dtpAlarmTime";
            dtpAlarmTime.Size = new Size(200, 23);
            dtpAlarmTime.TabIndex = 25;
            // 
            // btnAddAlarm
            // 
            btnAddAlarm.Location = new Point(12, 247);
            btnAddAlarm.Name = "btnAddAlarm";
            btnAddAlarm.Size = new Size(75, 23);
            btnAddAlarm.TabIndex = 26;
            btnAddAlarm.Text = "Add Alarm";
            btnAddAlarm.UseVisualStyleBackColor = true;
            // 
            // btnDeleteAlarm
            // 
            btnDeleteAlarm.Location = new Point(102, 247);
            btnDeleteAlarm.Name = "btnDeleteAlarm";
            btnDeleteAlarm.Size = new Size(130, 23);
            btnDeleteAlarm.TabIndex = 27;
            btnDeleteAlarm.Text = "Delete Selected Alarm";
            btnDeleteAlarm.UseVisualStyleBackColor = true;
            // 
            // cbAction
            // 
            cbAction.DropDownStyle = ComboBoxStyle.DropDownList;
            cbAction.FormattingEnabled = true;
            cbAction.Location = new Point(297, 31);
            cbAction.Name = "cbAction";
            cbAction.Size = new Size(194, 23);
            cbAction.TabIndex = 28;
            cbAction.SelectedIndexChanged += cbAction_SelectedIndexChanged;
            // 
            // cbActionDelay
            // 
            cbActionDelay.DropDownStyle = ComboBoxStyle.DropDownList;
            cbActionDelay.FormattingEnabled = true;
            cbActionDelay.Location = new Point(328, 119);
            cbActionDelay.Name = "cbActionDelay";
            cbActionDelay.Size = new Size(121, 23);
            cbActionDelay.TabIndex = 29;
            // 
            // cbAction2
            // 
            cbAction2.DropDownStyle = ComboBoxStyle.DropDownList;
            cbAction2.FormattingEnabled = true;
            cbAction2.Location = new Point(297, 75);
            cbAction2.Name = "cbAction2";
            cbAction2.Size = new Size(194, 23);
            cbAction2.TabIndex = 30;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(336, 9);
            label7.Name = "label7";
            label7.Size = new Size(113, 15);
            label7.TabIndex = 31;
            label7.Text = "KEYBOARD ACTION";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(336, 57);
            label8.Name = "label8";
            label8.Size = new Size(113, 15);
            label8.TabIndex = 32;
            label8.Text = "KEYBOARD ACTION";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(350, 101);
            label9.Name = "label9";
            label9.Size = new Size(78, 15);
            label9.TabIndex = 33;
            label9.Text = "DELAY TIMER";
            // 
            // chkTrayMode
            // 
            chkTrayMode.Appearance = Appearance.Button;
            chkTrayMode.BackColor = Color.Firebrick;
            chkTrayMode.ForeColor = Color.White;
            chkTrayMode.Location = new Point(738, 12);
            chkTrayMode.Name = "chkTrayMode";
            chkTrayMode.Size = new Size(50, 23);
            chkTrayMode.TabIndex = 34;
            chkTrayMode.Text = "OFF";
            chkTrayMode.TextAlign = ContentAlignment.MiddleCenter;
            chkTrayMode.UseVisualStyleBackColor = false;
            chkTrayMode.CheckedChanged += chkTrayMode_CheckedChanged;
            // 
            // lblTrayMode
            // 
            lblTrayMode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblTrayMode.AutoSize = true;
            lblTrayMode.Location = new Point(669, 16);
            lblTrayMode.Name = "lblTrayMode";
            lblTrayMode.Size = new Size(63, 15);
            lblTrayMode.TabIndex = 35;
            lblTrayMode.Text = "Tray Mode";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(lblTrayMode);
            Controls.Add(chkTrayMode);
            Controls.Add(label9);
            Controls.Add(label8);
            Controls.Add(label7);
            Controls.Add(cbAction2);
            Controls.Add(cbActionDelay);
            Controls.Add(cbAction);
            Controls.Add(btnDeleteAlarm);
            Controls.Add(btnAddAlarm);
            Controls.Add(dtpAlarmTime);
            Controls.Add(label6);
            Controls.Add(txtAlarmMessage);
            Controls.Add(label5);
            Controls.Add(label4);
            Controls.Add(lvAlarms);
            Controls.Add(label3);
            Controls.Add(label1);
            Controls.Add(btnDeleteSchedule);
            Controls.Add(btnAddSchedule);
            Controls.Add(txtScheduleMessage);
            Controls.Add(dtpScheduleTime);
            Controls.Add(lvSchedule);
            Controls.Add(lblStatus);
            Controls.Add(btnRefresh);
            Controls.Add(cmbWindowTitles);
            Controls.Add(btnStop);
            Controls.Add(btnStart);
            Controls.Add(label2);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        private void label3_Click(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        #endregion
        private Label label2;
        private Button btnStart;
        private Button btnStop;
        private ComboBox cmbWindowTitles;
        private Button btnRefresh;
        private Label lblStatus;
        private ListView lvSchedule;
        private ColumnHeader columnHeader1;
        private ColumnHeader columnHeader2;
        private ColumnHeader columnHeader3;
        private DateTimePicker dtpScheduleTime;
        private TextBox txtScheduleMessage;
        private Button btnAddSchedule;
        private Button btnDeleteSchedule;
        private Label label1;
        private Label label3;
        private ListView lvAlarms;
        private ColumnHeader columnHeader4;
        private ColumnHeader columnHeader5;
        private Label label4;
        private Label label5;
        private TextBox txtAlarmMessage;
        private Label label6;
        private DateTimePicker dtpAlarmTime;
        private Button btnAddAlarm;
        private Button btnDeleteAlarm;
        private ComboBox cbAction;
        private ColumnHeader columnHeader6;
        private ComboBox cbActionDelay;
        private ComboBox cbAction2;
        private Label label7;
        private Label label8;
        private Label label9;
        private CheckBox chkTrayMode;
        private Label lblTrayMode;
    }
}
