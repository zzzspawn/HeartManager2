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

    //class HeartNetworkHandler
    //{
    //    //https://heartanalyzer.azurewebsites.net/
    //    public static void sendPostRequest(Context context, string dataString, TextView codeView)
    //    {
    //        using (var client = new WebClient())
    //        {
    //            var values = new NameValueCollection();
    //            values.Add("hdatatype", "StepCount");
    //            values.Add("hupdated", "2019-09-05T08:58:57.5367850+02:00");
    //            //values.Add("hdatapoints", "[{\"dateTime\": \"2019-09-05T08:58:57.5367850+02:00\",\"value\": 10},{\"dateTime\": \"2019-09-05T13:34:37.7470520+02:00\",\"value\": 11},{\"dateTime\": \"2019-09-05T13:35:37.7470520+02:00\",\"value\": 4}]");
    //            values.Add("hdatapoints", dataString);
    //            client.Headers[HttpRequestHeader.Accept] = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
    //            string url = "https://heartanalyzer.azurewebsites.net/";

    //            //byte[] result = client.UploadValues(url, values);

    //            client.UploadValuesCompleted += (sender, e) =>
    //            {
    //                HeartDebugHandler.debugLog("Result received");
    //                if (e != null)
    //                {
    //                    try
    //                    {
    //                        var crash = e.Result;
    //                    }
    //                    catch (System.Reflection.TargetInvocationException exception)
    //                    {
                            
    //                        var crash = exception.InnerException;
    //                        if (crash != null)
    //                        {
    //                            HeartDebugHandler.debugLog(crash.ToString());
    //                        }
    //                        //HeartDebugHandler.debugLog("halt");
    //                    }

    //                    HeartDebugHandler.debugLog("e was not null");
    //                    if (e.Result != null)
    //                    {
    //                        HeartDebugHandler.debugLog("e.Result was not null");
    //                        byte[] result = e.Result;
    //                        string resultText = Encoding.UTF8.GetString(result);
    //                        HeartDebugHandler.debugLog(resultText);

    //                        //TOAST HERE
    //                        if (resultText.Length == 5)
    //                        {
    //                            Toast.MakeText(context, "Code: " + resultText, ToastLength.Long).Show();
    //                            codeView.Text = resultText;
    //                            codeView.Visibility = ViewStates.Visible;
    //                        }
    //                        else
    //                        {
    //                            Toast.MakeText(context, "Upload failed, response: " + resultText, ToastLength.Long).Show();
    //                            codeView.Text = "Upload Failed..";
    //                            codeView.Visibility = ViewStates.Visible;
    //                        }
                            
    //                    }
    //                    else
    //                    {
    //                        HeartDebugHandler.debugLog("e.Result was null");
    //                        HeartDebugHandler.debugLog("e.Error: " + e.Error);

    //                    }
    //                }
    //                else
    //                {
    //                    HeartDebugHandler.debugLog("e was null");
    //                }

    //            };
    //            HeartDebugHandler.debugLog("Sending data");
    //            client.UploadValuesAsync(new Uri(url),"POST", values);
                
    //            //Toast.MakeText(context, Encoding.UTF8.GetString(result), ToastLength.Long);
    //        }
    //    }
    //    //sendPostRequestAlternate
        
    //    //public static void sendPostRequest(Context context, string stepsDataString, string hearRateDataString, string heartBeatDataString, TextView codeView)
    //    //{
    //    //    bool stepsReceived = false;
    //    //    string stepsResult = null;
    //    //    bool hrReceived = false;
    //    //    string hrResult = null;
    //    //    bool hbReceived = false;
    //    //    string hbResult = null;
    //    //    //push stepsDataString
    //    //    using (var stepsClient = new WebClient())
    //    //    {
    //    //        //values.Add("hdatapoints", "[{\"dateTime\": \"2019-09-05T08:58:57.5367850+02:00\",\"value\": 10},{\"dateTime\": \"2019-09-05T13:34:37.7470520+02:00\",\"value\": 11},{\"dateTime\": \"2019-09-05T13:35:37.7470520+02:00\",\"value\": 4}]");

    //    //        var stepsValues = new NameValueCollection();
    //    //        stepsValues.Add("hdatatype", "StepCount");
    //    //        stepsValues.Add("hupdated", "2019-09-05T08:58:57.5367850+02:00");
    //    //        stepsValues.Add("hdatapoints", stepsDataString);

    //    //        var heartBeatValues = new NameValueCollection();
    //    //        heartBeatValues.Add("hdatatype", "HeartRate");
    //    //        heartBeatValues.Add("hupdated", "2019-09-05T08:58:57.5367850+02:00");
    //    //        heartBeatValues.Add("hdatapoints", stepsDataString);

    //    //        var heartRateValues = new NameValueCollection();
    //    //        heartRateValues.Add("hdatatype", "Heartbeat");
    //    //        heartRateValues.Add("hupdated", "2019-09-05T08:58:57.5367850+02:00");
    //    //        heartRateValues.Add("hdatapoints", stepsDataString);

    //    //        stepsClient.Headers[HttpRequestHeader.Accept] = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
    //    //        string url = "https://heartanalyzer.azurewebsites.net/";

    //    //        /*
    //    //         stepsClient.UploadValuesCompleted += (sender, e) =>
    //    //        {
    //    //            HeartDebugHandler.debugLog("Result received");
    //    //            if (e != null)
    //    //            {
    //    //                try
    //    //                {
    //    //                    var crash = e.Result;
    //    //                }
    //    //                catch (System.Reflection.TargetInvocationException exception)
    //    //                {

    //    //                    var crash = exception.InnerException;
    //    //                    if (crash != null)
    //    //                    {
    //    //                        HeartDebugHandler.debugLog(crash.ToString());
    //    //                    }
    //    //                    //HeartDebugHandler.debugLog("halt");
    //    //                }

    //    //                HeartDebugHandler.debugLog("e was not null");
    //    //                if (e.Result != null)
    //    //                {
    //    //                    HeartDebugHandler.debugLog("e.Result was not null");
    //    //                    byte[] result = e.Result;
    //    //                    string resultText = Encoding.UTF8.GetString(result);
    //    //                    HeartDebugHandler.debugLog(resultText);


    //    //                    if (e.UserState.ToString().Equals("steps"))
    //    //                    {
    //    //                        stepsReceived = true;
    //    //                        using (var heartBeatClient = new WebClient())
    //    //                        {
                                    



    //    //                        }
    //    //                    }
    //    //                    else if (e.UserState.ToString().Equals("heartbeat"))
    //    //                    {
    //    //                        hbReceived = true;
    //    //                    }
    //    //                    else if (e.UserState.ToString().Equals("heartrate"))
    //    //                    {
    //    //                        hrReceived = true;
    //    //                    }


    //    //                    //TOAST HERE
    //    //                    if (resultText.Length == 5)
    //    //                    {
    //    //                        //Toast.MakeText(context, "Code: " + resultText, ToastLength.Long).Show();
    //    //                        //codeView.Text = resultText;
    //    //                        //codeView.Visibility = ViewStates.Visible;

    //    //                        if (e.UserState.ToString().Equals("steps"))
    //    //                        {
    //    //                            //stepsReceived = true;
    //    //                            stepsResult = resultText;
    //    //                        }else if (e.UserState.ToString().Equals("heartbeat"))
    //    //                        {
    //    //                            //hbReceived = true;
    //    //                            hbResult = resultText;
    //    //                        }
    //    //                        else if (e.UserState.ToString().Equals("heartrate"))
    //    //                        {
    //    //                            //hrReceived = true;
    //    //                            hrResult = resultText;
    //    //                        }
    //    //                        else
    //    //                        {
    //    //                            Toast.MakeText(context, "No match: " + e.UserState.ToString(), ToastLength.Long).Show();
    //    //                        }
    //    //                    }
    //    //                    else
    //    //                    {
    //    //                        Toast.MakeText(context, "Upload failed, response: " + resultText, ToastLength.Long).Show();
    //    //                        //codeView.Text = "Upload Failed..";
    //    //                        //codeView.Visibility = ViewStates.Visible;
    //    //                    }

    //    //                    if (stepsReceived && hbReceived && hrReceived)
    //    //                    {
    //    //                        updateUIWithData(stepsResult, hbResult, hrResult, codeView);
    //    //                    }

    //    //                }
    //    //                else
    //    //                {
    //    //                    HeartDebugHandler.debugLog("e.Result was null");
    //    //                    HeartDebugHandler.debugLog("e.Error: " + e.Error);
    //    //                }
    //    //            }
    //    //            else
    //    //            {
    //    //                HeartDebugHandler.debugLog("e was null");
    //    //            }

    //    //        };
    //    //        */
    //    //        stepsClient.UploadValuesCompleted += SendData;
                
    //    //        HeartDebugHandler.debugLog("Sending data");
    //    //        stepsClient.UploadValuesAsync(new Uri(url), "POST", stepsValues, "steps");
    //    //        stepsClient.UploadValuesAsync(new Uri(url), "POST", heartBeatValues, "heartbeat");
    //    //        stepsClient.UploadValuesAsync(new Uri(url), "POST", heartRateValues, "heartRate");
                
    //    //        //Toast.MakeText(context, Encoding.UTF8.GetString(result), ToastLength.Long);
    //    //    }
    //    //}

    //    public static void updateUIWithData(string stepsCode, string hbCode, string hrCode, TextView codeView)
    //    {
    //        string endString = "Codes: " + "\n";
    //        if (stepsCode != null && stepsCode.Length == 5)
    //        {
    //            endString += "Steps: " + stepsCode + "\n";
    //        }
    //        else
    //        {
    //            endString += "Steps: " + "Failed/No data" + "\n";
    //        }
    //        if (hrCode != null && hrCode.Length == 5)
    //        {
    //            endString += "HRate: " + hrCode + "\n";
    //        }
    //        else
    //        {
    //            endString += "HRate: " + "Failed/No data" + "\n";
    //        }
    //        if (hbCode != null && hbCode.Length == 5)
    //        {
    //            endString += "HBeat: " + hbCode;
    //        }
    //        else
    //        {
    //            endString += "HBeat: " + "Failed/No data" + "\n";
    //        }

    //        codeView.Text = "Upload Failed..";
    //        codeView.Visibility = ViewStates.Visible;

    //    }

    //    public static async Task<string> sendPostRequestAlternate()
    //    {
            
    //        //RequestBody formBody = new FormBody.Builder()
    //        //    .Add("hdatatype", "StepCount")
    //        //    .Add("hupdated", "2019-09-05T08:58:57.5367850+02:00")
    //        //    .Add("hdatapoints", "[{\"dateTime\": \"2019-09-05T08:58:57.5367850+02:00\",\r\n\"value\": 10},{\"dateTime\": \"2019-09-05T13:34:37.7470520+02:00\",\r\n\"value\": 11},{\"dateTime\": \"2019-09-05T13:35:37.7470520+02:00\",\r\n\"value\": 4}]")
    //        //    .Build();

    //        //OkHttpClient client = new OkHttpClient();
    //        //Request request = new Request.Builder()
    //        //    .Url("https://heartanalyzer.azurewebsites.net/")
    //        //    .Post(formBody)
    //        //    .Build();
            
    //        //Response response = client.NewCall(request).Execute();
    //        //return response.Body().String();
    //        return await SendPostRequestTask("");
    //    }


    //    private static async Task<string> SendPostRequestTask(string data)
    //    {
    //        RequestBody formBody = new FormBody.Builder()
    //            .Add("hdatatype", "StepCountTest")
    //            .Add("hupdated", "2019-09-05T08:58:57.5367850+02:00")
    //            .Add("hdatapoints", "[{\"dateTime\": \"2019-09-05T08:58:57.5367850+02:00\",\r\n\"value\": 10},{\"dateTime\": \"2019-09-05T13:34:37.7470520+02:00\",\r\n\"value\": 11},{\"dateTime\": \"2019-09-05T13:35:37.7470520+02:00\",\r\n\"value\": 4}]")
    //            .Build();

    //        try
    //        {

    //            //OkHttpClient client = new OkHttpClient();
    //            OkHttpClient client = new OkHttpClient.Builder()
    //                .ConnectTimeout(30, TimeUnit.Seconds)
    //                .WriteTimeout(30, TimeUnit.Seconds)
    //                .ReadTimeout(30, TimeUnit.Seconds)
    //                .Build();

    //            Request request = new Request.Builder()
    //                .Url("https://heartanalyzer.azurewebsites.net/")
    //                .Post(formBody)
    //                .Build();
    //            //Response response = client.NewCall(request).Execute();
    //            Response response = await client.NewCall(request).ExecuteAsync();

    //            return response.Body().String();

    //        }
    //        catch (Java.Net.SocketTimeoutException e)
    //        {
    //            HeartDebugHandler.debugLog("Java.Net.SocketTimeoutException thrown");
    //            //throw;
    //            return "Java.Net.SocketTimeoutException thrown";
    //        }
    //    }


    //}

    

}

