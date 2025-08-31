Alarm and Auto Tool
A Windows desktop utility designed to automate keyboard inputs at scheduled times and provide customizable visual countdown alarms.
Features
This application combines two primary functions into a single, easy-to-use interface:
* Scheduler: Automate the task of typing a specific message into any open application window at a precise time.
   * Select any running application from a dynamic list.
   * Set a specific time (HH:MM:SS) for the action.
   * Customize the message to be typed.
   * Add multiple entries to a schedule queue.
   * Start and stop the automation engine at any time.
* Alarms: A separate, non-intrusive visual reminder system.
   * Set alarms with custom messages for any future date and time.
   * View all upcoming alarms in a dedicated panel.
   * See a live countdown for each alarm (days, hours, minutes, seconds).
   * Alarms visually change color when the countdown is low, providing an urgent visual cue.
How to Use
Using the Scheduler
1. Select Target: Choose the application you want to type in from the "Target Window Title" dropdown. Click "Refresh List" if you've opened a new program.
2. Set Message: In the "INPUT MESSAGE HERE" box, type the text you want the tool to automatically write.
3. Set Time: Use the time picker to set the exact time for the action to occur.
4. Add to Schedule: Click the "Add to Schedule" button. Your task will appear in the main schedule list.
5. Start: Once you have one or more items in the schedule, click the "Start" button to begin monitoring. The scheduler controls will lock, and the tool will execute each task at its scheduled time.
6. Stop: Click "Stop" at any time to halt the scheduler.
Using the Alarms
1. Set Alarm Message: In the dedicated alarm section, enter the text for your reminder (e.g., "Team meeting starts").
2. Set Alarm Time: Use the full date and time picker to set when the alarm should finish. You can set this for days, weeks, or months in the future.
3. Add Alarm: Click the "Add Alarm" button. Your reminder will appear in the "Upcoming Alarms" list with a live countdown.
4. Monitor: The countdown will update every second. The alarm will turn red when less than 10 seconds remain and will show "Finished" after the time has passed.
Building from Source
To build this application yourself, you will need:
* Visual Studio 2022 (or later)
* .NET Desktop Development workload installed
* .NET 8.0 SDK (or as specified in the project file)
Simply open the .sln file in Visual Studio and build the solution in either Debug or Release mode.