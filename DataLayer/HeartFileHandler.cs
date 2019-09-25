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
    /// <summary>
    /// A class handling all the file interactions.
    /// </summary>
    class HeartFileHandler
    {
        //The three different files data is stored in
        public const string FILENAME_STEPS = "stepsdata.json";
        public const string FILENAME_HEARTRATE = "hratedata.json";
        public const string FILENAME_HEARTBEAT = "hbeatdata.json";
        //An array storing the save status for each of the files, so we don't try to double write to one.
        private static string[] beingSaved = new string[3];

        //TODO: should probably think a little bit harder about how I'm using these task and async classes; could probably be condensed in some ways.
        
        /// <summary>
        /// just a go between class for getting the data using a task, so it can be awaited
        /// </summary>
        /// <param name="filename">is the filename passed through the task for reading.</param>
        /// <returns></returns>
        public static Task<List<HeartDataPoint>> getData(string filename)
        {
            return ReadDataPointsTask(filename);
        }

        /// <summary>
        /// Takes in a list(QUEUE) and stores it in a file with the provided filename, it then updates the application status(TextView) to make the user aware the file has been saved
        /// </summary>
        /// <param name="hdata">The list of items to be stored</param>
        /// <param name="filename">The filename of the file to store the data in</param>
        /// <param name="dataStatusHandler">the handler of the textview for statuses in the app gui</param>
        public static async void saveData(Queue<HeartDataPoint> hdata, string filename, StatusHandler dataStatusHandler)
        {

            bool cancelOp = false; //if this trips to true, then file won't be stored
            int selected = -1;


            //handling of "file already being manipulated" checks
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
            //first file manip check done

            if (!cancelOp)//if file is being manipulated, don't continue
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


                //now reset the file being saved to null, so that the program knows it can be used again
                //TODO: look into whether this can all be handled using a callback instead, and whether that's more efficient
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
                //setting the status TextView to this string
                dataStatusHandler.updateStatus("Data already being saved");
            }

        }

        
        /// <summary>
        /// Reads data from a file, and converts it into instances of the HeartData class, then returns an awaitable task with that data at the end of it.
        /// </summary>
        /// <param name="fileName">The filename of the file that is to be read.</param>
        /// <returns></returns>
        private static async Task<List<HeartDataPoint>> ReadDataPointsTask(string fileName)
        {
            string backingFile = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), fileName);

            HeartDebugHandler.debugLog("File to read: " + backingFile);

            if (backingFile == null || !System.IO.File.Exists(backingFile))
            {
                HeartDebugHandler.debugLog("File does not exist");
                //REMARK: this is checked against, so make sure you know what you do if you do any change to return value
                return null; //cancel operation
            }
            HeartDebugHandler.debugLog("file exists");
            List<HeartDataPoint> dataPoints = new List<HeartDataPoint>();

            using (var reader = new StreamReader(backingFile, true))
            {
                string line;
                HeartDataType dataType = HeartDataType.None;
                DateTime? date = null;
                string accuracy = null;
                int value = -1;
                bool dataReached = false;
                bool param1Found = false;
                bool param2Found = false;
                bool param3Found = false;

                //TODO: move this into the class definition of the dataclass, probably as a builder/factory or some fitting pattern, probably a better place to handle it.
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

                        if (param1Found && param2Found && line.Contains("accuracy"))
                        {
                            param3Found = true;
                            string accuracyString = line.Split(new[] { ':' }, 2)[1];
                            int first = accuracyString.IndexOf("\"") + 1;
                            int last = accuracyString.LastIndexOf("\"");
                            int length = last - first;
                            accuracy = accuracyString.Substring(first, length);

                        }

                        if (param1Found && param2Found && param3Found)
                        {
                            if (dataType != HeartDataType.None && value != -1 && date.HasValue)
                            {
                                
                                HeartDataPoint dataPoint = new HeartDataPoint(dataType, value, (DateTime)date, accuracy);
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
        
        /// <summary>
        /// Takes a filename, requests a file(list of objects), converts it into a string(in json formatting) then returns the string.
        /// </summary>
        /// <param name="filename">The filename of the file to request</param>
        /// <returns>string with json content</returns>
        internal static async Task<string> getJSONString(string filename)
        {
            //string example = "[{\"dateTime\": \"2019-09-05T08:58:57.5367850+02:00\",\"value\": 10},{\"dateTime\": \"2019-09-05T13:34:37.7470520+02:00\",\"value\": 11},{\"dateTime\": \"2019-09-05T13:35:37.7470520+02:00\",\"value\": 4}]";
            List<HeartDataPoint> list = await getData(filename);
            //TODO: move into class definition instead, so that it is handled in a more appropriate location, should probs be called something like toJSONString() or simply toJSON()
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
                    stringBuilder.Append(",\"accuracy\": ");
                    stringBuilder.Append(x.accuracy);
                    stringBuilder.Append("\"");
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

        /// <summary>
        /// The main task for writing the data to file; it get's the full set of data going in to the file,
        /// it then deletes the current file(if it exists) and creates a new file containing the incoming data, it then updates the status(TextView) of the app
        /// </summary>
        /// <param name="dataPoints"></param>
        /// <param name="fileName"></param>
        /// <param name="dataStatusHandler"></param>
        /// <returns></returns>
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
                // TODO: "FILESTORAGE" [0] this needs a better solution, writing to temp and replacing it solves most file loss, but not ram usage, so should probably use appending so you don't need to read the whole file to memory every time
                // TODO: "FILESTORAGE" cont [1]: this should probably use a version of https://stackoverflow.com/a/39562216 but instead of dropping the last few lines, you store them and add them back into the file after adding the new objects

                Java.IO.File file = new Java.IO.File(backingFile);
                if (file.Exists())//if the file exists, delete it.// TODO: "FILESTORAGE" cont [2]: this is probably where it should be split into two different methods, one for storing and creating a new file, and one for 'appending'
                {
                    HeartDebugHandler.debugLog("File already exists, will be overwritten");
                    file.Delete();
                }

                //string debugString = "";

                using (var writer = System.IO.File.CreateText(backingFile))
                {
                    //made these to make the print statements a bit more readable(although they could probably be made better).
                    var indentOne = "\t";
                    var indentTwo = "\t\t";
                    var indentThree = "\t\t\t";
                    await writer.WriteLineAsync("{"); //debugString += "{";
                    await writer.WriteLineAsync(indentOne + "\"updated\": \"" + currentTime.ToString("O") + "\","); //debugString += indentOne + "\"updated\": \"" + currentTime.ToString("O") + "\",";
                    await writer.WriteLineAsync(indentOne + "\"dataType\": \"" + dataType.ToString("G") + "\","); //debugString += indentOne + "\"dataType\": \"" + dataType.ToString("G") + "\",";

                    await writer.WriteLineAsync(indentOne + "\"data\": ["); //debugString += indentOne + "\"data\": [";

                    for (int i = 0; i < dataPoints.Count; i++)
                    {
                        HeartDataPoint point = dataPoints[i];
                        await writer.WriteLineAsync(indentTwo + "{"); //debugString += indentTwo + "{";
                        await writer.WriteLineAsync(indentThree + "\"DateTime\": " + "\"" + point.timestamp.ToString("O") + "\","); //debugString += indentThree + "\"DateTime\": " + "\"" + point.timestamp.ToString("O") + "\",";
                        await writer.WriteLineAsync(indentThree + "\"Value\": " + "\"" + point.amount + "\","); //debugString += indentThree + "\"Value\": " + point.amount;
                        await writer.WriteLineAsync(indentThree + "\"accuracy\": " + "\"" + point.accuracy + "\"");
                        string lastLine = "}";
                        if (i < dataPoints.Count - 1)
                        {
                            lastLine += ",";
                        }
                        await writer.WriteLineAsync(indentTwo + lastLine); //debugString += indentTwo + lastLine;
                    }
                    await writer.WriteLineAsync(indentOne + "]"); //debugString += indentOne + "]";
                    await writer.WriteLineAsync("}"); //debugString += "}";

                }
                dataStatusHandler.updateStatus("File saved");
                //if (debugString != "")
                //{
                //    HeartDebugHandler.debugLog("File that was saved: ");
                //    HeartDebugHandler.debugLog(debugString);
                //}
                //else
                //{
                //    HeartDebugHandler.debugLog("debug string contained nothing");
                //}

            }
            else
            {
                HeartDebugHandler.debugLog("No data to write, operation cancelled");
                dataStatusHandler.updateStatus("No data to store");
            }
        }
        /// <summary>
        /// simple to call, adds a parameter and calls the delete method
        /// </summary>
        public static void DeleteHeartRateData()
        {
            DeleteDataFile(FILENAME_HEARTRATE);
        }
        /// <summary>
        /// simple to call, adds a parameter and calls the delete method
        /// </summary>
        internal static void DeleteHeartBeatData()
        {
            DeleteDataFile(FILENAME_HEARTBEAT);
        }
        /// <summary>
        /// simple to call, adds a parameter and calls the delete method
        /// </summary>
        internal static void DeleteStepsData()
        {
            DeleteDataFile(FILENAME_STEPS);
        }
        /// <summary>
        /// simple to call, runs the delete method for all files; considered using the other methods, but decided this is better in case there are other files you
        /// can't delete as a user, then this would fit in better; otherwise those would need their own methods as well, which for now seems a bit excessive.
        /// </summary>
        internal static void DeleteAllData()
        {
            DeleteDataFile(FILENAME_HEARTRATE);
            DeleteDataFile(FILENAME_HEARTBEAT);
            DeleteDataFile(FILENAME_STEPS);
        }
        /// <summary>
        /// takes a filename, and if that file exists, it gets deleted.
        /// </summary>
        /// <param name="filename"></param>
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