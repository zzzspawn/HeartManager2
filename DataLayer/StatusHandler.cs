using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace DataLayer
{
    /// <summary>
    /// Updates the status, through a Textview from constructor parameters
    /// </summary>
    class StatusHandler
    {
        private string currentStatus; //the status last written(without number count)
        private TextView statusView;
        public StatusHandler(TextView statusView, string initialStatus)
        {
            this.statusView = statusView;
            this.currentStatus = initialStatus;

            statusView.Text = initialStatus;
        }

        /// <summary>
        /// Takes a string, check if the same has already been written, if so
        /// appends a number corresponding to how many times the same string has been written
        /// </summary>
        /// <param name="status"></param>
        public void updateStatus(string status)
        {
            if (status == currentStatus)
            {
                string currentText = statusView.Text;

                if (currentText.Contains("("))
                {
                    int num = extractNumber(currentText);

                    num++;

                    statusView.Text = status + " (" + num + ")";
                }
                else
                {
                    statusView.Text = status + " (1)";
                }
            }
            else
            {
                statusView.Text = status;
            }

            currentStatus = status;
        }
        //TODO: could probably read the string in reverse and extract the number from last parenthesis, instead of excluding "(" and ")" as legal characters in string
        /// <summary>
        /// extracts the number from string so it can be used in calculation for the new number
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private int extractNumber(string text)
        {
            int before = text.IndexOf('(');
            int after = text.IndexOf(')');

            string numText = text.Substring(before+1, (after - before)-1);

            int number;
            bool result = int.TryParse(numText, out number);

            if (result)
            {
                return number;
            }
            else
            {
                return 0;
            }
        }

    }
}