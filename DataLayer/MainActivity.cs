using System;
using System.Collections;
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
using Microcharts;
using SkiaSharp;
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
        const string DataPointsPath = "/data-points";
        private const int maxConnectionAttempts = 10;
        private int connectionAttempts;
        GoogleApiClient mGoogleApiClient;
        View startActivityBtn;

        private TextView statusTextView;
        private TextView connectionStatusTextView;
        private ImageView imageView;

        private Handler handler;

        private Queue<HeartDataPoint> hdata_Rate;
        private Queue<HeartDataPoint> hdata_Beat;
        private Queue<HeartDataPoint> hdata_Steps;

        private StatusHandler dataStatusHandler;
        private StatusHandler connectionStatusHandler;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            handler = new Handler();
            HeartDebugHandler.debugLog("OnCreate");
            connectionAttempts = 0;
            SetContentView(Resource.Layout.main_activity);
            HeartDebugHandler.debugLog("App Launched");
            SetupViews();
            SetupLists();

            dataStatusHandler = new StatusHandler(statusTextView, "No data received");
            connectionStatusHandler = new StatusHandler(connectionStatusTextView, "");

            mGoogleApiClient = new GoogleApiClient.Builder(this)
                .AddApi(WearableClass.API)
                .AddConnectionCallbacks(this)
                .AddOnConnectionFailedListener(this)
                .Build();
        }

        //public void SendData(string data, string path)
        //{
        //    try
        //    {
        //        var request = PutDataMapRequest.Create(path);
        //        var map = request.DataMap;
        //        map.PutString("Message", data);
        //        map.PutLong("UpdatedAt", DateTime.UtcNow.Ticks);
        //        WearableClass.DataApi.PutDataItem(mGoogleApiClient, request.AsPutDataRequest());
        //    }
        //    finally
        //    {
        //        //_client.Disconnect();
        //    }
            
        //}

        private void updateConnectionStatusString(string text)
        {
            RunOnUiThread(() =>
            {
                //connectionStatusTextView.Text = text;
                connectionStatusHandler.updateStatus(text);
            });
        }
        private void updateStatusString(string text)
        {
            RunOnUiThread(() =>
            {
                //statusTextView.Text = text;
                dataStatusHandler.updateStatus(text);
            });
        }

        private Microcharts.Droid.ChartView chartView;
        /// <summary>
        /// Sets up UI components and their callback handlers
        /// </summary>
        void SetupViews()
        {
            HeartDebugHandler.debugLog("Setting up views");
            startActivityBtn = FindViewById(Resource.Id.start_wearable_activity);
            statusTextView = (TextView) FindViewById(Resource.Id.statusText);
            connectionStatusTextView = (TextView) FindViewById(Resource.Id.connectionStatusText);
            chartView = (Microcharts.Droid.ChartView) FindViewById(Resource.Id.linechart);
            imageView = (ImageView) FindViewById(Resource.Id.statusImage);
        }

        void SetupLists()
        {
            HeartDebugHandler.debugLog("Setting up Lists");
            hdata_Beat = new Queue<HeartDataPoint>();
            hdata_Rate = new Queue<HeartDataPoint>();
            hdata_Steps = new Queue<HeartDataPoint>();
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            HeartDebugHandler.debugLog("Activity results are in!");
            if (resultCode == Result.Ok)
            {
                Bundle extras = data.Extras;
            }
        }

        protected override void OnStart()
        {
            base.OnStart();
            HeartDebugHandler.debugLog("OnStart ran");

            if (!mGoogleApiClient.IsConnected)
            {
                updateConnectionStatusString("Connecting");
                mGoogleApiClient.Connect();
            }
        }

        protected override async void OnResume()
        {
            base.OnResume();
            HeartDebugHandler.debugLog("OnResume ran");

            updateConnectionStatusString("Connecting");
            await WearableClass.DataApi.AddListener(mGoogleApiClient, this);
            await WearableClass.MessageApi.AddListener(mGoogleApiClient, this);
            await WearableClass.NodeApi.AddListener(mGoogleApiClient, this);

            if (!mGoogleApiClient.IsConnected)
            {
                
                mGoogleApiClient.Connect();
            }

        }

        protected override async void OnPause()
        {
            base.OnPause();
            HeartDebugHandler.debugLog("App Paused");
            //if (!mResolvingError) { }

            updateConnectionStatusString("Disconnecting");
            await WearableClass.DataApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.MessageApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.NodeApi.RemoveListenerAsync(mGoogleApiClient, this);
            mGoogleApiClient.Disconnect();
            
        
        }

        protected override async void OnStop()
        {
            base.OnStop();
            HeartDebugHandler.debugLog("App Stopped");
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
            HeartDebugHandler.debugLog("Google API CLient was connected");
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
            HeartDebugHandler.debugLog("Connection to Google API client was suspended");
            startActivityBtn.Enabled = false;
            updateConnectionStatusString("Connection Suspended");
        }

        public async void OnConnectionFailed(Android.Gms.Common.ConnectionResult result)
        {
            HeartDebugHandler.debugLog("Connection failed");
            connectionAttempts++;
            if (connectionAttempts < maxConnectionAttempts)
            {
                if (!mGoogleApiClient.IsConnected && !mGoogleApiClient.IsConnecting)
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
            HeartDebugHandler.debugLog("Data changed");

            var dataEvent = Enumerable.Range(0, dataEvents.Count)
                .Select(i => dataEvents.Get(i).JavaCast < IDataEvent>())
                .FirstOrDefault(x => x.Type == DataEvent.TypeChanged && x.DataItem.Uri.Path.Equals(DataPointsPath));
            if (dataEvent == null)
            {
                return;
            }else
            {
                var dataMapItem = DataMapItem.FromDataItem(dataEvent.DataItem);
                var map = dataMapItem.DataMap;
                string message = dataMapItem.DataMap.GetString("Message");
                HeartDebugHandler.debugLog("Data received! message: " + message);

                string[] allDataPoints;
                if (message.Contains("|"))
                {
                    allDataPoints = message.Split("|");
                }
                else
                {
                    allDataPoints = new[] { message };
                }

                int teller = 0;
                foreach (string pointData in allDataPoints)
                {
                    HeartDataPoint p = decodeDataPoint(pointData);
                    if (p != null)
                    {
                        teller++;
                    }
                    if (p.heartType == HeartDataType.HeartBeat) { hdata_Beat.Enqueue(p); }
                    else if (p.heartType == HeartDataType.HeartRate) { hdata_Rate.Enqueue(p); }
                    else if (p.heartType == HeartDataType.StepCount) { hdata_Steps.Enqueue(p); }
                }

                if (teller > 0)
                {
                    updateStatusString("Data received, Amount: " + teller + ".");
                    //saveStepData();//bør nok kjøres på en mer intelligent måte
                }
                else
                {
                    updateStatusString("Invalid data received.");
                }

            }
        }

        public void OnMessageReceived (IMessageEvent messageEvent)
		{
            
		}

        private HeartDataPoint decodeDataPoint(string data)
        {

            HeartDataPoint point = null;

            string[] types = data.Split(";");
            if (types.Length == 3)
            {

                Queue<HeartDataPoint> listRef;
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
            HeartDebugHandler.debugLog("OnPeerConencted: " + peer);
			handler.Post (() => {
                updateConnectionStatusString("Peer Connected");
            });
		}

		public void OnPeerDisconnected (INode peer)
		{
			HeartDebugHandler.debugLog("OnPeerDisconnected: " + peer);
			handler.Post (() => {
                updateConnectionStatusString("Peer Disconnected");
            });
		}

        [Export("onSettingsButtonClicked")]
        public void onSettingsButtonClicked(View view)
        {
            HeartDebugHandler.debugLog("Settings clicked!");
            Intent intent = new Intent(this, typeof(SettingsActivity));
            StartActivity(intent);
            HeartDebugHandler.debugLog("BreakPoint");
        }

        [Export("onToggleChartClicked")]
        public async void onToggleChartClicked(View view)
        {
            if (imageView.Visibility == ViewStates.Visible)
            {
                imageView.Visibility = ViewStates.Gone;
                chartView.Visibility = ViewStates.Visible;
                List<HeartDataPoint> dataPoints = await HeartFileHandler.getData(HeartFileHandler.FILENAME_STEPS);
                
                List<Entry> orderedSummarizedList = new List<Entry>();
                if(dataPoints != null && dataPoints.Count > 0) {

                        dataPoints.Sort((x,y) => x.timestamp.CompareTo(y.timestamp)); //ordering list by time

                        HeartDataPoint currentPoint = dataPoints[0];
                        //DateTime currentDate = currentPoint.timestamp;
                        //int currentHour = currentPoint.timestamp.Hour;
                        int currentTotal = 0;
                        int colorCount = 0;
                    for (int i = 0; i < dataPoints.Count; i++)
                    {
                        HeartDataPoint examinePoint = dataPoints[i];
                        if (currentPoint.timestamp.Date == examinePoint.timestamp.Date &&
                            currentPoint.timestamp.Hour == examinePoint.timestamp.Hour &&
                            currentPoint.timestamp.Minute == examinePoint.timestamp.Minute)
                        {
                            currentTotal += examinePoint.amount;
                        }
                        else
                        {
                            //create entry here
                            Entry entry = new Entry(currentTotal)
                            {
                                Label = currentPoint.timestamp.Date.ToString("d") + ", Hour: " + currentPoint.timestamp.Hour,
                                ValueLabel = currentTotal.ToString(),
                                Color = SKColor.Parse(getColor(colorCount))
                            };
                            colorCount++;
                            orderedSummarizedList.Add(entry);

                            currentPoint = dataPoints[i];
                            currentTotal = currentPoint.amount;//since it won't be examined against it self, like the first will
                        }
                    }
                    //Adding the last iteration of the list(also helps when list only contains one member)
                    Entry lastEntry = new Entry(currentTotal)
                    {
                        Label = currentPoint.timestamp.Date.ToString("d") + ", Hour: " + currentPoint.timestamp.Hour + "Min: " + currentPoint.timestamp.Minute,
                        ValueLabel = currentTotal.ToString(),
                        Color = SKColor.Parse(getColor(colorCount))
                    };
                    orderedSummarizedList.Add(lastEntry);

                    var entriesLC = orderedSummarizedList.ToArray();
                    var chart = new LineChart(){Entries = entriesLC};
                    chartView.Chart = chart;
                }
                else
                {
                    chartView.Visibility = ViewStates.Gone;
                    imageView.Visibility = ViewStates.Visible;
                    Toast.MakeText(this, "No data currently available", ToastLength.Short).Show();
                }
            }
            else
            {
                chartView.Visibility = ViewStates.Gone;
                imageView.Visibility = ViewStates.Visible;
            }
        }

        private string getColor(int selectedIndex)
        {

            string[] colors = new[] { "#91aaff", "#ff9e9e", "#ff80c5", "#7afbff", "#8aff9c" };

            if (selectedIndex >= colors.Length)
            {
                int newIndex = selectedIndex;
                while (newIndex >= colors.Length)
                {
                    newIndex -= colors.Length;
                }

                return colors[newIndex];
            }
            else
            {
                return colors[selectedIndex];
            }

        }


        [Export("onSaveToFileClicked")]
        public void onSaveToFileClicked(View view)
        {
            HeartDebugHandler.debugLog("Save to file clicked");
            
            HeartFileHandler.saveData(hdata_Steps, HeartFileHandler.FILENAME_STEPS, dataStatusHandler);
        }

    }
}


