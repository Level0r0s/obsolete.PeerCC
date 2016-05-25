using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using org.ortc;
using org.ortc.adapter;
using PeerConnectionClient.Signalling;
using PeerConnectionClient.Utilities;
using RtcPeerConnection = org.ortc.adapter.RTCPeerConnection;

namespace PeerConnectionClient.Win10.Shared
{
    public enum RTCStatsValueName
    {
        StatsValueNameActiveConnection = 0,
        StatsValueNameAudioInputLevel = 1,
        StatsValueNameAudioOutputLevel = 2,
        StatsValueNameBytesReceived = 3,
        StatsValueNameBytesSent = 4,
        StatsValueNameDataChannelId = 5,
        StatsValueNamePacketsLost = 6,
        StatsValueNamePacketsReceived = 7,
        StatsValueNamePacketsSent = 8,
        StatsValueNameProtocol = 9,
        StatsValueNameReceiving = 10,
        StatsValueNameSelectedCandidatePairId = 11,
        StatsValueNameSsrc = 12,
        StatsValueNameState = 13,
        StatsValueNameTransportId = 14,
        StatsValueNameAccelerateRate = 15,
        StatsValueNameActualEncBitrate = 16,
        StatsValueNameAdaptationChanges = 17,
        StatsValueNameAvailableReceiveBandwidth = 18,
        StatsValueNameAvailableSendBandwidth = 19,
        StatsValueNameAvgEncodeMs = 20,
        StatsValueNameBandwidthLimitedResolution = 21,
        StatsValueNameBucketDelay = 22,
        StatsValueNameCaptureStartNtpTimeMs = 23,
        StatsValueNameCandidateIPAddress = 24,
        StatsValueNameCandidateNetworkType = 25,
        StatsValueNameCandidatePortNumber = 26,
        StatsValueNameCandidatePriority = 27,
        StatsValueNameCandidateTransportType = 28,
        StatsValueNameCandidateType = 29,
        StatsValueNameChannelId = 30,
        StatsValueNameCodecName = 31,
        StatsValueNameComponent = 32,
        StatsValueNameContentName = 33,
        StatsValueNameCpuLimitedResolution = 34,
        StatsValueNameCurrentDelayMs = 35,
        StatsValueNameDecodeMs = 36,
        StatsValueNameDecodingCNG = 37,
        StatsValueNameDecodingCTN = 38,
        StatsValueNameDecodingCTSG = 39,
        StatsValueNameDecodingNormal = 40,
        StatsValueNameDecodingPLC = 41,
        StatsValueNameDecodingPLCCNG = 42,
        StatsValueNameDer = 43,
        StatsValueNameDtlsCipher = 44,
        StatsValueNameEchoCancellationQualityMin = 45,
        StatsValueNameEchoDelayMedian = 46,
        StatsValueNameEchoDelayStdDev = 47,
        StatsValueNameEchoReturnLoss = 48,
        StatsValueNameEchoReturnLossEnhancement = 49,
        StatsValueNameEncodeUsagePercent = 50,
        StatsValueNameExpandRate = 51,
        StatsValueNameFingerprint = 52,
        StatsValueNameFingerprintAlgorithm = 53,
        StatsValueNameFirsReceived = 54,
        StatsValueNameFirsSent = 55,
        StatsValueNameFrameHeightInput = 56,
        StatsValueNameFrameHeightReceived = 57,
        StatsValueNameFrameHeightSent = 58,
        StatsValueNameFrameRateDecoded = 59,
        StatsValueNameFrameRateInput = 60,
        StatsValueNameFrameRateOutput = 61,
        StatsValueNameFrameRateReceived = 62,
        StatsValueNameFrameRateSent = 63,
        StatsValueNameFrameWidthInput = 64,
        StatsValueNameFrameWidthReceived = 65,
        StatsValueNameFrameWidthSent = 66,
        StatsValueNameInitiator = 67,
        StatsValueNameIssuerId = 68,
        StatsValueNameJitterBufferMs = 69,
        StatsValueNameJitterReceived = 70,
        StatsValueNameLabel = 71,
        StatsValueNameLocalAddress = 72,
        StatsValueNameLocalCandidateId = 73,
        StatsValueNameLocalCandidateType = 74,
        StatsValueNameLocalCertificateId = 75,
        StatsValueNameMaxDecodeMs = 76,
        StatsValueNameMinPlayoutDelayMs = 77,
        StatsValueNameNacksReceived = 78,
        StatsValueNameNacksSent = 79,
        StatsValueNamePlisReceived = 80,
        StatsValueNamePlisSent = 81,
        StatsValueNamePreemptiveExpandRate = 82,
        StatsValueNamePreferredJitterBufferMs = 83,
        StatsValueNameRemoteAddress = 84,
        StatsValueNameRemoteCandidateId = 85,
        StatsValueNameRemoteCandidateType = 86,
        StatsValueNameRemoteCertificateId = 87,
        StatsValueNameRenderDelayMs = 88,
        StatsValueNameRetransmitBitrate = 89,
        StatsValueNameRtt = 90,
        StatsValueNameSecondaryDecodedRate = 91,
        StatsValueNameSendPacketsDiscarded = 92,
        StatsValueNameSpeechExpandRate = 93,
        StatsValueNameSrtpCipher = 94,
        StatsValueNameTargetDelayMs = 95,
        StatsValueNameTargetEncBitrate = 96,
        StatsValueNameTrackId = 97,
        StatsValueNameTransmitBitrate = 98,
        StatsValueNameTransportType = 99,
        StatsValueNameTypingNoiseState = 100,
        StatsValueNameViewLimitedResolution = 101,
        StatsValueNameWritable = 102,
        StatsValueNameCurrentEndToEndDelayMs = 103
    }

    public class CallMatrics
    {
        public IDictionary<RTCStatsValueName, IDictionary<string, CallMatricsData>> Data { get; set; }

        public CallMatrics()
        {
            Data = new Dictionary<RTCStatsValueName, IDictionary<string, CallMatricsData>>();
        }
    }

    public class CallMatricsData
    {
        public string Id { get; set; }
        public IList<object> DataList { get; set; }
        public IList<object> Timestamps { get; set; }

        public CallMatricsData()
        {
            DataList = new List<object>();
            Timestamps = new List<object>();
        }
    }
    class OrtcStatsManager
    {
        private static volatile OrtcStatsManager _instance;
        private static readonly object SyncRoot = new object();

        private RtcPeerConnection _peerConnection;
        private string _currentId;
        private bool _isStatsCollectionEnabled;
        private Timer _callMetricsTimer;

        private RTCStatsProvider StatsProviderPeerConnection { get; set; }
        private RTCStatsProvider StatsProviderPeerConnectionCall { get; set; }
        private IList<RTCStatsReport> callMetricsStatsReportList { get; set; }
        //private Dictionary<string,IDictionary<RTCStatsValueName,IList<object>>> callsMetricsDictionary {get; set;}
        private Dictionary<string, CallMatrics> callsMetricsDictionary { get; set; }
        private Dictionary<string, IList<RTCStatsReport>> callsStatsReportDictionary { get; set; }
        // /tester/peercc/device/codec/<notes-if-present>/<YYYMMDD-HHMM>/dataset 
        private OrtcStatsManager()
        {
            //this.callMetricsStatsReportList = new List<RTCStatsReport>();
            //callsMetricsDictionary = new Dictionary<string, IDictionary<RTCStatsValueName, IList<object>>>();
            callsMetricsDictionary = new Dictionary<string, CallMatrics>();
            callsStatsReportDictionary = new Dictionary<string, IList<RTCStatsReport>>();
        }

        public static OrtcStatsManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (SyncRoot)
                {
                    if (_instance == null) _instance = new OrtcStatsManager();
                }

                return _instance;
            }
        }

        /// <summary>
        /// For each call create unique id and a list where will be stored stats data.
        /// </summary>
        private void PrepareForStats()
        {
            _currentId = /*Conductor.Instance.Peer.Name +*/ "/" + Helper.ProductName() + "/" + Helper.DeviceName() + "/" + Helper.OsVersion() + "/" + Conductor.Instance.AudioCodec.Name + "_" + Conductor.Instance.VideoCodec.Name + "/" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "/dataset/Plot1";
            //Dictionary<RTCStatsValueName,IList<object>> callMatrics = new Dictionary<RTCStatsValueName, IList<object>>();
            CallMatrics callMatrics = new CallMatrics();
            //All call stats will be stored in this dict, so it can be safely uploaded to server, while other call can be in progress
            callsMetricsDictionary.Add(_currentId,callMatrics);

            callMatrics.Data.Add(RTCStatsValueName.StatsValueNameBytesSent, new Dictionary<string, CallMatricsData>());
            callMatrics.Data.Add(RTCStatsValueName.StatsValueNameBytesReceived, new Dictionary<string, CallMatricsData>());
            callMatrics.Data.Add(RTCStatsValueName.StatsValueNamePacketsLost, new Dictionary<string, CallMatricsData>());
            callMatrics.Data.Add(RTCStatsValueName.StatsValueNamePacketsSent, new Dictionary<string, CallMatricsData>());
            callMatrics.Data.Add(RTCStatsValueName.StatsValueNamePacketsReceived, new Dictionary<string, CallMatricsData>());
            callMatrics.Data.Add(RTCStatsValueName.StatsValueNameFrameHeightSent, new Dictionary<string, CallMatricsData>());
            callMatrics.Data.Add(RTCStatsValueName.StatsValueNameFrameWidthSent, new Dictionary<string, CallMatricsData>());
            callMatrics.Data.Add(RTCStatsValueName.StatsValueNameFrameHeightReceived, new Dictionary<string, CallMatricsData>());
            callMatrics.Data.Add(RTCStatsValueName.StatsValueNameFrameWidthReceived, new Dictionary<string, CallMatricsData>());
            callMatrics.Data.Add(RTCStatsValueName.StatsValueNameFrameRateSent, new Dictionary<string, CallMatricsData>());
            callMatrics.Data.Add(RTCStatsValueName.StatsValueNameFrameRateReceived, new Dictionary<string, CallMatricsData>());

            /*callMatrics.Add(RTCStatsValueName.StatsValueNameBytesSent, new List<object>());
            callMatrics.Add(RTCStatsValueName.StatsValueNameBytesReceived, new List<object>());
            callMatrics.Add(RTCStatsValueName.StatsValueNamePacketsLost, new List<object>());
            callMatrics.Add(RTCStatsValueName.StatsValueNamePacketsSent, new List<object>());
            callMatrics.Add(RTCStatsValueName.StatsValueNamePacketsReceived, new List<object>());
            callMatrics.Add(RTCStatsValueName.StatsValueNameFrameHeightSent, new List<object>());
            callMatrics.Add(RTCStatsValueName.StatsValueNameFrameWidthSent, new List<object>());
            callMatrics.Add(RTCStatsValueName.StatsValueNameFrameHeightReceived, new List<object>());
            callMatrics.Add(RTCStatsValueName.StatsValueNameFrameWidthReceived, new List<object>());
            callMatrics.Add(RTCStatsValueName.StatsValueNameFrameRateSent, new List<object>());
            callMatrics.Add(RTCStatsValueName.StatsValueNameFrameRateReceived, new List<object>());
            */
            callMetricsStatsReportList = new List<RTCStatsReport>();
        }
        public void Initialize(RtcPeerConnection pc)
        {
            if (pc != null)
            {
                _peerConnection = pc;
                PrepareForStats();
                RTCStatsProviderOptions options =
                    new RTCStatsProviderOptions(new List<RTCStatsType>
                    {
                        RTCStatsType.IceGatherer,
                        RTCStatsType.Codec,
                        RTCStatsType.DtlsTransport
                    });

                StatsProviderPeerConnection = new RTCStatsProvider(pc, options);
            }
            else
            {
                Debug.WriteLine("ORTCStatsManager: Cannot initialize peer connection by null pointer");
            }
        }

        public void Reset()
        {
            _peerConnection = null;
            callMetricsStatsReportList = null;
            //callMetricsStatsReportList = new List<RTCStatsReport>();
            //callMetricsStatsReportList.Clear();
            StatsProviderPeerConnectionCall = null;
        }

        public bool IsStatsCollectionEnabled
        {
            get { return _isStatsCollectionEnabled; }
            set
            {
                _isStatsCollectionEnabled = value;
                if (_peerConnection != null && StatsProviderPeerConnectionCall == null)
                {
                    if (_isStatsCollectionEnabled)
                    {
                        StartCallWatch();
                    }
                    else
                    {
                        Reset();
                    }
                }
                else
                {
                    Debug.WriteLine("StatsManager: Stats are not toggled as manager is not initialized yet.");
                }
            }
        }

        public void StartCallWatch()
        {
            if (_peerConnection != null)
            {
                RTCStatsProviderOptions options =
                    new RTCStatsProviderOptions(new List<RTCStatsType>
                    {
                        RTCStatsType.InboundRtp,
                        RTCStatsType.OutboundRtp,
                        RTCStatsType.Track
                    });
                StatsProviderPeerConnectionCall = new RTCStatsProvider(_peerConnection, options);

                AutoResetEvent autoEvent = new AutoResetEvent(false);
                TimerCallback tcb = CollectCallMetrics;
                _callMetricsTimer = new Timer(tcb, autoEvent, 0, 2000);
            }
            else
            {
                Debug.WriteLine("ORTCStatsManager: Cannot create stats provider if peer connection is null pointer");
            }
        }

        public async void CallEnded()
        {
            StopCallWatch();
            if (callMetricsStatsReportList != null && callMetricsStatsReportList.Count > 0)
            {
                //callsStatsReportDictionary.Add(_currentId,callMetricsStatsReportList);
                //await ProcessStats();
                IDictionary<string, CallMatricsData> dataDict =
                    callsMetricsDictionary[_currentId].Data[RTCStatsValueName.StatsValueNameBytesReceived];
                CallMatricsData dataList = dataDict["bruka"];

                PlotlyManager.Instance.SendData(dataList.DataList, dataList.Timestamps,_currentId);

                /*PlotlyManager.Instance.SendData(callsMetricsDictionary[_currentId].Data[RTCStatsValueName.StatsValueNameBytesReceived].First().Value.DataList, callsMetricsDictionary[_currentId].Data[RTCStatsValueName.StatsValueNameBytesReceived].First().Value.Timestamps);*/
            }
        }


        public void ProcessStatsReport(RTCStatsReport report)
        {
            List<object> xList = new List<object>();
            List<object> yList = new List<object>();
            //IList<object>> dict = callsMetricsDictionary[_currentId];
            CallMatrics callMatrics = callsMetricsDictionary[_currentId];
            foreach (var statId in report.StatsIds)
            {
                RTCStats stats = report.GetStats(statId);
                switch (stats.StatsType)
                {
                    case RTCStatsType.InboundRtp:
                        RTCInboundRtpStreamStats inboundRtpStreamStats = stats.ToInboundRtp();
                        if (inboundRtpStreamStats!=null)
                        {
                            CallMatricsData dataBytesReceived = GetMetricData(statId, callMatrics, RTCStatsValueName.StatsValueNameBytesReceived);
                            dataBytesReceived.DataList.Add(inboundRtpStreamStats.BytesReceived);
                            dataBytesReceived.Timestamps.Add(stats.Timestamp);

                            CallMatricsData dataPacketsLost = GetMetricData(statId, callMatrics, RTCStatsValueName.StatsValueNamePacketsLost);
                            dataPacketsLost.DataList.Add(inboundRtpStreamStats.PacketsLost);
                            dataPacketsLost.Timestamps.Add(stats.Timestamp);

                            CallMatricsData dataPacketsReceived = GetMetricData(statId, callMatrics, RTCStatsValueName.StatsValueNamePacketsReceived);
                            dataPacketsReceived.DataList.Add(inboundRtpStreamStats.PacketsReceived);
                            dataPacketsReceived.Timestamps.Add(stats.Timestamp);
                            /* dict[StatsValueNameBytesReceived].Add(inboundRtpStreamStats.BytesReceived);
                            dict[RTCStatsValueName.StatsValueNamePacketsReceived].Add(inboundRtpStreamStats.PacketsReceived);
                            dict[RTCStatsValueName.StatsValueNamePacketsLost].Add(inboundRtpStreamStats.PacketsLost);*/
                        }
                        break;
                    case RTCStatsType.OutboundRtp:
                        RTCOutboundRtpStreamStats outboundRtpStreamStats = stats.ToOutboundRtp();
                        if (outboundRtpStreamStats != null)
                        {
                            CallMatricsData dataBytesSent = GetMetricData(statId, callMatrics, RTCStatsValueName.StatsValueNameBytesSent);
                            dataBytesSent.DataList.Add(outboundRtpStreamStats.BytesSent);
                            dataBytesSent.Timestamps.Add(stats.Timestamp);

                            CallMatricsData dataPacketsSent = GetMetricData(statId, callMatrics, RTCStatsValueName.StatsValueNamePacketsSent);
                            dataPacketsSent.DataList.Add(outboundRtpStreamStats.PacketsSent);
                            dataPacketsSent.Timestamps.Add(stats.Timestamp);
                        }
                        break;
                    case RTCStatsType.Track:
                        RTCMediaStreamTrackStats mediaStreamTrackStats = stats.ToTrack();
                        if (mediaStreamTrackStats != null)
                        {
                            CallMatricsData dataFrameRateSent = GetMetricData(statId, callMatrics, RTCStatsValueName.StatsValueNameFrameRateSent);
                            dataFrameRateSent.DataList.Add(mediaStreamTrackStats.FramesSent);
                            dataFrameRateSent.Timestamps.Add(stats.Timestamp);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private static CallMatricsData GetMetricData(string statId1, CallMatrics callMatrics, RTCStatsValueName statsValueName)
        {
            CallMatricsData data;
            IDictionary<string, CallMatricsData> dict = callMatrics.Data[statsValueName];
            string statId = "bruka";
            if (!dict.TryGetValue(statId, out data))
            {
                data = new CallMatricsData { Id = statId };
                callMatrics.Data[statsValueName].Add(statId, data);
            }
            /*else
            //CallMatricsData data = callMatrics.Data[statsValueName][statId];
            //if (data == null)
            {
                data = new CallMatricsData {Id = statId};
                callMatrics.Data[statsValueName].Add(statId, data);
            }*/
            return data;
        }

        public IAsyncAction ProcessStats()
        {
            return Task.Run(() =>
            {
                foreach (RTCStatsReport statsReport in callsStatsReportDictionary[_currentId])
                {
                    ProcessStatsReport(statsReport);
                    /*foreach (var statId in statsReport.StatsIds)
                    {
                        RTCStats stats = statsReport.GetStats(statId);
                        switch (stats.StatsType)
                        {
                            case RTCStatsType.InboundRtp:
                                RTCInboundRtpStreamStats inboundRtpStreamStats = stats.ToInboundRtp();
                                if (inboundRtpStreamStats != null)
                                {
                                    Debug.WriteLine("to");
                                }
                                break;
                            case RTCStatsType.OutboundRtp:
                                RTCOutboundRtpStreamStats outboundRtpStreamStats = stats.ToOutboundRtp();
                                if (outboundRtpStreamStats != null)
                                {
                                    Debug.WriteLine("to");
                                }
                                break;
                            case RTCStatsType.Track:
                                RTCMediaStreamTrackStats mediaStreamTrackStats = stats.ToTrack();
                                if (mediaStreamTrackStats != null)
                                {
                                    Debug.WriteLine("to");
                                }
                                break;
                            default:
                                break;
                        }
                    }*/
                }
                PlotlyManager.Instance.SendData(callsMetricsDictionary[_currentId].Data[RTCStatsValueName.StatsValueNameBytesSent].First().Value.DataList, callsMetricsDictionary[_currentId].Data[RTCStatsValueName.StatsValueNameBytesSent].First().Value.Timestamps,_currentId);
                /*RTCStatsReport report = await StatsProviderPeerConnection.GetStats();
                if (report != null)
                {
                    ProcessStatsReport(report);
                        }*/

            }).AsAsyncAction();
        }
        public static int counter = 0;
        public static bool send = true;
        public void StopCallWatch()
        {
            if (_callMetricsTimer != null)
            {
                _callMetricsTimer.Dispose();
            }
        }
        private async void CollectCallMetrics(object state)
        {
            if (counter > 20)
            {
                if (send)
                {
                    send = false;
                    counter++;
                    IDictionary<string, CallMatricsData> dataDict =
                        callsMetricsDictionary[_currentId].Data[RTCStatsValueName.StatsValueNameBytesReceived];
                    CallMatricsData dataList = dataDict["bruka"];

                    PlotlyManager.Instance.SendData(dataList.DataList, dataList.Timestamps, _currentId);
                }
                return;
            }
            RTCStatsReport report = await StatsProviderPeerConnectionCall.GetStats();
            CallMatrics callMatrics = callsMetricsDictionary[_currentId];
            if (report != null)
            {
                callMetricsStatsReportList.Add(report);
                
                foreach (var statId in report.StatsIds)
                {
                    RTCStats stats = report.GetStats(statId);
                    switch (stats.StatsType)
                    {
                        case RTCStatsType.InboundRtp:
                            Debug.WriteLine("RTCStatsType.InboundRtp:"+statId);
                            RTCInboundRtpStreamStats inboundRtpStreamStats = stats.ToInboundRtp();
                            if (inboundRtpStreamStats != null)
                            {
                                counter++;
                                CallMatricsData dataBytesReceived = GetMetricData(statId, callMatrics, RTCStatsValueName.StatsValueNameBytesReceived);
                                dataBytesReceived.DataList.Add(inboundRtpStreamStats.BytesReceived);
                                dataBytesReceived.Timestamps.Add(counter);

                                CallMatricsData dataPacketsLost = GetMetricData(statId, callMatrics, RTCStatsValueName.StatsValueNamePacketsLost);
                                dataPacketsLost.DataList.Add(inboundRtpStreamStats.PacketsLost);
                                dataPacketsLost.Timestamps.Add(counter);

                                CallMatricsData dataPacketsReceived = GetMetricData(statId, callMatrics, RTCStatsValueName.StatsValueNamePacketsReceived);
                                dataPacketsReceived.DataList.Add(inboundRtpStreamStats.PacketsReceived);
                                dataPacketsReceived.Timestamps.Add(counter);
                                
                            }
                            break;
                        case RTCStatsType.OutboundRtp:
                            RTCOutboundRtpStreamStats outboundRtpStreamStats = stats.ToOutboundRtp();
                            if (outboundRtpStreamStats != null)
                            {
                                CallMatricsData dataBytesSent = GetMetricData(statId, callMatrics, RTCStatsValueName.StatsValueNameBytesSent);
                                dataBytesSent.DataList.Add(outboundRtpStreamStats.BytesSent);
                                dataBytesSent.Timestamps.Add(counter);

                                CallMatricsData dataPacketsSent = GetMetricData(statId, callMatrics, RTCStatsValueName.StatsValueNamePacketsSent);
                                dataPacketsSent.DataList.Add(outboundRtpStreamStats.PacketsSent);
                                dataPacketsSent.Timestamps.Add(counter);
                            }
                            break;
                        case RTCStatsType.Track:
                            RTCMediaStreamTrackStats mediaStreamTrackStats = stats.ToTrack();
                            if (mediaStreamTrackStats != null)
                            {
                                CallMatricsData dataFrameRateSent = GetMetricData(statId, callMatrics, RTCStatsValueName.StatsValueNameFrameRateSent);
                                dataFrameRateSent.DataList.Add(mediaStreamTrackStats.FramesSent);
                                dataFrameRateSent.Timestamps.Add(counter);
                            }
                            break;
                        default:
                            break;
                    }
                    
                /*RTCStats stats = report.GetStats(statId);
                    switch (stats.StatsType)
                        {
                            case RTCStatsType.InboundRtp:
                                RTCInboundRtpStreamStats inboundRtpStreamStats = stats.ToInboundRtp();
                                if (inboundRtpStreamStats != null)
                                {
                                    Debug.WriteLine("to");
                                }
                                break;
                            case RTCStatsType.OutboundRtp:
                                RTCOutboundRtpStreamStats outboundRtpStreamStats = stats.ToOutboundRtp();
                                if (outboundRtpStreamStats != null)
                                {
                                    Debug.WriteLine("to");
                                }
                                break;
                            case RTCStatsType.Track:
                                RTCMediaStreamTrackStats mediaStreamTrackStats = stats.ToTrack();
                                if (mediaStreamTrackStats != null)
                                {
                                    Debug.WriteLine("to");
                                }
                                break;
                            default:
                                break;
                }*/
                }

            }
           
        }
    }
}
