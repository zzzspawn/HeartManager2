using System;

using Android.App;
using Android.Content;
using Android.Runtime;
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
using System.Text;
using Android.Util;
using Android.Content.PM;
using System.Threading.Tasks;
using Android;
using Java.Interop;
using Android.Hardware;
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

        private Queue<HeartDataPoint> dataPoints;

        const string FunMessagePath = "/fun-message";
        const string DataPointPath = "/data-point";
        const string DataPointsPath = "/data-points";
        private int BODYSENSOR_CODE = 123;
        protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			handler = new Handler ();
			//DataLayerListenerService.LOGD (Tag, "OnCreate");
			SetContentView (Resource.Layout.main_activity);
			Window.AddFlags (WindowManagerFlags.KeepScreenOn);

            dataPoints = new Queue<HeartDataPoint>();

            if (!checkPermissions())
            {
                ActivityCompat.RequestPermissions(this, new String[] { Manifest.Permission.BodySensors }, BODYSENSOR_CODE);
            }


			introText = (TextView)FindViewById (Resource.Id.intro);
			layout = FindViewById (Resource.Id.layout);
            SendFunMessageBtn = FindViewById(Resource.Id.sendMessageBtn);
            trackingBtn = (Button) FindViewById(Resource.Id.trackingbutton);

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

            googleApiClient = new GoogleApiClient.Builder (this)
				.AddApi (WearableClass.API)
				.AddConnectionCallbacks (this)
				.AddOnConnectionFailedListener (this)
				.Build ();
		}

        private bool checkPermissions()
        {
            return ContextCompat.CheckSelfPermission(this, Manifest.Permission.BodySensors) == (int) Permission.Granted;
        }

		protected override void OnResume ()
		{
			base.OnResume ();
			googleApiClient.Connect ();
            trackingBtn.Visibility = ViewStates.Gone;
            trackingBtn.Enabled = false;
            introText.Visibility = ViewStates.Visible;
            introText.Text = "Connecting";
            
        }

		protected override async void OnPause ()
		{
			base.OnPause ();
			await WearableClass.DataApi.RemoveListenerAsync (googleApiClient, this);
            await WearableClass.MessageApi.RemoveListenerAsync (googleApiClient, this);
            await WearableClass.NodeApi.RemoveListenerAsync (googleApiClient, this);
			googleApiClient.Disconnect ();

            trackingBtn.Visibility = ViewStates.Gone;
            trackingBtn.Enabled = false;
            introText.Visibility = ViewStates.Visible;
            introText.Text = "Disconnecting";
        }

		public async void OnConnected (Bundle bundle)
		{
            //DataLayerListenerService.LOGD (Tag, "OnConnected(): Successfully connected to Google API client");
            SendFunMessageBtn.Enabled = true;
            await WearableClass.DataApi.AddListenerAsync (googleApiClient, this);
			await WearableClass.MessageApi.AddListenerAsync (googleApiClient, this);
			await WearableClass.NodeApi.AddListenerAsync (googleApiClient, this);

            introText.Visibility = ViewStates.Gone;
            trackingBtn.Visibility = ViewStates.Visible;
            trackingBtn.Enabled = true;
        }

		public void OnConnectionSuspended (int p0)
		{
			//DataLayerListenerService.LOGD (Tag, "OnConnectionSuspended(): Connection to Google API client was suspended");
            SendFunMessageBtn.Enabled = false;

            trackingBtn.Visibility = ViewStates.Gone;
            trackingBtn.Enabled = false;
            introText.Visibility = ViewStates.Visible;
            introText.Text = "Connection Suspended";
        }

		public void OnConnectionFailed (Android.Gms.Common.ConnectionResult result)
		{
			//DataLayerListenerService.LOGD (Tag, "OnConnectionFailed(): Failed to connect, with result: " + result);
            SendFunMessageBtn.Enabled = false;


            trackingBtn.Visibility = ViewStates.Gone;
            trackingBtn.Enabled = false;
            introText.Visibility = ViewStates.Visible;
            introText.Text = "Connection Failed";
        }

		void GenerateEvent(string title, string text)
		{
			//RunOnUiThread (() => {
			//	//introText.Visibility = ViewStates.Invisible;
			//	//dataItemListAdapter.Add(new Event(title, text));
			//});
		}

		public async void OnDataChanged (DataEventBuffer dataEvents)
		{
			//DataLayerListenerService.LOGD (Tag, "OnDatachanged() : " + dataEvents);

			//IList events = FreezableUtils.FreezeIterable (dataEvents);
			//dataEvents.Release();
			//foreach (var ev in events) {
			//	var e = Extensions.JavaCast<IDataEvent> (((Java.Lang.Object)ev));
			//	if (e.Type == DataEvent.TypeChanged) {
			//		String path = e.DataItem.Uri.Path;
   //                 if (DataLayerListenerService.CountPath.Equals (path)) {
			//			DataLayerListenerService.LOGD (Tag, "Data Changed for CountPath");
			//			GenerateEvent ("DataItem Changed", e.DataItem.ToString ());
			//		} else {
			//			DataLayerListenerService.LOGD (Tag, "Unrecognized path: " + path);
			//		}
			//	} else if (e.Type == DataEvent.TypeDeleted) {
			//		GenerateEvent ("DataItem Changed", e.DataItem.ToString ());
			//	} else {
			//		DataLayerListenerService.LOGD ("Unknown data event type", "Type = " + e.Type);
			//	}
			//}
		}


        /// <summary>
        /// Sends an RPC to start a fullscreen Activity on the wearable
        /// </summary>
        /// <param name="view"></param>
        [Export("onSendMessageBtnClick")]
        public void onSendMessageBtnClick(View view)
        {
            // Trigger an AsyncTask that will query for a list of connected noded and send a "fun" message to each connecte node
            var task = new SendMessageTask() { Activity = this };
            task.Execute();
        }

        class SendMessageTask : AsyncTask
        {
            public MainActivity Activity;
            protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
            {
                if (Activity != null)
                {
                    var nodes = Activity.Nodes;
                    foreach (var node in nodes)
                    {
                        Activity.SendFunMessage(node);
                    }
                }
                return null;
            }
        }


        /// <summary>
        /// Sends an RPC to start a fullscreen Activity on the wearable
        /// </summary>
        /// <param name="view"></param>
        [Export("onStartTracking")]
        public void onStartTracking(View view)
        {
            onSendDatapoint(HeartDataType.StepCount, 10);
        }


        public void onSendDatapoint(HeartDataType dataType, int value)
        {
            // Trigger an AsyncTask that will query for a list of connected noded and send a "fun" message to each connecte node
            var task = new SendDatapointTask() { Activity = this, heartDataType = dataType, numbervalue = value };
            task.Execute();

        }

        class SendDatapointTask : AsyncTask
        {
            public MainActivity Activity;
            public HeartDataType heartDataType;
            public int numbervalue;

            protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
            {
                if (Activity != null)
                {
                    var nodes = Activity.Nodes;
                    foreach (var node in nodes)
                    {
                        Activity.SendDataPointMessage(node, heartDataType, numbervalue);
                    }
                }
                return null;
            }
        }


        public enum HeartDataType
        {
            None,
            HeartBeat,
            HeartRate,
            StepCount//test type for quick data
        }

        async Task SendDataPointMessage(string node, HeartDataType dataType, int number)
        {
            string message = "";
            DateTime dateToSave = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);
            string dateString = dateToSave.ToString("o");
            string typeString = dataType.ToString("G");
            string numberString = number.ToString();
            message = typeString + ";" + numberString + ";" + dateString;


            var bytes = Encoding.Default.GetBytes(message);
            var res = await WearableClass.MessageApi.SendMessageAsync(googleApiClient, node, DataPointPath, bytes);
            if (!res.Status.IsSuccess)
            {
                Log.Error(Tag, "Failed to send message with status code: " + res.Status.StatusCode);

            }

        }

        


        async Task SendFunMessage(String node)
        {
            var bytes = Encoding.Default.GetBytes("This is a fun message");
            var res = await WearableClass.MessageApi.SendMessageAsync(googleApiClient, node, FunMessagePath, bytes);
            if (!res.Status.IsSuccess)
            {
                Log.Error(Tag, "Failed to send message with status code: " + res.Status.StatusCode);
            }
            trackingBtn.Visibility = ViewStates.Visible;
            trackingBtn.Enabled = true;
            introText.Visibility = ViewStates.Gone;
        }

        ICollection<string> Nodes
        {
            get
            {
                HashSet<string> results = new HashSet<string>();
                var nodes = WearableClass.NodeApi.GetConnectedNodesAsync(googleApiClient).Result;

                foreach (var node in nodes.Nodes)
                {
                    results.Add(node.Id);
                }
                return results;
            }
        }


        public void OnMessageReceived (IMessageEvent ev)
		{
            //DataLayerListenerService.LOGD(Tag, "OnMessageReceived: " + ev);

            if (ev.Path.Equals(FunMessagePath)){
                trackingBtn.Visibility = ViewStates.Gone;
                trackingBtn.Enabled = false;
                introText.Visibility = ViewStates.Visible;
                introText.Text = "Fun message received";
            }
            else
            {
                //GenerateEvent("Message", ev.ToString());
            }
        }

		public void OnPeerConnected (INode node)
		{
			//GenerateEvent ("Node Connected", node.Id);
            trackingBtn.Visibility = ViewStates.Visible;
            trackingBtn.Enabled = true;
            introText.Visibility = ViewStates.Gone;
            //introText.Text = "Connection Failed";
        }

		public void OnPeerDisconnected (INode node)
		{
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
                Log.Debug("HH_TEST", "Accuracy changed for: " + sensor.Type);
                //textView.Text = "Accuracy changed, sensor was: " + sensor.Type.ToString();

                if (stepCounter != null && sensor.Type == stepCounter.Type)
                {
                    
                }
                else if (heartBeatsensor != null && sensor.Type == heartBeatsensor.Type)
                {
                    
                }
                else if (heartRatesensor != null && sensor.Type == heartRatesensor.Type)
                {
                    
                }
            }
        }

        
        public void OnSensorChanged(SensorEvent e)
        {
            if (e.Sensor != null)
            {
                //e.Values[0]
                if (stepCounter != null && e.Sensor.Type == stepCounter.Type)
                {
                    dataPoints.Enqueue(new HeartDataPoint(HeartDataType.StepCount, (int) e.Values[0],DateTime.Now));
                }
                else if (heartBeatsensor != null && e.Sensor.Type == heartBeatsensor.Type)
                {
                    dataPoints.Enqueue(new HeartDataPoint(HeartDataType.HeartBeat, (int)e.Values[0], DateTime.Now));
                }
                else if (heartRatesensor != null && e.Sensor.Type == heartRatesensor.Type)
                {
                    dataPoints.Enqueue(new HeartDataPoint(HeartDataType.HeartRate, (int)e.Values[0], DateTime.Now));
                }

                Log.Info("HH_TEST", "Datapoints Count: " + dataPoints.Count);

                if (dataPoints.Count > 0)
                {
                    trySendData();
                }

                foreach (float val in e.Values)
                {
                    Log.Info("HH_TEST", "Type: "+ e.Sensor.Type + ", Float value: " + val.ToString());
                }

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

            var task = new SendMultipleDatapointsTask() { Activity = this, dataString = message, backup = backupList};
            task.Execute();

        }

        class SendMultipleDatapointsTask : AsyncTask
        {
            public MainActivity Activity;
            public string dataString;
            public Queue<HeartDataPoint> backup;
            protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] @params)
            {
                if (Activity != null)
                {
                    var nodes = Activity.Nodes;
                    foreach (var node in nodes)
                    {
                        if (dataString != null && dataString != "")
                        {
                            Log.Info("HH_TEST", "Valid datastring: " + dataString);
                            Activity.SendDataPointsMessage(node, dataString, backup);
                        }
                        else
                        {
                            Log.Info("HH_TEST", "Invalid datastring: " + dataString);
                        }
                        
                    }
                }
                return null;
            }
        }


        async Task SendDataPointsMessage(string node, string text, Queue<HeartDataPoint> backup)
        {

            var bytes = Encoding.Default.GetBytes(text);
            var res = await WearableClass.MessageApi.SendMessageAsync(googleApiClient, node, DataPointsPath, bytes);
            if (!res.Status.IsSuccess)
            {
                Log.Error(Tag, "Failed to send message with status code: " + res.Status.StatusCode);
                Log.Info("HH_TEST", "Failed to send message, re-adding backup");
                //re enqueue
                while (backup.Count > 0)
                {
                    dataPoints.Enqueue(backup.Dequeue());
                }
                backup.Clear();
                Log.Info("HH_TEST", "Backup added and cleared");
            }

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



        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            if (requestCode == BODYSENSOR_CODE)
            {
                // Received permission result for camera permission.
                Log.Info("HH_Info", "Received response for Body sensor permission request.");

                // Check if the only required permission has been granted
                if ((grantResults.Length == 1) && (grantResults[0] == Permission.Granted))
                {
                    // Body sensor permission has been granted, okay to retrieve the Sensor data of the device.
                    Log.Info("HH_Info", "Body sensor permission has now been granted.");
                    Snackbar.Make(layout, "Permission to see sensors granted", Snackbar.LengthShort).Show();
                }
                else
                {
                    Log.Info("HH_Info", "Body sensor permission was NOT granted.");
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


