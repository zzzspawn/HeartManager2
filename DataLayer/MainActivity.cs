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
//TODO: clean up imports, this applies to everything(although do other todo's first, in case they're needed (Dependency managment is hard in C# and xamarin))

namespace DataLayer
{
    //TODO: maybe move some functionality away from the home screen, maybe
    /// <summary>
    /// This activity does most everything in the application, except from deleting data, that is done in the settings
    /// </summary>
    [Activity(MainLauncher = true, Label = "@string/app_name", LaunchMode = LaunchMode.SingleTask,
        Icon = "@drawable/ic_launcher")]
    public class MainActivity : Activity, IDataApiDataListener, IMessageApiMessageListener, INodeApiNodeListener,
        GoogleApiClient.IConnectionCallbacks, GoogleApiClient.IOnConnectionFailedListener
    {
        //Path to look for incoming data
        const string DataPointsPath = "/data-points";
        //amount of times to try connecting to the wearable again if you can't connect, better than trying to resolve errors, as that just turns into a right mess(As in better to sever connection and try again)..
        private const int maxConnectionAttempts = 10;
        private int connectionAttempts;
        GoogleApiClient mGoogleApiClient;
        View startActivityBtn;

        private TextView statusTextView;
        private TextView connectionStatusTextView;
        private ImageView imageView;

        private Handler handler;

        //received data, not saved yet
        private Queue<HeartDataPoint> hdata_Rate; 
        private Queue<HeartDataPoint> hdata_Beat;
        private Queue<HeartDataPoint> hdata_Steps;

        private StatusHandler dataStatusHandler;
        private StatusHandler connectionStatusHandler;

        private Microcharts.Droid.ChartView chartView; //chart container

        /// <summary>
        /// Main method sort of speak
        /// </summary>
        /// <param name="bundle"></param>
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

        /// <summary>
        /// Legacy, ported to new status string handler
        /// </summary>
        /// <param name="text"></param>
        private void updateConnectionStatusString(string text)
        {
            RunOnUiThread(() =>
            {
                //connectionStatusTextView.Text = text;
                connectionStatusHandler.updateStatus(text);
            });
        }
        /// <summary>
        /// Legacy, ported to new status string handler
        /// </summary>
        /// <param name="text"></param>
        private void updateStatusString(string text)
        {
            RunOnUiThread(() =>
            {
                //statusTextView.Text = text;
                dataStatusHandler.updateStatus(text);
            });
        }

        
        /// <summary>
        /// gets up UI components and assigns them to variables
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

        /// <summary>
        /// Initiates lists used
        /// </summary>
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

        /// <summary>
        /// On start, reconnects to the wearable if it's not already connected
        /// </summary>
        protected override async void OnStart()
        {
            base.OnStart();
            HeartDebugHandler.debugLog("OnStart ran");
            //Removes listeners first, then adds them; haven't found a good way to check if there is a listener currently
            await WearableClass.DataApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.DataApi.AddListener(mGoogleApiClient, this);

            await WearableClass.MessageApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.MessageApi.AddListener(mGoogleApiClient, this);

            await WearableClass.NodeApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.NodeApi.AddListener(mGoogleApiClient, this);

            if (!mGoogleApiClient.IsConnected && !mGoogleApiClient.IsConnecting)
            {
                updateConnectionStatusString("Connecting");
                mGoogleApiClient.Connect();
            }
        }
        /// <summary>
        /// onResume reconnects if app isn't already connected or currently connecting
        /// </summary>
        protected override async void OnResume()
        {
            base.OnResume();
            HeartDebugHandler.debugLog("OnResume ran");

            updateConnectionStatusString("Connecting");
            await WearableClass.DataApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.DataApi.AddListener(mGoogleApiClient, this);

            await WearableClass.MessageApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.MessageApi.AddListener(mGoogleApiClient, this);

            await WearableClass.NodeApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.NodeApi.AddListener(mGoogleApiClient, this);

            if (!mGoogleApiClient.IsConnected && !mGoogleApiClient.IsConnecting)
            {
                
                mGoogleApiClient.Connect();
            }

        }

        /// <summary>
        /// Disconnects the wearable connection, and removes listeners
        /// </summary>
        protected override async void OnPause()
        {
            base.OnPause();
            HeartDebugHandler.debugLog("App Paused");
            //if (!mResolvingError) { }

            updateConnectionStatusString("Disconnecting");
            //TODO: should probably look into which listeners are actually needed, I believe it's only the dataApi and maybe the NodeApi that are actually in use
            await WearableClass.DataApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.MessageApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.NodeApi.RemoveListenerAsync(mGoogleApiClient, this);
            mGoogleApiClient.Disconnect();
            
        
        }
        /// <summary>
        /// Removes listeners and disconnects from the wearable
        /// </summary>
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

        //TODO: Clean up the whole connections and listeners mess, debug and find out when each is called and connect/add listeners where appropriate

        /// <summary>
        /// When connected it adds the listeners again
        /// </summary>
        /// <param name="connectionHint"></param>
        public async void OnConnected(Bundle connectionHint)
        {
            HeartDebugHandler.debugLog("Google API CLient was connected");
            //mResolvingError = false;
            connectionAttempts = 0;
            startActivityBtn.Enabled = true;
            await WearableClass.DataApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.DataApi.AddListener(mGoogleApiClient, this);

            await WearableClass.MessageApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.MessageApi.AddListener(mGoogleApiClient, this);

            await WearableClass.NodeApi.RemoveListenerAsync(mGoogleApiClient, this);
            await WearableClass.NodeApi.AddListener(mGoogleApiClient, this);
            updateConnectionStatusString("Connected");
        }

        /// <summary>
        /// Connection was suspended it pretty much just notifies user about this
        /// </summary>
        /// <param name="cause"></param>
        public void OnConnectionSuspended(int cause)
        {
            HeartDebugHandler.debugLog("Connection to Google API client was suspended");
            updateConnectionStatusString("Connection Suspended");
        }

        /// <summary>
        /// Notifies user that connection was failed, although it tries connecting up to 10 times(defined variable up top)
        /// </summary>
        /// <param name="result"></param>
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

        /// <summary>
        /// get's called when data from the wearable is received
        /// needs to be different from earlier data, which is why you need the timestamps as part of the data if you want each data point
        /// </summary>
        /// <param name="dataEvents"></param>
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
                bool stepReceived = false;
                bool hbReceived = false;
                bool hrReceived = false;
                foreach (string pointData in allDataPoints)
                {
                    HeartDataPoint p = decodeDataPoint(pointData);
                    if (p != null)
                    {
                        teller++;
                    }

                    if (p.heartType == HeartDataType.HeartBeat)
                    {
                        if (!hbReceived) {hbReceived = true;}
                        hdata_Beat.Enqueue(p);

                        if (hdata_Beat.Count > 50)
                        {
                            HeartFileHandler.saveData(hdata_Beat, HeartFileHandler.FILENAME_HEARTBEAT, dataStatusHandler);
                        }

                    }
                    else if (p.heartType == HeartDataType.HeartRate)
                    {
                        if (!hrReceived) { hrReceived = true; }
                        hdata_Rate.Enqueue(p);

                        if (hdata_Rate.Count > 50)
                        {
                            HeartFileHandler.saveData(hdata_Rate, HeartFileHandler.FILENAME_HEARTRATE, dataStatusHandler);
                        }
                    }
                    else if (p.heartType == HeartDataType.StepCount)
                    {
                        if (!stepReceived) { stepReceived = true; }
                        hdata_Steps.Enqueue(p);
                        if (hdata_Steps.Count > 50)
                        {
                            HeartFileHandler.saveData(hdata_Steps, HeartFileHandler.FILENAME_STEPS, dataStatusHandler);
                        }
                    }
                }

                if (teller > 0)
                {
                    string types = "";
                    if (hrReceived) {types += "HR,"; }
                    if (hbReceived) { types += "HB,"; }
                    if (stepReceived) { types += "St"; }
                    updateStatusString("Data received, Types: {"+ types + "}, Amount: " + teller + ".");
                    //saveStepData();//bør nok kjøres på en mer intelligent måte
                }
                else
                {
                    updateStatusString("Invalid data received.");
                }

            }
        }

        //TODO: remove message handler and rely instead on the data handler
        /// <summary>
        /// called when a message from wearable is received
        /// </summary>
        /// <param name="messageEvent"></param>
        public void OnMessageReceived (IMessageEvent messageEvent)
		{
            
		}
        //TODO: Move the decoding of datastring to datapoint to heart data point class, maybe as a constructor overload, or as part of a factory pattern
        /// <summary>
        /// Decodes a received string into a data point class instance instead
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
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

        //TODO: The peer connected stuff can probably be removed
        /// <summary>
        /// Node stuff, can probably be removed
        /// </summary>
        /// <param name="peer"></param>
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

        /// <summary>
        /// Opens the settings activity
        /// </summary>
        /// <param name="view"></param>
        [Export("onSettingsButtonClicked")]
        public void onSettingsButtonClicked(View view)
        {
            HeartDebugHandler.debugLog("Settings clicked!");
            Intent intent = new Intent(this, typeof(SettingsActivity));
            StartActivity(intent);
            HeartDebugHandler.debugLog("BreakPoint");
        }
        //TODO: Create a better interface for the charts, and display a chart for each data type
        /// <summary>
        /// Creates the chart from data on file, and displays it(only stepdata so far)
        /// </summary>
        /// <param name="view"></param>
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

        //TODO: if ever needed, could add a calculation using length and selectedIndex and figure out how many times it's divisible on the length instead of the loop, but optimizing here isn't really needed
        /// <summary>
        /// Takes a number(in this case the index of the data)
        /// Returns a color corresponding to that number, and if the number is bigger than the array of colors, it wraps around
        /// </summary>
        /// <param name="selectedIndex"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Saves any unsaved data to file, any data stored will be dequeued from the list
        /// </summary>
        /// <param name="view"></param>
        [Export("onSaveToFileClicked")]
        public void onSaveToFileClicked(View view)
        {
            HeartDebugHandler.debugLog("Save to file clicked");

            if (hdata_Steps.Count > 0)
            {
                HeartFileHandler.saveData(hdata_Steps, HeartFileHandler.FILENAME_STEPS, dataStatusHandler);
            }

            if (hdata_Rate.Count > 0)
            {
                HeartFileHandler.saveData(hdata_Rate, HeartFileHandler.FILENAME_HEARTRATE, dataStatusHandler);
            }

            if (hdata_Beat.Count > 0)
            {
                HeartFileHandler.saveData(hdata_Beat, HeartFileHandler.FILENAME_HEARTBEAT, dataStatusHandler);
            }

            if (hdata_Steps.Count == 0 && hdata_Beat.Count == 0 && hdata_Rate.Count == 0)
            {
                dataStatusHandler.updateStatus("No unsaved data");
            }

        }

        //TODO: Add another upload that sends all codes to server, and get a new code in return, this should display a page that combines all the data into one view
        /// <summary>
        /// Tries to upload all data from files to azure website,
        /// then notifies the user on the status of the upload for each data type
        /// </summary>
        /// <param name="view"></param>
        [Export("onUploadAllClicked")]
        public async void onUploadAllClicked(View view)
        {

            TextView codeView = FindViewById<TextView>(Resource.Id.mainCodeTV);
            codeView.Visibility = ViewStates.Invisible;

            HeartDebugHandler.debugLog("Getting json string");
            string jsonStringSteps = await HeartFileHandler.getJSONString(HeartFileHandler.FILENAME_STEPS);
            string jsonStringRate = await HeartFileHandler.getJSONString(HeartFileHandler.FILENAME_HEARTRATE);
            string jsonStringBeat = await HeartFileHandler.getJSONString(HeartFileHandler.FILENAME_HEARTBEAT);

            //HeartDebugHandler.debugLog(jsonString.Substring(jsonString.Length-30));
            //HeartDebugHandler.debugLog("String got, length: " + jsonString.Length);

            HeartDebugHandler.debugLog("Sending data");

            //HeartNetworkHandler.sendPostRequest(this, jsonString, codeView);
            //HeartNetworkHandler.sendPostRequest(this, jsonStringSteps, jsonStringRate, jsonStringBeat, codeView);
            HeartPostSender hps = new HeartPostSender(this, codeView, jsonStringSteps, jsonStringRate, jsonStringBeat);

            hps.SendData();

        }

    }
}


