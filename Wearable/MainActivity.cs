﻿using System;

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
using Java.Interop;

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
	IMessageApiMessageListener, INodeApiNodeListener
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
        const string FunMessagePath = "/fun-message";
        const string DataPointPath = "/data-point";
        protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			handler = new Handler ();
			DataLayerListenerService.LOGD (Tag, "OnCreate");
			SetContentView (Resource.Layout.main_activity);
			Window.AddFlags (WindowManagerFlags.KeepScreenOn);
			
			introText = (TextView)FindViewById (Resource.Id.intro);
			layout = FindViewById (Resource.Id.layout);
            SendFunMessageBtn = FindViewById(Resource.Id.sendMessageBtn);
            trackingBtn = (Button) FindViewById(Resource.Id.trackingbutton);
            // Stores data events received by the local broadcaster.
            //dataItemList = (ListView)FindViewById(Resource.Id.dataItem_list);
            //dataItemListAdapter = new DataItemAdapter (this, Android.Resource.Layout.SimpleListItem1);
			//dataItemList.Adapter = dataItemListAdapter;

			googleApiClient = new GoogleApiClient.Builder (this)
				.AddApi (WearableClass.API)
				.AddConnectionCallbacks (this)
				.AddOnConnectionFailedListener (this)
				.Build ();
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
            var res = await WearableClass.MessageApi.SendMessageAsync(googleApiClient, node, FunMessagePath, new byte[0]);
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
            DataLayerListenerService.LOGD(Tag, "OnMessageReceived: " + ev);

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

  //      private class DataItemAdapter : ArrayAdapter<Event> {
		//	private readonly Context mContext;

		//	public DataItemAdapter(Context context, int unusedResource) 
		//		:base(context, unusedResource) {
		//		mContext = context;
		//	}
		//	public override View GetView (int position, View convertView, ViewGroup parent)
		//	{
		//		ViewHolder holder;
		//		if (convertView == null) {
		//			holder = new ViewHolder ();
		//			LayoutInflater inflater = (LayoutInflater)mContext.GetSystemService (Context.LayoutInflaterService);
		//			convertView = inflater.Inflate (Android.Resource.Layout.TwoLineListItem, null);
		//			convertView.Tag = holder;
		//			holder.Text1 = (TextView)convertView.FindViewById (Android.Resource.Id.Text1);
		//			holder.Text2 = (TextView)convertView.FindViewById (Android.Resource.Id.Text2);
		//		} else {
		//			holder = (ViewHolder)convertView.Tag;
		//		}
		//		Event e = GetItem (position);
		//		holder.Text1.Text = e.Title;
		//		holder.Text2.Text = e.Text;
		//		return convertView;
		//	}
		//	private class ViewHolder : Java.Lang.Object {
		//		public TextView Text1;
		//		public TextView Text2;
		//	}
		//}

		//public class Event {
		//	public String Title, Text;

		//	public Event(String title, String text) {
		//		this.Title = title;
		//		this.Text = text;
		//	}
		//}
	}
}

