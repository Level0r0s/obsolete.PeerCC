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
    public delegate void UploadedStatsData(string id);
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

    
    class OrtcStatsManager
    {
        private static volatile OrtcStatsManager _instance;
        private static readonly object SyncRoot = new object();

        private RtcPeerConnection _peerConnection;
        private string _currentId;
        private bool _isStatsCollectionEnabled;
        private Timer _callMetricsTimer;
        private const int scheduleTimeInSeconds = 1;
        private int CallDuration { get; set; }
        private RTCStatsProvider StatsProviderPeerConnection { get; set; }
        private RTCStatsProvider StatsProviderPeerConnectionCall { get; set; }
        private IList<RTCStatsReport> callMetricsStatsReportList { get; set; }

        private Dictionary<string, StatsData> callsStatsDictionary { get; set; }
        private Dictionary<string, IList<RTCStatsReport>> callsStatsReportDictionary { get; set; }
        public static int counter = 0;
        public static bool send = true;


        public static event FramesPerSecondChangedEventHandler FramesPerSecondChanged;
        public static event ResolutionChangedEventHandler ResolutionChanged;

        private OrtcStatsManager()
        {
            callsStatsReportDictionary = new Dictionary<string, IList<RTCStatsReport>>();
            callsStatsDictionary = new Dictionary<string, StatsData>();
            PlotlyManager.StatsUploaded += PlotlyManager_StatsUploaded;
        }

        private void PlotlyManager_StatsUploaded(string id)
        {
            if (callsStatsDictionary.ContainsKey(id))
                callsStatsDictionary.Remove(id);
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
            
            _currentId = /*Conductor.Instance.Peer.Name + "/" +*/ Helper.ProductName() + "/" + Helper.DeviceName() + "/" + Helper.OsVersion() + "/" + Conductor.Instance.AudioCodec.Name + "_" + Conductor.Instance.VideoCodec.Name + "/" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "/dataset";

            StatsData statsData = new StatsData();
            statsData.StarTime = DateTime.Now;
            //All call stats will be stored in this dict, so it can be safely uploaded to server, while other call can be in progress
            callsStatsDictionary.Add(_currentId,statsData);
            

            callMetricsStatsReportList = new List<RTCStatsReport>();
        }

        private void PrepareStatsForCall(string callId, bool isCaller)
        {
            _currentId = callId;

            try
            {
                StatsData statsData = new StatsData();
                statsData.IsCaller = isCaller;
                statsData.StarTime = DateTime.Now;
                //All call stats will be stored in this dict, so it can be safely uploaded to server, while other call can be in progress
                callsStatsDictionary.Add(_currentId, statsData);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            callMetricsStatsReportList = new List<RTCStatsReport>();
        }
        public void Initialize(RtcPeerConnection pc)
        {
            if (pc != null)
            {
                _peerConnection = pc;
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
                        //StartCallWatch();
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

        

        public void StartCallWatch(string callId, bool isCaller)
        {
            if (_peerConnection != null)
            {
                PrepareStatsForCall(callId, isCaller);

                RTCStatsProviderOptions options =
                    new RTCStatsProviderOptions(new List<RTCStatsType>
                    {
                        RTCStatsType.InboundRtp,
                        RTCStatsType.OutboundRtp,
                        RTCStatsType.Track
                    });
                StatsProviderPeerConnectionCall = new RTCStatsProvider(_peerConnection, options);
                CallDuration = 0;
                AutoResetEvent autoEvent = new AutoResetEvent(false);
                TimerCallback tcb = CollectCallMetrics3;
                _callMetricsTimer = new Timer(tcb, autoEvent, 0, scheduleTimeInSeconds*1000);
            }
            else
            {
                Debug.WriteLine("ORTCStatsManager: Cannot create stats provider if peer connection is null pointer");
            }
        }

        public async void CallEnded()
        {
            StopCallWatch();
            
            if (callsStatsDictionary != null && callsStatsDictionary.ContainsKey(_currentId))
            {
                await PlotlyManager.Instance.SendData(callsStatsDictionary[_currentId], _currentId);
            }
        }
        
        
        public void StopCallWatch()
        {
            if (_callMetricsTimer != null)
            {
                _callMetricsTimer.Dispose();
            }
        }



        private StatsData currentStatsData;
        internal class StatsData
        {
            public DateTime StarTime { get; set; }
            public bool IsCaller { get; set; }
            public IList<int> Timestamps { get; } 
            public Dictionary<string,TrackStatsData> TrackStatsDictionary { get; }

            public TimeSpan TimeToSetupCall { get; set; }

            public StatsData()
            {
                Timestamps = new List<int>();
                TrackStatsDictionary = new Dictionary<string, TrackStatsData>();
                
            }

            public TrackStatsData GetTrackStatsData(string trackId, bool outgoing = true)
            {
                TrackStatsData ret = null;
                if (trackId != null && TrackStatsDictionary.ContainsKey(trackId))
                    ret = TrackStatsDictionary[trackId];
                else
                {
                    ret = new TrackStatsData(trackId);
                    ret.outgoing = outgoing;
                    TrackStatsDictionary.Add(trackId,ret);
                }
                return ret;
            }
        }
        internal class TrackStatsData
        {
            public string MediaTrackId { get; set; }
            public Dictionary<RTCStatsValueName,IList<double>> Data{ get; }
            public Dictionary<RTCStatsValueName, double> LastValues { get; }

            public bool outgoing { get; set; }
            public bool isAudio { get; set; }

            private double _reactionPercentage = 0.0;
            public TrackStatsData(string trackId)
            {
                MediaTrackId = trackId;
                isAudio = trackId.Contains("audio");
                Data = new Dictionary<RTCStatsValueName, IList<double>>();
                LastValues = new Dictionary<RTCStatsValueName, double>();
            }

            public void AddData(RTCStatsValueName valueName, double value)
            {
                IList<double> list;
                if (Data.ContainsKey(valueName))
                {
                    list = Data[valueName];
                }
                else
                {
                    list = new List<double>();
                    Data.Add(valueName,list);
                }
                list.Add(value);
            }

            public void AddAverage(RTCStatsValueName valueName, double value)
            {
                try
                {
                    IList<double> list;
                    double lastValue = 0;
                    if (Data.ContainsKey(valueName))
                    {
                        list = (IList<double>)Data[valueName];
                        lastValue = list.Last();
                    }
                    else
                    {
                        list = new List<double>();
                        Data.Add(valueName, list);
                        lastValue = value;
                    }

                    /*if (LastValues.ContainsKey(valueName))
                        lastValue =  LastValues[valueName];
                    else
                        LastValues.Add(valueName, lastValue);*/

                    lastValue = ((lastValue * (1.0 - _reactionPercentage)) + (value * _reactionPercentage));
                    list.Add(lastValue);

                    /*int totalLength = list.Count;
                    double lastAverage = totalLength > 0 ? list.Last() : 0;
                    double valueToAdd = (lastAverage * totalLength + (value - lastValue)) / (totalLength + 1);
                    LastValues[valueName]=value;
                    list.Add(valueToAdd);*/
                }
                catch (Exception e)
                {
                    Debug.Write(e);
                }
                
            }
        }

        private StatsData activeStatsData;

        private void ParseStats(RTCStats stats, StatsData statsData)
        {
            switch (stats.StatsType)
            {
                case RTCStatsType.InboundRtp:
                    //Debug.WriteLine("RTCStatsType.InboundRtp:" + statId);
                    RTCInboundRtpStreamStats inboundRtpStreamStats = stats.ToInboundRtp();
                    if (inboundRtpStreamStats != null)
                    {
                        TrackStatsData tsd =
                            statsData.GetTrackStatsData(inboundRtpStreamStats.RtpStreamStats.MediaTrackId, false);

                        if (tsd != null)
                        {
                            if (statsData.TimeToSetupCall.Milliseconds == 0 && inboundRtpStreamStats.PacketsReceived > 0)
                                statsData.TimeToSetupCall = DateTime.Now - statsData.StarTime;

                            tsd.AddAverage(RTCStatsValueName.StatsValueNameBytesReceived,
                                inboundRtpStreamStats.BytesReceived);

                            tsd.AddAverage(RTCStatsValueName.StatsValueNamePacketsReceived,
                                inboundRtpStreamStats.PacketsReceived);

                            tsd.AddAverage(RTCStatsValueName.StatsValueNamePacketsLost, inboundRtpStreamStats.PacketsLost);

                            tsd.AddData(RTCStatsValueName.StatsValueNameCurrentEndToEndDelayMs,
                                inboundRtpStreamStats.EndToEndDelay);
                        }
                    }
                    break;
                case RTCStatsType.OutboundRtp:
                    RTCOutboundRtpStreamStats outboundRtpStreamStats = stats.ToOutboundRtp();
                    if (outboundRtpStreamStats != null)
                    {
                        TrackStatsData tsd =
                            statsData.GetTrackStatsData(outboundRtpStreamStats.RtpStreamStats.MediaTrackId);

                        if (tsd != null)
                        {
                            tsd.AddAverage(RTCStatsValueName.StatsValueNameBytesSent, outboundRtpStreamStats.BytesSent);

                            tsd.AddAverage(RTCStatsValueName.StatsValueNamePacketsSent, outboundRtpStreamStats.PacketsSent);
                        }
                    }
                    break;
                case RTCStatsType.Track:
                    RTCMediaStreamTrackStats mediaStreamTrackStats = stats.ToTrack();
                    if (mediaStreamTrackStats != null)
                    {
                        TrackStatsData tsd =
                            statsData.GetTrackStatsData(mediaStreamTrackStats.TrackId,!mediaStreamTrackStats.RemoteSource);
                        
                        if (tsd != null && !tsd.isAudio)
                        {
                            if (mediaStreamTrackStats.RemoteSource)
                            {
                                tsd.AddData(RTCStatsValueName.StatsValueNameFrameRateReceived,
                                    mediaStreamTrackStats.FramesPerSecond);
                                tsd.AddData(RTCStatsValueName.StatsValueNameFrameWidthReceived,
                                    mediaStreamTrackStats.FrameWidth);
                                tsd.AddData(RTCStatsValueName.StatsValueNameFrameHeightReceived,
                                    mediaStreamTrackStats.FrameHeight);
                                FramesPerSecondChanged?.Invoke("PEER", mediaStreamTrackStats.FramesPerSecond.ToString("0.#"));
                                ResolutionChanged?.Invoke("PEER", mediaStreamTrackStats.FrameWidth, mediaStreamTrackStats.FrameHeight);
                            }
                            else
                            {
                                tsd.AddData(RTCStatsValueName.StatsValueNameFrameRateSent,
                                    mediaStreamTrackStats.FramesPerSecond);
                                tsd.AddData(RTCStatsValueName.StatsValueNameFrameWidthSent,
                                    mediaStreamTrackStats.FrameWidth);
                                tsd.AddData(RTCStatsValueName.StatsValueNameFrameHeightSent,
                                    mediaStreamTrackStats.FrameHeight);
                                FramesPerSecondChanged?.Invoke("SELF", mediaStreamTrackStats.FramesPerSecond.ToString("0.#"));
                                ResolutionChanged?.Invoke("SELF", mediaStreamTrackStats.FrameWidth, mediaStreamTrackStats.FrameHeight);
                            }
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        private void UpdateCurrentFrame(RTCStats stats, StatsData statsData)
        {
            switch (stats.StatsType)
            {
                case RTCStatsType.Track:
                    RTCMediaStreamTrackStats mediaStreamTrackStats = stats.ToTrack();
                    if (mediaStreamTrackStats != null && !mediaStreamTrackStats.TrackId.Contains("audio"))
                    {
                        if (mediaStreamTrackStats.RemoteSource)
                        {
                            FramesPerSecondChanged?.Invoke("PEER", mediaStreamTrackStats.FramesPerSecond.ToString("0.#"));
                            ResolutionChanged?.Invoke("PEER", mediaStreamTrackStats.FrameWidth, mediaStreamTrackStats.FrameHeight);
                        }
                        else
                        {
                            FramesPerSecondChanged?.Invoke("SELF", mediaStreamTrackStats.FramesPerSecond.ToString("0.#"));
                            ResolutionChanged?.Invoke("SELF", mediaStreamTrackStats.FrameWidth, mediaStreamTrackStats.FrameHeight);
                        }
                    }
                    break;
                default:
                    break;
            }
        }
        private async void CollectCallMetrics3(object state)
        {
            CallDuration += scheduleTimeInSeconds;
            StatsData statsData = callsStatsDictionary[_currentId];
            statsData.Timestamps.Add(CallDuration);
            RTCStatsReport report = await StatsProviderPeerConnectionCall.GetStats();
            if (report != null)
            {
                foreach (var statId in report.StatsIds)
                {
                    RTCStats stats = report.GetStats(statId);
                    if (IsStatsCollectionEnabled)
                        ParseStats(stats, statsData);
                    else
                        UpdateCurrentFrame(stats,statsData);
                }
            }
        }

    }
}
