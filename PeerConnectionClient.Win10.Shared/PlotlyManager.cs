using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PeerConnectionClient.Win10.Shared
{
    class PlotlyManager
    {
        private static volatile PlotlyManager _instance;
        private static readonly object SyncRoot = new object();

        private readonly string username = "hookflash";
        private readonly string password = "QGC4s8XjbPmZoq";
        private readonly string key = "hei6r5e6rd";
        private readonly string email = "erik@hookflash.com";
        private readonly string restAPIUrl = "https://plot.ly/clientresp";
        public static PlotlyManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (SyncRoot)
                {
                    if (_instance == null) _instance = new PlotlyManager();
                }

                return _instance;
            }
        }

        //public async void SendData(List<object> dict)
        public async void SendData(IList<object> data, IList<object> timestamps, string name)
        {
            var dataStr = "[" + String.Join(",", data.ToArray()) + "]";
            var timestampsStr = "[" + String.Join(",", timestamps.ToArray()) + "]";
            var argsStr = "\"[" + dataStr + "," + timestampsStr + "]\"";
            using (var client = new HttpClient())
            {
                var values = new Dictionary<string, string>
                {
                   { "un", username },
                   { "key", key },
                   {"origin", "plot" },
                    {"platform","lisp"},
                    { "args",argsStr},
                    {"kwargs","{\"filename\": \"" + name + "\",\"fileopt\": \"new\",\"style\": {\"type\": \"scatter\"},\"layout\": {\"title\": \"experimental data\"},\"world_readable\": true}"},
                };
                /*var values = new Dictionary<string, string>
                {
                   { "un", username },
                   { "key", key },
                   {"origin", "plot" },
                    {"platform","lisp"},
                    { "args","[[0, 1, 2], [3, 1, 6]],[{\"x\": [0, 1, 2], \"y\": [3, 1, 6]}],[{\"x\": [0, 1, 2], \"y\": [3, 1, 6], \"name\": \"Experimental\", \"marker\": {\"symbol\": \"square\", \"color\": \"purple\"}}, {\"x\": [1, 2, 3], \"y\": [3, 4, 5], \"name\": \"Control\"}]"},
                    {"kwargs","{\"filename\": \"plot from api\",\"fileopt\": \"overwrite\",\"style\": {\"type\": \"bar\"}, \"traces\": [0,3,5],\"layout\": {\"title\": \"experimental data\"},\"world_readable\": true}"},
                };*/

                var content = new FormUrlEncodedContent(values);

                var response = await client.PostAsync(restAPIUrl, content);

                var responseString = await response.Content.ReadAsStringAsync();
                if (responseString != null && responseString.Length > 0)
                {
                    Debug.Write(responseString);
                }
            }
        }

        public string GetTitle(RTCStatsValueName valueName)
        {
            switch (valueName)
            {
                case RTCStatsValueName.StatsValueNameBytesReceived:
                    return "Received Bytes";
                    break;
                case RTCStatsValueName.StatsValueNamePacketsReceived:
                    return "Received Packets";
                    break;
                case RTCStatsValueName.StatsValueNamePacketsLost:
                    return "Lost Packets";
                    break;
                case RTCStatsValueName.StatsValueNameCurrentEndToEndDelayMs:
                    return "End To End Delay";
                    break;
                case RTCStatsValueName.StatsValueNameBytesSent:
                    return "Sent Bytes";
                    break;
                case RTCStatsValueName.StatsValueNamePacketsSent:
                    return "Sent Packets";
                    break;
                case RTCStatsValueName.StatsValueNameFrameRateReceived:
                    return "Frame Rate Received";
                    break;
                case RTCStatsValueName.StatsValueNameFrameWidthReceived:
                    return "Frame Width Received";
                    break;
                case RTCStatsValueName.StatsValueNameFrameHeightReceived:
                    return "Frame Heigh Received";
                    break;
                case RTCStatsValueName.StatsValueNameFrameRateSent:
                    return "Frame Rate Sent";
                    break;
                case RTCStatsValueName.StatsValueNameFrameWidthSent:
                    return "Frame Width Sent";
                    break;
                case RTCStatsValueName.StatsValueNameFrameHeightSent:
                    return "Frame Height Sent";
                    break;
            }
            return "";
        }

        public string GetColor(RTCStatsValueName valueName)
        {
            switch (valueName)
            {
                case RTCStatsValueName.StatsValueNameBytesReceived:
                case RTCStatsValueName.StatsValueNameBytesSent:
                    return "blue";
                    break;
                case RTCStatsValueName.StatsValueNamePacketsReceived:
                case RTCStatsValueName.StatsValueNamePacketsSent:
                    return "green";
                    break;
                case RTCStatsValueName.StatsValueNamePacketsLost:
                    return "red";
                    break;
                case RTCStatsValueName.StatsValueNameCurrentEndToEndDelayMs:
                    return "yellow";
                    break;
                case RTCStatsValueName.StatsValueNameFrameRateReceived:
                case RTCStatsValueName.StatsValueNameFrameRateSent:
                    return "black";
                    break;
                case RTCStatsValueName.StatsValueNameFrameWidthReceived:
                case RTCStatsValueName.StatsValueNameFrameWidthSent:
                    return "grey";
                    break;
                case RTCStatsValueName.StatsValueNameFrameHeightReceived:
                case RTCStatsValueName.StatsValueNameFrameHeightSent:
                    return "orange";
                    break;
            }
            return "purple";
        }
        //{\"x\": [0, 1, 2], \"y\": [3, 11, 16],\"line\": {\"color\":\"purple\"},\"name\":\"Frame Rates\"}
        private string FormatDataForSending(OrtcStatsManager.TrackStatsData trackStatsData, string formatedTimestamps)
        {
            List<string> datasets= new List<string>();
            //foreach (var values in trackStatsData.Data.Values)
            foreach (var key in trackStatsData.Data.Keys)
            {
                var values = trackStatsData.Data[key];
                var valuesStr = "[" + String.Join(",", values.ToArray()) + "]";
                string graphTitle = GetTitle(key);
                string color = GetColor(key);

                string formated = "{\"x\" " + formatedTimestamps + ", \"y\": " + valuesStr + ",\"line\": " + "{\"color\":\"" + color + "\"}" + ",\"name\":\"" + graphTitle + "\"}";
                datasets.Add(formated);
            }
            string ret = "[" + String.Join(",", datasets.ToArray()) + "]";
            return ret;
        }
        public async Task SendData(OrtcStatsManager.StatsData statsData, string id)
        {
            if (statsData == null)
                return;
            var timestampsStr = "[" + String.Join(",", statsData.Timestamps.ToArray()) + "]";

            foreach (var key in statsData.TrackStatsDictionary.Keys)
            {
                OrtcStatsManager.TrackStatsData trackStatsData = statsData.TrackStatsDictionary[key];
                string dataToSend = FormatDataForSending(trackStatsData, timestampsStr);
                await SentToPlotly(dataToSend,id,key);
            }
        }

        public async Task SentToPlotly(string args, string name, string title)
        {
            string filename = name + "\\" + title;
            using (var client = new HttpClient())
            {
                var values = new Dictionary<string, string>
                {
                   { "un", username },
                   { "key", key },
                   {"origin", "plot" },
                    {"platform","lisp"},
                    { "args",args},
                    {"kwargs","{\"filename\": \"" + filename + "\",\"fileopt\": \"new\",\"style\": {\"type\": \"scatter\"},\"layout\": {\"title\": \"" + title + "\"},\"world_readable\": true}"},
                };

                var content = new FormUrlEncodedContent(values);

                var response = await client.PostAsync(restAPIUrl, content);

                var responseString = await response.Content.ReadAsStringAsync();
                if (responseString != null && responseString.Length > 0)
                {
                    Debug.Write(responseString);
                }
            }
        }
    }
}
