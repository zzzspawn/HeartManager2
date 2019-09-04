using System;

using Android.App;
using Android.Content;
//using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Gms.Common.Apis;
using Android.Graphics;
using Java.Util.Concurrent;
using Android.Gms.Wearable;
using System.Collections.Generic;
using System.Globalization;
using Android.Gms.Common.Data;
using Java.Interop;
using Android.Provider;
using Java.IO;
using System.IO;
using System.Linq;
using System.Text;
using Android.Util;
using Android.Content.PM;
using System.Threading.Tasks;
using Java.Sql;
using Environment = System.Environment;
using File = Java.IO.File;

namespace DataLayer
{
    /// <summary>
    /// Receives its own events using a listener API designed for foreground activities. Updates a data item every second while it is open.
    /// Also allows user to take a photo and send that as an asset to the paired wearable.
    /// </summary>
    [Activity(MainLauncher = true, Label = "@string/app_name", LaunchMode = LaunchMode.SingleTask,
        Icon = "@drawable/ic_launcher")]
    public class MainActivity : Activity, IDataApiDataListener, IMessageApiMessageListener, INodeApiNodeListener,
        GoogleApiClient.IConnectionCallbacks, GoogleApiClient.IOnConnectionFailedListener
    {
        const string Tag = "MainActivity";

        /// <summary>
        /// Request code for launching the Intent to resolve Google Play services errors
        /// </summary>
        const int RequestResolveError = 1000;

        const string StartActivityPath = "/start-activity";
        const string CountPath = "/count";
        const string CountKey = "count";
        const string FunMessagePath = "/fun-message";
        const string DataPointPath = "/data-point";
        const string DataPointsPath = "/data-points";
        const string TestDataPath = "/data-test";
        private const string filenameSteps = "stepsdata.json";
        private const int maxConnectionAttempts = 10;
        private int connectionAttempts;
        GoogleApiClient mGoogleApiClient;
        //bool mResolvingError = false;

        View startActivityBtn;

        //View SendFunMessageBtn;
        private TextView statusTextView;
        private TextView connectionStatusTextView;

        private Handler handler;

        private List<HeartDataPoint> hdata_Rate;
        private List<HeartDataPoint> hdata_Beat;
        private List<HeartDataPoint> hdata_Steps;


        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            handler = new Handler();
            LOGD(Tag, "OnCreate");
            connectionAttempts = 0;
            SetContentView(Resource.Layout.main_activity);
            debugLog("App Launched");
            SetupViews();
            SetupLists();

            mGoogleApiClient = new GoogleApiClient.Builder(this)
                .AddApi(WearableClass.API)
                .AddConnectionCallbacks(this)
                .AddOnConnectionFailedListener(this)
                .Build();
        }


        //Side project start

        




        //Side project end





        private async void saveStepData()
        {

            List<HeartDataPoint> existingData;
            existingData = await ReadDataPointsTask(filenameSteps);

            while (hdata_Steps.Count > 0)
            {
                HeartDataPoint element = hdata_Steps[0];
                existingData.Add(element);
                hdata_Steps.RemoveAt(0);
                
            }

            await storeDataPointsTask(existingData, filenameSteps);

        }

        private async Task storeDataPointsTask(List<HeartDataPoint> dataPoints, string fileName)
        {
            if (dataPoints != null && dataPoints.Count > 0)
            {

                //needed values
                HeartDataType dataType = dataPoints[0].heartType;
                DateTime currentTime = DateTime.Now;


                string filePath = System.Environment.GetFolderPath(Environment.SpecialFolder.Personal);

                string backingFile = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), fileName);

                File file = new File(backingFile);

                if (file.Exists())
                {
                    file.Delete();
                }


                using (var writer = System.IO.File.CreateText(backingFile))
                {
                    //await writer.WriteLineAsync(count.ToString());
                    var indentOne = "\t";
                    var indentTwo = "\t\t";
                    var indentThree = "\t\t\t";
                    await writer.WriteLineAsync("{");
                    await writer.WriteLineAsync(indentOne + "\"updated\": \"" + currentTime.ToString("O") + "\",");
                    await writer.WriteLineAsync(indentOne + "\"dataType\": \"" + dataType.ToString("G") + "\",");

                    await writer.WriteLineAsync(indentOne + "\"data\": [");

                    for (int i = 0; i < dataPoints.Count; i++)
                    {
                        HeartDataPoint point = dataPoints[i];
                        await writer.WriteLineAsync(indentTwo + "{");
                        await writer.WriteLineAsync(indentThree + "\"DateTime\": " + "\"" + point.timestamp.ToString("O") + "\"");
                        await writer.WriteLineAsync(indentThree + "\"Value\": " + "\"" + point.amount + "\"");
                        string lastLine = "}";
                        if (i < dataPoints.Count - 1)
                        {
                            lastLine += ",";
                        }
                        await writer.WriteLineAsync(indentTwo + lastLine);
                    }
                    await writer.WriteLineAsync(indentOne + "]");
                    await writer.WriteLineAsync("]");

                }
            }
        }


        private async Task<List<HeartDataPoint>> ReadDataPointsTask(string fileName)
        {
            var backingFile = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), fileName);

            if (backingFile == null || !System.IO.File.Exists(backingFile))
            {
                return null;
            }

            List<HeartDataPoint> dataPoints = new List<HeartDataPoint>();

            using (var reader = new StreamReader(backingFile, true))
            {
                string line;
                HeartDataType dataType = HeartDataType.None;
                DateTime? date = null;
                int value = -1;
                bool dataReached = false;
                bool param1Found = false;
                bool param2Found = false;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (!dataReached && line.Contains("dataType"))
                    {
                        string type = line.Split(":")[1]; ///smårisky, men ingen god måte å handle det på dersom det ikke stemmer.
                        type = type.Substring(1, type.Length);
                        type = type.Substring(0, type.Length-1);

                        dataType = (HeartDataType) Enum.Parse(typeof(HeartDataType), type); //might crash if no match exists
                    }else if (!dataReached && line.Contains("["))
                    {
                        dataReached = true;
                    }

                    if (dataReached)
                    {
                        if (line.Contains("DateTime"))
                        {
                            param1Found = true;
                            string dateString = line.Split(":")[1];
                            dateString = dateString.Substring(1, dateString.Length);
                            dateString = dateString.Substring(0, dateString.Length - 1);
                            date = DateTime.Parse(dateString, null, DateTimeStyles.RoundtripKind);
                        }

                        if (param1Found && line.Contains("Value"))
                        {
                            param2Found = true;
                            string valueString = line.Split(":")[1];
                            value = int.Parse(valueString);
                        }

                        if (param1Found && param2Found)
                        {
                            if (dataType != HeartDataType.None && value != -1 && date.HasValue)
                            {
                                dataPoints.Add(new HeartDataPoint(dataType, value, (DateTime) date));
                                dataType = HeartDataType.None;
                                value = -1;
                                date = null;
                                param1Found = false;
                                param2Found = false;
                            }
                            
                        }

                        if (line.Contains("]"))//implicit "&& dataReached"
                        {
                            dataReached = false;
                        }
                    }
                }
            }

            return dataPoints; //return actual value
        }


        private void debugLog(string text)
        {
            Log.Info("HH_TEST", text);
        }

        private void updateConnectionStatusString(string text)
        {
            RunOnUiThread(() =>
            {
                connectionStatusTextView.Text = text;
            });
        }
        private void updateStatusString(string text)
        {
            RunOnUiThread(() =>
            {
                statusTextView.Text = text;
            });
        }

        /// <summary>
        /// Sets up UI components and their callback handlers
        /// </summary>
        void SetupViews()
        {
            debugLog("Setting up views");
            startActivityBtn = FindViewById(Resource.Id.start_wearable_activity);
            statusTextView = (TextView)FindViewById(Resource.Id.statusText);
            connectionStatusTextView = (TextView)FindViewById(Resource.Id.connectionStatusText);
        }

        void SetupLists()
        {
            debugLog("Setting up Lists");
            hdata_Beat = new List<HeartDataPoint>();
            hdata_Rate = new List<HeartDataPoint>();
            hdata_Steps = new List<HeartDataPoint>();
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            debugLog("Activity results are in!");
            if (resultCode == Result.Ok)
            {
                Bundle extras = data.Extras;
            }
        }

        protected override void OnStart()
        {
            base.OnStart();
            debugLog("OnStart ran");

            if (!mGoogleApiClient.IsConnected)
            {
                mGoogleApiClient.Connect();
            }
            updateConnectionStatusString("Connecting");
                
            
        }

        protected override void OnResume()
        {
            base.OnResume();
            debugLog("OnResume ran");
            //if (!mResolvingError) { }

            if (!mGoogleApiClient.IsConnected)
            {
                mGoogleApiClient.Connect();
            }

            updateConnectionStatusString("Connecting");

            
        }

        

        protected override async void OnPause()
        {
            base.OnPause();
            debugLog("App Paused");
            //if (!mResolvingError) { }
            
            await WearableClass.DataApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.MessageApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.NodeApi.RemoveListenerAsync(mGoogleApiClient, this);
            mGoogleApiClient.Disconnect();
            updateConnectionStatusString("Disconnecting");
        
        }

        protected override async void OnStop()
        {
            base.OnStop();
            debugLog("App Stopped");
            //if (!mResolvingError)
            //{
                await WearableClass.DataApi.RemoveListenerAsync(mGoogleApiClient, this);
                await WearableClass.MessageApi.RemoveListenerAsync(mGoogleApiClient, this);
                await WearableClass.NodeApi.RemoveListenerAsync(mGoogleApiClient, this);
                mGoogleApiClient.Disconnect();
                updateConnectionStatusString("Disconnecting");
            //}
        }

        

        public async void OnConnected(Bundle connectionHint)
        {
            LOGD(Tag, "Google API CLient was connected");
            //mResolvingError = false;
            connectionAttempts = 0;
            startActivityBtn.Enabled = true;
            await WearableClass.DataApi.AddListenerAsync(mGoogleApiClient, this);
            await WearableClass.MessageApi.AddListenerAsync(mGoogleApiClient, this);
            await WearableClass.NodeApi.AddListenerAsync(mGoogleApiClient, this);
            updateConnectionStatusString("Connected");
        }

        public void OnConnectionSuspended(int cause)
        {
            LOGD(Tag, "Connection to Google API client was suspended");
            startActivityBtn.Enabled = false;
            updateConnectionStatusString("Connection Suspended");
        }

        public async void OnConnectionFailed(Android.Gms.Common.ConnectionResult result)
        {
            debugLog("Connection failed");

            //if (mResolvingError)
            //{
            //    // Already attempting to resolve an error
            //    return;
            //}
            //else if (result.HasResolution)
            //{
            //    try
            //    {
            //        mResolvingError = true;
            //        result.StartResolutionForResult(this, RequestResolveError);
            //    }
            //    catch (IntentSender.SendIntentException e)
            //    {
            //        debugLog("Reconnecting after failed connection");
            //        // There was an error with the resolution intent. Try again.
            //        mGoogleApiClient.Connect();
            //        updateConnectionStatusString("Connecting");
            //    }
            //}
            //else
            //{
            //    debugLog("Connection failed");
            //    mResolvingError = false;
            //    startActivityBtn.Enabled = false;
            //    //SendFunMessageBtn.Enabled = false;
            //    await WearableClass.DataApi.RemoveListenerAsync(mGoogleApiClient, this);
            //    await WearableClass.MessageApi.RemoveListenerAsync(mGoogleApiClient, this);
            //    await WearableClass.NodeApi.RemoveListenerAsync(mGoogleApiClient, this);
            //    updateConnectionStatusString("Connection Failed");
            //}
            //await WearableClass.DataApi.RemoveListenerAsync(mGoogleApiClient, this);
            //await WearableClass.MessageApi.RemoveListenerAsync(mGoogleApiClient, this);
            //await WearableClass.NodeApi.RemoveListenerAsync(mGoogleApiClient, this);
            
            connectionAttempts++;
            if (connectionAttempts < maxConnectionAttempts)
            {
                if (!mGoogleApiClient.IsConnected)
                {
                    mGoogleApiClient.Connect();
                }
                updateConnectionStatusString("Connecting");
            }
            else
            {
                updateConnectionStatusString("Connection failed");
            }
            
        }


        public void OnDataChanged(DataEventBuffer dataEvents)
        {
            debugLog("Data changed");

            var dataEvent = Enumerable.Range(0, dataEvents.Count)
                .Select(i => dataEvents.Get(i).JavaCast < IDataEvent>())
                .FirstOrDefault(x => x.Type == DataEvent.TypeChanged && x.DataItem.Uri.Path.Equals(TestDataPath));
            if (dataEvent == null)
            {
                return;
            }

            else
            {
                var dataMapItem = DataMapItem.FromDataItem(dataEvent.DataItem);
                var map = dataMapItem.DataMap;
                string message = dataMapItem.DataMap.GetString("Message");
                debugLog("Test data actually received! message: " + message);
            }
            //do stuffs here

        }

        public void OnMessageReceived (IMessageEvent messageEvent)
		{
            debugLog("Message received");
            //LOGD (Tag, "OnMessageReceived() A message from the watch was received: " + messageEvent.RequestId + " " + messageEvent.Path);
            handler.Post( () => {
                if (messageEvent.Path == FunMessagePath)
                {
                    //dataItemListAdapter.Add(new Event("Manual message from watch", messageEvent.ToString()));
                    var buffer = messageEvent.GetData();
                    string initialMessage = Encoding.Default.GetString(buffer, 0, buffer.Length);
                    updateStatusString(initialMessage);
                    debugLog("Fun Message received, contained: " + initialMessage);
                }
                else if (messageEvent.Path == DataPointPath)
                {
                    debugLog("One Datapoint received");
                    //dataItemListAdapter.Add(new Event("Manual message from watch", messageEvent.ToString()));
                    var buffer = messageEvent.GetData();
                    string initialMessage = Encoding.Default.GetString(buffer, 0, buffer.Length);
                    HeartDataPoint datapoint = decodeDataPoint(initialMessage);


                        if (datapoint != null)
                        {
                            updateStatusString("Data received(" + datapoint.heartType + ", " + datapoint.amount + ", " + datapoint.timestamp + ").");
                        }
                        else
                        {
                            updateStatusString("Invalid data received");
                        }
                        
                    
                }else if (messageEvent.Path == DataPointsPath) //i hovedsak denne som brukes for øyeblikket.
                {
                    debugLog("Multiple datapoints received");
                    var buffer = messageEvent.GetData();
                    string initialMessage = Encoding.Default.GetString(buffer, 0, buffer.Length);
                    string[] allDataPoints;
                    if (initialMessage.Contains("|"))
                    {
                        allDataPoints = initialMessage.Split("|");
                    }
                    else
                    {
                        allDataPoints = new[] { initialMessage };
                    }

                    int teller = 0;
                    foreach (string pointData in allDataPoints)
                    {
                        HeartDataPoint p = decodeDataPoint(pointData);
                        if (p != null)
                        {
                            teller++;
                        }
                        if(p.heartType == HeartDataType.HeartBeat) { hdata_Beat.Add(p); }
                        else if (p.heartType == HeartDataType.HeartRate) { hdata_Rate.Add(p); }
                        else if (p.heartType == HeartDataType.StepCount) { hdata_Steps.Add(p); }
                    }

                    if (teller > 0)
                    {
                        updateStatusString("Data received(Amount: " +teller+ ").");
                        //saveStepData();//bør nok kjøres på en mer intelligent måte
                    }
                    else
                    {
                        updateStatusString("No valid data received.");
                    }

                }
                else
                {
                    
                    //do nothing
                    debugLog("No Match for message path");
                }
            });
		}

        private HeartDataPoint decodeDataPoint(string data)
        {

            HeartDataPoint point = null;


            string[] types = data.Split(";");
            if (types.Length == 3)
            {

                List<HeartDataPoint> listRef;
                HeartDataType dataType;

                string type = types[0];
                if (type == "HeartBeat")
                {
                    listRef = hdata_Beat;
                    dataType = HeartDataType.HeartBeat;
                }
                else if (type == "HeartRate")
                {
                    listRef = hdata_Rate;
                    dataType = HeartDataType.HeartRate;
                }
                else if (type == "StepCount")
                {
                    listRef = hdata_Steps;
                    dataType = HeartDataType.StepCount;
                }
                else
                {
                    listRef = null;
                    dataType = HeartDataType.None;
                }

                int value = 0;
                bool wasNumber = Int32.TryParse(types[1], out value);
                if (wasNumber && dataType != HeartDataType.None && listRef != null)
                {
                    //string dateString = date.ToString("o");
                    DateTime restoredDate = DateTime.Parse(types[2], null, DateTimeStyles.RoundtripKind);

                    point = new HeartDataPoint(dataType, value, restoredDate);

                }

            }
            return point;
        }




        public void OnPeerConnected (INode peer)
		{
			LOGD (Tag, "OnPeerConencted: " + peer);
			handler.Post (() => {
                updateConnectionStatusString("Peer Connected");
            });
		}

		public void OnPeerDisconnected (INode peer)
		{
			LOGD (Tag, "OnPeerDisconnected: " + peer);
			handler.Post (() => {
                updateConnectionStatusString("Peer Disconnected");
            });
		}

		
		ICollection<string> Nodes {
			get {
				HashSet<string> results = new HashSet<string> ();
				var  nodes = WearableClass.NodeApi.GetConnectedNodesAsync (mGoogleApiClient).Result;

				foreach (var node in nodes.Nodes) {
					results.Add (node.Id);
				}
				return results;
			}
		}

        async Task SendStartActivityMessage(String node) {
            debugLog("Attempting to start activity");
            var res = await WearableClass.MessageApi.SendMessageAsync (mGoogleApiClient, node, StartActivityPath, new byte[0]);
    		if (!res.Status.IsSuccess) {
				Log.Error(Tag, "Failed to send message with status code: " + res.Status.StatusCode);
                updateStatusString("Failed to send signal");
            }
            else
            {
                updateStatusString("Start signal sent");
            }
		}

		class StartWearableActivityTask : AsyncTask
		{
			public MainActivity Activity;
			protected override Java.Lang.Object DoInBackground (params Java.Lang.Object[] @params)
			{
				if (Activity != null) {
					var nodes = Activity.Nodes;
					foreach (var node in nodes) {
						Activity.SendStartActivityMessage (node);
					}
				}
				return null;
			}
		}

        /// <summary>
        /// Sends an RPC to start a fullscreen Activity on the wearable
        /// </summary>
        /// <param name="view"></param>
        [Export("onStartWearableActivityClick")]
		public void OnStartWearableActivityClick(View view) {
            debugLog("Start wearable activity clicked");

            // Trigger an AsyncTask that will query for a list of connected nodes and send a "start-activity" message to each connected node
            var task = new StartWearableActivityTask () { Activity = this };
			task.Execute ();
		}


        /// <summary>
        /// A simple wrapper around Log.Debug
        /// </summary>
        /// <param name="tag">Tag</param>
        /// <param name="message">Message to log</param>
        static void LOGD(string tag, string message) 
		{
			if (Log.IsLoggable(tag, LogPriority.Debug)) {
				Log.Debug(tag, message);
			}
        }

        private enum HeartDataType
        {
            None,
            HeartBeat,
            HeartRate,
            StepCount//test type for quick data
        }

        private class HeartDataPoint
        {
            public HeartDataType heartType { get; }
            public int amount { get; set; }
            public DateTime timestamp { get; }
            public HeartDataPoint(HeartDataType heartType, int amount, DateTime timestamp)
            {
                this.heartType = heartType;
                this.amount = amount;
                this.timestamp = timestamp;
            }

        }

	}
}


