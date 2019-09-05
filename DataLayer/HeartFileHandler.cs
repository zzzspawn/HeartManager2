﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

        public static Task<List<HeartDataPoint>> getData(string filename)
        {
            return ReadDataPointsTask(filename);
        }

        public static async void saveData(Queue<HeartDataPoint> hdata, string filename)
        {

            HeartDebugHandler.debugLog("Save step start");
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
            await storeDataPointsTask(existingData, filename);
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
                            HeartDebugHandler.debugLog("line: " + line);
                            string dateString = line.Split(new[] { ':' }, 2)[1];
                            int first = dateString.IndexOf("\"") + 1;
                            int last = dateString.LastIndexOf("\"");
                            int length = last - first;
                            HeartDebugHandler.debugLog("Breakpoint");
                            dateString = dateString.Substring(first, length);
                            HeartDebugHandler.debugLog("Breakpoint");
                            date = DateTime.Parse(dateString, null, DateTimeStyles.RoundtripKind);
                            HeartDebugHandler.debugLog("Breakpoint");
                        }

                        if (param1Found && line.Contains("Value"))
                        {
                            param2Found = true;
                            HeartDebugHandler.debugLog("line: " + line);
                            string valueString = line.Split(":")[1];
                            HeartDebugHandler.debugLog("Breakpoint");
                            valueString = valueString.Replace("\"", "");
                            valueString = valueString.Replace(" ", "");
                            valueString = valueString.Replace(",", "");
                            HeartDebugHandler.debugLog("Breakpoint");
                            value = int.Parse(valueString);
                        }

                        if (param1Found && param2Found)
                        {
                            if (dataType != HeartDataType.None && value != -1 && date.HasValue)
                            {
                                HeartDataPoint dataPoint = new HeartDataPoint(dataType, value, (DateTime)date);
                                HeartDebugHandler.debugLog("Breakpoinmarker");
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

        private static async Task storeDataPointsTask(List<HeartDataPoint> dataPoints, string fileName)
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
            }
        }

    }
}