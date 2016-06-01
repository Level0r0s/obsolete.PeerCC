using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Connectivity;
using PeerConnectionClient.Signalling;
using PeerConnectionClient.Utilities;

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

        public static event UploadedStatsData StatsUploaded;

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

        public string GetTitle(RtcStatsValueName valueName)
        {
            string ret = "";
            switch (valueName)
            {
                case RtcStatsValueName.StatsValueNameBytesReceived:
                    ret = "Received Bytes";
                    break;
                case RtcStatsValueName.StatsValueNamePacketsReceived:
                    ret = "Received Packets";
                    break;
                case RtcStatsValueName.StatsValueNamePacketsLost:
                    ret = "Lost Packets";
                    break;
                case RtcStatsValueName.StatsValueNameCurrentEndToEndDelayMs:
                    ret = "End To End Delay";
                    break;
                case RtcStatsValueName.StatsValueNameBytesSent:
                    ret = "Sent Bytes";
                    break;
                case RtcStatsValueName.StatsValueNamePacketsSent:
                    ret = "Sent Packets";
                    break;
                case RtcStatsValueName.StatsValueNameFrameRateReceived:
                    ret = "Frame Rate Received";
                    break;
                case RtcStatsValueName.StatsValueNameFrameWidthReceived:
                    ret = "Frame Width Received";
                    break;
                case RtcStatsValueName.StatsValueNameFrameHeightReceived:
                    ret = "Frame Heigh Received";
                    break;
                case RtcStatsValueName.StatsValueNameFrameRateSent:
                    ret = "Frame Rate Sent";
                    break;
                case RtcStatsValueName.StatsValueNameFrameWidthSent:
                    ret = "Frame Width Sent";
                    break;
                case RtcStatsValueName.StatsValueNameFrameHeightSent:
                    ret = "Frame Height Sent";
                    break;
            }
            return ret;
        }


        public string GetColor(RtcStatsValueName valueName)
        {
            string ret = "black";
            switch (valueName)
            {
                case RtcStatsValueName.StatsValueNameBytesReceived:
                case RtcStatsValueName.StatsValueNameBytesSent:
                    ret = "blue";
                    break;
                case RtcStatsValueName.StatsValueNamePacketsReceived:
                case RtcStatsValueName.StatsValueNamePacketsSent:
                    ret = "green";
                    break;
                case RtcStatsValueName.StatsValueNamePacketsLost:
                    ret = "red";
                    break;
                case RtcStatsValueName.StatsValueNameCurrentEndToEndDelayMs:
                    ret = "yellow";
                    break;
                case RtcStatsValueName.StatsValueNameFrameRateReceived:
                case RtcStatsValueName.StatsValueNameFrameRateSent:
                    ret = "black";
                    break;
                case RtcStatsValueName.StatsValueNameFrameWidthReceived:
                case RtcStatsValueName.StatsValueNameFrameWidthSent:
                    ret = "grey";
                    break;
                case RtcStatsValueName.StatsValueNameFrameHeightReceived:
                case RtcStatsValueName.StatsValueNameFrameHeightSent:
                    ret = "orange";
                    break;
            }
            return ret;
        }

        private string GetYAxisTitle(RtcStatsValueName valueName)
        {
            string ret = "";
            switch (valueName)
            {
                case RtcStatsValueName.StatsValueNameBytesSent:
                case RtcStatsValueName.StatsValueNameBytesReceived:
                    ret = "Bytes";
                    break;
                case RtcStatsValueName.StatsValueNamePacketsSent:
                case RtcStatsValueName.StatsValueNamePacketsReceived:
                case RtcStatsValueName.StatsValueNamePacketsLost:
                    ret = "Packets";
                    break;

                case RtcStatsValueName.StatsValueNameCurrentEndToEndDelayMs:
                    ret = "Delay (ms)";
                    break;

                case RtcStatsValueName.StatsValueNameFrameRateSent:
                case RtcStatsValueName.StatsValueNameFrameRateReceived:
                    ret = "Frames";
                    break;
                case RtcStatsValueName.StatsValueNameFrameHeightReceived:
                case RtcStatsValueName.StatsValueNameFrameWidthReceived:
                case RtcStatsValueName.StatsValueNameFrameWidthSent:
                case RtcStatsValueName.StatsValueNameFrameHeightSent:
                    ret = "Pixels";
                    break;
            }
            return ret;
        }

        private Dictionary<string, string> FormatDataForSending(OrtcStatsManager.TrackStatsData trackStatsData, string formatedTimestamps, RtcStatsValueName valueNames)
        {
            var values = trackStatsData.Data[valueNames];
            var valuesStr = "[" + String.Join(",", values.ToArray()) + "]";
            string graphTitle = GetTitle(valueNames);
            string color = GetColor(valueNames);

            string formated = "{\"x\": " + formatedTimestamps + ", \"y\": " + valuesStr + ",\"line\": " + "{\"color\":\"" + color + "\"}" + ",\"name\":\"" + graphTitle + "\"}";

            Dictionary<string,string> dictionary = new Dictionary<string, string>
            {
                 { "args",formated },
                 { "title",graphTitle },
            };

            return dictionary;
        }

        public async Task SendSummary(List<Dictionary<string, string>> datasets, string path, string trackId, bool outgoing)
        {
            string ret = datasets.Aggregate("[", (current, dataset) => (current.Length == 1 ? current : current + ",") + dataset["args"]);
            ret += "]";
            string title = "Summary for the track " + trackId;
            await SendToPlotly(ret, path, trackId,title,outgoing);
        }

        public async Task CreateCallSummary(OrtcStatsManager.StatsData statsData, string id, string path)
        {
            IList<string> argsCallItems = new List<string>();

            //string formatedArgsStartTime = "{\"x\": " + "[" + statsData.StarTime + "]" + ", \"y\": " + "[0]" + ",\"line\": " + "{\"color\":\"" + "black" + "\"}" + ",\"name\":\"" + "Start time" + "\"}";
            //argsCallItems.Add(formatedArgsStartTime);
            string formatedArgsTimeToSetupCall = "{\"x\": " + "[" + statsData.TimeToSetupCall.Milliseconds + "]" + ", \"y\": " + "[0]" + ",\"line\": " + "{\"color\":\"" + "black" + "\"}" + ",\"name\":\"" + "Time To Setup Call" + "\"}";
            argsCallItems.Add(formatedArgsTimeToSetupCall);

            foreach (var trackId in statsData.TrackStatsDictionary.Keys)
            {
                OrtcStatsManager.TrackStatsData trackStatsData = statsData.TrackStatsDictionary[trackId];
                IList<string> argsItems = (from valueName in trackStatsData.Data.Keys let values = trackStatsData.Data[valueName] let averageValue = values.Average() select "{\"x\": " + "[" + averageValue.ToString("0.##") + "]" + ", \"y\": " + "[0]" + ",\"line\": " + "{\"color\":\"" + (trackStatsData.Outgoing ? "blue" : "green") + "\"}" + ",\"name\":\"" + "Average " + (trackStatsData.Outgoing ? "outgoing " : "incoming ") + GetTitle(valueName) + "\"}").ToList();
                string argsTrack = String.Join(",", argsItems.ToArray());
                argsCallItems.Add(argsTrack);
            }
            string args = "[" + String.Join(",", argsCallItems.ToArray()) + "]";
            string title = "Call Summary";
            await SendToPlotly(args, path, "CallSummary", title);
        }
        public async Task SendData(OrtcStatsManager.StatsData statsData, string id)
        {
            if (statsData == null)
                return;

            var timestampsStr = "[" + String.Join(",", statsData.Timestamps.ToArray()) + "]";

            var hostname = NetworkInformation.GetHostNames().FirstOrDefault(h => h.Type == HostNameType.DomainName);
            string ret = hostname?.CanonicalName ?? "<unknown host>";
            var strCaller = statsData.IsCaller ? "caller_" : "callee_";
            var basePath = statsData.StarTime.ToString("yyyyMMdd") + "/" + id + "/" + strCaller + hostname;
            if (statsData.IsCaller)
                basePath += "_" + Conductor.Instance.AudioCodec.Name + "_" + Conductor.Instance.VideoCodec.Name;
            foreach (var trackId in statsData.TrackStatsDictionary.Keys)
            {
                List<Dictionary<string, string>> datasets = new List<Dictionary<string, string>>();
                    
                OrtcStatsManager.TrackStatsData trackStatsData = statsData.TrackStatsDictionary[trackId];
                foreach (var valueNames in trackStatsData.Data.Keys)
                {
                    Dictionary<string, string> dictionary = FormatDataForSending(trackStatsData, timestampsStr, valueNames);
                    dictionary.Add("trackId",trackId);
                    dictionary.Add("yaxisTitle", GetYAxisTitle(valueNames));
                    datasets.Add(dictionary);
                }
                foreach (Dictionary<string, string> dict in datasets)
                {
                    await SendToPlotly(dict, basePath, trackStatsData.Outgoing);
                }

                await SendSummary(datasets, basePath, trackId, trackStatsData.Outgoing);
            }

            await CreateCallSummary(statsData, id, basePath);
            StatsUploaded?.Invoke(id);
        }

        public async Task SendToPlotly(Dictionary<string, string> dictionary, string path, bool outgoing)
        {
            string filename = path + "/" + (outgoing ? "o-" : "i-") + dictionary["trackId"] + "/" + dictionary["title"];

            using (var client = new HttpClient())
            {
                var values = new Dictionary<string, string>
                {
                   { "un", username },
                   { "key", key },
                   {"origin", "plot" },
                    {"platform","lisp"},
                    { "args","[" + dictionary["args"] + "]"},
                    {"kwargs","{\"filename\": \"" + filename + "\",\"fileopt\": \"new\",\"style\": {\"type\": \"scatter\"},\"layout\": {\"title\": \"" + dictionary["trackId"] + "_" + (outgoing?"outgoing":"incoming") + " (" + dictionary["title"] + ")"+ "\",\"xaxis\": {\"title\": \"Time (s)\"},\"yaxis\": {\"title\":" + "\"" + dictionary["yaxisTitle"]+"\"}},\"world_readable\": true}"}
                };

                var content = new FormUrlEncodedContent(values);

                var response = await client.PostAsync(restAPIUrl, content);

                var responseString = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(responseString))
                {
                    Debug.Write(responseString);
                }
            }
        }
        public async Task SendToPlotly(string args, string path, string trackId, string title=null, bool outgoing=true)
        {
            string filename = path + "/" + (outgoing ? "o-" : "i-") + trackId;
            string plotTitle = title ?? trackId;
            using (var client = new HttpClient())
            {
                var values = new Dictionary<string, string>
                {
                   { "un", username },
                   { "key", key },
                   {"origin", "plot" },
                    {"platform","lisp"},
                    { "args",args},
                    {"kwargs","{\"filename\": \"" + filename + "\",\"fileopt\": \"new\",\"style\": {\"type\": \"scatter\"},\"layout\": {\"title\": \"" + plotTitle + "\"},\"world_readable\": true}"},
                };

                var content = new FormUrlEncodedContent(values);

                var response = await client.PostAsync(restAPIUrl, content);

                var responseString = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(responseString))
                {
                    Debug.Write(responseString);
                }
            }
        }

        
    }
}
