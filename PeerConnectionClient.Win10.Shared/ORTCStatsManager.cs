using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using org.ortc;
using org.ortc.adapter;
using PeerConnectionClient.Utilities;
using RtcPeerConnection = org.ortc.adapter.RTCPeerConnection;

namespace PeerConnectionClient.Win10.Shared
{

    class ORTCStatsManager
    {
        private static volatile ORTCStatsManager _instance;
        private static readonly object SyncRoot = new object();

        private RtcPeerConnection _peerConnection;
        private bool _isStatsCollectionEnabled;
        private Timer _callMetricsTimer;

        private RTCStatsProvider statsProviderPeerConnection { get; set; }
        private RTCStatsProvider statsProviderPeerConnectionCall { get; set; }
        private IList<RTCStatsReport> callMetricsStatsReportList;
        private ORTCStatsManager()
        {
            this.callMetricsStatsReportList = new List<RTCStatsReport>();
        }

        public static ORTCStatsManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (SyncRoot)
                {
                    if (_instance == null) _instance = new ORTCStatsManager();
                }

                return _instance;
            }
        }

        public void Initialize(RtcPeerConnection pc)
        {
            if (pc != null)
            {
                _peerConnection = pc;

                RTCStatsProviderOptions options =
                    new RTCStatsProviderOptions(new List<RTCStatsType>
                    {
                        RTCStatsType.IceGatherer,
                        RTCStatsType.Codec,
                        RTCStatsType.DtlsTransport
                    });

                statsProviderPeerConnection = new RTCStatsProvider(pc, options);
            }
            else
            {
                Debug.WriteLine("ORTCStatsManager: Cannot initialize peer connection by null pointer");
            }
        }

        public void Reset()
        {
            _peerConnection = null;
            callMetricsStatsReportList.Clear();
        }

        /*public bool IsStatsCollectionEnabled
        {
            get { return _isStatsCollectionEnabled; }
            set
            {
                _isStatsCollectionEnabled = value;
                if (_peerConnection != null)
                {
                    if (_isStatsCollectionEnabled)
                    {
                        AutoResetEvent autoEvent = new AutoResetEvent(false);
                        _metricsCollector = new AudioVideoMetricsCollector(_telemetry);
                        TimerCallback tcb = _metricsCollector.TrackMetrics;
                        _metricsTimer = new Timer(tcb, autoEvent, 60000, 60000);
                    }
                    else {
                        Reset();
                    }
                }
                else
                {
                    Debug.WriteLine("StatsManager: Stats are not toggled as manager is not initialized yet.");
                }
            }
        }*/

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
                statsProviderPeerConnectionCall = new RTCStatsProvider(_peerConnection, options);

                AutoResetEvent autoEvent = new AutoResetEvent(false);
                TimerCallback tcb = CollectCallMetrics;
                _callMetricsTimer = new Timer(tcb, autoEvent, 0, 20000);
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
                await ProcessStats();
        }


        public void ProcessStatsReport(RTCStatsReport report)
        {
            foreach (var statId in report.StatsIds)
            {
                RTCStats stats = report.GetStats(statId);
                switch (stats.StatsType)
                {
                    case RTCStatsType.InboundRtp:
                        RTCInboundRtpStreamStats inboundRtpStreamStats = stats.ToInboundRtp();
                        break;
                    case RTCStatsType.OutboundRtp:
                        RTCOutboundRtpStreamStats outboundRtpStreamStats = stats.ToOutboundRtp();
                        break;
                    case RTCStatsType.Track:
                        RTCMediaStreamTrackStats mediaStreamTrackStats = stats.ToTrack();
                        break;
                    default:
                        break;
                }
            }
        }
        public IAsyncAction ProcessStats()
        {
            return Task.Run(async () =>
            {
                foreach (RTCStatsReport statsReport in callMetricsStatsReportList)
                {
                    ProcessStatsReport(statsReport);
                    /*foreach (var statId in statsReport.StatsIds)
                    {
                        RTCStats stats = statsReport.GetStats(statId);
                        switch (stats.StatsType)
                        {
                            case RTCStatsType.InboundRtp:
                                RTCInboundRtpStreamStats inboundRtpStreamStats = stats.ToInboundRtp();
                                break;
                            case RTCStatsType.OutboundRtp:
                                RTCOutboundRtpStreamStats outboundRtpStreamStats = stats.ToOutboundRtp();
                                break;
                            case RTCStatsType.Track:
                                RTCMediaStreamTrackStats mediaStreamTrackStats = stats.ToTrack();
                                break;
                            default:
                                break;
                        }
                    }*/
                }

                RTCStatsReport report = await statsProviderPeerConnection.GetStats();
                if (report != null)
                {
                    ProcessStatsReport(report);
                }

            }).AsAsyncAction();
        }

        public void StopCallWatch()
        {
            if (_callMetricsTimer != null)
            {
                _callMetricsTimer.Dispose();
            }
        }
        private async void CollectCallMetrics(object state)
        {
            RTCStatsReport report = await statsProviderPeerConnectionCall.GetStats();
            if (report != null)
            {
                callMetricsStatsReportList.Add(report);
                /*
                foreach (var statId in report.StatsIds)
                {
                    RTCStats stats = report.GetStats(statId);
                    callMetricsStatsList.Add(stats);
                    switch (stats.StatsType)
                    {
                        case RTCStatsType.InboundRtp:
                            RTCInboundRtpStreamStats inboundRtpStreamStats = stats.ToInboundRtp();
                            break;
                        case RTCStatsType.OutboundRtp:
                            RTCOutboundRtpStreamStats outboundRtpStreamStats = stats.ToOutboundRtp();
                            break;
                        case RTCStatsType.Track:
                            RTCMediaStreamTrackStats mediaStreamTrackStats = stats.ToTrack();
                            break;
                        default:
                            break;
                    }
                }*/

            }
        }
    }
}
