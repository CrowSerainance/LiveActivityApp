Release notes (v0.1.2)
• New: Enabled additional commands while program is running.

• New: Rebuffed switch tile window, commits fully to changing window as long as program is running as "Admin".


Release notes (v0.1.1)

Tray Mode & Inline Editor

• New: Tray Mode with system tray icon. Close/minimize sends the app to tray; tray menu exposes Show, Start/Stop, Exit. The menu text updates automatically when you start/stop.

• New: Inline schedule edit — double-click a row to change time, message, target window, two action tokens, and per-row delay.

• Improved: Rich action tokens (ENTER, CTRL+ENTER, TAB, ALT+TAB, CTRL/ALT/SHIFT + A–Z) and optional 0.5–5.0 s delay between actions.

• Improved: More robust key sending using SendInput with a safe SendKeys fallback.

• UI: Single combined Date & Time picker for scheduling; cleaner run/stop state.


# LiveActivityApp

Alarm and Auto Tool
A Windows desktop utility designed to automate keyboard inputs at scheduled times and provide customizable visual countdown alarms.

Features

This application combines two primary functions into a single, easy-to-use interface:

Scheduler: Automate the task of typing a specific message into any open application window at a precise time.

Select any running application from a dynamic list.

Set a specific time (HH:MM:SS) for the action.

Customize the message to be typed.

Add multiple entries to a schedule queue.

Start and stop the automation engine at any time.



Alarms: A separate, non-intrusive visual reminder system.

Set alarms with custom messages for any future date and time.

View all upcoming alarms in a dedicated panel.

See a live countdown for each alarm (days, hours, minutes, seconds).

Alarms visually change color when the countdown is low, providing an urgent visual cue.



How to Use

Using the Scheduler

Select Target: Choose the application you want to type in from the "Target Window Title" dropdown. 

Click "Refresh List" if you've opened a new program.

Set Message: In the "INPUT MESSAGE HERE" box, type the text you want the tool to automatically write.

Set Time: Use the time picker to set the exact time for the action to occur.

Add to Schedule: Click the "Add to Schedule" button. 

Your task will appear in the main schedule list.

Start: Once you have one or more items in the schedule, click the "Start" button to begin monitoring.

The scheduler controls will lock, and the tool will execute each task at its scheduled time.

Stop: Click "Stop" at any time to halt the scheduler.


Using the Alarms

Set Alarm Message: In the dedicated alarm section, enter the text for your reminder (e.g., "Team meeting starts").

Set Alarm Time: Use the full date and time picker to set when the alarm should finish. 

You can set this for days, weeks, or months in the future.

Add Alarm: Click the "Add Alarm" button. 

Your reminder will appear in the "Upcoming Alarms" list with a live countdown.

Monitor: The countdown will update every second. The alarm will turn red when less than 10 seconds remain and will show "Finished" after the time has passed.



Building from Source

To build this application yourself, you will need:

Visual Studio 2022 (or later).

NET Desktop Development workload installed.

NET 8.0 SDK (or as specified in the project file)

Simply open the .sln file in Visual Studio and build the solution in either Debug or Release mode.
