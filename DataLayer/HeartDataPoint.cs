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
    /// <summary>
    /// This class is just a data container for the data coming from the watch; could probably store the sensor accuracy for the data as well
    /// as it could help determine what data to display for data like heart rate, so it could be a weighted average perhaps.
    /// </summary>
    class HeartDataPoint
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
        //TODO: should probably move the different "stringifying" methods into this class, so that it is better encapsulated in the correct area.
    }

    /// <summary>
    /// This is an enum used for keeping track of the different datatypes
    /// </summary>
    public enum HeartDataType
    {
        None,
        HeartBeat,
        HeartRate,
        StepCount//test type for quick data
    }

    /// <summary>
    /// This is just a standardized console writer, so that you always use the same tags when writing to the console
    /// saves you remembering what tag to use, and looks better generally.
    /// could possibly rewrite it into a "message" handler, and handle toasts, text on screen and everything with it, but for now I think this is good enough
    /// </summary>
    public class HeartDebugHandler
    {
        public static void debugLog(string text)
        {
            Log.Info("HH_TEST", text);
        }
    }
    
}