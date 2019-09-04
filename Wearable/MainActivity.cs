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
using Xamarin.Essentials;

namespace Wearable
{

	/// <summary>
	/// Shows events and photo from the Wearable APIs
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
		TextView introText;
		//DataItemAdapter dataItemListAdapter;
		View layout;
		Handler handler;
        View SendFunMessageBtn;
        private Button trackingBtn;

        SensorManager sensorManager;
        Sensor heartRatesensor;
        Sensor heartBeatsensor;
        Sensor stepCounter;

        private Queue<HeartDataPoint> dataPoints; //queue of datapoints that are to be sent on to the other device(might be able to skip this now with the new data system)

        //Paths that are used on both devices to check what type of message was received
        const string FunMessagePath = "/fun-message";
        const string DataPointPath = "/data-point";
        const string DataPointsPath = "/data-points";
        const string TestDataPath = "/data-test";
        private int BODYSENSOR_CODE = 123; //needed to check if the permission you asked for is the same you got the result for
        protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
            handler = new Handler ();
            SetContentView (Resource.Layout.main_activity);
			Window.AddFlags (WindowManagerFlags.KeepScreenOn);
            debugLog("App Launched");

            dataPoints = new Queue<HeartDataPoint>();

            setUpPermissions();

            setUpViews();

            setUpSensors();

            googleApiClient = new GoogleApiClient.Builder (this)
				.AddApi (WearableClass.API)
				.AddConnectionCallbacks (this)
				.AddOnConnectionFailedListener (this)
				.Build ();
		}

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

        private void setUpViews()
        {
            introText = (TextView)FindViewById(Resource.Id.intro);
            layout = FindViewById(Resource.Id.layout);
            SendFunMessageBtn = FindViewById(Resource.Id.sendMessageBtn);
            trackingBtn = (Button)FindViewById(Resource.Id.trackingbutton);
        }

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
        }
        private void endSensorTracking()
        {
            if (heartRatesensor != null)
            {
                sensorManager.UnregisterListener(this);
            }
        }

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
                stepCounter = sensorManager.GetDefaultSensor(SensorType.StepCounter);
            }
            else
            {
                stepCounter = null;
            }
        }

        private void debugLog(string text)
        {
            Log.Info("HH_TEST", text);
        }

        private bool checkPermissions()
        {
            return ContextCompat.CheckSelfPermission(this, Manifest.Permission.BodySensors) == (int) Permission.Granted;
        }

		protected override void OnResume ()
		{
			base.OnResume ();
            debugLog("App Resumed");
            if (!googleApiClient.IsConnected)
            {
                googleApiClient.Connect();
                introText.Text = "Connecting";
                debugLog("Connecting");
            }
            
            trackingBtn.Visibility = ViewStates.Gone;
            trackingBtn.Enabled = false;
            introText.Visibility = ViewStates.Visible;
            
            
        }

		protected override void OnPause ()
		{
			base.OnPause ();
            debugLog("Pausing App");
            WearableClass.DataApi.RemoveListener(googleApiClient, this);
            //await WearableClass.MessageApi.RemoveListenerAsync (googleApiClient, this);
            //await WearableClass.NodeApi.RemoveListenerAsync (googleApiClient, this);

            trackingBtn.Visibility = ViewStates.Gone;
            trackingBtn.Enabled = false;
            introText.Visibility = ViewStates.Visible;
            if (googleApiClient.IsConnected)
            {
                googleApiClient.Disconnect();
                debugLog("Disconnecting");
                introText.Text = "Disconnecting";
            }
        }

		public void OnConnected (Bundle bundle)
		{
            debugLog("Connection established");
            WearableClass.DataApi.AddListener(googleApiClient, this);
            //await WearableClass.MessageApi.AddListenerAsync (googleApiClient, this);
            //await WearableClass.NodeApi.AddListenerAsync (googleApiClient, this);

            introText.Visibility = ViewStates.Gone;
            trackingBtn.Visibility = ViewStates.Visible;
            trackingBtn.Enabled = true;
            SendFunMessageBtn.Enabled = true;
        }

		public void OnConnectionSuspended (int p0)
		{
            debugLog("Connection suspended");
            WearableClass.DataApi.RemoveListener(googleApiClient, this);

            SendFunMessageBtn.Enabled = false;
            trackingBtn.Visibility = ViewStates.Gone;
            trackingBtn.Enabled = false;
            introText.Visibility = ViewStates.Visible;
            introText.Text = "Connection Suspended";
            
        }

		public void OnConnectionFailed (Android.Gms.Common.ConnectionResult result)
		{
            debugLog("Connection failed");
            SendFunMessageBtn.Enabled = false;
            trackingBtn.Visibility = ViewStates.Gone;
            trackingBtn.Enabled = false;
            introText.Visibility = ViewStates.Visible;
            introText.Text = "Connection Failed";
        }

        public void OnDataChanged(DataEventBuffer dataEvents)
        {
            debugLog("Data changed");

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
        /// Sends an RPC to start a fullscreen Activity on the wearable
        /// </summary>
        /// <param name="view"></param>
        [Export("onSendMessageBtnClick")]
        public void onSendMessageBtnClick(View view)
        {

            
            //debugLog("Send Message clicked");
            //// Trigger an AsyncTask that will query for a list of connected noded and send a "fun" message to each connecte node
            //var task = new SendMessageTask() { Activity = this };
            //task.Execute();
            SendData("This is a fun message",FunMessagePath);
        }

        //class SendMessageTask : AsyncTask
        //{
        //    public MainActivity Activity;
        //    protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
        //    {
        //        Log.Info("HH_TEST","Trying to Send Message");
        //        if (Activity != null)
        //        {
        //            Log.Info("HH_TEST", "Activity was not null");
        //            var nodes = Activity.Nodes;
        //            foreach (var node in nodes)
        //            {
        //                Activity.SendFunMessage(node);
        //            }
        //        }
        //        else
        //        {
        //            Log.Info("HH_TEST", "Activity was null");
        //        }
        //        return null;
        //    }
        //}

        /// <summary>
        /// Sends an RPC to start a fullscreen Activity on the wearable
        /// </summary>
        /// <param name="view"></param>
        [Export("onStartTracking")]
        public void onStartTracking(View view)
        {
            debugLog("Start tracking clicked");
            //onSendDatapoint(HeartDataType.StepCount, 10);

            startSensorTracking();

            DateTime dateToSave = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);
            string dateString = dateToSave.ToString("o");
            string message;
            message = HeartDataType.StepCount.ToString("G") + ";" + "10" + ";" + dateString;
            SendData(message, DataPointPath);
        }


        //public void onSendDatapoint(HeartDataType dataType, int value)
        //{
        //    debugLog("Sending datapoint, values: Type: " + dataType.ToString("G") + ", Numbervalue: " + value.ToString());
        //    // Trigger an AsyncTask that will query for a list of connected noded and send a "fun" message to each connecte node
        //    var task = new SendDatapointTask() { Activity = this, heartDataType = dataType, numbervalue = value };
        //    task.Execute();

        //}

        //class SendDatapointTask : AsyncTask
        //{
        //    public MainActivity Activity;
        //    public HeartDataType heartDataType;
        //    public int numbervalue;

        //    protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
        //    {
        //        if (Activity != null)
        //        {
        //            var nodes = Activity.Nodes;
        //            foreach (var node in nodes)
        //            {
        //                Activity.SendDataPointMessage(node, heartDataType, numbervalue);
        //            }
        //        }
        //        return null;
        //    }
        //}


        public enum HeartDataType
        {
            None,
            HeartBeat,
            HeartRate,
            StepCount//test type for quick data
        }

        //async Task SendDataPointMessage(string node, HeartDataType dataType, int number)
        //{
        //    debugLog("Sending datapoint: Type: " + dataType.ToString("G") + ", Value: " + number.ToString());
        //    string message = "";
        //    DateTime dateToSave = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);
        //    string dateString = dateToSave.ToString("o");
        //    string typeString = dataType.ToString("G");
        //    string numberString = number.ToString();
        //    message = typeString + ";" + numberString + ";" + dateString;


        //    var bytes = Encoding.Default.GetBytes(message);
        //    var res = await WearableClass.MessageApi.SendMessageAsync(googleApiClient, node, DataPointPath, bytes);
        //    if (!res.Status.IsSuccess)
        //    {
        //        debugLog("Failed to send datapoint message");
        //        Log.Error(Tag, "Failed to send message with status code: " + res.Status.StatusCode);

        //    }
        //    else
        //    {
        //        debugLog("Successfully sent datapoint message");
        //    }

        //}

        


        //async Task SendFunMessage(String node)
        //{
        //    var bytes = Encoding.Default.GetBytes("This is a fun message");
        //    var res = await WearableClass.MessageApi.SendMessageAsync(googleApiClient, node, FunMessagePath, bytes);
        //    if (!res.Status.IsSuccess)
        //    {
        //        Log.Error(Tag, "Failed to send message with status code: " + res.Status.StatusCode);
        //        debugLog("Failed to send Fun message");
        //    }
        //    else
        //    {
        //        debugLog("Fun message sent");
        //    }
        //    trackingBtn.Visibility = ViewStates.Visible;
        //    trackingBtn.Enabled = true;
        //    introText.Visibility = ViewStates.Gone;
        //}

        //ICollection<string> Nodes
        //{
        //    get
        //    {
        //        HashSet<string> results = new HashSet<string>();
        //        var nodes = WearableClass.NodeApi.GetConnectedNodesAsync(googleApiClient).Result;

        //        foreach (var node in nodes.Nodes)
        //        {
        //            results.Add(node.Id);
        //        }
        //        return results;
        //    }
        //}


        public void OnMessageReceived (IMessageEvent ev)
		{
            debugLog("Message received");
            //DataLayerListenerService.LOGD(Tag, "OnMessageReceived: " + ev);

            if (ev.Path.Equals(FunMessagePath)){
                debugLog("Path Matched FunMessage");
                trackingBtn.Visibility = ViewStates.Gone;
                trackingBtn.Enabled = false;
                introText.Visibility = ViewStates.Visible;
                introText.Text = "Fun message received";
            }
            else
            {
                debugLog("No match found for message path");
                //GenerateEvent("Message", ev.ToString());
            }
        }

		public void OnPeerConnected (INode node)
		{
            debugLog("Peer connected");
            //GenerateEvent ("Node Connected", node.Id);
            trackingBtn.Visibility = ViewStates.Visible;
            trackingBtn.Enabled = true;
            introText.Visibility = ViewStates.Gone;
            //introText.Text = "Connection Failed";
        }

		public void OnPeerDisconnected (INode node)
		{
            debugLog("Peer disconnected");
            //GenerateEvent ("Node disonnected", node.Id);
            trackingBtn.Visibility = ViewStates.Gone;
            trackingBtn.Enabled = false;
            introText.Visibility = ViewStates.Visible;
            introText.Text = "Disconnected";
        }

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
                }
                else if (heartBeatsensor != null && sensor.Type == heartBeatsensor.Type)
                {
                    debugLog("Accuracy changed for heartbeat sensor");
                }
                else if (heartRatesensor != null && sensor.Type == heartRatesensor.Type)
                {
                    debugLog("Accuracy changed for heart rate sensor");
                }
            }
        }

        
        public void OnSensorChanged(SensorEvent e)
        {
            debugLog("Sensor Changed");
            if (e.Sensor != null)
            {
                debugLog("Sensor changed was: " + e.Sensor.Type);
                //e.Values[0]
                if (stepCounter != null && e.Sensor.Type == stepCounter.Type)
                {
                    debugLog("Sensor change match for stepcounter");
                    dataPoints.Enqueue(new HeartDataPoint(HeartDataType.StepCount, (int) e.Values[0],DateTime.Now));
                }
                else if (heartBeatsensor != null && e.Sensor.Type == heartBeatsensor.Type)
                {
                    debugLog("Sensor change match for heartbeat sensor");
                    dataPoints.Enqueue(new HeartDataPoint(HeartDataType.HeartBeat, (int)e.Values[0], DateTime.Now));
                }
                else if (heartRatesensor != null && e.Sensor.Type == heartRatesensor.Type)
                {
                    debugLog("Sensor change match for heart rate sensor");
                    dataPoints.Enqueue(new HeartDataPoint(HeartDataType.HeartRate, (int)e.Values[0], DateTime.Now));
                }

                Log.Info("HH_TEST", "Datapoints Count: " + dataPoints.Count);
                debugLog("Amount of datapoints queued: " + dataPoints.Count);
                if (dataPoints.Count > 0)
                {
                    debugLog("Trying to send data");
                    trySendData();
                }

                debugLog("Printing available values next: ");
                foreach (float val in e.Values)
                {
                    Log.Info("HH_TEST", "Type: "+ e.Sensor.Type + ", Float value: " + val.ToString());
                }
                debugLog("Printed values, END");

            }
        }

        private void trySendData()
        {
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
                message += typeString + ";" + numberString + ";" + dateString;

                if (i < 100 - 1 && dataPoints.Count > 0)
                {
                    message += "|";
                }
            }


            if (message != null && message != "")
            {
                debugLog("Sending multiple datapoints");
                //var task = new SendMultipleDatapointsTask() { Activity = this, dataString = message, backup = backupList};
                //task.Execute();

                SendData(message, DataPointsPath);
            }
            else
            {
                debugLog("Sending multiple datapoints failed, message was null or blank");
            }
            

        }

        //Alternate universe data sending START

        public void SendData(string data, string path)
        {
            try
            {
                var request = PutDataMapRequest.Create(path);
                var map = request.DataMap;
                map.PutString("Message", data);
                map.PutLong("UpdatedAt", DateTime.UtcNow.Ticks);
                WearableClass.DataApi.PutDataItem(googleApiClient, request.AsPutDataRequest());
            }
            finally
            {
                //_client.Disconnect();
            }

        }

        //Alternate universe data sending END

        //class SendMultipleDatapointsTask : AsyncTask
        //{
        //    public MainActivity Activity;
        //    public string dataString;
        //    public Queue<HeartDataPoint> backup;
        //    protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
        //    {
        //        if (Activity != null)
        //        {
        //            var nodes = Activity.Nodes;
        //            foreach (var node in nodes)
        //            {
        //                if (dataString != null && dataString != "")
        //                {
                            
        //                    Log.Info("HH_TEST", "Valid datastring(So sending now): " + dataString);
        //                    Activity.SendDataPointsMessage(node, dataString, backup);
        //                }
        //                else
        //                {
        //                    Log.Info("HH_TEST", "Invalid datastring(So not sending): " + dataString);
        //                }
                        
        //            }
        //        }
        //        return null;
        //    }
        //}


        //async Task SendDataPointsMessage(string node, string text, Queue<HeartDataPoint> backup)
        //{

        //    var bytes = Encoding.Default.GetBytes(text);
        //    var res = await WearableClass.MessageApi.SendMessageAsync(googleApiClient, node, DataPointsPath, bytes);
        //    if (!res.Status.IsSuccess)
        //    {
        //        Log.Error(Tag, "Failed to send message with status code: " + res.Status.StatusCode);
        //        debugLog("Failed to send message, re-adding backup to queue");
        //        //re enqueue
        //        while (backup.Count > 0)
        //        {
        //            dataPoints.Enqueue(backup.Dequeue());
        //        }
        //        backup.Clear();
        //        debugLog("Backup added and cleared");
        //    }

        //}

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

        
    }
}


