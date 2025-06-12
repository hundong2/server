using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks; // Added for Task.Delay
using Microsoft.UI.Dispatching; // Correct DispatcherTimer for MAUI

namespace MouseMacro;

/// <summary>
/// MainPage for the MouseMacro application.
/// This page contains controls to start and stop a macro that
/// periodically moves the mouse cursor to random screen locations and simulates a click.
/// It utilizes P/Invoke calls to interact with the Windows API for mouse control and system metrics.
/// </summary>
public partial class MainPage : ContentPage
{
    // P/Invoke declarations for Windows API functions

    /// <summary>
    /// Sets the cursor's position.
    /// </summary>
    /// <param name="X">The new X-coordinate of the cursor, in screen coordinates.</param>
    /// <param name="Y">The new Y-coordinate of the cursor, in screen coordinates.</param>
    /// <returns>True if successful, false otherwise.</returns>
    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int X, int Y);

    /// <summary>
    /// Retrieves the cursor's position, in screen coordinates.
    /// </summary>
    /// <param name="lpPoint">A POINT struct that receives the screen coordinates of the cursor.</param>
    /// <returns>True if successful, false otherwise.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetCursorPos(out POINT lpPoint);

    /// <summary>
    /// Synthesizes mouse motion and button clicks.
    /// </summary>
    /// <param name="dwFlags">Flags that specify various aspects of mouse motion and button clicking.
    /// Common flags include MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP.</param>
    /// <param name="dx">The mouse's absolute position along the x-axis or its amount of motion since the last mouse event,
    /// depending on the setting of MOUSEEVENTF_ABSOLUTE.</param>
    /// <param name="dy">The mouse's absolute position along the y-axis or its amount of motion since the last mouse event,
    /// depending on the setting of MOUSEEVENTF_ABSOLUTE.</param>
    /// <param name="cButtons">If dwFlags includes MOUSEEVENTF_WHEEL, this parameter specifies the amount of wheel movement.
    /// Otherwise, it is not used.</param>
    /// <param name="dwExtraInfo">An additional value associated with the mouse event. An application calls GetMessageExtraInfo to obtain this additional information.</param>
    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

    // Mouse event flags used by mouse_event
    private const uint MOUSEEVENTF_LEFTDOWN = 0x02; // Specifies that the left button is down.
    private const uint MOUSEEVENTF_LEFTUP = 0x04;   // Specifies that the left button is up.
    // private const uint MOUSEEVENTF_RIGHTDOWN = 0x08; // Not used for now
    // private const uint MOUSEEVENTF_RIGHTUP = 0x10;  // Not used for now

    /// <summary>
    /// Retrieves the specified system metric or system configuration setting.
    /// </summary>
    /// <param name="nIndex">The system metric or configuration setting to be retrieved.
    /// SM_CXSCREEN and SM_CYSCREEN are used to get screen width and height.</param>
    /// <returns>The requested system metric or configuration setting.</returns>
    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);

    // System Metrics constants used with GetSystemMetrics
    public const int SM_CXSCREEN = 0; // Index for screen width in pixels.
    public const int SM_CYSCREEN = 1; // Index for screen height in pixels.

    /// <summary>
    /// Represents a point in 2D space (X and Y coordinates).
    /// Used by GetCursorPos.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    private DispatcherTimer mouseMoveTimer; // Timer to trigger mouse movements periodically.
    private Random random = new Random();   // Random number generator for positions and delays.
    private bool isMacroRunning = false;    // Flag to track if the macro is currently active.

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class.
    /// Sets up UI components and the timer for the macro.
    /// </summary>
    public MainPage()
    {
        InitializeComponent(); // Standard MAUI initialization.
        InitializeTimer();     // Sets up the DispatcherTimer.
        StatusLabel.Text = "Macro is stopped"; // Initial status display.
    }

    /// <summary>
    /// Initializes the DispatcherTimer for periodic mouse actions.
    /// The timer is configured but not started here; it's controlled by the user via a button.
    /// Ensures timer creation on the UI thread.
    /// </summary>
    private void InitializeTimer()
    {
        // Ensure timer is created on the UI thread for MAUI applications.
        if (Dispatcher.HasThreadAccess)
        {
            mouseMoveTimer = new DispatcherTimer();
            mouseMoveTimer.Interval = TimeSpan.FromSeconds(5); // Set interval for macro actions.
            mouseMoveTimer.Tick += Timer_Tick;                 // Assign event handler for timer ticks.
        }
        else
        {
            // If not on UI thread, enqueue the initialization.
            Dispatcher.TryEnqueue(() => {
                mouseMoveTimer = new DispatcherTimer();
                mouseMoveTimer.Interval = TimeSpan.FromSeconds(5);
                mouseMoveTimer.Tick += Timer_Tick;
            });
        }
    }

    /// <summary>
    /// Handles the click event of the MacroButton.
    /// Toggles the macro's running state (start/stop) and updates the UI accordingly.
    /// </summary>
    /// <param name="sender">The source of the event (MacroButton).</param>
    /// <param name="e">Event arguments.</param>
    private void OnMacroButtonClicked(object sender, EventArgs e)
    {
        isMacroRunning = !isMacroRunning; // Toggle the macro state.

        if (isMacroRunning)
        {
            mouseMoveTimer?.Start(); // Start the timer if not null.
            MacroButton.Text = "Stop Macro"; // Update button text.
            StatusLabel.Text = "Macro is running"; // Update status label.
        }
        else
        {
            mouseMoveTimer?.Stop(); // Stop the timer.
            MacroButton.Text = "Start Macro"; // Update button text.
            StatusLabel.Text = "Macro is stopped"; // Update status label.
        }
    }

    /// <summary>
    /// Event handler for the mouseMoveTimer's Tick event.
    /// This method is called periodically when the timer is running.
    /// It triggers the random mouse movement and click simulation.
    /// </summary>
    /// <param name="sender">The source of the event (mouseMoveTimer).</param>
    /// <param name="e">Event arguments.</param>
    private async void Timer_Tick(object sender, object e)
    {
        await MoveMouseRandomly(); // Perform the mouse movement.
        SimulateMouseClick();      // Simulate a mouse click.
    }

    /// <summary>
    /// Moves the mouse cursor to a random position on the screen in a human-like manner.
    /// This involves:
    /// 1. Getting current cursor position.
    /// 2. Determining screen dimensions dynamically using GetSystemMetrics.
    /// 3. Generating random target X and Y coordinates within screen bounds.
    /// 4. Simulating a path by moving the cursor in small, incremental steps.
    /// 5. Adding slight random deviations to each step for a non-linear path.
    /// 6. Introducing small, variable delays between steps to simulate varied movement speed.
    /// </summary>
    private async Task MoveMouseRandomly()
    {
        GetCursorPos(out POINT currentPosition); // Get current mouse position.

        // Get actual screen dimensions.
        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);

        // Generate random target coordinates.
        int targetX = random.Next(0, screenWidth);
        int targetY = random.Next(0, screenHeight);

        // Determine the number of steps for the movement to make it appear smoother.
        int steps = random.Next(50, 150);

        for (int i = 0; i <= steps; i++)
        {
            float progress = (float)i / steps; // Calculate current progress towards the target.
            // Interpolate position linearly towards the target.
            int newX = (int)(currentPosition.X + (targetX - currentPosition.X) * progress);
            int newY = (int)(currentPosition.Y + (targetY - currentPosition.Y) * progress);

            // Add a small random deviation to make the path less predictable and more human-like.
            newX += random.Next(-10, 11);
            newY += random.Next(-10, 11);

            // Ensure the new position stays within the screen boundaries.
            newX = Math.Max(0, Math.Min(screenWidth - 1, newX));
            newY = Math.Max(0, Math.Min(screenHeight - 1, newY));

            SetCursorPos(newX, newY); // Move the cursor to the new intermediate position.
            // Wait for a short, random duration to simulate variable movement speed.
            await Task.Delay(random.Next(10, 30));
        }
        SetCursorPos(targetX, targetY); // Ensure the cursor reaches the exact target position.
    }

    /// <summary>
    /// Simulates a left mouse button click at the current cursor position.
    /// It uses the mouse_event function to send left button down and up events.
    /// </summary>
    private void SimulateMouseClick()
    {
        // Get current cursor position to click at the right place.
        GetCursorPos(out POINT currentPosition);
        // Simulate left mouse button down followed by left mouse button up.
        mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)currentPosition.X, (uint)currentPosition.Y, 0, 0);
    }

    /// <summary>
    /// Called when the page is disappearing.
    /// Ensures the macro timer is stopped and the UI is updated to reflect the stopped state.
    /// This prevents the macro from running in the background if the page is navigated away from or closed.
    /// </summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        mouseMoveTimer?.Stop(); // Stop the timer.
        isMacroRunning = false; // Update the macro state.

        // Ensure UI reflects the stopped state if the page disappears.
        // Check for null in case elements are already disposed or not fully initialized during teardown.
        if (MacroButton != null)
        {
            MacroButton.Text = "Start Macro";
        }
        if (StatusLabel != null)
        {
            StatusLabel.Text = "Macro is stopped";
        }
    }
}
