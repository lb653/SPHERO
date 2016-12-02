using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace BasicDriveApp
{
    public static class Globals
    {
        public static bool successful_end = true;
        public static bool act1on = false;
        public static bool act2on = false;
        public static bool act3on = false;
        public static bool act4on = false;
        public static bool angryon = false;
        public static bool happyon = false;
        public static bool scaredon = false;
        public static bool sadon = false;
        public static bool recordon = false;
        public static bool playbackon = false;
        public static bool stopon = false;
        public static bool comfort = false;
        public static bool toprighton = false;
        public static bool toplefton = false;
        public static bool bottomrighton = false;
        public static bool bottomlefton = false;
        public static bool centeron = false;
        public static bool blueon = false;
        public static bool greenon = false;
        public static bool redon = false;
        public static bool colorsoff = true;
        public static bool vibrateon = false;
        public static bool smallcircleon = false;
        public static bool bigcircleon = false;
        public static bool collisionOccurred = false;
        public static bool fileIOsetup = false;


        // Directional attributes
        public static float accel_x = 0;
        public static float accel_y = 0;
        public static float accel_z = 0;
        public static float gyro_x = 0;
        public static float gyro_y = 0;
        public static float gyro_z = 0;
        public static float speed = 0.1f;
        public static int heading = 0;
        public static int lastdirection = 0;
        public static int record_index=0;

        // Color attributes
        public static int color_R = 255;
        public static int color_G = 100;
        public static int color_B = 0;

        // Timers for all phases
        public static int startingSecs;
        public static int start_phaseTime;
        public static int end_phaseTime;

        public static bool firsttime = true;
        public static bool keyPressed = false;

        public static TimeSpan gyro_time;
        public static Stopwatch gyro_stopWatch = new Stopwatch();
        public static bool isTiming = false;
        public static bool isTimingAct4 = false;
        public static int writefileon = 0;

        public static int hurtnum = 0;

        public static String output;
        public static String act_state = "none";
        public static String sound_state = "none";
        public static String color_state = "none";
        public static String emotion_state = "none";
        public static String [,] recorded = new String[10,2];

    }
}
