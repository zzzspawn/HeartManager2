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
using System.Text;
using Android.Util;
using Android.Content.PM;
using System.Threading.Tasks;
using Java.Sql;

namespace DataLayer
{
	/// <summary>
	/// Receives its own events using a listener API designed for foreground activities. Updates a data item every second while it is open.
	/// Also allows user to take a photo and send that as an asset to the paired wearable.
	/// </summary>
	[Activity (MainLauncher = true, Label="@string/app_name", LaunchMode = LaunchMode.SingleTask, Icon = "@drawable/ic_launcher")]
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

        GoogleApiClient mGoogleApiClient;
		bool mResolvingError = false;

        //ListView dataItemList;
        View startActivityBtn;
        View SendFunMessageBtn;
        private TextView statusTextView;

        //private DataItemAdapter dataItemListAdapter;
        private Handler handler;

        private List<HeartDataPoint> hdata_Rate;
        private List<HeartDataPoint> hdata_Beat;
        private List<HeartDataPoint> hdata_Steps;


        protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			handler = new Handler ();
			LOGD (Tag, "OnCreate");
            SetContentView (Resource.Layout.main_activity);
			SetupViews ();
            SetupLists();

			// Stores DataItems received by the local broadcaster of from the paired watch
			//dataItemListAdapter = new DataItemAdapter (this, Android.Resource.Layout.SimpleListItem1);
			//dataItemList.Adapter = dataItemListAdapter;

            mGoogleApiClient = new GoogleApiClient.Builder (this)
				.AddApi (WearableClass.API)
				.AddConnectionCallbacks (this)
				.AddOnConnectionFailedListener (this)
				.Build ();
		}

		protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
		{
			if (resultCode == Result.Ok) {
				Bundle extras = data.Extras;
            }
		}

		protected override void OnStart ()
		{
			base.OnStart ();
			if (!mResolvingError) {
				mGoogleApiClient.Connect ();
                statusTextView.Text = "Connecting";
            }
		}

		protected override void OnResume ()
		{
			base.OnResume ();
            if (!mResolvingError)
            {
                mGoogleApiClient.Connect();
                statusTextView.Text = "Connecting";
            }
        }

		protected override async void OnPause ()
		{
			base.OnPause ();
            if (!mResolvingError)
            {
                await WearableClass.DataApi.RemoveListenerAsync(mGoogleApiClient, this);
                await WearableClass.MessageApi.RemoveListenerAsync(mGoogleApiClient, this);
                await WearableClass.NodeApi.RemoveListenerAsync(mGoogleApiClient, this);
                mGoogleApiClient.Disconnect();
                statusTextView.Text = "Disconnecting";
            }
        }

		protected override async void OnStop ()
		{
			base.OnStop ();
			if (!mResolvingError) {
				await WearableClass.DataApi.RemoveListenerAsync (mGoogleApiClient, this);
                await WearableClass.MessageApi.RemoveListenerAsync (mGoogleApiClient, this);
                await WearableClass.NodeApi.RemoveListenerAsync (mGoogleApiClient, this);
				mGoogleApiClient.Disconnect ();
                statusTextView.Text = "Disconnecting";
            }
		}

		public async void OnConnected (Bundle connectionHint)
		{
			LOGD (Tag, "Google API CLient was connected");
			mResolvingError = false;
			startActivityBtn.Enabled = true;
            SendFunMessageBtn.Enabled = true;
            await WearableClass.DataApi.AddListenerAsync (mGoogleApiClient, this);
			await WearableClass.MessageApi.AddListenerAsync (mGoogleApiClient, this);
			await WearableClass.NodeApi.AddListenerAsync (mGoogleApiClient, this);
            statusTextView.Text = "Connected";
        }

		public void OnConnectionSuspended (int cause)
		{
			LOGD (Tag, "Connection to Google API client was suspended");
			startActivityBtn.Enabled = false;
            SendFunMessageBtn.Enabled = false;
            statusTextView.Text = "Connection Suspended";
        }

		public async void OnConnectionFailed (Android.Gms.Common.ConnectionResult result)
		{
			if (mResolvingError) {
				// Already attempting to resolve an error
				return;
			} else if (result.HasResolution) {
				try {
					mResolvingError = true;
					result.StartResolutionForResult(this, RequestResolveError);
				} catch (IntentSender.SendIntentException e) {
					// There was an error with the resolution intent. Try again.
					mGoogleApiClient.Connect ();
                    statusTextView.Text = "Connecting";
                } 
			} else {
				Log.Error(Tag, "Connection to Google API client has failed");
				mResolvingError = false;
				startActivityBtn.Enabled = false;
                SendFunMessageBtn.Enabled = false;
                await WearableClass.DataApi.RemoveListenerAsync (mGoogleApiClient, this);
				await WearableClass.MessageApi.RemoveListenerAsync (mGoogleApiClient, this);
				await WearableClass.NodeApi.RemoveListenerAsync (mGoogleApiClient, this);
                statusTextView.Text = "Connection Failed";
            }
		}


        public void OnDataChanged(DataEventBuffer dataEvents)
        {
            LOGD(Tag, "OnDataChanged: " + dataEvents);
            var events = new List<IDataEvent>();
            events.AddRange(dataEvents);
            RunOnUiThread(() =>
            {
                foreach (var ev in events)
                {
                    if (ev.Type == DataEvent.TypeChanged)
                    {
                        //dataItemListAdapter.Add(
                        //    new Event("DataItem Changed", ev.DataItem.ToString()));
                    }
                    else if (ev.Type == DataEvent.TypeDeleted)
                    {
                        //dataItemListAdapter.Add(
                        //    new Event("DataItem Deleted", ev.DataItem.ToString()));
                    }
                }
            });
        }
        


        public void OnMessageReceived (IMessageEvent messageEvent)
		{
            Log.Info("HH_TEST", "Message Event");
            //LOGD (Tag, "OnMessageReceived() A message from the watch was received: " + messageEvent.RequestId + " " + messageEvent.Path);
            handler.Post( () => {
                if (messageEvent.Path == FunMessagePath)
                {
                    //dataItemListAdapter.Add(new Event("Manual message from watch", messageEvent.ToString()));
                    var buffer = messageEvent.GetData();
                    string initialMessage = Encoding.Default.GetString(buffer, 0, buffer.Length);
                    RunOnUiThread(() =>
                    {
                        statusTextView.Text = initialMessage;
                        Log.Info("HH_TEST", "Fun Message received, contained: " + initialMessage);
                    });
                    
                }
                else if (messageEvent.Path == DataPointPath)
                {
                    Log.Info("HH_TEST", "One Datapoint received");
                    //dataItemListAdapter.Add(new Event("Manual message from watch", messageEvent.ToString()));
                    var buffer = messageEvent.GetData();
                    string initialMessage = Encoding.Default.GetString(buffer, 0, buffer.Length);
                    HeartDataPoint datapoint = decodeDataPoint(initialMessage);


                        if (datapoint != null)
                        {
                            RunOnUiThread(() =>
                            {
                                statusTextView.Text = "Data received(" + datapoint.heartType + ", " + datapoint.amount + ", " + datapoint.timestamp + ").";
                            });

                        }
                        else
                        {
                            RunOnUiThread(() => {
                                statusTextView.Text = "Invalid data received";
                            });
                        }
                        
                    
                }else if (messageEvent.Path == DataPointsPath)
                {
                    Log.Info("HH_TEST", "Multiple Messages received");
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
                    }

                    if (teller > 0)
                    {
                        RunOnUiThread(() =>
                        {
                            statusTextView.Text = "Data received(Amount: " +teller+ ").";
                        });

                    }
                    else
                    {
                        RunOnUiThread(() =>
                        {
                            statusTextView.Text = "No valid data received.";
                        });
                    }

                }
                else
                {
                    //dataItemListAdapter.Add(new Event("Message from watch", messageEvent.ToString()));
                    //do nothing
                    Log.Info("HH_TEST", "No Match for path");
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
                //dataItemListAdapter.Add(new Event("Connected", peer.ToString()));
                RunOnUiThread(() =>
                {
                    statusTextView.Text = "Peer Connected";
                });

            });
		}

		public void OnPeerDisconnected (INode peer)
		{
			LOGD (Tag, "OnPeerDisconnected: " + peer);
			handler.Post (() => {
                //dataItemListAdapter.Add(new Event("Disconnected", peer.ToString()));
                RunOnUiThread(() =>
                {
                    statusTextView.Text = "Peer Disconnected";
                });
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
            var res = await WearableClass.MessageApi.SendMessageAsync (mGoogleApiClient, node, StartActivityPath, new byte[0]);
    		if (!res.Status.IsSuccess) {
				Log.Error(Tag, "Failed to send message with status code: " + res.Status.StatusCode);
                RunOnUiThread(() =>
                {
                    statusTextView.Text = "Failed to send signal";
                });
            }
            else
            {
                RunOnUiThread(() =>
                {
                    statusTextView.Text = "Start signal sent";
                });
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
        [Export("onStartWearableActivityClick")]
		public void OnStartWearableActivityClick(View view) {
			LOGD (Tag, "Generating RPC");

			// Trigger an AsyncTask that will query for a list of connected nodes and send a "start-activity" message to each connected node
			var task = new StartWearableActivityTask () { Activity = this };
			task.Execute ();
		}
        /// <summary>
        /// Sends a fixed message to watch
        /// </summary>
        /// <param name="view"></param>
        [Export("onSendMessageBtnClick")]
        public void onSendMessageBtnClick(View view)
        {
            LOGD(Tag, "Sending Fun message");
            // Trigger an AsyncTask that will query for a list of connected nodes and send a "fun" message to each connected node
            var task = new SendMessageTask() { Activity = this };
            task.Execute();

        }

        async Task SendFunMessage(String node)
        {
            var bytes = Encoding.Default.GetBytes("This is a fun message");
            var res = await WearableClass.MessageApi.SendMessageAsync(mGoogleApiClient, node, FunMessagePath, new byte[0]);
            if (!res.Status.IsSuccess)
            {
                Log.Error(Tag, "Failed to send message with status code: " + res.Status.StatusCode);

            }
        }



        /// <summary>
        /// Generates a DataItem based on an incrementing count
        /// </summary>
        private class DataItemGenerator : Java.Lang.Object, Java.Lang.IRunnable 
		{
			int count = 0;
			public MainActivity Activity;
			public async void Run ()
			{
				if (Activity != null) {
					var putDataMapRequest = PutDataMapRequest.Create (CountPath);
					putDataMapRequest.DataMap.PutInt (CountKey, count++);
					var request = putDataMapRequest.AsPutDataRequest ();

					LOGD (Tag, "Generating DataItem: " + request);
					if (!Activity.mGoogleApiClient.IsConnected)
						return;
                    var res = await WearableClass.DataApi.PutDataItemAsync (Activity.mGoogleApiClient, request);
					if (!res.Status.IsSuccess) {
						Log.Error(Tag, "Failed to send message with status code: " + res.Status.StatusCode);
					}
				}
			}
		}

        

        /// <summary>
		/// Sets up UI components and their callback handlers
		/// </summary>
		void SetupViews() 
		{
            //dataItemList = (ListView)FindViewById (Resource.Id.data_item_list);

			startActivityBtn = FindViewById (Resource.Id.start_wearable_activity);

            SendFunMessageBtn = FindViewById(Resource.Id.SendMessageBtn);

            statusTextView = (TextView) FindViewById(Resource.Id.statusText);

        }

        void SetupLists()
        {
            hdata_Beat = new List<HeartDataPoint>();
            hdata_Rate = new List<HeartDataPoint>();
            hdata_Steps = new List<HeartDataPoint>();
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


