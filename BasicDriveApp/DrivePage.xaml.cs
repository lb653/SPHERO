using System;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Windows.Storage;
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
         * @brief handle the user launching this page in the application
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
                //SpheroName.Text = string.Format(kConnectingToSphero, "Sphero-BPR");
                //SpheroName.Text = string.Format(kConnectingToSphero, "Sphero-OWR"); // The nice one :)
                //SpheroName.Text = string.Format(kConnectingToSphero, "Sphero-YRG");
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
            int canfollow = 0;
            Debug.WriteLine(string.Format("Connected to {0}", robot));
            ConnectionToggle.IsOn = true;
            ConnectionToggle.OnContent = "Connected";

            m_robot.SetRGBLED(255, 0, 0);
            Globals.color_state = "red";

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

            Globals.accel_x = (float)Convert.ToDouble(AccelerometerX.Text);
            Globals.accel_y = (float)Convert.ToDouble(AccelerometerY.Text);
            Globals.accel_z = (float)Convert.ToDouble(AccelerometerZ.Text);

            try
            {
                WriteOutput();
            }
            catch
            {
                // do nothing
            }
        }

        private void OnGyrometerUpdated(object sender, GyrometerReading reading)
        {
            GyroscopeX.Text = "" + reading.X;
            GyroscopeY.Text = "" + reading.Y;
            GyroscopeZ.Text = "" + reading.Z;

            Globals.gyro_x = (float)Convert.ToDouble(GyroscopeX.Text);
            Globals.gyro_y = (float)Convert.ToDouble(GyroscopeY.Text);
            Globals.gyro_z = (float)Convert.ToDouble(GyroscopeZ.Text);

            if (Globals.act2on) AngryWithMotion();
            else if (Globals.act4on) HappyWithMotion();
        }

        public async void WriteOutput()
        {

            String accelx = Globals.accel_x.ToString();
            String accely = Globals.accel_y.ToString();
            String accelz = Globals.accel_z.ToString();

            String gyrox = Globals.gyro_x.ToString();
            String gyroy = Globals.gyro_y.ToString();
            String gyroz = Globals.gyro_z.ToString();
            gyroz = gyroz + "\n";

            DateTime now = DateTime.Now;
            Globals.output = now.ToString() + "," + accelx + "," + accely + "," + accelz + "," + gyrox + "," + gyroy + "," + gyroz + "," + Globals.emotion_state + "," + Globals.color_state + "," + Globals.sound_state + "," + Globals.act_state;
            var line = new List<string>();
            line.Add(Globals.output);

            // create file 
            StorageFolder folder = await ApplicationData.Current.RoamingFolder.CreateFolderAsync("participant_data", CreationCollisionOption.OpenIfExists);
            StorageFile file = await folder.CreateFileAsync("participant1.txt", CreationCollisionOption.OpenIfExists);

            await Windows.Storage.FileIO.AppendLinesAsync(file, line);

        }

        public void AngryWithMotion()
        {
            Globals.stopon = false;

            if (Math.Abs(Globals.gyro_x) > 85.0 || Math.Abs(Globals.gyro_y) > 85.0 || Math.Abs(Globals.gyro_z) > 85.0)
            {
                if (Globals.isTiming == false && Globals.act2on)
                {
                    m_robot.Roll(0, 0);
                    m_robot.SetRGBLED(0, 0, 0);
                    Globals.color_state = "none";
                    Globals.isTiming = true;
                    Globals.gyro_stopWatch.Start();
                    if (angry_music.CurrentState != MediaElementState.Playing) { angry_music.Play(); }
                    m_robot.SetRGBLED(255, 0, 0);
                    Globals.color_state = "red";
                }
            }
            else if (Globals.isTiming == true && (Math.Abs(Globals.gyro_x) < 85.0 && Math.Abs(Globals.gyro_y) < 85.0 && Math.Abs(Globals.gyro_z) < 85.0))
            {
                Globals.gyro_time = Globals.gyro_stopWatch.Elapsed;
                if (Globals.gyro_time.Seconds > 2)
                {
                    m_robot.Roll(0, 0);
                    m_robot.SetRGBLED(0, 0, 0);
                    Globals.color_state = "none";
                    angry_music.Stop();
                    Globals.gyro_stopWatch.Stop();
                    Globals.isTiming = false;
                    Globals.gyro_stopWatch.Reset();
                }
            }
        }

        public void HappyWithMotion()
        {
            Globals.stopon = false;
            int colorstate = 0;

            if (Math.Abs(Globals.gyro_x) > 85.0 || Math.Abs(Globals.gyro_y) > 85.0 || Math.Abs(Globals.gyro_z) > 85.0)
            {
                if (Globals.isTimingAct4 == false)
                {
                    m_robot.Roll(0, 0);
                    m_robot.SetRGBLED(0, 0, 0);
                    Globals.color_state = "none";
                    Globals.isTimingAct4 = true;
                    Globals.gyro_stopWatch.Start();
                    if (happy_music.CurrentState != MediaElementState.Playing) { happy_music.Play(); }
                    if (colorstate == 0) { m_robot.SetRGBLED(255, 0, 0); colorstate++; }
                    if (colorstate == 1) { m_robot.SetRGBLED(0, 255, 0); colorstate++; }
                    if (colorstate == 2) { m_robot.SetRGBLED(0, 0, 255); colorstate = 0; }
                    Globals.color_state = "multi";
                }
            }
            else if (Globals.isTimingAct4 == true && (Math.Abs(Globals.gyro_x) < 85.0 && Math.Abs(Globals.gyro_y) < 85.0 && Math.Abs(Globals.gyro_z) < 85.0))
            {
                Globals.gyro_time = Globals.gyro_stopWatch.Elapsed;
                if (Globals.gyro_time.Seconds > 2)
                {
                    m_robot.Roll(0, 0);
                    //if (child_crying.CurrentState != MediaElementState.Playing) { child_crying.Play(); }
                    m_robot.SetRGBLED(0, 0, 255);
                    Globals.color_state = "blue";
                    happy_music.Stop();
                    Globals.gyro_stopWatch.Stop();
                    Globals.isTimingAct4 = false;
                    Globals.gyro_stopWatch.Reset();
                }
            }
        }

        public async Task DelayMyTask(int ms)
        {
            await Task.Delay(ms);
        }

        private async void OnCollisionDetected(object sender, CollisionData data)
        {
            Globals.collisionOccurred = true;
            if ((Math.Abs(Globals.gyro_x) > 85.0 || Math.Abs(Globals.gyro_y) > 85.0 || Math.Abs(Globals.gyro_z) > 85.0) && Globals.act1on == false && Globals.act2on == false && Globals.act4on == false && Globals.angryon == false && Globals.happyon == false && Globals.scaredon == false && Globals.sadon == false && Globals.toplefton == false && Globals.toprighton == false && Globals.bottomlefton == false && Globals.bottomrighton == false && Globals.centeron == false) ouch.Play();
            else if(Globals.act3on)
            {
                if (m_robot.BluetoothName == "Sphero-BPR" && (Math.Abs(Globals.gyro_x) > 20.0 || Math.Abs(Globals.gyro_y) > 20.0 || Math.Abs(Globals.gyro_z) > 20.0))
                {
                    m_robot.Roll(0, 0);
                    await DelayMyTask(1000);
                    m_robot.SetRGBLED(0, 0, 0);
                    Globals.color_state = "none";
                    m_robot.Roll(90, 0.1f);
                    haha_mean.Play();
                    for (int x = 0; x < 40; x++)
                    {
                        if (Globals.color_B == 255) { m_robot.SetRGBLED(0, 0, 255); await DelayMyTask(100); Globals.color_B = 0; Globals.color_state = "blue";  }
                        else { m_robot.SetRGBLED(0, 0, 0); await DelayMyTask(100); Globals.color_B = 255; Globals.color_state = "none"; }
                    }
                    await DelayMyTask(1500);
                    m_robot.SetRGBLED(0, 0, 0);
                }
               else
               {
                    if (Math.Abs(Globals.gyro_x) > 20.0 || Math.Abs(Globals.gyro_y) > 20.0 || Math.Abs(Globals.gyro_z) > 20.0)
                    {
                        ah.Play();
                        await DelayMyTask(600);
                        m_robot.Roll(0, 0);
                        child_crying.Play();
                        for (int x = 0; x < 40; x++)
                        {
                            if (Globals.color_R == 255) { m_robot.SetRGBLED(255, 0, 0); await DelayMyTask(40); Globals.color_R = 0; Globals.color_state = "red"; }
                            else { m_robot.SetRGBLED(0, 0, 0); await DelayMyTask(40); Globals.color_R = 255; Globals.color_state = "none"; }
                        }
                        await DelayMyTask(1500);
                        m_robot.SetRGBLED(0, 0, 0);
                    }
                }
            }

            Globals.collisionOccurred = false;
        }

        private async void act1_button_Click(object sender, RoutedEventArgs e)
        {
            Globals.act_state = "act1";
            m_robot.Roll(0, 0);
            m_robot.SetRGBLED(0, 0, 0);
            Globals.color_state = "none";
            Globals.stopon = false;

            if (Globals.act1on == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.angryon == false && Globals.happyon == false && Globals.scaredon == false)
            {
                act1_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                act1_button.Content = "Activity 1 Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.act1on = true;
            }
            else
            {
                act1_button.Background = new SolidColorBrush(Windows.UI.Colors.Gray);
                act1_button.Content = "Activity 1";
                happy_music.Stop();
                angry_music.Stop();
                scared_music.Stop();
                m_robot.Roll(0, 0);
                Globals.act1on = false;
                return;
            }

            await Happy();
            if (Globals.successful_end) await Happy();
            if (Globals.successful_end) await Angry();
            if (Globals.successful_end) await Angry();
            if (Globals.successful_end) await Scared();
            if (Globals.successful_end) await Scared();

 
            act1_button.Background = new SolidColorBrush(Windows.UI.Colors.Gray);
            act1_button.Content = "Activity 1";
            Globals.successful_end = true;
            m_robot.Roll(0, 0);
            m_robot.SetRGBLED(0,0,0);
            Globals.color_state = "none";
            Globals.act_state = "none";
            return;       
        }


        private void act2_button_Click(object sender, RoutedEventArgs e)
        {
            Globals.act_state = "act2";
            m_robot.Roll(0, 0);
            m_robot.SetRGBLED(0, 0, 0);
            Globals.color_state = "none";
            Globals.stopon = false;

            if (Globals.act1on == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.angryon == false && Globals.happyon == false && Globals.scaredon == false)
            {
                act2_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                act2_button.Content = "Activity 2 Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.act2on = true;
            }

            else
            {
                Globals.act2on = false;
                act2_button.Background = new SolidColorBrush(Windows.UI.Colors.Gray);
                act2_button.Content = "Activity 2";
                angry_music.Stop();
                m_robot.Roll(0, 0);
                return;
            }
            Globals.act_state = "none";
            return;
        }
        private async void act3_button_Click(object sender, RoutedEventArgs e)
        {
            Globals.act_state = "act3";
            m_robot.Roll(0, 0);
            m_robot.SetRGBLED(0, 0, 0);
            Globals.color_state = "none";
            Globals.stopon = false;
            if (Globals.act2on == false && Globals.act1on == false && Globals.act4on == false && Globals.angryon == false && Globals.scaredon == false)
            {
                act3_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                act3_button.Content = "Activity 3 Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.act3on = true;
            }
            else
            {
                act3_button.Background = new SolidColorBrush(Windows.UI.Colors.Gray);
                act3_button.Content = "Activity 3";
                Globals.act3on = false;
                m_robot.Roll(0, 0);
                return;
                // Stop activity 3
            }

            // This is the happy SPHERO so she will dance and play happy music
            while (Globals.stopon == false)
            {
                await Happy(); await DelayMyTask(3000);
                if (Globals.stopon)
                {
                    m_robot.Roll(0, 0);
                    m_robot.SetRGBLED(0, 0, 0);
                    Globals.color_state = "none";
                }
            }
            Globals.act_state = "none";
        }

        private async void act4_button_Click(object sender, RoutedEventArgs e)
        {
            Globals.act_state = "act4";
            m_robot.Roll(0, 0);
            m_robot.SetRGBLED(0, 0, 0);
            Globals.color_state = "none";
            Globals.stopon = false;

            if (Globals.act1on == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.angryon == false && Globals.happyon == false && Globals.scaredon == false)
            {
                act4_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                act4_button.Content = "Activity 4 Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.act4on = true;
            }

            else
            {
                Globals.act4on = false;
                act4_button.Background = new SolidColorBrush(Windows.UI.Colors.Gray);
                act4_button.Content = "Activity 4";
                angry_music.Stop();
                m_robot.Roll(0, 0);
                return;
            }
            Globals.act_state = "none";
            return;
        }

        private async void angry_button_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                angry_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                angry_button.Content = "ANGRY!";
                await DelayMyTask(500);
                angry_button.Background = new SolidColorBrush(Windows.UI.Colors.LightYellow);
                angry_button.Content = "Angry";
                Globals.recorded[Globals.record_index, 0] = "sound";
                Globals.recorded[Globals.record_index, 1] = "angry";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            m_robot.Roll(0, 0);
            m_robot.SetRGBLED(0, 0, 0);
            Globals.color_state = "none";
            Globals.stopon = false;
            if (Globals.angryon == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.act1on == false && Globals.happyon == false && Globals.scaredon == false)
            {
                angry_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                angry_button.Content = "Angry Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.angryon = true;
            }
            else
            {
                angry_button.Background = new SolidColorBrush(Windows.UI.Colors.LightYellow);
                angry_button.Content = "Angry";
                Globals.angryon = false;
                m_robot.Roll(0, 0);
                return;
                // Stop Angry
            }

            await Angry();
            angry_button.Background = new SolidColorBrush(Windows.UI.Colors.LightYellow);
            angry_button.Content = "Angry";
            Globals.angryon = false;

            return;
        }

        private async void happy_button_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                happy_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                happy_button.Content = "HAPPY!";
                await DelayMyTask(500);
                happy_button.Background = new SolidColorBrush(Windows.UI.Colors.LightYellow);
                happy_button.Content = "Happy";
                Globals.recorded[Globals.record_index, 0] = "sound";
                Globals.recorded[Globals.record_index, 1] = "happy";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            m_robot.Roll(0, 0);
            m_robot.SetRGBLED(0, 0, 0);
            Globals.color_state = "none";
            Globals.stopon = false;
            if (Globals.happyon == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.act1on == false && Globals.angryon == false && Globals.scaredon == false)
            {
                happy_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                happy_button.Content = "Happy Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.happyon = true;
            }
            else
            {
                happy_button.Background = new SolidColorBrush(Windows.UI.Colors.LightYellow);
                happy_button.Content = "Happy";
                Globals.happyon = false;
                m_robot.Roll(0, 0);
                return;
                // Stop Happy
            }

            await Happy();
            happy_button.Background = new SolidColorBrush(Windows.UI.Colors.LightYellow);
            happy_button.Content = "Happy";
            Globals.happyon = false;
            return;
        }
        private async void scared_button_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                scared_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                scared_button.Content = "SCARED!";
                await DelayMyTask(500);
                scared_button.Background = new SolidColorBrush(Windows.UI.Colors.LightYellow);
                scared_button.Content = "Scared";
                Globals.recorded[Globals.record_index, 0] = "sound";
                Globals.recorded[Globals.record_index, 1] = "scared";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            m_robot.Roll(0, 0);
            m_robot.SetRGBLED(0, 0, 0);
            Globals.color_state = "none";
            Globals.stopon = false;
            if (Globals.scaredon == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.angryon == false && Globals.happyon == false && Globals.act1on == false)
            {
                scared_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                scared_button.Content = "Scared Playing";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.scaredon = true;
            }
            else
            {
                scared_button.Background = new SolidColorBrush(Windows.UI.Colors.LightYellow);
                scared_button.Content = "Scared";
                Globals.scaredon = false;
                return;
                // Stop scared
            }

            await Scared();
            scared_button.Background = new SolidColorBrush(Windows.UI.Colors.LightYellow);
            scared_button.Content = "Scared";
            Globals.scaredon = false;
            return;
        }
        private void stop_button_Click(object sender, RoutedEventArgs e)
        {
            m_robot.Roll(0, 0);
            Globals.color_R = 0; Globals.color_G = 0; Globals.color_B = 0;
            Globals.color_state = "none";
            m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);
            stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
            act1_button.Background = new SolidColorBrush(Windows.UI.Colors.LightGreen);
            act1_button.Content = "Activity 1";
            act2_button.Background = new SolidColorBrush(Windows.UI.Colors.LightGreen);
            act2_button.Content = "Activity 2";
            act3_button.Background = new SolidColorBrush(Windows.UI.Colors.LightGreen);
            act3_button.Content = "Activity 3";
            act4_button.Background = new SolidColorBrush(Windows.UI.Colors.LightGreen);
            act4_button.Content = "Activity 4";
            angry_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSeaGreen);
            angry_button.Content = "Angry";
            happy_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSeaGreen);
            happy_button.Content = "Happy";
            sad_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSeaGreen);
            sad_button.Content = "Sad";
            scared_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSeaGreen);
            scared_button.Content = "Scared";
            center_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            center_button.Content = "Center";
            topright_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            topright_button.Content = "Right";
            topleft_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            topleft_button.Content = "Left";
            bottomleft_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            bottomleft_button.Content = "Up";
            bottomright_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            bottomright_button.Content = "Down";
            big_circle.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            big_circle.Content = "Big Circles";
            small_circle.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            small_circle.Content = "Small Circles";
            vibrate.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
            vibrate.Content = "Vibrate";
            slow.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
            slow.Content = "Slow";
            fast.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
            fast.Content = "Fast";
            blue_button.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
            blue_button.Content = "Blue";
            red_button.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
            red_button.Content = "Red";
            green_button.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
            green_button.Content = "Green";
            Globals.recordon = false;
            Globals.stopon = true;
            Globals.act1on = Globals.act2on = Globals.act3on = Globals.act4on = Globals.angryon = Globals.happyon = Globals.scaredon = Globals.sadon = false;
            happy_music.Stop(); angry_music.Stop(); scared_music.Stop(); happy_vocal.Stop(); angry_vocal.Stop(); scared_vocal.Stop(); child_crying.Stop();
            m_robot.Roll(0, 0);
        }

        private async void topright_Click(object sender, RoutedEventArgs e) 
        {
            if (Globals.recordon)
            {
                topright_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                topright_button.Content = "RIGHT!";
                await DelayMyTask(500);
                topright_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                topright_button.Content = "Top Right";
                Globals.recorded[Globals.record_index, 0] = "move";
                Globals.recorded[Globals.record_index, 1] = "top";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);
            m_robot.Roll(0, 0);

            //m_robot.SetHeading(0);
            await DelayMyTask(1000);

            Globals.stopon = false;
            if (Globals.vibrateon == false && Globals.smallcircleon == false && Globals.bigcircleon == false && Globals.centeron == false && Globals.toprighton == false && Globals.toplefton == false && Globals.bottomlefton == false && Globals.bottomrighton == false && Globals.scaredon == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.angryon == false && Globals.happyon == false && Globals.act1on == false)
            {
                topright_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                topright_button.Content = "MOVING RIGHT";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.toprighton = true;
            }
            else
            {
                topright_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                topright_button.Content = "Right";
                Globals.toprighton = false;
                return;
                // Stop top right
            }

            m_robot.Roll(45, 0.3f);
            await DelayMyTask(4000);
            m_robot.Roll(0, 0);

            topright_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            topright_button.Content = "Right";

            Globals.toprighton = false;
            Globals.lastdirection = 45;
            return;
        }

        private async void bottomright_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                angry_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                angry_button.Content = "DOWN!";
                await DelayMyTask(500);
                angry_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                angry_button.Content = "Down";
                Globals.recorded[Globals.record_index, 0] = "move";
                Globals.recorded[Globals.record_index, 1] = "down";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);
            m_robot.Roll(0, 0);

            //m_robot.SetHeading(0);

            Globals.stopon = false;
            if (Globals.vibrateon == false && Globals.smallcircleon == false && Globals.bigcircleon == false && Globals.centeron == false && Globals.toprighton == false && Globals.toplefton == false && Globals.bottomlefton == false && Globals.bottomrighton == false && Globals.scaredon == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.angryon == false && Globals.happyon == false && Globals.act1on == false)
            {
                bottomright_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                bottomright_button.Content = "MOVING DOWN";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.bottomrighton = true;
            }
            else
            {
                bottomright_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                bottomright_button.Content = "Down";
                Globals.bottomrighton = false;
                return;
                // Stop bottom right
            }

            m_robot.Roll(135, 0.3f);
            await DelayMyTask(4000);
            m_robot.Roll(0, 0);

            bottomright_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            bottomright_button.Content = "Down";
            Globals.bottomrighton = false;
            Globals.lastdirection = 135;
            return;
        }

        private async void topleft_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                topleft_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                topleft_button.Content = "LEFT!";
                await DelayMyTask(500);
                topleft_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                topleft_button.Content = "Left";
                Globals.recorded[Globals.record_index, 0] = "move";
                Globals.recorded[Globals.record_index, 1] = "left";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);
            m_robot.Roll(0, 0);

            //m_robot.SetHeading(0);

            Globals.stopon = false;
            if (Globals.vibrateon == false && Globals.smallcircleon == false && Globals.bigcircleon == false && Globals.centeron == false && Globals.toprighton == false && Globals.toplefton == false && Globals.bottomlefton == false && Globals.bottomrighton == false && Globals.scaredon == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.angryon == false && Globals.sadon == false && Globals.happyon == false && Globals.act1on == false)
            {
                topleft_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                topleft_button.Content = "MOVING LEFT";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.toplefton = true;
            }
            else
            {
                topleft_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                topleft_button.Content = "Left";
                Globals.toplefton = false;
                return;
                // Stop bottom right
            }

            m_robot.Roll(315, 0.3f);
            await DelayMyTask(4000);
            m_robot.Roll(0, 0);

            topleft_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            topleft_button.Content = "Left";
            Globals.toplefton = false;
            Globals.lastdirection = 315;
            return;

        }

        private async void bottomleft_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                bottomleft_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                bottomleft_button.Content = "UP!";
                await DelayMyTask(500);
                bottomleft_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                bottomleft_button.Content = "Up";
                Globals.recorded[Globals.record_index, 0] = "move";
                Globals.recorded[Globals.record_index, 1] = "up";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);
            m_robot.Roll(0, 0);

            //m_robot.SetHeading(0);

            Globals.stopon = false;
            if (Globals.vibrateon == false && Globals.smallcircleon == false && Globals.bigcircleon == false && Globals.centeron == false && Globals.toprighton == false && Globals.toplefton == false && Globals.bottomlefton == false && Globals.bottomrighton == false && Globals.scaredon == false && Globals.act2on == false && Globals.act3on == false && Globals.act4on == false && Globals.angryon == false && Globals.happyon == false && Globals.sadon == false && Globals.act1on == false)
            {
                bottomleft_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                bottomleft_button.Content = "MOVING UP";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.bottomlefton = true;
            }
            else
            {
                bottomleft_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                bottomleft_button.Content = "Up";
                Globals.bottomlefton = false;
                return;
                // Stop bottom right
            }

            m_robot.Roll(225, 0.3f);
            await DelayMyTask(4000);
            m_robot.Roll(0, 0);

            bottomleft_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            bottomleft_button.Content = "Up";
            Globals.bottomlefton = false;
            Globals.lastdirection = 225;

            return;

        }

        private async void center_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                Globals.recorded[Globals.record_index, 0] = "move";
                Globals.recorded[Globals.record_index, 1] = "center";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
            }
            m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);
            m_robot.Roll(0, 0);
            Globals.stopon = false;
            if (Globals.vibrateon == false && Globals.smallcircleon == false && Globals.bigcircleon == false && Globals.centeron == false && Globals.toprighton == false && Globals.toplefton == false && Globals.bottomlefton == false && Globals.bottomrighton == false && Globals.scaredon == false && Globals.act4on && Globals.act2on == false && Globals.act3on == false && Globals.angryon == false && Globals.happyon == false && Globals.act1on == false && Globals.sadon == false)
            {
                center_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                center_button.Content = "Going to center";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.centeron = true;
            }
            else
            {
                center_button.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                center_button.Content = "Center";
                Globals.centeron = false;
                return;
                // Stop bottom right
            }

            switch(Globals.lastdirection)
            {
                case 45: 
                    Globals.heading = 225;
                    break;
                case 135: 
                    Globals.heading = 315;
                    break;
                case 225: 
                    Globals.heading = 45;
                    break;
                case 315: 
                    Globals.heading = 135;
                    break;
                default:
                    Globals.heading = 0;
                    break;
            }

            m_robot.Roll(Globals.heading, 0.3f);
            await DelayMyTask(4000);
            m_robot.Roll(0, 0);
            center_button.Background = new SolidColorBrush(Windows.UI.Colors.Gray);
            center_button.Content = "Center";
            Globals.centeron = false;

        }

        public async void blue_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                blue_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                blue_button.Content = "BLUE!";
                await DelayMyTask(500);
                blue_button.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                blue_button.Content = "Blue";
                Globals.recorded[Globals.record_index, 0] = "color";
                Globals.recorded[Globals.record_index, 1] = "blue";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            Globals.stopon = false;
                blue_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                blue_button.Content = "BLUE";
                red_button.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                red_button.Content = "Red";
                green_button.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                green_button.Content = "Green";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.blueon = true;

                Globals.color_R = 0;
                Globals.color_G = 0;
                Globals.color_B = 255;
                Globals.color_state = "blue";
                m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);

        }

        public async void green_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                green_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                green_button.Content = "GREEN!";
                await DelayMyTask(500);
                green_button.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                green_button.Content = "Green";
                Globals.recorded[Globals.record_index, 0] = "color";
                Globals.recorded[Globals.record_index, 1] = "green";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            Globals.stopon = false;

            green_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                green_button.Content = "GREEN";
                red_button.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                red_button.Content = "Red";
                blue_button.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                blue_button.Content = "Blue";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.greenon = true;

                Globals.color_R = 0;
                Globals.color_G = 255;
                Globals.color_B = 0;
                Globals.color_state = "green";
                m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);
            
        }

        public async void red_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                red_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                red_button.Content = "RED!";
                await DelayMyTask(500);
                red_button.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                red_button.Content = "Red";
                Globals.recorded[Globals.record_index, 0] = "color";
                Globals.recorded[Globals.record_index, 1] = "red";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            Globals.stopon = false;

            red_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                red_button.Content = "RED";
                blue_button.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                blue_button.Content = "Blue";
                green_button.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                green_button.Content = "Green";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                Globals.redon = true;
 
            Globals.color_R = 255;
            Globals.color_G = 0;
            Globals.color_B = 0;
            Globals.color_state = "red";
            m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);

        }

        private async void smallcircle_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                small_circle.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                small_circle.Content = "SMALL CIRCLE!";
                await DelayMyTask(500);
                small_circle.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                small_circle.Content = "Small Circle";
                Globals.recorded[Globals.record_index, 0] = "move";
                Globals.recorded[Globals.record_index, 1] = "smallcircle";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            Globals.act_state = "smallcircle";
            Globals.stopon = false;
            Globals.smallcircleon = true;
            m_robot.Roll(0, 0);
            m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);

                small_circle.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                small_circle.Content = "Circling (small)";
                big_circle.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                big_circle.Content = "Big Circles";
                vibrate.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                vibrate.Content = "Vibrate";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);

                int head_change = 0;
                int limit = 0;
                int delay = 200;
                TimeSpan s_time;
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                s_time = stopWatch.Elapsed;

                while (s_time.Milliseconds < 500)
                {
                    s_time = stopWatch.Elapsed;
                }

                Globals.speed = 0.2f;
                Globals.heading = 0;
                while (Globals.stopon == false)
                {
                    if (Globals.speed <= 0.2f) { head_change = 30; limit = 330; delay = 80; }
                    else if (Globals.speed <= 0.3f) { head_change = 40; limit = 320; delay = 95; }
                    else if (Globals.speed <= 0.5f) { head_change = 50; limit = 310; delay = 110; }

                    if (Globals.heading == limit) Globals.heading = 359;
                    else if (Globals.heading > limit) Globals.heading = 0;
                    else if (Globals.heading < limit) Globals.heading += head_change;

                    m_robot.Roll(Globals.heading, Globals.speed);
                    await DelayMyTask(delay);
                    //result = (int)GetAsyncKeyState(VK_LBUTTON);

                    if (Globals.act1on || Globals.act2on || Globals.act3on || Globals.act4on || Globals.bigcircleon || Globals.vibrateon || Globals.stopon)
                    {
                         m_robot.Roll(0, 0);
                         Globals.smallcircleon = false;
                         break;
                    }
                }

                stopWatch.Stop();
                Globals.smallcircleon = false;
                Globals.act_state = "none";
        }

        private async void bigcircle_Click(object sender, RoutedEventArgs e)
        {

            if (Globals.recordon)
            {
                big_circle.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                big_circle.Content = "BIG CIRCLE!";
                await DelayMyTask(500);
                big_circle.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                big_circle.Content = "Big Circle";
                Globals.recorded[Globals.record_index, 0] = "move";
                Globals.recorded[Globals.record_index, 1] = "bigcircle";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            Globals.act_state = "bigcircle";
            Globals.stopon = false;
            
                Globals.bigcircleon = true;
                m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);

                big_circle.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                big_circle.Content = "Circling (big)";
                small_circle.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
                small_circle.Content = "Small Circles";
                vibrate.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                vibrate.Content = "Vibrate";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);

                int result = 0;
                int head_change = 0;
                int limit = 0;
                int delay = 0;

                TimeSpan s_time;
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                if (Globals.speed == 0.0) Globals.speed = 0.2f;

                while (s_time.Milliseconds < 500)
                {
                    s_time = stopWatch.Elapsed;
                }

                Globals.heading = 0;
                Globals.speed = 0.2f;
                while(Globals.stopon == false)
                {
                    if (Globals.speed <= 0.2) { head_change = 20; limit = 340; delay = 150; }
                    else if (Globals.speed <= 0.3) { head_change = 30; limit = 330; delay = 180; }
                    else if (Globals.speed <= 0.5) { head_change = 40; limit = 320; delay = 210; }

                    if (Globals.heading == limit) Globals.heading = 359;
                    else if (Globals.heading > limit) Globals.heading = 0;
                    else if (Globals.heading < limit) Globals.heading += head_change;

                    m_robot.Roll(Globals.heading, Globals.speed);
                    await DelayMyTask(delay);
                    result = (int)GetAsyncKeyState(VK_LBUTTON);
                    if(Globals.act1on || Globals.act2on || Globals.act3on || Globals.vibrateon || Globals.smallcircleon || Globals.stopon)
                    {
                        m_robot.Roll(0, 0);
                        break;
                    }
                }

                stopWatch.Stop();
                Globals.bigcircleon = false;
                Globals.act_state = "none";
        }

        private async void vibrate_sphero()
        {
            if (Globals.recordon)
            {
                vibrate.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                vibrate.Content = "VIBRATE!";
                await DelayMyTask(500);
                vibrate.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                vibrate.Content = "Vibrate";
                Globals.recorded[Globals.record_index, 0] = "move";
                Globals.recorded[Globals.record_index, 1] = "vibrate";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            Globals.act_state = "vibrate";
            int result = 0;
            int delay = 15;
            TimeSpan s_time;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            if (Globals.speed == 0.0) Globals.speed = 0.03f;

            while (s_time.Milliseconds < 500)
            {
                s_time = stopWatch.Elapsed;
            }

            Globals.heading = 0;
            Globals.speed = 0.4f;
            while (Globals.stopon == false)
            {
                if (Globals.heading == 0) Globals.heading = 180;
                else Globals.heading = 0;

                m_robot.Roll(Globals.heading, 0.05f);
                await DelayMyTask(delay);
                result = (int)GetAsyncKeyState(VK_LBUTTON);

                if (Globals.act1on || Globals.act2on || Globals.act3on || Globals.bigcircleon || Globals.smallcircleon || Globals.stopon)
                {
                    m_robot.Roll(0, 0);
                    Globals.vibrateon = false;
                    break;
                }
            }
            stopWatch.Stop();

        }

        private void vibrate_Click(object sender, RoutedEventArgs e)
        {

            Globals.stopon = false;
            Globals.vibrateon = true;
            m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);

            vibrate.Background = new SolidColorBrush(Windows.UI.Colors.Green);
            vibrate.Content = "Vibrating";
            small_circle.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            small_circle.Content = "Small Circles";
            big_circle.Background = new SolidColorBrush(Windows.UI.Colors.MediumOrchid);
            big_circle.Content = "Big Circles";
            stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);

            vibrate_sphero();

            Globals.vibrateon = false;
        }

        public async Task Happy()
        {
            if (Globals.act2on)
            {
                return;
            }
            Globals.successful_end = true;
            Globals.emotion_state = "happy";
            Globals.happyon = true;
            TimeSpan s_time;
            Stopwatch stopWatch = new Stopwatch();
            int result = 0;
            int secondsoflaughter = 0;

            // Timers for time movement and lights
            stopWatch.Start();
        
            happy_music.Play();
            for (int x = 0; x < 60; x++)
            {
                    s_time = stopWatch.Elapsed;
                    if (Globals.color_R > 254) Globals.color_R = 0; else Globals.color_R += 40;
                    if (Globals.color_G > 254) Globals.color_G = 0; else Globals.color_G += 40;
                    if (Globals.color_B > 254) Globals.color_B = 0; else Globals.color_B += 40;
                    m_robot.SetRGBLED(Globals.color_R, Globals.color_G, Globals.color_B);
                    Globals.color_state = "multi";

                    if (Globals.heading > 310) Globals.heading = 0;
                    if (Globals.heading < 310) Globals.heading += 50;

                    m_robot.Roll(Globals.heading, 0.2f);
                    await DelayMyTask(300);

                    if (Globals.stopon || (Globals.act3on && Globals.collisionOccurred))
                    {
                        Globals.successful_end = false;
                        break;
                    }

                    secondsoflaughter = s_time.Seconds;
                    if (secondsoflaughter % 8 == 0)
                    {
                        happy_vocal.Play();
                    }

            }
                Globals.happyon = false;
                stopWatch.Stop();
                happy_music.Stop();
                m_robot.Roll(0, 0);
                await DelayMyTask(1500);
                Globals.emotion_state = "none";
                return;

        }

        public async Task Angry()
        {
            Globals.emotion_state = "angry";
            Globals.angryon = true;
            TimeSpan s_time;
            Stopwatch stopWatch = new Stopwatch();
            int result = 0;
            Globals.heading = 0;
          
            // Timers for time movement and lights
            stopWatch.Start();
            s_time = stopWatch.Elapsed;

            angry_music.Play();
            for (int x = 0; x < 22; x++)
            {
                m_robot.SetRGBLED(0, 0, 0);
                Globals.color_state = "none";

                if (Globals.heading == 0) Globals.heading = 45;
                else if (Globals.heading == 45) Globals.heading = 225;
                else if (Globals.heading == 225) Globals.heading = 90;
                else if (Globals.heading == 90) Globals.heading = 270;
                else if (Globals.heading == 270) Globals.heading = 135;
                else if (Globals.heading == 135) Globals.heading = 315;
                else if (Globals.heading == 315) Globals.heading = 180;
                else if (Globals.heading == 180) Globals.heading = 0;

                m_robot.Roll(Globals.heading, 0.6f);
                await DelayMyTask(400);
                m_robot.SetRGBLED(255, 0, 0);
                Globals.color_state = "red";
                m_robot.Roll(Globals.heading, 0);
                await DelayMyTask(400);

                result = (int)GetAsyncKeyState(VK_LBUTTON);
                if (((result == -32767 || result == -32768) && s_time.Milliseconds > 500) || Globals.stopon)
                {
                    Globals.successful_end = false;
                    break;
                }
            }
            stopWatch.Stop();
            angry_music.Stop();
            m_robot.Roll(0, 0);
            Globals.angryon = false;
            await DelayMyTask(1000);
            Globals.emotion_state = "none";
        }

        public async Task Scared()
        {
            Globals.emotion_state = "scared";
            Globals.scaredon = true;
            TimeSpan s_time;
            Stopwatch stopWatch = new Stopwatch();
            int result = 0;
            Globals.heading = 0;

            // Timers for time movement and lights
            stopWatch.Start();
            s_time = stopWatch.Elapsed;

            scared_music.Play();
            for (int x = 0; x < 55; x++)
            {
                if (x % 2 == 0 || x % 3 == 0) { m_robot.SetRGBLED(255, 255, 255); Globals.color_state = "white"; }
                else m_robot.SetRGBLED(0, 0, 0);
                Globals.color_state = "none";

                if (Globals.heading > 311) Globals.heading = 0;
                if (Globals.heading < 311) Globals.heading += 110;

                m_robot.Roll(Globals.heading, 0.7f);
                m_robot.SetRGBLED(0, 0, 0);
                Globals.color_state = "none";
                await DelayMyTask(400);

                result = (int)GetAsyncKeyState(VK_LBUTTON);
                if (((result == -32767 || result == -32768) && s_time.Milliseconds > 500) || Globals.stopon)
                {
                    Globals.successful_end = false;
                    break;
                }
            }
            stopWatch.Stop();
            scared_music.Stop();
            m_robot.Roll(0, 0);
            await DelayMyTask(1000);
            Globals.emotion_state = "none";
        }

        public void slow_Click(object sender, RoutedEventArgs e)
        {
            Globals.stopon = false;
            slow.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                slow.Content = "SLOW";
                fast.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                fast.Content = "Fast";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);

            Globals.speed = 0.2f;
        }

        public void fast_Click(object sender, RoutedEventArgs e)
        {
            Globals.stopon = false;

            fast.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                fast.Content = "FAST";
                slow.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
                slow.Content = "Slow";
                stop_button.Background = new SolidColorBrush(Windows.UI.Colors.Red);

            Globals.speed = 0.5f;
        }

        public async void cheer_button_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                cheer_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                cheer_button.Content = "CHEER!";
                await DelayMyTask(500);
                cheer_button.Background = new SolidColorBrush(Windows.UI.Colors.LightYellow);
                cheer_button.Content = "Cheer";
                Globals.recorded[Globals.record_index, 0] = "sound";
                Globals.recorded[Globals.record_index, 1] = "cheer";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            Globals.emotion_state = "cheering";
            Globals.stopon = false;

            cheer_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
            cheer_button.Content = "CHEER!";

            // play cheer
            cheering.Play();

            // flash lights 
            m_robot.SetRGBLED(255, 255, 255);
            await DelayMyTask(200);
            m_robot.SetRGBLED(255,0,0);
            await DelayMyTask(200);
            m_robot.SetRGBLED(0, 255, 0);
            await DelayMyTask(200);
            m_robot.SetRGBLED(0, 0, 255);
            await DelayMyTask(200);
            m_robot.SetRGBLED(0, 0, 0);

            // stop motion
            m_robot.Roll(0, 0);

            cheer_button.Background = new SolidColorBrush(Windows.UI.Colors.BlueViolet);
            cheer_button.Content = "Cheer!";
            Globals.emotion_state = "none";
        }

        private async void sad_button_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.recordon)
            {
                sad_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                sad_button.Content = "SAD!";
                await DelayMyTask(500);
                sad_button.Background = new SolidColorBrush(Windows.UI.Colors.LightYellow);
                sad_button.Content = "Sad";
                Globals.recorded[Globals.record_index, 0] = "sound";
                Globals.recorded[Globals.record_index, 1] = "sad";
                if (Globals.record_index < 9)
                {
                    Globals.record_index++;
                }
                else
                {
                    Globals.record_index = 0;
                }
                return;
            }
            Globals.emotion_state = "sad";
            sad_button.Background = new SolidColorBrush(Windows.UI.Colors.Green);
            sad_button.Content = "SAD";
            Globals.sadon = true;
            TimeSpan s_time;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            child_crying.Play();

            while (Globals.stopon == false)
            {
                if (Globals.act1on || Globals.act2on || Globals.act3on || Globals.act4on || Globals.vibrateon || Globals.smallcircleon || Globals.bigcircleon || Globals.angryon || Globals.happyon || Globals.stopon)
                {
                    m_robot.Roll(0, 0);
                    child_crying.Stop();
                    Globals.sadon = false;
                    sad_button.Background = new SolidColorBrush(Windows.UI.Colors.LightSeaGreen);
                    sad_button.Content = "Sad";
                    break;
                }
                else
                {

                    s_time = stopWatch.Elapsed;
                    m_robot.Roll(0, 0.2f);
                    m_robot.SetRGBLED(0, 0, 255);
                    Globals.color_state = "blue";
                    await DelayMyTask(60);

                    m_robot.Roll(90, 0);
                    await DelayMyTask(400);

                    m_robot.Roll(90, 0.2f);
                    m_robot.SetRGBLED(0, 0, 0);
                    Globals.color_state = "none";
                    await DelayMyTask(60);

                    m_robot.Roll(0, 0);
                    await DelayMyTask(400);

                    if (child_crying.CurrentState != MediaElementState.Playing) 
                    {
                        child_crying.Play();
                    }
    
    
                }
            }
            Globals.emotion_state = "none";
        }
        public void record_Click(object sender, RoutedEventArgs e)
        {
            record.Background = new SolidColorBrush(Windows.UI.Colors.Green);
            record.Content = "RECORD";

            m_robot.Roll(0, 0);
            m_robot.SetRGBLED(0, 0, 0);
            child_crying.Stop();
            happy_music.Stop();
            angry_music.Stop();
            scared_music.Stop();

            if (Globals.recordon == true)
            {
                return;
            }
            if (Globals.recordon == false)                
            {
                Globals.recordon = true;
                Globals.record_index = 0;
            }
            Globals.act_state = "recording";
        }
        public async void playback_Click(object sender, RoutedEventArgs e)
        {
            Globals.act_state = "playback";
            Globals.recordon = false;
            record.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
            record.Content = "Record";
            playback.Background = new SolidColorBrush(Windows.UI.Colors.Green);
            playback.Content = "PLAYBACK";
            m_robot.Roll(0, 0);
            m_robot.SetRGBLED(0, 0, 0);
            child_crying.Stop();
            happy_music.Stop();
            angry_music.Stop();
            scared_music.Stop();
            for (int x = 0; x < Globals.record_index; x++)
            {
                switch (Globals.recorded[x,0])
                {
                    case "color":
                        if (Globals.recorded[x, 1]=="red") m_robot.SetRGBLED(255, 0, 0);
                        if (Globals.recorded[x, 1] == "blue") m_robot.SetRGBLED(0, 0, 255);
                        if (Globals.recorded[x, 1] == "green") m_robot.SetRGBLED(0, 255, 0);
                        break;
                    case "move":
                        if (Globals.recorded[x, 1] == "left") topleft_Click(sender, e);
                        if (Globals.recorded[x, 1] == "right") topright_Click(sender, e);
                        if (Globals.recorded[x, 1] == "bottom") bottomleft_Click(sender, e);
                        if (Globals.recorded[x, 1] == "top") bottomright_Click(sender, e);
                        if (Globals.recorded[x, 1] == "vibrate") vibrate_Click(sender, e);
                        if (Globals.recorded[x, 1] == "smallcircle") smallcircle_Click(sender, e);
                        if (Globals.recorded[x, 1] == "bigcircle") bigcircle_Click(sender, e);
                        break;
                    case "sound":
                        if (Globals.recorded[x, 1] == "happy") await Happy(); //happy_music.Play();
                        if (Globals.recorded[x, 1] == "sad") cry1.Play();
                        if (Globals.recorded[x, 1] == "angry") await Angry(); //angry_music.Play();
                        if (Globals.recorded[x, 1] == "scared") await Scared(); //scared_music.Play();
                        if (Globals.recorded[x, 1] == "cheer") cheering.Play();
                        break;
                }
                await DelayMyTask(2500);
                m_robot.Roll(0, 0);
                m_robot.SetRGBLED(0, 0, 0);
                child_crying.Stop();
                happy_music.Stop();
                angry_music.Stop();
                scared_music.Stop();
                await DelayMyTask(1500);
            }
            playback.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
            playback.Content = "Playback";
            cheering.Play();
            Globals.act_state = "none";
        }
    }
}