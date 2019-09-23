using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Interop;

namespace DataLayer
{
    [Activity(Label = "SettingsActivity")]
    public class SettingsActivity : Activity
    {
        /// <summary>
        /// Initiates the activity
        /// </summary>
        /// <param name="savedInstanceState"></param>
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.settings_activity);
            HeartDebugHandler.debugLog("Settings Launched");
            // Create your application here
        }

        /// <summary>
        /// Deletes all the data files
        /// </summary>
        /// <param name="view"></param>
        [Export("onPurgeAllData")]
        public void onPurgeAllData(View view)
        {
            HeartDebugHandler.debugLog("Purge all data clicked!");
            HeartFileHandler.DeleteAllData();
        }

        /// <summary>
        /// Deletes the heart beat data
        /// </summary>
        /// <param name="view"></param>
        [Export("onPurgeHeartBeatData")]
        public void onPurgeHeartBeatData(View view)
        {
            HeartDebugHandler.debugLog("Purge Heart Beat data clicked!");
            HeartFileHandler.DeleteHeartBeatData();
        }

        /// <summary>
        /// Deletes the heart rate data
        /// </summary>
        /// <param name="view"></param>
        [Export("onPurgeHeartRateData")]
        public void onPurgeHeartRateData(View view)
        {
            HeartDebugHandler.debugLog("Purge Heart Rate data clicked!");
            HeartFileHandler.DeleteHeartRateData();
        }

        /// <summary>
        /// Deletes the steps data
        /// </summary>
        /// <param name="view"></param>
        [Export("onPurgeStepsData")]
        public void onPurgeStepsData(View view)
        {
            HeartDebugHandler.debugLog("Purge Steps data clicked!");
            HeartFileHandler.DeleteStepsData();
        }

        //TODO: should probably make use of the native back functionality instead of restarting the main activity again, could lead to quite a large stack.
        /// <summary>
        /// Starts the main activity screen
        /// </summary>
        /// <param name="view"></param>
        [Export("onBackClicked")]
        public void onBackClicked(View view)
        {
            Intent intent = new Intent(this, typeof(MainActivity));
            StartActivity(intent);
        }


    }
}