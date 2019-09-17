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
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.settings_activity);
            HeartDebugHandler.debugLog("Settings Launched");
            // Create your application here
        }

        /// <summary>
        /// Sends an RPC to start a fullscreen Activity on the wearable
        /// </summary>
        /// <param name="view"></param>
        [Export("onPurgeAllData")]
        public void onPurgeAllData(View view)
        {
            HeartDebugHandler.debugLog("Purge all data clicked!");
            HeartFileHandler.DeleteAllData();
        }

        /// <summary>
        /// Sends an RPC to start a fullscreen Activity on the wearable
        /// </summary>
        /// <param name="view"></param>
        [Export("onPurgeHeartBeatData")]
        public void onPurgeHeartBeatData(View view)
        {
            HeartDebugHandler.debugLog("Purge Heart Beat data clicked!");
            HeartFileHandler.DeleteHeartBeatData();
        }

        /// <summary>
        /// Sends an RPC to start a fullscreen Activity on the wearable
        /// </summary>
        /// <param name="view"></param>
        [Export("onPurgeHeartRateData")]
        public void onPurgeHeartRateData(View view)
        {
            HeartDebugHandler.debugLog("Purge Heart Rate data clicked!");
            HeartFileHandler.DeleteHeartRateData();
        }

        /// <summary>
        /// Sends an RPC to start a fullscreen Activity on the wearable
        /// </summary>
        /// <param name="view"></param>
        [Export("onPurgeStepsData")]
        public void onPurgeStepsData(View view)
        {
            HeartDebugHandler.debugLog("Purge Steps data clicked!");
            HeartFileHandler.DeleteStepsData();
        }

        /// <summary>
        /// Sends an RPC to start a fullscreen Activity on the wearable
        /// </summary>
        /// <param name="view"></param>
        [Export("onBackClicked")]
        public void onBackClicked(View view)
        {
            Intent intent = new Intent(this, typeof(MainActivity));
            StartActivity(intent);
        }

        /// <summary>
        /// Sends an RPC to start a fullscreen Activity on the wearable
        /// </summary>
        /// <param name="view"></param>
        [Export("onTestPostRequest")]
        public async void onTestPostRequest(View view)
        {
            TextView codeView = FindViewById<TextView>(Resource.Id.CodeTextViewCode);
            codeView.Visibility = ViewStates.Invisible;

            HeartDebugHandler.debugLog("Getting json string");
            string jsonString = await HeartFileHandler.getJSONString();

            //HeartDebugHandler.debugLog(jsonString.Substring(jsonString.Length-30));
            HeartDebugHandler.debugLog("String got, length: " + jsonString.Length);

            HeartDebugHandler.debugLog("Sending data");

            //HeartNetworkHandler.sendPostRequest(this, jsonString, codeView);

            // -- var response = await HeartNetworkHandler.sendPostRequest();
            // -- HeartDebugHandler.debugLog("Java.Net.SocketTimeoutException thrown");
            // -- Toast.MakeText(this, response, ToastLength.Long);
        }


    }
}