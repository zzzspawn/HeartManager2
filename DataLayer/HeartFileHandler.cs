using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Environment = System.Environment;

namespace DataLayer
{
    class HeartFileHandler
    {
        public const string FILENAME_STEPS = "stepsdata.json";
        public const string FILENAME_HEARTRATE = "hratedata.json";
        public const string FILENAME_HEARTBEAT = "hbeatdata.json";

        private static string[] beingSaved = new string[3];

        public static Task<List<HeartDataPoint>> getData(string filename)
        {
            return ReadDataPointsTask(filename);
        }

        public static async void saveData(Queue<HeartDataPoint> hdata, string filename, StatusHandler dataStatusHandler)
        {

            bool cancelOp = false;
            int selected = -1;

            switch (filename)
            {
                case FILENAME_STEPS: selected = 0;
                    break;
                case FILENAME_HEARTBEAT: selected = 1;
                    break;
                case FILENAME_HEARTRATE: selected = 2;
                    break;
            }

            if (selected != -1 && beingSaved[selected] == null)
            {
                beingSaved[selected] = filename;
            }
            else
            {
                cancelOp = true;
            }


            if (!cancelOp)
            {
                HeartDebugHandler.debugLog("Save start");
                List<HeartDataPoint> existingData;
                HeartDebugHandler.debugLog("Reading existing data start");
                existingData = await ReadDataPointsTask(filename);
                HeartDebugHandler.debugLog("Reading existing data finished");
                HeartDebugHandler.debugLog("Merging the two datasets");
                if (existingData == null)
                {
                    existingData = new List<HeartDataPoint>();
                }
                else
                {
                    if (existingData.Count > 0)
                    {
                        HeartDebugHandler.debugLog("There was existing data, amount" + existingData.Count.ToString());
                        HeartDebugHandler.debugLog("First datapoint contained: " + existingData[0].heartType.ToString("G") + ";" + existingData[0].amount.ToString() + ";" + existingData[0].timestamp + ".");
                    }
                }
                while (hdata.Any())
                {
                    HeartDataPoint element = hdata.Dequeue();
                    existingData.Add(element);
                }
                HeartDebugHandler.debugLog("Datasets have been merged, final tally: " + existingData.Count.ToString());
                HeartDebugHandler.debugLog("Actually writing the file");
                await storeDataPointsTask(existingData, filename, dataStatusHandler);



                if (filename.Equals(FILENAME_STEPS))
                {
                    beingSaved[0] = null;
                }else if (filename.Equals(FILENAME_HEARTBEAT))
                {
                    beingSaved[1] = null;
                }
                else if (filename.Equals(FILENAME_HEARTRATE))
                {
                    beingSaved[2] = null;
                }


            }
            else
            {
                dataStatusHandler.updateStatus("Data already being saved");
            }

        }

        

        private static async Task<List<HeartDataPoint>> ReadDataPointsTask(string fileName)
        {
            string backingFile = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), fileName);

            HeartDebugHandler.debugLog("File to read: " + backingFile);

            if (backingFile == null || !System.IO.File.Exists(backingFile))
            {
                HeartDebugHandler.debugLog("File does not exist");
                return null;
            }
            HeartDebugHandler.debugLog("file exists");
            List<HeartDataPoint> dataPoints = new List<HeartDataPoint>();

            using (var reader = new StreamReader(backingFile, true))
            {
                string line;
                HeartDataType dataType = HeartDataType.None;
                DateTime? date = null;
                int value = -1;
                bool dataReached = false;
                bool param1Found = false;
                bool param2Found = false;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (!dataReached && line.Contains("dataType"))
                    {
                        string[] types = line.Split(":");
                        if (types.Length < 2)
                        {
                            HeartDebugHandler.debugLog("file error, nothing after datatype");
                            break;
                        }
                        string type = line.Split(":")[1];
                        //HeartDebugHandler.debugLog("type: " + type);
                        int first = type.IndexOf("\"") + 1;
                        //HeartDebugHandler.debugLog("first: " + first);
                        int last = type.LastIndexOf("\"");
                        //HeartDebugHandler.debugLog("last: " + last);
                        int length = last - first;
                        //HeartDebugHandler.debugLog("length: " + length);
                        string typeVal = type.Substring(first, length);
                        //HeartDebugHandler.debugLog("typeVal: " + typeVal);
                        dataType = (HeartDataType)Enum.Parse(typeof(HeartDataType), typeVal); //will crash if no match exists
                        //HeartDebugHandler.debugLog("dataType: " + dataType.ToString("G"));
                    }
                    else if (!dataReached && line.Contains("["))
                    {
                        dataReached = true;
                    }

                    if (dataReached)
                    {
                        if (line.Contains("DateTime"))
                        {
                            param1Found = true;
                            //HeartDebugHandler.debugLog("line: " + line);
                            string dateString = line.Split(new[] { ':' }, 2)[1];
                            int first = dateString.IndexOf("\"") + 1;
                            int last = dateString.LastIndexOf("\"");
                            int length = last - first;
                            //HeartDebugHandler.debugLog("Breakpoint");
                            dateString = dateString.Substring(first, length);
                            //HeartDebugHandler.debugLog("Breakpoint");
                            date = DateTime.Parse(dateString, null, DateTimeStyles.RoundtripKind);
                            //HeartDebugHandler.debugLog("Breakpoint");
                        }

                        if (param1Found && line.Contains("Value"))
                        {
                            param2Found = true;
                            //HeartDebugHandler.debugLog("line: " + line);
                            string valueString = line.Split(":")[1];
                            //HeartDebugHandler.debugLog("Breakpoint");
                            valueString = valueString.Replace("\"", "");
                            valueString = valueString.Replace(" ", "");
                            valueString = valueString.Replace(",", "");
                            //HeartDebugHandler.debugLog("Breakpoint");
                            value = int.Parse(valueString);
                        }

                        if (param1Found && param2Found)
                        {
                            if (dataType != HeartDataType.None && value != -1 && date.HasValue)
                            {
                                HeartDataPoint dataPoint = new HeartDataPoint(dataType, value, (DateTime)date);
                                //HeartDebugHandler.debugLog("Breakpoinmarker");
                                dataPoints.Add(dataPoint);

                                //dataType = HeartDataType.None;
                                value = -1;
                                date = null;
                                param1Found = false;
                                param2Found = false;
                            }

                        }

                        if (line.Contains("]"))//implicit "&& dataReached"
                        {
                            dataReached = false;
                        }
                    }
                }
            }

            return dataPoints; //return actual value
        }

        internal static async Task<string> getJSONString()
        {

            //string example = "[{\"dateTime\": \"2019-09-05T08:58:57.5367850+02:00\",\"value\": 10},{\"dateTime\": \"2019-09-05T13:34:37.7470520+02:00\",\"value\": 11},{\"dateTime\": \"2019-09-05T13:35:37.7470520+02:00\",\"value\": 4}]";
            List<HeartDataPoint> list = await getData(FILENAME_STEPS);
            StringBuilder stringBuilder = new StringBuilder("[");

            for(int i = 0; i < list.Count; i++)
            {
                HeartDataPoint x = list[i];
                stringBuilder.Append("{\"dateTime\": ");
                stringBuilder.Append("\"");
                stringBuilder.Append(x.timestamp.ToString("O"));
                stringBuilder.Append("\"");
                stringBuilder.Append(",\"value\": ");
                stringBuilder.Append(x.amount.ToString());
                stringBuilder.Append("}");
                if (i < list.Count - 1)
                {
                    stringBuilder.Append(",");
                }
            };

            stringBuilder.Append("]");
            return stringBuilder.ToString();
        }


        internal static async Task<string> getJSONString(string filename)
        {

            //string example = "[{\"dateTime\": \"2019-09-05T08:58:57.5367850+02:00\",\"value\": 10},{\"dateTime\": \"2019-09-05T13:34:37.7470520+02:00\",\"value\": 11},{\"dateTime\": \"2019-09-05T13:35:37.7470520+02:00\",\"value\": 4}]";
            List<HeartDataPoint> list = await getData(filename);
            StringBuilder stringBuilder = new StringBuilder("[");

            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    HeartDataPoint x = list[i];
                    stringBuilder.Append("{\"dateTime\": ");
                    stringBuilder.Append("\"");
                    stringBuilder.Append(x.timestamp.ToString("O"));
                    stringBuilder.Append("\"");
                    stringBuilder.Append(",\"value\": ");
                    stringBuilder.Append(x.amount.ToString());
                    stringBuilder.Append("}");
                    if (i < list.Count - 1)
                    {
                        stringBuilder.Append(",");
                    }
                }
            }
            stringBuilder.Append("]");
            return stringBuilder.ToString();
        }




        private static async Task storeDataPointsTask(List<HeartDataPoint> dataPoints, string fileName, StatusHandler dataStatusHandler)
        {
            if (dataPoints != null && dataPoints.Count > 0)
            {
                HeartDebugHandler.debugLog("There is data to be written: " + dataPoints.Count.ToString());
                //needed values
                HeartDataType dataType = dataPoints[0].heartType;
                DateTime currentTime = DateTime.Now;


                string filePath = System.Environment.GetFolderPath(Environment.SpecialFolder.Personal);

                string backingFile = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), fileName);

                Java.IO.File file = new Java.IO.File(backingFile);
                if (file.Exists())
                {
                    HeartDebugHandler.debugLog("File already exists, will be overwritten");
                    file.Delete();
                }

                string debugString = "";

                using (var writer = System.IO.File.CreateText(backingFile))
                {
                    //await writer.WriteLineAsync(count.ToString());
                    var indentOne = "\t";
                    var indentTwo = "\t\t";
                    var indentThree = "\t\t\t";
                    await writer.WriteLineAsync("{"); debugString += "{";
                    await writer.WriteLineAsync(indentOne + "\"updated\": \"" + currentTime.ToString("O") + "\","); debugString += indentOne + "\"updated\": \"" + currentTime.ToString("O") + "\",";
                    await writer.WriteLineAsync(indentOne + "\"dataType\": \"" + dataType.ToString("G") + "\","); debugString += indentOne + "\"dataType\": \"" + dataType.ToString("G") + "\",";

                    await writer.WriteLineAsync(indentOne + "\"data\": ["); debugString += indentOne + "\"data\": [";

                    for (int i = 0; i < dataPoints.Count; i++)
                    {
                        HeartDataPoint point = dataPoints[i];
                        await writer.WriteLineAsync(indentTwo + "{"); debugString += indentTwo + "{";
                        await writer.WriteLineAsync(indentThree + "\"DateTime\": " + "\"" + point.timestamp.ToString("O") + "\","); debugString += indentThree + "\"DateTime\": " + "\"" + point.timestamp.ToString("O") + "\",";
                        await writer.WriteLineAsync(indentThree + "\"Value\": " + "\"" + point.amount + "\""); debugString += indentThree + "\"Value\": " + point.amount;
                        string lastLine = "}";
                        if (i < dataPoints.Count - 1)
                        {
                            lastLine += ",";
                        }
                        await writer.WriteLineAsync(indentTwo + lastLine); debugString += indentTwo + lastLine;
                    }
                    await writer.WriteLineAsync(indentOne + "]"); debugString += indentOne + "]";
                    await writer.WriteLineAsync("}"); debugString += "}";

                }
                dataStatusHandler.updateStatus("File saved");
                if (debugString != "")
                {
                    HeartDebugHandler.debugLog("File that was saved: ");
                    HeartDebugHandler.debugLog(debugString);
                }
                else
                {
                    HeartDebugHandler.debugLog("debug string contained nothing");
                }

            }
            else
            {
                HeartDebugHandler.debugLog("No data to write, operation cancelled");
                dataStatusHandler.updateStatus("No data to store");
            }
        }

        public static void DeleteHeartRateData()
        {
            DeleteDataFile(FILENAME_HEARTRATE);
        }

        internal static void DeleteHeartBeatData()
        {
            DeleteDataFile(FILENAME_HEARTBEAT);
        }

        internal static void DeleteStepsData()
        {
            DeleteDataFile(FILENAME_STEPS);
        }

        internal static void DeleteAllData()
        {
            DeleteDataFile(FILENAME_HEARTRATE);
            DeleteDataFile(FILENAME_HEARTBEAT);
            DeleteDataFile(FILENAME_STEPS);
        }

        private static void DeleteDataFile(string filename)
        {
            string fileString = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), filename);

            Java.IO.File file = new Java.IO.File(fileString);
            if (file.Exists())
            {
                HeartDebugHandler.debugLog("File exists, deleting");
                file.Delete();
            }
        }

    }
}