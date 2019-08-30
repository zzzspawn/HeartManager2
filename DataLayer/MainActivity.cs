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

		GoogleApiClient mGoogleApiClient;
		bool mResolvingError = false;

        ListView dataItemList;
        View startActivityBtn;
        View SendFunMessageBtn;

        private DataItemAdapter dataItemListAdapter;
		private Handler handler;
        

        protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			handler = new Handler ();
			LOGD (Tag, "OnCreate");
            SetContentView (Resource.Layout.main_activity);
			SetupViews ();

			// Stores DataItems received by the local broadcaster of from the paired watch
			dataItemListAdapter = new DataItemAdapter (this, Android.Resource.Layout.SimpleListItem1);
			dataItemList.Adapter = dataItemListAdapter;

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
			}
		}

		protected override void OnResume ()
		{
			base.OnResume ();
        }

		protected override void OnPause ()
		{
			base.OnPause ();
        }

		protected override async void OnStop ()
		{
			base.OnStop ();
			if (!mResolvingError) {
				await WearableClass.DataApi.RemoveListenerAsync (mGoogleApiClient, this);
                await WearableClass.MessageApi.RemoveListenerAsync (mGoogleApiClient, this);
                await WearableClass.NodeApi.RemoveListenerAsync (mGoogleApiClient, this);
				mGoogleApiClient.Disconnect ();
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
		}

		public void OnConnectionSuspended (int cause)
		{
			LOGD (Tag, "Connection to Google API client was suspended");
			startActivityBtn.Enabled = false;
            SendFunMessageBtn.Enabled = false;
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
				} 
			} else {
				Log.Error(Tag, "Connection to Google API client has failed");
				mResolvingError = false;
				startActivityBtn.Enabled = false;
                SendFunMessageBtn.Enabled = false;
                await WearableClass.DataApi.RemoveListenerAsync (mGoogleApiClient, this);
				await WearableClass.MessageApi.RemoveListenerAsync (mGoogleApiClient, this);
				await WearableClass.NodeApi.RemoveListenerAsync (mGoogleApiClient, this);
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
                        dataItemListAdapter.Add(
                            new Event("DataItem Changed", ev.DataItem.ToString()));
                    }
                    else if (ev.Type == DataEvent.TypeDeleted)
                    {
                        dataItemListAdapter.Add(
                            new Event("DataItem Deleted", ev.DataItem.ToString()));
                    }
                }
            });
        }
        //public void OnDataChanged(DataEventBuffer dataEvents)
        //{
        //    //do nothing
        //}


        public void OnMessageReceived (IMessageEvent messageEvent)
		{
			LOGD (Tag, "OnMessageReceived() A message from the watch was received: " + messageEvent.RequestId + " " + messageEvent.Path);
			handler.Post( () => {
                if (messageEvent.Path == FunMessagePath)
                {
                    dataItemListAdapter.Add(new Event("Manual message from watch", messageEvent.ToString()));
                }
                else
                {
                    //dataItemListAdapter.Add(new Event("Message from watch", messageEvent.ToString()));
                    //do nothing
                }
            });
		}

		public void OnPeerConnected (INode peer)
		{
			LOGD (Tag, "OnPeerConencted: " + peer);
			handler.Post (() => {
				dataItemListAdapter.Add(new Event("Connected", peer.ToString()));
			});
		}

		public void OnPeerDisconnected (INode peer)
		{
			LOGD (Tag, "OnPeerDisconnected: " + peer);
			handler.Post (() => {
				dataItemListAdapter.Add(new Event("Disconnected", peer.ToString()));
			});
		}

		/// <summary>
		/// A View Adapter for presenting the Event objects in a list
		/// </summary>
		private class DataItemAdapter : ArrayAdapter<Event> {
			private readonly Context mContext;

			public DataItemAdapter(Context context, int unusedResource) 
				:base(context, unusedResource) {
				mContext = context;
			}
			public override View GetView (int position, View convertView, ViewGroup parent)
			{
				ViewHolder holder;
				if (convertView == null) {
					holder = new ViewHolder ();
					LayoutInflater inflater = (LayoutInflater)mContext.GetSystemService (Context.LayoutInflaterService);
					convertView = inflater.Inflate (Android.Resource.Layout.TwoLineListItem, null);
					convertView.Tag = holder;
					holder.Text1 = (TextView)convertView.FindViewById (Android.Resource.Id.Text1);
					holder.Text2 = (TextView)convertView.FindViewById (Android.Resource.Id.Text2);
				} else {
					holder = (ViewHolder)convertView.Tag;
				}
				Event e = GetItem (position);
				holder.Text1.Text = e.Title;
				holder.Text2.Text = e.Text;
				return convertView;
			}
			private class ViewHolder : Java.Lang.Object {
				public TextView Text1;
				public TextView Text2;
			}
		}
		public class Event {
			public String Title, Text;

			public Event(String title, String text) {
				this.Title = title;
				this.Text = text;
			}
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
            dataItemList = (ListView)FindViewById (Resource.Id.data_item_list);

			startActivityBtn = FindViewById (Resource.Id.start_wearable_activity);

            SendFunMessageBtn = FindViewById(Resource.Id.SendMessageBtn);

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
            HeartBeat,
            HeartRate,
            Steps//test type for quick data
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


