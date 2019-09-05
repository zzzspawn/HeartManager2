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
        protected override void OnResume()
        {
            base.OnResume();
            debugLog("App Resumed");
            if (!googleApiClient.IsConnected && !googleApiClient.IsConnecting)
            {
                googleApiClient.Connect();
                introText.Text = "Connecting";
                debugLog("Connecting");
                WearableClass.DataApi.AddListener(googleApiClient, this);
            }
            introText.Visibility = ViewStates.Visible;
        }
        protected override void OnPause()
        {
            base.OnPause();
            debugLog("Pausing App");

            introText.Visibility = ViewStates.Visible;
            if (googleApiClient.IsConnected)
            {
                googleApiClient.Disconnect();
                debugLog("Disconnecting");
                introText.Text = "Disconnecting";
            }
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
        private void stopSensorTracking()
        {
            if (heartRatesensor != null)
            {
                sensorManager.UnregisterListener(this);
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
        
        public void OnConnected (Bundle bundle)
		{
            debugLog("Connection established");
            WearableClass.DataApi.AddListener(googleApiClient, this);
            introText.Visibility = ViewStates.Gone;
            SendFunMessageBtn.Enabled = true;
        }
        public void OnConnectionSuspended (int p0)
		{
            debugLog("Connection suspended");
            WearableClass.DataApi.RemoveListener(googleApiClient, this);

            SendFunMessageBtn.Enabled = false;
            introText.Visibility = ViewStates.Visible;
            introText.Text = "Connection Suspended";
            
        }
        public void OnConnectionFailed (Android.Gms.Common.ConnectionResult result)
		{
            debugLog("Connection failed");
            WearableClass.DataApi.RemoveListener(googleApiClient, this);
            SendFunMessageBtn.Enabled = false;
            introText.Visibility = ViewStates.Visible;
            introText.Text = "Connection Failed";
        }
        public void OnPeerConnected(INode node)
        {
            debugLog("Peer connected");
            introText.Visibility = ViewStates.Gone;
        }
        public void OnPeerDisconnected(INode node)
        {
            debugLog("Peer disconnected");
            introText.Visibility = ViewStates.Visible;
            introText.Text = "Disconnected";
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
            SendData("This is a fun message",FunMessagePath);
        }

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

            //DateTime dateToSave = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);
            //string dateString = dateToSave.ToString("o");
            //string message;
            //message = HeartDataType.StepCount.ToString("G") + ";" + "10" + ";" + dateString;
            //SendData(message, DataPointPath);
        }

        /// <summary>
        /// Sends an RPC to start a fullscreen Activity on the wearable
        /// </summary>
        /// <param name="view"></param>
        [Export("onStopTracking")]
        public void onStopTracking(View view)
        {
            debugLog("Start tracking clicked");
            stopSensorTracking();
        }

        public enum HeartDataType
        {
            None,
            HeartBeat,
            HeartRate,
            StepCount//test type for quick data
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
        public void OnMessageReceived(IMessageEvent ev)
        {
            debugLog("Message received(This shouldn't happen anymore)");
        }
    }
}


