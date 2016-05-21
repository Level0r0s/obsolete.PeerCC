//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Threading;
using Microsoft.ApplicationInsights.Extensibility;
using Windows.Networking.Connectivity;
using org.ortc;
using org.ortc.adapter;
using RtcPeerConnection = org.ortc.adapter.RTCPeerConnection;

#if USE_WEBRTC_API
using RtcPeerConnection = webrtc_winrt_api.RTCPeerConnection;
using RtcStatsReport = webrtc_winrt_api.RTCStatsReport;
using RtcStatsType = webrtc_winrt_api.RTCStatsType;
using RtcStatsValueName = webrtc_winrt_api.RTCStatsValueName;
using RtcStatsReportsReadyEvent = webrtc_winrt_api.RTCStatsReportsReadyEvent;
#elif USE_ORTC_API
using RtcPeerConnection = ChatterBox.Client.Voip.Rtc.RTCPeerConnection;
using RtcStatsReport = ChatterBox.Client.Voip.Rtc.RTCStatsReport;
using RtcStatsType = ChatterBox.Client.Voip.Rtc.RTCStatsType;
using RtcStatsValueName = ChatterBox.Client.Voip.Rtc.RTCStatsValueName;
using RtcStatsReportsReadyEvent = ChatterBox.Client.Voip.Rtc.RTCStatsReportsReadyEvent;
#endif //USE_WEBRTC_API

namespace ChatterBox.Client.Universal.Background
{
    internal sealed class RtcStatsReport
    {
        //public RTCStatsReport() { }

        public RtcStatsType StatsType { get; set; }
        //public double Timestamp { get; set; }
        public IDictionary<RtcStatsValueName, object> Values { get; set; }
    }

    internal sealed class RtcStatsReportsReadyEvent
    {
        //public RTCStatsReportsReadyEvent() { }

        public IList<RtcStatsReport> rtcStatsReports { get; set; }
    }

    internal enum RtcStatsType
    {
        StatsReportTypeSession = 0,
        StatsReportTypeTransport = 1,
        StatsReportTypeComponent = 2,
        StatsReportTypeCandidatePair = 3,
        StatsReportTypeBwe = 4,
        StatsReportTypeSsrc = 5,
        StatsReportTypeRemoteSsrc = 6,
        StatsReportTypeTrack = 7,
        StatsReportTypeIceLocalCandidate = 8,
        StatsReportTypeIceRemoteCandidate = 9,
        StatsReportTypeCertificate = 10,
        StatsReportTypeDataChannel = 11
    }

    internal enum RtcStatsValueName
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
    internal sealed class StatsManager
    {
        public StatsManager()
        {
            _telemetry = new TelemetryClient();
            _telemetry.Context.Session.Id = Guid.NewGuid().ToString();
        }

        ~StatsManager() {
            _telemetry.Flush();
        }

        RtcPeerConnection _peerConnection;
        TelemetryClient _telemetry;
        Timer _metricsTimer;
        Timer _networkTimer;
        AudioVideoMetricsCollector _metricsCollector;
        public void Initialize(RtcPeerConnection pc)
        {
            if (pc != null)
            {
                _peerConnection = pc;
#warning StatsManager: Check what should do with OnRTCStatsReportsReady
                //_peerConnection.OnRTCStatsReportsReady += PeerConnection_OnRTCStatsReportsReady;
            }
            else
            {
                Debug.WriteLine("StatsManager: Cannot initialize peer connection by null pointer");
            }
        }

        public void Reset()
        {
            if (_peerConnection != null)
            {
#warning StatsManager: Check what should do with ToggleRTCStats
                //_peerConnection.ToggleRTCStats(false);
                _peerConnection = null;
            }
            if (_metricsTimer != null) {
                _metricsTimer.Dispose();
            }
        }

        private bool _isStatsCollectionEnabled;
        public bool IsStatsCollectionEnabled
        {
            get { return _isStatsCollectionEnabled; }
            set
            {
                _isStatsCollectionEnabled = value;
                if (_peerConnection != null)
                {
#warning StatsManager: Check what should do with ToggleRTCStats
                    //_peerConnection.ToggleRTCStats(value);
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
        }

        public void DisableTelemetry(bool disable)
        {
            TelemetryConfiguration.Active.DisableTelemetry = disable;
        }

        private void ProcessReports(IList<RtcStatsReport> reports)
        {
            foreach (var report in reports)
            {
                if (report.StatsType == RtcStatsType.StatsReportTypeSsrc) {
                    IDictionary<RtcStatsValueName, Object> statValues = report.Values;
                    if (statValues.Keys.Contains(RtcStatsValueName.StatsValueNameTrackId))
                    {
                        string trackId = statValues[RtcStatsValueName.StatsValueNameTrackId].ToString();
                        if (trackId.StartsWith("audio_label"))
                        {
                            if (statValues.Keys.Contains(RtcStatsValueName.StatsValueNamePacketsSent)) {
                                _metricsCollector._audioPacketsSent += Convert.ToInt32(statValues[RtcStatsValueName.StatsValueNamePacketsSent]);
                            }
                            if (statValues.Keys.Contains(RtcStatsValueName.StatsValueNamePacketsLost))
                            {
                                _metricsCollector._audioPacketsLost += Convert.ToInt32(statValues[RtcStatsValueName.StatsValueNamePacketsLost]);
                            }
                            if (statValues.Keys.Contains(RtcStatsValueName.StatsValueNameCurrentDelayMs))
                            {
                                _metricsCollector._audioCurrentDelayMs += Convert.ToDouble(statValues[RtcStatsValueName.StatsValueNameCurrentDelayMs]);
                                _metricsCollector._audioDelayCount++;
                            }
                            if (statValues.Keys.Contains(RtcStatsValueName.StatsValueNameCodecName))
                            {
                                _metricsCollector.AudioCodec = statValues[RtcStatsValueName.StatsValueNameCodecName].ToString();
                            }
                        }
                        else if (trackId.StartsWith("video_label"))
                        {
                            if (statValues.Keys.Contains(RtcStatsValueName.StatsValueNamePacketsSent))
                            {
                                _metricsCollector._videoPacketsSent += Convert.ToInt32(statValues[RtcStatsValueName.StatsValueNamePacketsSent]);
                            }
                            if (statValues.Keys.Contains(RtcStatsValueName.StatsValueNamePacketsLost))
                            {
                                _metricsCollector._videoPacketsLost += Convert.ToInt32(statValues[RtcStatsValueName.StatsValueNamePacketsLost]);
                            }
                            if (statValues.Keys.Contains(RtcStatsValueName.StatsValueNameCurrentDelayMs))
                            {
                                _metricsCollector._videoCurrentDelayMs += Convert.ToDouble(statValues[RtcStatsValueName.StatsValueNameCurrentDelayMs]);
                                _metricsCollector._videoDelayCount++;
                            }
                            if (statValues.Keys.Contains(RtcStatsValueName.StatsValueNameFrameHeightSent))
                            {
                                _metricsCollector.FrameHeight = Convert.ToInt32(statValues[RtcStatsValueName.StatsValueNameFrameHeightSent]);
                            }
                            if (statValues.Keys.Contains(RtcStatsValueName.StatsValueNameFrameWidthSent))
                            {
                                _metricsCollector.FrameWidth = Convert.ToInt32(statValues[RtcStatsValueName.StatsValueNameFrameWidthSent]);
                            }
                            if (statValues.Keys.Contains(RtcStatsValueName.StatsValueNameCodecName))
                            {
                                _metricsCollector.VideoCodec = statValues[RtcStatsValueName.StatsValueNameCodecName].ToString();
                            }
                        }
                    }
                }
            }
        }

        private void PeerConnection_OnRTCStatsReportsReady(RtcStatsReportsReadyEvent evt)
        {
            IList<RtcStatsReport> reports = evt.rtcStatsReports;
            Task.Run(() => ProcessReports(reports));
        }

        private string ToMetricName(RtcStatsValueName name)
        {
            switch (name)
            {
                case RtcStatsValueName.StatsValueNameAudioOutputLevel:
                    return "audioOutputLevel";
                case RtcStatsValueName.StatsValueNameAudioInputLevel:
                    return "audioInputLevel";
                case RtcStatsValueName.StatsValueNameBytesSent:
                    return "bytesSent";
                case RtcStatsValueName.StatsValueNamePacketsSent:
                    return "packetsSent";
                case RtcStatsValueName.StatsValueNameBytesReceived:
                    return "bytesReceived";
                case RtcStatsValueName.StatsValueNameLabel:
                    return "label";
                case RtcStatsValueName.StatsValueNamePacketsReceived:
                    return "packetsReceived";
                case RtcStatsValueName.StatsValueNamePacketsLost:
                    return "packetsLost";
                case RtcStatsValueName.StatsValueNameProtocol:
                    return "protocol";
                case RtcStatsValueName.StatsValueNameTransportId:
                    return "transportId";
                case RtcStatsValueName.StatsValueNameSelectedCandidatePairId:
                    return "selectedCandidatePairId";
                case RtcStatsValueName.StatsValueNameSsrc:
                    return "ssrc";
                case RtcStatsValueName.StatsValueNameState:
                    return "state";
                case RtcStatsValueName.StatsValueNameDataChannelId:
                    return "datachannelid";

                // 'goog' prefixed constants.
                case RtcStatsValueName.StatsValueNameAccelerateRate:
                    return "googAccelerateRate";
                case RtcStatsValueName.StatsValueNameActiveConnection:
                    return "googActiveConnection";
                case RtcStatsValueName.StatsValueNameActualEncBitrate:
                    return "googActualEncBitrate";
                case RtcStatsValueName.StatsValueNameAvailableReceiveBandwidth:
                    return "googAvailableReceiveBandwidth";
                case RtcStatsValueName.StatsValueNameAvailableSendBandwidth:
                    return "googAvailableSendBandwidth";
                case RtcStatsValueName.StatsValueNameAvgEncodeMs:
                    return "googAvgEncodeMs";
                case RtcStatsValueName.StatsValueNameBucketDelay:
                    return "googBucketDelay";
                case RtcStatsValueName.StatsValueNameBandwidthLimitedResolution:
                    return "googBandwidthLimitedResolution";

                // Candidate related attributes. Values are taken from
                // http://w3c.github.io/webrtc-stats/#rtcstatstype-enum*.
                case RtcStatsValueName.StatsValueNameCandidateIPAddress:
                    return "ipAddress";
                case RtcStatsValueName.StatsValueNameCandidateNetworkType:
                    return "networkType";
                case RtcStatsValueName.StatsValueNameCandidatePortNumber:
                    return "portNumber";
                case RtcStatsValueName.StatsValueNameCandidatePriority:
                    return "priority";
                case RtcStatsValueName.StatsValueNameCandidateTransportType:
                    return "transport";
                case RtcStatsValueName.StatsValueNameCandidateType:
                    return "candidateType";

                case RtcStatsValueName.StatsValueNameChannelId:
                    return "googChannelId";
                case RtcStatsValueName.StatsValueNameCodecName:
                    return "googCodecName";
                case RtcStatsValueName.StatsValueNameComponent:
                    return "googComponent";
                case RtcStatsValueName.StatsValueNameContentName:
                    return "googContentName";
                case RtcStatsValueName.StatsValueNameCpuLimitedResolution:
                    return "googCpuLimitedResolution";
                case RtcStatsValueName.StatsValueNameDecodingCTSG:
                    return "googDecodingCTSG";
                case RtcStatsValueName.StatsValueNameDecodingCTN:
                    return "googDecodingCTN";
                case RtcStatsValueName.StatsValueNameDecodingNormal:
                    return "googDecodingNormal";
                case RtcStatsValueName.StatsValueNameDecodingPLC:
                    return "googDecodingPLC";
                case RtcStatsValueName.StatsValueNameDecodingCNG:
                    return "googDecodingCNG";
                case RtcStatsValueName.StatsValueNameDecodingPLCCNG:
                    return "googDecodingPLCCNG";
                case RtcStatsValueName.StatsValueNameDer:
                    return "googDerBase64";
                case RtcStatsValueName.StatsValueNameDtlsCipher:
                    return "dtlsCipher";
                case RtcStatsValueName.StatsValueNameEchoCancellationQualityMin:
                    return "googEchoCancellationQualityMin";
                case RtcStatsValueName.StatsValueNameEchoDelayMedian:
                    return "googEchoCancellationEchoDelayMedian";
                case RtcStatsValueName.StatsValueNameEchoDelayStdDev:
                    return "googEchoCancellationEchoDelayStdDev";
                case RtcStatsValueName.StatsValueNameEchoReturnLoss:
                    return "googEchoCancellationReturnLoss";
                case RtcStatsValueName.StatsValueNameEchoReturnLossEnhancement:
                    return "googEchoCancellationReturnLossEnhancement";
                case RtcStatsValueName.StatsValueNameEncodeUsagePercent:
                    return "googEncodeUsagePercent";
                case RtcStatsValueName.StatsValueNameExpandRate:
                    return "googExpandRate";
                case RtcStatsValueName.StatsValueNameFingerprint:
                    return "googFingerprint";
                case RtcStatsValueName.StatsValueNameFingerprintAlgorithm:
                    return "googFingerprintAlgorithm";
                case RtcStatsValueName.StatsValueNameFirsReceived:
                    return "googFirsReceived";
                case RtcStatsValueName.StatsValueNameFirsSent:
                    return "googFirsSent";
                case RtcStatsValueName.StatsValueNameFrameHeightInput:
                    return "googFrameHeightInput";
                case RtcStatsValueName.StatsValueNameFrameHeightReceived:
                    return "googFrameHeightReceived";
                case RtcStatsValueName.StatsValueNameFrameHeightSent:
                    return "googFrameHeightSent";
                case RtcStatsValueName.StatsValueNameFrameRateReceived:
                    return "googFrameRateReceived";
                case RtcStatsValueName.StatsValueNameFrameRateDecoded:
                    return "googFrameRateDecoded";
                case RtcStatsValueName.StatsValueNameFrameRateOutput:
                    return "googFrameRateOutput";
                case RtcStatsValueName.StatsValueNameDecodeMs:
                    return "googDecodeMs";
                case RtcStatsValueName.StatsValueNameMaxDecodeMs:
                    return "googMaxDecodeMs";
                case RtcStatsValueName.StatsValueNameCurrentDelayMs:
                    return "googCurrentDelayMs";
                case RtcStatsValueName.StatsValueNameTargetDelayMs:
                    return "googTargetDelayMs";
                case RtcStatsValueName.StatsValueNameJitterBufferMs:
                    return "googJitterBufferMs";
                case RtcStatsValueName.StatsValueNameMinPlayoutDelayMs:
                    return "googMinPlayoutDelayMs";
                case RtcStatsValueName.StatsValueNameRenderDelayMs:
                    return "googRenderDelayMs";
                case RtcStatsValueName.StatsValueNameCaptureStartNtpTimeMs:
                    return "googCaptureStartNtpTimeMs";
                case RtcStatsValueName.StatsValueNameFrameRateInput:
                    return "googFrameRateInput";
                case RtcStatsValueName.StatsValueNameFrameRateSent:
                    return "googFrameRateSent";
                case RtcStatsValueName.StatsValueNameFrameWidthInput:
                    return "googFrameWidthInput";
                case RtcStatsValueName.StatsValueNameFrameWidthReceived:
                    return "googFrameWidthReceived";
                case RtcStatsValueName.StatsValueNameFrameWidthSent:
                    return "googFrameWidthSent";
                case RtcStatsValueName.StatsValueNameInitiator:
                    return "googInitiator";
                case RtcStatsValueName.StatsValueNameIssuerId:
                    return "googIssuerId";
                case RtcStatsValueName.StatsValueNameJitterReceived:
                    return "googJitterReceived";
                case RtcStatsValueName.StatsValueNameLocalAddress:
                    return "googLocalAddress";
                case RtcStatsValueName.StatsValueNameLocalCandidateId:
                    return "localCandidateId";
                case RtcStatsValueName.StatsValueNameLocalCandidateType:
                    return "googLocalCandidateType";
                case RtcStatsValueName.StatsValueNameLocalCertificateId:
                    return "localCertificateId";
                case RtcStatsValueName.StatsValueNameAdaptationChanges:
                    return "googAdaptationChanges";
                case RtcStatsValueName.StatsValueNameNacksReceived:
                    return "googNacksReceived";
                case RtcStatsValueName.StatsValueNameNacksSent:
                    return "googNacksSent";
                case RtcStatsValueName.StatsValueNamePreemptiveExpandRate:
                    return "googPreemptiveExpandRate";
                case RtcStatsValueName.StatsValueNamePlisReceived:
                    return "googPlisReceived";
                case RtcStatsValueName.StatsValueNamePlisSent:
                    return "googPlisSent";
                case RtcStatsValueName.StatsValueNamePreferredJitterBufferMs:
                    return "googPreferredJitterBufferMs";
                case RtcStatsValueName.StatsValueNameRemoteAddress:
                    return "googRemoteAddress";
                case RtcStatsValueName.StatsValueNameRemoteCandidateId:
                    return "remoteCandidateId";
                case RtcStatsValueName.StatsValueNameRemoteCandidateType:
                    return "googRemoteCandidateType";
                case RtcStatsValueName.StatsValueNameRemoteCertificateId:
                    return "remoteCertificateId";
                case RtcStatsValueName.StatsValueNameRetransmitBitrate:
                    return "googRetransmitBitrate";
                case RtcStatsValueName.StatsValueNameRtt:
                    return "googRtt";
                case RtcStatsValueName.StatsValueNameSecondaryDecodedRate:
                    return "googSecondaryDecodedRate";
                case RtcStatsValueName.StatsValueNameSendPacketsDiscarded:
                    return "packetsDiscardedOnSend";
                case RtcStatsValueName.StatsValueNameSpeechExpandRate:
                    return "googSpeechExpandRate";
                case RtcStatsValueName.StatsValueNameSrtpCipher:
                    return "srtpCipher";
                case RtcStatsValueName.StatsValueNameTargetEncBitrate:
                    return "googTargetEncBitrate";
                case RtcStatsValueName.StatsValueNameTransmitBitrate:
                    return "googTransmitBitrate";
                case RtcStatsValueName.StatsValueNameTransportType:
                    return "googTransportType";
                case RtcStatsValueName.StatsValueNameTrackId:
                    return "googTrackId";
                case RtcStatsValueName.StatsValueNameTypingNoiseState:
                    return "googTypingNoiseState";
                case RtcStatsValueName.StatsValueNameViewLimitedResolution:
                    return "googViewLimitedResolution";
                case RtcStatsValueName.StatsValueNameWritable:
                    return "googWritable";
                case RtcStatsValueName.StatsValueNameCurrentEndToEndDelayMs:
                    return "currentEndToEndDelayMs";
                default:
                    return String.Empty;
            }
        }

        public void TrackEvent(String name)
        {
            Task.Run(() => _telemetry.TrackEvent(name));
        }

        public void TrackEvent(String name, IDictionary<string, string> props)
        {
            if (props == null)
            {
                Task.Run(() => _telemetry.TrackEvent(name));
            }
            else
            {
                props.Add("Timestamp", System.DateTimeOffset.UtcNow.ToString(@"hh\:mm\:ss"));
                Task.Run(() => _telemetry.TrackEvent(name, props));
            }
        }

        public void TrackMetric(String name, double value) {
            MetricTelemetry metric = new MetricTelemetry(name, value);
            metric.Timestamp = System.DateTimeOffset.UtcNow;
            Task.Run(() => _telemetry.TrackMetric(metric));
        }

        public void TrackException(Exception e) {
            ExceptionTelemetry excTelemetry = new ExceptionTelemetry(e);
            excTelemetry.SeverityLevel = SeverityLevel.Critical;
            excTelemetry.HandledAt = ExceptionHandledAt.Unhandled;
            excTelemetry.Timestamp = System.DateTimeOffset.UtcNow;
            _telemetry.TrackException(excTelemetry);
        }

        private Stopwatch _callWatch;
        public void StartCallWatch()
        {
            _telemetry.Context.Operation.Name = "Call Duration tracking";

            _callWatch = Stopwatch.StartNew();

            AutoResetEvent autoEvent = new AutoResetEvent(false);
            Debug.Assert(_metricsCollector != null);
            TimerCallback tcb = _metricsCollector.CollectNewtorkMetrics;
            _networkTimer = new Timer(tcb, autoEvent, 0, 20000);

        }

        public void StopCallWatch()
        {
            if (_callWatch != null)
            {
                _callWatch.Stop();
                DateTime currentDateTime = DateTime.Now;
                TimeSpan time = _callWatch.Elapsed;
                Task.Run(() => _telemetry.TrackRequest("Call Duration", currentDateTime,
                   time,
                   "200", true));  // Response code, success
                if (_metricsCollector != null)
                {
                    _metricsCollector.TrackCurrentDelayMetrics();
                    _metricsCollector.TrackNetworkQualityMetrics();
                }
                if (_networkTimer != null)
                {
                    _networkTimer.Dispose();
                }
            }
        }
    }

    class AudioVideoMetricsCollector
    {
        private TelemetryClient _telemetry;
        public int _audioPacketsSent;
        public int _audioPacketsLost;
        public int _videoPacketsSent;
        public int _videoPacketsLost;
        public double _audioCurrentDelayMs;
        public int _audioDelayCount;
        public double _videoCurrentDelayMs;
        public int _videoDelayCount;
        ulong _inboundMaxBitsPerSecondSum;
        int _inboundMaxBitsPerSecondCount;
        ulong _outboundMaxBitsPerSecondSum;
        int _outboundMaxBitsPerSecondCount;

        private  int _frameHeight;
        public int FrameHeight
        {
            get { return _frameHeight; }
            set
            {
                if (_frameHeight > value)
                {
                    TrackVideoResolutionDowngrade(_frameHeight, value, "Height");
                }
                _frameHeight = value;
            }
        }

        private int _frameWidth;
        public int FrameWidth
        {
            get { return _frameWidth; }
            set
            {
                if (_frameWidth > value)
                {
                    TrackVideoResolutionDowngrade(_frameWidth, value, "Width");
                }
                _frameWidth = value;
            }
        }

        private string _audioCodec;
        public string AudioCodec
        {
            get { return _audioCodec; }
            set
            {
                if (_audioCodec != value)
                {
                    _audioCodec = value;
                    TrackCodecUseForCall(value, "Audio");
                }
            }
        }

        private string _videoCodec;
        public string VideoCodec
        {
            get { return _videoCodec; }
            set
            {
                if (_videoCodec != value)
                {
                    _videoCodec = value;
                    TrackCodecUseForCall(value, "Video");
                }
            }
        }
        public AudioVideoMetricsCollector(TelemetryClient tc)
        {
            _telemetry = tc;
            ResetPackets();
            ResetDelays();
            _frameHeight = 0;
            _frameWidth = 0;
            _inboundMaxBitsPerSecondSum = 0;
            _inboundMaxBitsPerSecondCount = 0;
            _outboundMaxBitsPerSecondSum = 0;
            _outboundMaxBitsPerSecondCount = 0;
        }

        private void ResetPackets()
        {
            _audioPacketsSent = 0;
            _audioPacketsLost = 0;
            _videoPacketsSent = 0;
            _videoPacketsLost = 0;
        }
        private void ResetDelays()
        {
            _audioCurrentDelayMs = 0;
            _audioDelayCount = 0;
            _videoCurrentDelayMs = 0;
            _videoDelayCount = 0;
        }

        public void TrackMetrics(Object state)
        {
            double audioPacketRatio = (_audioPacketsSent != 0) ? (double) _audioPacketsLost / _audioPacketsSent : 0;
            double videoPacketRatio = (_videoPacketsSent != 0) ? (double) _videoPacketsLost / _videoPacketsSent : 0;

            if (_audioPacketsSent > 0) {
                MetricTelemetry metric = new MetricTelemetry("Audio Packet Lost Ratio", audioPacketRatio);
                metric.Timestamp = System.DateTimeOffset.UtcNow;
                Task.Run(() => _telemetry.TrackMetric(metric));
            }

            if (_videoPacketsSent > 0)
            {
                MetricTelemetry metric = new MetricTelemetry("Video Packet Lost Ratio", videoPacketRatio);
                metric.Timestamp = System.DateTimeOffset.UtcNow;
                Task.Run(() => _telemetry.TrackMetric(metric));
            }

            // reset flags for the new time period
            ResetPackets();
        }

        public void CollectNewtorkMetrics(Object state)
        {
            var networkAdapter = NetworkInformation.GetInternetConnectionProfile().NetworkAdapter;
            _inboundMaxBitsPerSecondSum += networkAdapter.InboundMaxBitsPerSecond;
            _outboundMaxBitsPerSecondSum += networkAdapter.OutboundMaxBitsPerSecond;
            _inboundMaxBitsPerSecondCount++;
            _outboundMaxBitsPerSecondCount++;
        }

        public void TrackCurrentDelayMetrics()
        {
            if (_audioDelayCount > 0)
            {
                MetricTelemetry metric = new MetricTelemetry("Audio Current Delay Ratio", _audioCurrentDelayMs / _audioDelayCount);
                metric.Timestamp = System.DateTimeOffset.UtcNow;
                Task.Run(() => _telemetry.TrackMetric(metric));
            }
            if (_videoDelayCount > 0)
            {
                MetricTelemetry metric = new MetricTelemetry("Video Current Delay Ratio", _videoCurrentDelayMs / _videoDelayCount);
                metric.Timestamp = System.DateTimeOffset.UtcNow;
                Task.Run(() => _telemetry.TrackMetric(metric));
            }
            ResetDelays();
        }

        private void TrackVideoResolutionDowngrade(int oldValue, int newValue, string name)
        {
            IDictionary<string, string> properties = new Dictionary<string, string> {
                { "Timestamp", System.DateTimeOffset.UtcNow.ToString(@"hh\:mm\:ss") } };
            IDictionary<string, double> metrics = new Dictionary<string, double> {
                { "Old " + name, oldValue},
                { "New " + name, newValue} };
            Task.Run(() => _telemetry.TrackEvent("Video " + name + " Downgrade", properties, metrics));
        }

        private void TrackCodecUseForCall(string codecValue, string codecType)
        {
            IDictionary<string, string> properties = new Dictionary<string, string> {
                { "Timestamp", System.DateTimeOffset.UtcNow.ToString(@"hh\:mm\:ss") },
                { codecType + " codec used for call", codecValue}};
            Task.Run(() => _telemetry.TrackEvent(codecType + " codec", properties));
        }

        public void TrackNetworkQualityMetrics()
        {
            IDictionary<string, string> properties = new Dictionary<string, string> {
                { "Timestamp", System.DateTimeOffset.UtcNow.ToString(@"hh\:mm\:ss") } };
            IDictionary<string, double> metrics = new Dictionary<string, double>();

            if (_inboundMaxBitsPerSecondCount != 0)
            {
                metrics.Add("Maximum Inbound Speed (bit/sec)",
                    (double)_inboundMaxBitsPerSecondSum / _inboundMaxBitsPerSecondCount);
            }
            if (_outboundMaxBitsPerSecondCount != 0)
            {
                metrics.Add("Maximum Outbound Speed (bit/sec)",
                    (double)_outboundMaxBitsPerSecondSum / _outboundMaxBitsPerSecondCount);
            }
            Task.Run(() => _telemetry.TrackEvent("Network Avarage Quality During Call", properties, metrics));
            _inboundMaxBitsPerSecondSum = 0;
            _inboundMaxBitsPerSecondCount = 0;
            _outboundMaxBitsPerSecondSum = 0;
            _outboundMaxBitsPerSecondCount = 0; 
        }
    }

   
}
