using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace DataLayer
{
    class HeartDataPoint
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

    public enum HeartDataType
    {
        None,
        HeartBeat,
        HeartRate,
        StepCount//test type for quick data
    }

    public class HeartDebugHandler
    {
        public static void debugLog(string text)
        {
            Log.Info("HH_TEST", text);
        }
    }
    
}