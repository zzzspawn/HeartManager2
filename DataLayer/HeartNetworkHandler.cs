using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang.Reflect;
using Java.Util.Concurrent;
using RestSharp;
using Square.OkHttp3;
using System.Net.Http;

namespace DataLayer
{
    /// <summary>
    /// this class takes care of sending data to the web server
    /// </summary>
    class HeartPostSender
    {
        private bool stepsReceived;
        private string stepsResult;
        private bool hrReceived;
        private string hrResult;
        private bool hbReceived;
        private string hbResult;
        private Context context;
        private TextView codeView;
        readonly string url = "https://heartanalyzer.azurewebsites.net/";
        private string stepsToken = "steps";
        private string heartRateToken = "heartRate";
        private string heartbeatToken = "heartbeat";
        private string stepsDataString;
        private string heartRateDataString;
        private string heartBeatDataString;

        /// <summary>
        /// Constructor for the data sending manager
        /// </summary>
        /// <param name="context">The application cotext, needed for toasts</param>
        /// <param name="codeView">The TextView you want the feedback to appear in</param>
        /// <param name="stepsDataString">Stepsdata json string</param>
        /// <param name="heartRateDataString">Heart Rate json string</param>
        /// <param name="heartBeatDataString">Heart beat json string</param>
        public HeartPostSender(Context context, TextView codeView, string stepsDataString, string heartRateDataString, string heartBeatDataString)
        {
            this.stepsReceived = false;
            this.stepsResult = null;
            this.hrReceived = false;
            this.hrResult = null;
            this.hbReceived = false;
            this.hbResult = null;
            this.stepsDataString = stepsDataString;
            this.heartRateDataString = heartRateDataString;
            this.heartBeatDataString = heartBeatDataString;
            this.context = context;
            this.codeView = codeView;
        }

        /// <summary>
        /// test method for sending just the stepdata, good for easy testing
        /// </summary>
        public void SendData()
        {
            using (var stepsClient = new WebClient())
            {
                var stepValues = new NameValueCollection();
                stepValues.Add("hdatatype", "StepCount");
                stepValues.Add("hupdated", "2019-09-05T08:58:57.5367850+02:00"); //replace with actual date
                stepValues.Add("hdatapoints", stepsDataString);
                stepsClient.Headers[HttpRequestHeader.Accept] =
                    "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

                stepsClient.UploadValuesCompleted += dataUploadedCallback;
                stepsClient.UploadValuesAsync(new Uri(url), "POST", stepValues, stepsToken);
            }
        }

        /// <summary>
        /// the callback that happens when any data has been sent, so going through event args to see what, and generating a response display to client based on the result of that data upload.
        /// then it starts the upload for the next type, and when all uploads have completed(as in connection has been terminated) it finishes and updates the UI.
        /// so sort of recursive
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void dataUploadedCallback(object sender, UploadValuesCompletedEventArgs e)
        {
            if (e != null)
            {
                try
                {
                    var crasha = e.Result;
                    HeartDebugHandler.debugLog("Crasha ikke");
                }
                catch (System.Reflection.TargetInvocationException exception)
                {

                    var crash = exception.InnerException;
                    if (crash != null)
                    {
                        HeartDebugHandler.debugLog(crash.ToString());
                    }

                    //HeartDebugHandler.debugLog("halt");
                }

                HeartDebugHandler.debugLog("e was not null");
                if (e.Result != null)
                {
                    HeartDebugHandler.debugLog("e.Result was not null");
                    byte[] result = e.Result;
                    string resultText = Encoding.UTF8.GetString(result);
                    HeartDebugHandler.debugLog(resultText);

                    if (e.UserState.ToString().Equals(stepsToken))
                    {
                        stepsReceived = true;
                    }else if (e.UserState.ToString().Equals(heartRateToken))
                    {
                        hrReceived = true;
                    }else if (e.UserState.ToString().Equals(heartbeatToken))
                    {
                        hbReceived = true;
                    }



                    //TOAST HERE
                    if (resultText.Length == 5)
                    {
                        //Toast.MakeText(context, "Code: " + resultText, ToastLength.Long).Show();
                        //codeView.Text = resultText;
                        //codeView.Visibility = ViewStates.Visible;

                        if (e.UserState.ToString().Equals(stepsToken))
                        {
                            stepsResult = resultText;
                        }
                        else if (e.UserState.ToString().Equals(heartRateToken))
                        {
                            hrResult = resultText;
                        }
                        else if (e.UserState.ToString().Equals(heartbeatToken))
                        {
                            hbResult = resultText;
                        }

                    }
                    else
                    {
                        Toast.MakeText(context, "Upload failed, response: " + resultText, ToastLength.Long).Show();
                        codeView.Text = "Upload Failed..";
                        codeView.Visibility = ViewStates.Visible;
                    }

                }
                else
                {
                    HeartDebugHandler.debugLog("e.Result was null");
                    HeartDebugHandler.debugLog("e.Error: " + e.Error);

                }
            }
            else
            {
                HeartDebugHandler.debugLog("e was null, skipping the rest");
                stepsReceived = true;
                hbReceived = true;
                hrReceived = true;
            }


            if (stepsReceived && !hbReceived)
            {
                using (var heartBeatClient = new WebClient())
                {

                    var heartBeatValues = new NameValueCollection();
                    heartBeatValues.Add("hdatatype", "Heartbeat");
                    heartBeatValues.Add("hupdated", "2019-09-05T08:58:57.5367850+02:00"); //replace with actual date
                    heartBeatValues.Add("hdatapoints", heartBeatDataString);
                    heartBeatClient.Headers[HttpRequestHeader.Accept] =
                        "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";


                    heartBeatClient.UploadValuesCompleted += dataUploadedCallback;
                    heartBeatClient.UploadValuesAsync(new Uri(url), "POST", heartBeatValues, heartbeatToken);

                }
            }

            if (hbReceived && stepsReceived && !hrReceived)
            {
                using (var heartRateClient = new WebClient())
                {

                    var heartRateValues = new NameValueCollection();
                    heartRateValues.Add("hdatatype", "HeartRate");
                    heartRateValues.Add("hupdated", "2019-09-05T08:58:57.5367850+02:00"); //replace with actual date
                    heartRateValues.Add("hdatapoints", heartRateDataString);
                    heartRateClient.Headers[HttpRequestHeader.Accept] =
                        "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                    heartRateClient.UploadValuesCompleted += dataUploadedCallback;
                    heartRateClient.UploadValuesAsync(new Uri(url), "POST", heartRateValues, heartRateToken);
                }
            }

            if (stepsReceived && hbReceived && hrReceived)
            {
                updateUIWithData();
            }

        }
        /// <summary>
        /// grabs the different results created in the upload callbacks and then displays the final tally to the client/user
        /// </summary>
        public void updateUIWithData()
        {
            string endString = "Codes: " + "\n";
            if (stepsResult != null && stepsResult.Length == 5)
            {
                endString += "Steps: " + stepsResult + "\n";
            }
            else
            {
                endString += "Steps: " + "Failed/No data" + "\n";
            }
            if (hrResult != null && hrResult.Length == 5)
            {
                endString += "HRate: " + hrResult + "\n";
            }
            else
            {
                endString += "HRate: " + "Failed/No data" + "\n";
            }
            if (hbResult != null && hbResult.Length == 5)
            {
                endString += "HBeat: " + hbResult;
            }
            else
            {
                endString += "HBeat: " + "Failed/No data" + "\n";
            }

            codeView.Text = endString;
            codeView.Visibility = ViewStates.Visible;

        }
    }

}

