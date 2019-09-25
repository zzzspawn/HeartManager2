using System;

using Android.App;
using Android.Content;
//using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Gms.Common.Apis;
using Android.Gms.Wearable;
using Android.Gms.Common.Data;
using System.Collections.Generic;
using System.Collections;
using Android.Graphics;
using Android.Graphics.Drawables;
using System.IO;
using System.Linq;
using System.Text;
using Android.Util;
using Android.Content.PM;
using System.Threading.Tasks;
using Android;
using Java.Interop;
using Android.Hardware;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V4.Widget;
using DataLayer;
using Xamarin.Essentials;

namespace Wearable
{
    //TODO: add a "reconnect" button
    //TODO: find out what the parameters in activity does, and remove those not needed.
	/// <summary>
	/// wearable activity, sends data from sensors to handheld
	/// </summary>
	[Activity (Label = "Wearable", 
		MainLauncher = true,
		ScreenOrientation = ScreenOrientation.Portrait,
		Icon = "@drawable/icon"),
		IntentFilter( new string[]{ "android.intent.action.MAIN" }, Categories = new string[]{ "android.intent.category.LAUNCHER" }),
		IntentFilter( new string[]{ "com.example.android.wearable.datalayer.EXAMPLE" }, 
			Categories = new string[]{ "android.intent.category.DEFAULT" })]
	public class MainActivity : Activity, GoogleApiClient.IConnectionCallbacks, GoogleApiClient.IOnConnectionFailedListener, IDataApiDataListener,
	IMessageApiMessageListener, INodeApiNodeListener, Android.Hardware.ISensorEventListener
	{
		public const string Tag = "MainActivity";

		GoogleApiClient googleApiClient;
		//ListView dataItemList;
		private TextView connectionTextView;
        private TextView dataStatusTextView;
        private TextView sensorDataTextView;

        private StatusHandler connectionStatusHandler;
        private StatusHandler dataStatusHandler;
        private StatusHandler sensorStatusHandler;

        //DataItemAdapter dataItemListAdapter;
        View layout;
		Handler handler;

        SensorManager sensorManager;
        Sensor heartRatesensor;
        Sensor heartBeatsensor;
        Sensor stepCounter;

        private string[] accuracies;

        private Queue<HeartDataPoint> dataPoints; //queue of datapoints that are to be sent on to the other device(might be able to skip this now with the new data system)

        //Paths that are used on both devices to check what type of message was received
        const string FunMessagePath = "/fun-message";
        const string DataPointPath = "/data-point";
        const string DataPointsPath = "/data-points";
        const string TestDataPath = "/data-test";
        private int BODYSENSOR_CODE = 123; //needed to check if the permission you asked for is the same you got the result for

        SwipeRefreshLayout swipeRefreshLayout;


        /// <summary>
        /// inits the activity and asks the screen to stay on
        /// </summary>
        /// <param name="bundle"></param>
        protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
            handler = new Handler ();
            SetContentView (Resource.Layout.main_activity);
            //TODO: Find out if keeping the screen on really is necessary
			Window.AddFlags (WindowManagerFlags.KeepScreenOn);
            debugLog("App Launched");

            dataPoints = new Queue<HeartDataPoint>();
            accuracies = new string[3];

            setUpPermissions();

            setUpViews();

            setUpSensors();

            swipeRefreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);

            swipeRefreshLayout.Refresh += delegate (object sender, System.EventArgs e)
            {
                swipeRefreshLayout.Refreshing = true;

                //Toast.MakeText(this, "Reconnecting", ToastLength.Short).Show();
                if (!googleApiClient.IsConnected && !googleApiClient.IsConnecting)
                {
                    sensorStatusHandler.updateStatus("Connecting");
                    googleApiClient.Connect();
                }


                swipeRefreshLayout.Refreshing = false;
            };


            googleApiClient = new GoogleApiClient.Builder (this)
				.AddApi (WearableClass.API)
				.AddConnectionCallbacks (this)
				.AddOnConnectionFailedListener (this)
				.Build ();
		}

        /// <summary>
        /// if not currently connecting the app will try connecting to handheld
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();
            debugLog("App Resumed");
            if (!googleApiClient.IsConnected && !googleApiClient.IsConnecting)
            {
                googleApiClient.Connect();
                connectionStatusHandler.updateStatus("Connecting");
                debugLog("Connecting");
                WearableClass.DataApi.AddListener(googleApiClient, this);
            }
            //connectionTextView.Visibility = ViewStates.Visible;
        }

        /// <summary>
        /// Will try to disconnect from the handheld if it is connected
        /// </summary>
        protected override void OnPause()
        {
            base.OnPause();
            debugLog("Pausing App");

            //connectionTextView.Visibility = ViewStates.Visible;
            if (googleApiClient.IsConnected)
            {
                googleApiClient.Disconnect();
                debugLog("Disconnecting");
                connectionStatusHandler.updateStatus("Disconnecting");
            }
        }

        /// <summary>
        /// Checks if you have sensor permissions(should be available, but just in case)
        /// </summary>
        private void setUpPermissions()
        {
            debugLog("Checking Permissions");
            if (!checkPermissions())
            {
                debugLog("Missing permissions, requesting access");
                ActivityCompat.RequestPermissions(this, new String[] { Manifest.Permission.BodySensors }, BODYSENSOR_CODE);
            }
            else
            {
                debugLog("Permissions exist and allow use");
            }
        }
        /// <summary>
        /// Fetches view references and sets them to variables
        /// </summary>
        private void setUpViews()
        {
            dataStatusTextView = (TextView)FindViewById(Resource.Id.dataStatus);
            connectionTextView = (TextView)FindViewById(Resource.Id.connectionStatus);
            sensorDataTextView = (TextView)FindViewById(Resource.Id.sensorStatus);

            dataStatusHandler = new StatusHandler(dataStatusTextView, "No data change");
            connectionStatusHandler = new StatusHandler(connectionTextView, "No connection change");
            sensorStatusHandler = new StatusHandler(sensorDataTextView, "No sensor change");


            layout = FindViewById(Resource.Id.layout);
            
        }
        /// <summary>
        /// Fetches references to the sensors, through the SensorManager class
        /// </summary>
        private void setUpSensors()
        {
            sensorManager = (SensorManager)GetSystemService(Context.SensorService);

            if (sensorManager.GetSensorList(SensorType.HeartRate).Count > 0)
            {
                heartRatesensor = sensorManager.GetDefaultSensor(SensorType.HeartRate);
            }
            else
            {
                heartRatesensor = null;
            }

            if (sensorManager.GetSensorList(SensorType.HeartBeat).Count > 0)
            {
                heartBeatsensor = sensorManager.GetDefaultSensor(SensorType.HeartBeat);
            }
            else
            {
                heartBeatsensor = null;
            }

            if (sensorManager.GetSensorList(SensorType.StepCounter).Count > 0)
            {
                //stepCounter = sensorManager.GetDefaultSensor(SensorType.StepCounter);
                stepCounter = sensorManager.GetDefaultSensor(SensorType.StepDetector);
            }
            else
            {
                stepCounter = null;
            }
            sensorStatusHandler.updateStatus("Sensors ready");
        }

        //TODO: find a way to make sure data get's processed even when you leave the app, as the sensors seem to still be tracking it would seem like it should be possible
        /// <summary>
        /// Actually starts the sensors tracking, they will continue tracking until you stop listening
        /// although the callbacks don't seem to be called properly if you exit the app
        /// </summary>
        private void startSensorTracking()
        {
            if (heartRatesensor != null)
            {
                sensorManager.RegisterListener(this, heartRatesensor, SensorDelay.Fastest);
            }
            if (heartBeatsensor != null)
            {
                sensorManager.RegisterListener(this, heartBeatsensor, SensorDelay.Fastest);
            }
            if (stepCounter != null)
            {
                sensorManager.RegisterListener(this, stepCounter, SensorDelay.Fastest);
            }
            sensorStatusHandler.updateStatus("Sensors tracking");
        }
        /// <summary>
        /// Unregisters all sensor listeners, haven't found a way to unregister one
        /// </summary>
        private void stopSensorTracking()
        {
            //TODO: find out why I check for !null on one specific sensor here
            if (heartRatesensor != null)
            {
                sensorManager.UnregisterListener(this);
            }
            sensorStatusHandler.updateStatus("Sensors stopped listening");
        }
        /// <summary>
        /// Standardized call to log.info with the same tag
        /// </summary>
        /// <param name="text"></param>
        private void debugLog(string text)
        {
            Log.Info("HH_TEST", text);
        }
        /// <summary>
        /// starts the permissions check
        /// </summary>
        /// <returns></returns>
        private bool checkPermissions()
        {
            return ContextCompat.CheckSelfPermission(this, Manifest.Permission.BodySensors) == (int) Permission.Granted;
        }
        /// <summary>
        /// Starts listening for data, 
        /// </summary>
        /// <param name="bundle"></param>
        public void OnConnected (Bundle bundle)
		{
            debugLog("Connection established");
            connectionStatusHandler.updateStatus("Connected");
            WearableClass.DataApi.AddListener(googleApiClient, this);
            //connectionTextView.Visibility = ViewStates.Gone;
        }
        /// <summary>
        /// Removes the listener from the dataApi
        /// </summary>
        /// <param name="p0"></param>
        public void OnConnectionSuspended (int p0)
		{
            debugLog("Connection suspended");
            WearableClass.DataApi.RemoveListener(googleApiClient, this);
            //connectionTextView.Visibility = ViewStates.Visible;
            connectionStatusHandler.updateStatus("Connection Suspended");

        }
        /// <summary>
        /// Removes the listener from the dataApi
        /// </summary>
        /// <param name="result"></param>
        public void OnConnectionFailed (Android.Gms.Common.ConnectionResult result)
		{
            debugLog("Connection failed");
            WearableClass.DataApi.RemoveListener(googleApiClient, this);
            //connectionTextView.Visibility = ViewStates.Visible;
            connectionStatusHandler.updateStatus("Connection Failed");
        }
        /// <summary>
        /// Informs the user that the peer has connected
        /// </summary>
        /// <param name="node"></param>
        public void OnPeerConnected(INode node)
        {
            debugLog("Peer connected");
            //connectionTextView.Visibility = ViewStates.Gone;
        }

        /// <summary>
        /// Informs the user that the peer has disconnected
        /// </summary>
        /// <param name="node"></param>
        public void OnPeerDisconnected(INode node)
        {
            debugLog("Peer disconnected");
            //connectionTextView.Visibility = ViewStates.Visible;
            connectionStatusHandler.updateStatus("Disconnected");
        }

        /// <summary>
        /// Receives data from handheld; this isn't really used, but is handy for connection tests and such
        /// </summary>
        /// <param name="dataEvents"></param>
        public void OnDataChanged(DataEventBuffer dataEvents)
        {
            debugLog("Data changed");
            dataStatusHandler.updateStatus("Data changed");
            var dataEvent = Enumerable.Range(0, dataEvents.Count)
                .Select(i => JavaObjectExtensions.JavaCast<IDataEvent>(dataEvents.Get(i)))
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
        }


        /// <summary>
        /// Sends a message to the handheld
        /// </summary>
        /// <param name="view"></param>
        [Export("onSendMessageBtnClick")]
        public void onSendMessageBtnClick(View view)
        {
            SendData("This is a fun message",FunMessagePath);
        }

        /// <summary>
        /// initiates tracking on the sensors
        /// </summary>
        /// <param name="view"></param>
        [Export("onStartTracking")]
        public void onStartTracking(View view)
        {
            debugLog("Start tracking clicked");
            startSensorTracking();
            sensorStatusHandler.updateStatus("Sensors tracking");
        }

        /// <summary>
        /// Stops tracking on the sensors
        /// </summary>
        /// <param name="view"></param>
        [Export("onStopTracking")]
        public void onStopTracking(View view)
        {
            debugLog("Start tracking clicked");
            stopSensorTracking();
            sensorStatusHandler.updateStatus("Sensors not tracking");
        }

        /// <summary>
        /// Enum that is used to keep track of data-type
        /// </summary>
        public enum HeartDataType
        {
            None,
            HeartBeat,
            HeartRate,
            StepCount//test type for quick data
        }

        //TODO: store sensor accuracy in a global variable(one for each sensor(array?)), and then just update the variable in OnAccuracyChanged, and include it with The datapoints
        /// <summary>
        /// if a sensors accuracy changes then this gets called
        /// </summary>
        /// <param name="sensor"></param>
        /// <param name="accuracy"></param>
        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            if (sensor != null) 
            {
                debugLog("Sensor Accuracy changed, Sensor: " + sensor.Type);
                Log.Debug("HH_TEST", "Accuracy changed for: " + sensor.Type);
                //textView.Text = "Accuracy changed, sensor was: " + sensor.Type.ToString();

                if (stepCounter != null && sensor.Type == stepCounter.Type)
                {
                    debugLog("Accuracy changed for stepcounter");
                    accuracies[0] = accuracy.ToString("G");
                }
                else if (heartBeatsensor != null && sensor.Type == heartBeatsensor.Type)
                {
                    debugLog("Accuracy changed for heartbeat sensor");
                    accuracies[1] = accuracy.ToString("G");
                }
                else if (heartRatesensor != null && sensor.Type == heartRatesensor.Type)
                {
                    debugLog("Accuracy changed for heart rate sensor");
                    accuracies[2] = accuracy.ToString("G");
                }
            }

            if (sensor != null)
            {
                string type = sensor.Type.ToString("G");
                string acc = accuracy.ToString();
                sensorStatusHandler.updateStatus(type + "accuracy changed(" + acc + ")" );
            }
            else
            {
                sensorStatusHandler.updateStatus("Sensor accuracy changed");
            }

            
        }

        /// <summary>
        /// This is where the sensor data is stored/tracked, if there is data, it tries to send it.
        /// if the data get's sent, then it is dequeued from the data-point list.
        /// </summary>
        /// <param name="e"></param>
        public void OnSensorChanged(SensorEvent e)
        {
            sensorStatusHandler.updateStatus("New sensor data");
            debugLog("Sensor Changed");
            if (e.Sensor != null)
            {
                debugLog("Sensor changed was: " + e.Sensor.Type);
                //e.Values[0]
                if (stepCounter != null && e.Sensor.Type == stepCounter.Type)
                {
                    debugLog("Sensor change match for stepcounter");
                    if (accuracies[0] == null)
                    {
                        accuracies[0] = SensorStatus.NoContact.ToString("G");
                    }
                    dataPoints.Enqueue(new HeartDataPoint(HeartDataType.StepCount, (int) e.Values[0],DateTime.Now, accuracies[0]));
                }
                else if (heartBeatsensor != null && e.Sensor.Type == heartBeatsensor.Type)
                {
                    debugLog("Sensor change match for heartbeat sensor");
                    if (accuracies[1] == null)
                    {
                        accuracies[1] = SensorStatus.NoContact.ToString("G");
                    }
                    dataPoints.Enqueue(new HeartDataPoint(HeartDataType.HeartBeat, (int)e.Values[0], DateTime.Now, accuracies[1]));
                }
                else if (heartRatesensor != null && e.Sensor.Type == heartRatesensor.Type)
                {
                    debugLog("Sensor change match for heart rate sensor");
                    if (accuracies[2] == null)
                    {
                        accuracies[2] = SensorStatus.NoContact.ToString("G");
                    }
                    dataPoints.Enqueue(new HeartDataPoint(HeartDataType.HeartRate, (int)e.Values[0], DateTime.Now, accuracies[2]));
                }

                Log.Info("HH_TEST", "Datapoints Count: " + dataPoints.Count);
                debugLog("Amount of datapoints queued: " + dataPoints.Count);
                if (dataPoints.Count > 0)
                {
                    trySendData(); //TODO: Find a way to confirm data being received, and fail if not, then store data for later transfer
                }

                debugLog("Printing available values next: ");
                foreach (float val in e.Values)
                {
                    Log.Info("HH_TEST", "Type: "+ e.Sensor.Type + ", Float value: " + val.ToString());
                }
                debugLog("Printed values, END");

            }
        }

        /// <summary>
        /// Trying to send data stored the data-point list; sends the data as a string.
        /// Only queues data for a 100 datapoints at a time, to avoid too long a string/message
        /// </summary>
        private void trySendData()
        {
            dataStatusHandler.updateStatus("Trying to Send data");
            string message = "";
            Queue<HeartDataPoint> backupList = new Queue<HeartDataPoint>();
            for (var i = 0; i < 100 && dataPoints.Count > 0; i++)
            {
                HeartDataPoint point = dataPoints.Dequeue();

                backupList.Enqueue(point);

                DateTime dateToSave = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);
                string dateString = point.timestamp.ToString("o");
                string typeString = point.heartType.ToString("G");
                string numberString = point.amount.ToString();
                string accuracy = point.accuracy;
                
                message += typeString + ";" + numberString + ";" + dateString + ";" + accuracy;

                if (i < 100 - 1 && dataPoints.Count > 0)
                {
                    message += "|";
                }
            }

            if (message != null && message != "")
            {
                debugLog("Sending multiple datapoints");
                dataStatusHandler.updateStatus("Sending data");
                SendData(message, DataPointsPath);

            }
            else
            {
                debugLog("Sending multiple datapoints failed, message was null or blank");
                dataStatusHandler.updateStatus("Data could not be sent");
                for (int i = 0; i < backupList.Count; i++)
                {
                    dataPoints.Append(backupList.Dequeue());
                }
            }
            

        }
        
        /// <summary>
        /// Actually sends the data
        /// </summary>
        /// <param name="data">The string to be sent</param>
        /// <param name="path">the path where the data can be found</param>
        public void SendData(string data, string path)
        {
            try
            {
                var request = PutDataMapRequest.Create(path);
                var map = request.DataMap;
                map.PutString("Message", data);
                map.PutLong("UpdatedAt", DateTime.UtcNow.Ticks);
                WearableClass.DataApi.PutDataItem(googleApiClient, request.AsPutDataRequest());
                dataStatusHandler.updateStatus("Data sent");
            }
            finally
            {
                //_client.Disconnect();
            }
        }
        
        /// <summary>
        /// The data-point class, stores type, amount, timestamp and hopefully sensor accuracy eventually
        /// </summary>
        private class HeartDataPoint
        {
            public HeartDataType heartType { get; }
            public int amount { get; set; }
            public DateTime timestamp { get; }
            public string accuracy { get; }
            public HeartDataPoint(HeartDataType heartType, int amount, DateTime timestamp, string accuracy)
            {
                this.heartType = heartType;
                this.amount = amount;
                this.timestamp = timestamp;
                this.accuracy = accuracy;
            }

        }
        /// <summary>
        ///This is called after we've requested permissions to use the body sensors.
        /// </summary>
        /// <param name="requestCode">Predefined code that is sent with the original request, used to verify which permission you got a callback from</param>
        /// <param name="permissions"></param>
        /// <param name="grantResults"></param>
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            if (requestCode == BODYSENSOR_CODE)
            {
                // Received permission result for camera permission.
                Log.Info("HH_Info", "Received response for Body sensor permission request.");
                debugLog("Received response for Body sensor permission request, result was: " + (grantResults[0] == Permission.Granted).ToString());

                // Check if the only required permission has been granted
                if ((grantResults.Length == 1) && (grantResults[0] == Permission.Granted))
                {
                    // Body sensor permission has been granted, okay to retrieve the Sensor data of the device.
                    debugLog("Body sensor permission has now been granted");
                    Snackbar.Make(layout, "Permission to see sensors granted", Snackbar.LengthShort).Show();
                }
                else
                {
                    debugLog("Body sensor permission was NOT granted, exiting application");
                    Snackbar.Make(layout, "Permission was not granted, goodbye.", Snackbar.LengthShort).Show();
                    var activity = (Activity)this;
                    activity.FinishAffinity();
                }
            }
            else
            {
                base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            }
        }
        /// <summary>
        /// Called when messages are received, is not used in this project any longer
        /// </summary>
        /// <param name="ev"></param>
        public void OnMessageReceived(IMessageEvent ev)
        {
            debugLog("Message received(This shouldn't happen anymore)");
        }
    }
}


