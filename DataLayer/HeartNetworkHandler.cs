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
    class HeartNetworkHandler
    {
        //https://heartanalyzer.azurewebsites.net/
        public static void sendPostRequest(Context context, string dataString, TextView codeView)
        {
            using (var client = new WebClient())
            {
                var values = new NameValueCollection();
                values.Add("hdatatype", "StepCount");
                values.Add("hupdated", "2019-09-05T08:58:57.5367850+02:00");
                //values.Add("hdatapoints", "[{\"dateTime\": \"2019-09-05T08:58:57.5367850+02:00\",\"value\": 10},{\"dateTime\": \"2019-09-05T13:34:37.7470520+02:00\",\"value\": 11},{\"dateTime\": \"2019-09-05T13:35:37.7470520+02:00\",\"value\": 4}]");
                values.Add("hdatapoints", dataString);
                client.Headers[HttpRequestHeader.Accept] = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                string url = "https://heartanalyzer.azurewebsites.net/";

                //byte[] result = client.UploadValues(url, values);

                client.UploadValuesCompleted += (sender, e) =>
                {
                    HeartDebugHandler.debugLog("Result received");
                    if (e != null)
                    {
                        try
                        {
                            var crash = e.Result;
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

                            //TOAST HERE
                            if (resultText.Length == 5)
                            {
                                Toast.MakeText(context, "Code: " + resultText, ToastLength.Long).Show();
                                codeView.Text = resultText;
                                codeView.Visibility = ViewStates.Visible;
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
                        HeartDebugHandler.debugLog("e was null");
                    }

                };
                HeartDebugHandler.debugLog("Sending data");
                client.UploadValuesAsync(new Uri(url),"POST", values);
                
                //Toast.MakeText(context, Encoding.UTF8.GetString(result), ToastLength.Long);
            }
        }
        //sendPostRequestAlternate
        public static async Task<string> sendPostRequestAlternate()
        {
            
            //RequestBody formBody = new FormBody.Builder()
            //    .Add("hdatatype", "StepCount")
            //    .Add("hupdated", "2019-09-05T08:58:57.5367850+02:00")
            //    .Add("hdatapoints", "[{\"dateTime\": \"2019-09-05T08:58:57.5367850+02:00\",\r\n\"value\": 10},{\"dateTime\": \"2019-09-05T13:34:37.7470520+02:00\",\r\n\"value\": 11},{\"dateTime\": \"2019-09-05T13:35:37.7470520+02:00\",\r\n\"value\": 4}]")
            //    .Build();

            //OkHttpClient client = new OkHttpClient();
            //Request request = new Request.Builder()
            //    .Url("https://heartanalyzer.azurewebsites.net/")
            //    .Post(formBody)
            //    .Build();
            
            //Response response = client.NewCall(request).Execute();
            //return response.Body().String();
            return await SendPostRequestTask("");
        }


        private static async Task<string> SendPostRequestTask(string data)
        {
            RequestBody formBody = new FormBody.Builder()
                .Add("hdatatype", "StepCountTest")
                .Add("hupdated", "2019-09-05T08:58:57.5367850+02:00")
                .Add("hdatapoints", "[{\"dateTime\": \"2019-09-05T08:58:57.5367850+02:00\",\r\n\"value\": 10},{\"dateTime\": \"2019-09-05T13:34:37.7470520+02:00\",\r\n\"value\": 11},{\"dateTime\": \"2019-09-05T13:35:37.7470520+02:00\",\r\n\"value\": 4}]")
                .Build();

            try
            {

                //OkHttpClient client = new OkHttpClient();
                OkHttpClient client = new OkHttpClient.Builder()
                    .ConnectTimeout(30, TimeUnit.Seconds)
                    .WriteTimeout(30, TimeUnit.Seconds)
                    .ReadTimeout(30, TimeUnit.Seconds)
                    .Build();

                Request request = new Request.Builder()
                    .Url("https://heartanalyzer.azurewebsites.net/")
                    .Post(formBody)
                    .Build();
                //Response response = client.NewCall(request).Execute();
                Response response = await client.NewCall(request).ExecuteAsync();

                return response.Body().String();

            }
            catch (Java.Net.SocketTimeoutException e)
            {
                HeartDebugHandler.debugLog("Java.Net.SocketTimeoutException thrown");
                //throw;
                return "Java.Net.SocketTimeoutException thrown";
            }
        }


    }



}

