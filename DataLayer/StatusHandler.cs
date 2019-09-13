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
    class StatusHandler
    {
        private string currentStatus;
        private TextView statusView;
        public StatusHandler(TextView statusView, string initialStatus)
        {
            this.statusView = statusView;
            this.currentStatus = initialStatus;

            statusView.Text = initialStatus;
        }

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