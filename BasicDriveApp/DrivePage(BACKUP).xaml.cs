using System;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

using RobotKit;
using System.Threading.Tasks;

namespace BasicDriveApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 

    public sealed partial class MainPage : Page
    {

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(UInt16 virtualKeyCode);
        private const UInt16 VK_MBUTTON = 0x04;//middle mouse button
        private const UInt16 VK_LBUTTON = 0x01;//left mouse button
        private const UInt16 VK_RBUTTON = 0x02;//right mouse button

        //! @brief  the default string to show no sphero connected
        private const string kNoSpheroConnected = "No Sphero Connected";

        //! @brief  the default string to show when connecting to a sphero ({0})
        private const string kConnectingToSphero = "Connecting to {0}";

        //! @brief  the default string to show when connected to a sphero ({0})
        private const string kSpheroConnected = "Connected to {0}";


        //! @brief  the robot we're connecting to
        Sphero m_robot = null;

        //! @brief  the joystick to control m_robot
        private Joystick m_joystick;

        //! @brief  the color wheel to control m_robot color
        private ColorWheel m_colorwheel;

        //! @brief  the calibration wheel to calibrate m_robot
        private CalibrateElement m_calibrateElement;

        public MainPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            SetupRobotConnection();
            Application app = Application.Current;
            app.Suspending += OnSuspending;
        }

        /*!
         * @brief   handle the user launching this page in the application
         * 
         *  connects to sphero and sets up the ui
         */
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            ShutdownRobotConnection();
            ShutdownControls();

            Application app = Application.Current;
            app.Suspending -= OnSuspending;
        }

        //! @brief  handle the application entering the background
        private void OnSuspending(object sender, SuspendingEventArgs args)
        {
            ShutdownRobotConnection();
        }

        //! @brief  search for a robot to connect to
        private void SetupRobotConnection()
        {
            SpheroName.Text = kNoSpheroConnected;

            RobotProvider provider = RobotProvider.GetSharedProvider();
            provider.DiscoveredRobotEvent += OnRobotDiscovered;
            provider.NoRobotsEvent += OnNoRobotsEvent;
            provider.ConnectedRobotEvent += OnRobotConnected;
            provider.FindRobots();
        }

        //! @brief  disconnect from the robot and stop listening
        private void ShutdownRobotConnection()
        {
            if (m_robot != null)
            {
                m_robot.SensorControl.StopAll();
                m_robot.Sleep();
                // temporary while I work on Disconnect.
                //m_robot.Disconnect();
                ConnectionToggle.OffContent = "Disconnected";
                SpheroName.Text = kNoSpheroConnected;

                m_robot.SensorControl.AccelerometerUpdatedEvent -= OnAccelerometerUpdated;
                m_robot.SensorControl.GyrometerUpdatedEvent -= OnGyrometerUpdated;

                m_robot.CollisionControl.StopDetection();
                m_robot.CollisionControl.CollisionDetectedEvent -= OnCollisionDetected;

                RobotProvider provider = RobotProvider.GetSharedProvider();
                provider.DiscoveredRobotEvent -= OnRobotDiscovered;
                provider.NoRobotsEvent -= OnNoRobotsEvent;
                provider.ConnectedRobotEvent -= OnRobotConnected;
            }
        }

        //! @brief  configures the various sphero controls
        private void SetupControls()
        {
            m_colorwheel = new ColorWheel(ColorPuck, m_robot);
            m_joystick = new Joystick(Puck, m_robot);

            m_calibrateElement = new CalibrateElement(
                CalibrateRotationRoot,
                CalibrateTarget,
                CalibrateRingOuter,
                CalibrateRingMiddle,
                CalibrateRingInner,
                CalibrationFingerPoint,
                m_robot);
        }

        //! @brief  shuts down the various sphero controls
        private void ShutdownControls()
        {
            // I'm pretty sure this does nothing, we should just write modifiers - PJM
            m_joystick = null;
            m_colorwheel = null;
            m_calibrateElement = null;
        }

        //! @brief  when a robot is discovered, connect!
        private void OnRobotDiscovered(object sender, Robot robot)
        {
            Debug.WriteLine(string.Format("Discovered \"{0}\"", robot.BluetoothName));

            if (m_robot == null)
            {
                RobotProvider provider = RobotProvider.GetSharedProvider();
                provider.ConnectRobot(robot);
                ConnectionToggle.OnContent = "Connecting...";
                m_robot = (Sphero)robot;
                SpheroName.Text = string.Format(kConnectingToSphero, robot.BluetoothName);
            }
        }


        private void OnNoRobotsEvent(object sender, EventArgs e)
        {
            MessageDialog dialog = new MessageDialog("No Sphero Paired");
            dialog.DefaultCommandIndex = 0;
            dialog.CancelCommandIndex = 1;
            dialog.ShowAsync();
        }


        //! @brief  when a robot is connected, get ready to drive!
        private void OnRobotConnected(object sender, Robot robot)
        {
            Debug.WriteLine(string.Format("Connected to {0}", robot));
            ConnectionToggle.IsOn = true;
            ConnectionToggle.OnContent = "Connected";

            m_robot.SetRGBLED(255, 255, 255);
            SpheroName.Text = string.Format(kSpheroConnected, robot.BluetoothName);
            SetupControls();

            m_robot.SensorControl.Hz = 10;
            m_robot.SensorControl.AccelerometerUpdatedEvent += OnAccelerometerUpdated;
            m_robot.SensorControl.GyrometerUpdatedEvent += OnGyrometerUpdated;

            m_robot.CollisionControl.StartDetectionForWallCollisions();
            m_robot.CollisionControl.CollisionDetectedEvent += OnCollisionDetected;
        }


        private void ConnectionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Connection Toggled : " + ConnectionToggle.IsOn);
            ConnectionToggle.OnContent = "Connecting...";
            if (ConnectionToggle.IsOn)
            {
                if (m_robot == null)
                {
                    SetupRobotConnection();
                }
            }
            else
            {
                ShutdownRobotConnection();
            }
        }

        private void OnAccelerometerUpdated(object sender, AccelerometerReading reading)
        {
            AccelerometerX.Text = "" + reading.X;
            AccelerometerY.Text = "" + reading.Y;
            AccelerometerZ.Text = "" + reading.Z;
            Globals.accel_x = (int)reading.X;
            Globals.accel_y = (int)reading.Y;
            Globals.accel_z = (int)reading.Z;

        }

        private void OnGyrometerUpdated(object sender, GyrometerReading reading)
        {
            GyroscopeX.Text = "" + reading.X;
            GyroscopeY.Text = "" + reading.Y;
            GyroscopeZ.Text = "" + reading.Z;
            Globals.gyro_x = (int)reading.X;
            Globals.gyro_y = (int)reading.Y;
            Globals.gyro_z = (int)reading.Z;
        }

        private void OnCollisionDetected(object sender, CollisionData data)
        {
            Debug.WriteLine("Wall collision was detected");
            smash.Play();
        }

        private void act1_button_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.act1on == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.angryon == false && Globals.happyon == false && Globals.scaredon == false)
            {
                act1_button.Background = new SolidColorBrush(Windows.UI.Colors.White);
                act1_button.Content = "Activity 1 Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
                Globals.act1on = true;
            }

            if (!Happy())
            {
                act1_button.Content = "Activity 1";
                Globals.act1on = false;
                happy_music.Stop();
                m_robot.Roll(0, 0);
                return;
            }
            if (!Angry())
            {
                act1_button.Content = "Activity 1";
                Globals.act1on = false;
                angry_music.Stop();
                m_robot.Roll(0, 0);
                return;
            }

            Scared();
            scared_music.Stop();
            m_robot.Roll(0, 0);
            Globals.act1on = false;
            act1_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
            act1_button.Content = "Activity 1";
            return;
        }

        private void act2_button_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.act1on == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.angryon == false && Globals.happyon == false && Globals.scaredon == false)
            {
                act2_button.Background = new SolidColorBrush(Windows.UI.Colors.White);
                act2_button.Content = "Activity 2 Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
                Globals.act2on = true;
            }

                //stop_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
                Globals.act2on = true;
                Stopwatch stopWatch1 = new Stopwatch();
                Stopwatch stopWatch2 = new Stopwatch();

                int result = 0;
                TimeSpan s_time1, s_time2;

                // Timers for time movement and lights
                stopWatch1.Start();
                s_time1 = stopWatch1.Elapsed;

                while (s_time1.Milliseconds < 300000)
                {
                    m_robot.SensorControl.GyrometerUpdatedEvent += OnGyrometerUpdated;
                    if ((result != -32767 && result != -32768) || s_time1.Milliseconds < 300) // No mouse click
                    {
                        //////   ANGRY PHASE when touched   ///////
                        Debug.WriteLine("Gyro reading: {0},{1}", Globals.gyro_x, Globals.gyro_y);
                        if (Globals.gyro_x > Math.Abs(50) || Globals.gyro_y > Math.Abs(50) || Globals.gyro_z > Math.Abs(50)) // motion detected
                        {
                            Debug.WriteLine("Now I'm angry!!");
                            stopWatch2.Start();
                            s_time2 = stopWatch2.Elapsed;
                            while (s_time2.Milliseconds < 1000)
                            {
                                m_robot.SetRGBLED(30, 30, 30);
                                angry_music.Play();
                                s_time2 = stopWatch2.Elapsed;
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Returning to still state");
                            m_robot.Roll(0, 0);
                            m_robot.SetRGBLED(0, 0, 0);
                            stopWatch1.Stop();
                            stopWatch2.Stop();
                        }

                        result = (int)GetAsyncKeyState(VK_LBUTTON);
                    }
                    else
                    {
                        Globals.act2on = false;
                        Debug.WriteLine("Exiting activity 2");
                        m_robot.Roll(0, 0);
                        m_robot.SetRGBLED(0, 0, 0);
                        stopWatch1.Stop();
                        stopWatch2.Stop();
                        return;
                    }
                }
                Debug.WriteLine("Exiting activity 2");
                Globals.act2on = false;
                m_robot.Roll(0, 0);
                m_robot.SetRGBLED(0, 0, 0);
                stopWatch1.Stop();
                stopWatch2.Stop();
                return;
        }
        private void act3_button_Click(object sender, RoutedEventArgs e)
        {
            Globals.stopon = false;
            if (Globals.act3on == false && Globals.act2on == false && Globals.act1on == false && Globals.act4on == false && Globals.angryon == false && Globals.happyon == false && Globals.scaredon == false)
            {
                act3_button.Background = new SolidColorBrush(Windows.UI.Colors.White);
                act3_button.Content = "Activity 3 Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
                Globals.act3on = true;
            }
            else
            {
                act3_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
                act3_button.Content = "Activity 3";
                Globals.act3on = false;
                // Stop activity 3
            }
        }
        private void act4_button_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.act4on == false && Globals.act2on == false && Globals.act3on == false && Globals.act1on == false && Globals.angryon == false && Globals.happyon == false && Globals.scaredon == false)
            {
                act4_button.Background = new SolidColorBrush(Windows.UI.Colors.White);
                act4_button.Content = "Activity 4 Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
                Globals.act4on = true;
            }
            else
            {
                act4_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
                act4_button.Content = "Activity 4";
                Globals.act4on = false;
                // Stop activity 4
            }
        }
        private void angry_button_Click(object sender, RoutedEventArgs e)
        {
            Globals.stopon = false;
            if (Globals.angryon == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.act1on == false && Globals.happyon == false && Globals.scaredon == false)
            {
                angry_button.Background = new SolidColorBrush(Windows.UI.Colors.White);
                angry_button.Content = "Angry Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
                Globals.angryon = true;

                Stopwatch stopWatch = new Stopwatch();
                TimeSpan s_time;

                if (Globals.firsttime == true)
                {
                    stopWatch.Start();
                    s_time = stopWatch.Elapsed;
                    Debug.WriteLine("Playing angry music and moving erratically");
                    Globals.firsttime = false;
                }

                else
                {
                    if (s_time.Seconds % 2 == 0)
                        Globals.heading *= -1;
                }

                m_robot.Roll(Globals.heading, (float)0.2);
                m_robot.SetRGBLED(30, 30, 30);
                angry_music.Play();
                return;
            }
        }

        private void happy_button_Click(object sender, RoutedEventArgs e)
        {
            Globals.stopon = false;
            if (Globals.happyon == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.angryon == false && Globals.act1on == false && Globals.scaredon == false)
            {
                happy_button.Background = new SolidColorBrush(Windows.UI.Colors.White);
                happy_button.Content = "Happy Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
                Globals.happyon = true;

                // Play this for 10 seconds
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                TimeSpan s_time = stopWatch.Elapsed;
                int startingSecs = s_time.Seconds;
                while (s_time.Seconds - startingSecs < 10)
                {
                    Debug.WriteLine("Playing happy music and circling...");
                    s_time = stopWatch.Elapsed;
                    m_robot.Roll(120, (float)0.3);
                }
                stopWatch.Stop();
                Debug.WriteLine("Exited timed loop\n");

                // Format and display the TimeSpan value. 
                /*string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    time.Hours, time.Minutes, time.Seconds,
                    time.Milliseconds / 10);
                */

            }
            else
            {
                happy_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
                happy_button.Content = "Happy";
                Globals.happyon = false;
                // Stop happy
            }
        }
        private void scared_button_Click(object sender, RoutedEventArgs e)
        {
            Globals.stopon = false;
            if (Globals.scaredon == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.angryon == false && Globals.happyon == false && Globals.act1on == false)
            {
                scared_button.Background = new SolidColorBrush(Windows.UI.Colors.White);
                scared_button.Content = "Scared Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
                Globals.scaredon = true;
            }
            else
            {
                scared_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
                scared_button.Content = "Scared";
                Globals.scaredon = false;
                // Stop scared
            }
        }
        private void stop_button_Click(object sender, RoutedEventArgs e)
        {
            stop_button.Background = new SolidColorBrush(Windows.UI.Colors.White);
            act1_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
            act1_button.Content = "Activity 1";
            act2_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
            act2_button.Content = "Activity 2";
            act3_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
            act3_button.Content = "Activity 3";
            act4_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
            act4_button.Content = "Activity 4";
            angry_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
            angry_button.Content = "Angry";
            happy_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
            happy_button.Content = "Happy";
            scared_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);
            scared_button.Content = "Scared";
            Globals.stopon = true;
            Globals.act1on = false;
            Globals.act2on = false;
            Globals.act3on = false;
            Globals.act4on = false;
            Globals.angryon = false;
            Globals.happyon = false;
            Globals.scaredon = false;
            Globals.act1on = false;
            happy_music.Stop();
            angry_music.Stop();
            scared_music.Stop();
            happy_vocal.Stop();
            angry_vocal.Stop();
            scared_vocal.Stop();
            m_robot.Roll(0, 0);
            m_robot.SetRGBLED(0, 0, 0);
        }

        public bool Happy()
        {
            Debug.WriteLine("Playing happy music and circling...");
            TimeSpan s_time;
            Stopwatch stopWatch = new Stopwatch();
            int result = 0;

            act1_button.Background = new SolidColorBrush(Windows.UI.Colors.White);
            act1_button.Content = "Activity 1 Playing";
            stop_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSkyBlue);

            // Timers for time movement and lights
            stopWatch.Start();
            s_time = stopWatch.Elapsed;

            while (s_time.Milliseconds < 3000)
            {
                if((result!=-32767 && result!= -32768) || s_time.Milliseconds < 300)
                {
                    m_robot.Roll(Globals.heading, (float)0.2);
                    m_robot.SetRGBLED(255, 0, 0);
                    happy_music.Play();
                    s_time = stopWatch.Elapsed;
                }
                else
                {
                    Debug.WriteLine("Exited happy loop\n");
                    stopWatch.Stop();
                    happy_music.Stop();
                    m_robot.Roll(0, 0);
                    return false;
                }
                result = (int)GetAsyncKeyState(VK_LBUTTON);

            }

            return true;
        }

        public bool Angry()
        {
            Debug.WriteLine("Playing angry music and moving erratically...");
            TimeSpan s_time;
            //int headingchange = -10;
            Stopwatch stopWatch = new Stopwatch();
            int result = 0;

            //if(Globals.heading == 250)  headingchange *= -1;
            //Globals.heading += headingchange;

            // Timers for time movement and lights
            stopWatch.Start();
            s_time = stopWatch.Elapsed;

            while (s_time.Milliseconds < 3000)
            {
                if ((result != -32767 && result != -32768) || s_time.Milliseconds < 300)
                {
                    m_robot.Roll(Globals.heading, (float)0.2);
                    m_robot.SetRGBLED(255, 0, 0);
                    angry_music.Play();
                    s_time = stopWatch.Elapsed;
                }
                else
                {
                    Debug.WriteLine("Exited angry loop\n");
                    stopWatch.Stop();
                    angry_music.Stop();
                    m_robot.Roll(0, 0);
                    return false;
                }
                result = (int)GetAsyncKeyState(VK_LBUTTON);

            }

            return true;
        }

        public void Scared()
        {

            /////   SCARED PHASE for 30 seconds   ///////
            Debug.WriteLine("Playing scary music and circling...");
            TimeSpan s_time;
            //int headingchange = -10;
            Stopwatch stopWatch = new Stopwatch();
            int result = 0;

            //if(Globals.heading == 250)  headingchange *= -1;
            //Globals.heading += headingchange;

            // Timers for time movement and lights
            stopWatch.Start();
            s_time = stopWatch.Elapsed;

            while (s_time.Milliseconds < 3000)
            {
                if ((result != -32767 && result != -32768) || s_time.Milliseconds < 300)
                {
                    m_robot.Roll(Globals.heading, (float)0.2);
                    m_robot.SetRGBLED(255, 0, 0);
                    scared_music.Play();
                    s_time = stopWatch.Elapsed;
                }
                else
                {
                    Debug.WriteLine("Exited scared loop\n");
                    stopWatch.Stop();
                    scared_music.Stop();
                    m_robot.Roll(0, 0);
                    return;
                }
                result = (int)GetAsyncKeyState(VK_LBUTTON);

            }

            return;
        }
    }
}