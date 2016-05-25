using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using org.ortc;
using org.ortc.adapter;
using PeerConnectionClient.Media_Extension;

namespace PeerConnectionClient.Utilities
{ 
    class Helper
    {
        public static MediaDevice ToMediaDevice(MediaDeviceInfo device)
        {
            return new MediaDevice(device.DeviceId, device.Label);
        }

        public static IList<RTCRtpCodecCapability> GetCodecs(string kind)
        {
            var caps = RTCRtpSender.GetCapabilities(kind);
            var results = new List<RTCRtpCodecCapability>(caps.Codecs);
            return results;
        }
                
        public static MediaStreamConstraints MakeConstraints(
            bool shouldDoThis,
            MediaStreamConstraints existingConstraints,
            MediaDeviceKind kind,
            MediaDevice device
            )
        {
            if (!shouldDoThis) return existingConstraints;
            if (null == device) return existingConstraints;

            if (null == existingConstraints) existingConstraints = new MediaStreamConstraints();
            MediaTrackConstraints trackConstraints = null;

            switch (kind)
            {
                case MediaDeviceKind.AudioInput:
                    trackConstraints = existingConstraints.Audio;
                    break;
                case MediaDeviceKind.AudioOutput:
                    trackConstraints = existingConstraints.Audio;
                    break;
                case MediaDeviceKind.VideoInput:
                    trackConstraints = existingConstraints.Video;
                    break;
            }
            if (null == trackConstraints) trackConstraints = new MediaTrackConstraints();

            if (null == trackConstraints.Advanced)
                trackConstraints.Advanced = new List<MediaTrackConstraintSet>();

            var constraintSet = new MediaTrackConstraintSet
            {
                DeviceId = new ConstrainString
                {
                    Parameters = new ConstrainStringParameters
                    {
                        Exact = new StringOrStringList {Value = device.Id}
                    },
                    Value = new StringOrStringList {Value = device.Id}
                }
            };

            trackConstraints.Advanced.Add(constraintSet);

            switch (kind)
            {
                case MediaDeviceKind.AudioInput:
                    existingConstraints.Audio = trackConstraints;
                    break;
                case MediaDeviceKind.AudioOutput:
                    existingConstraints.Audio = trackConstraints;
                    break;
                case MediaDeviceKind.VideoInput:
                    existingConstraints.Video = trackConstraints;
                    break;
            }
            return existingConstraints;
        }
                
        public static RTCPeerConnectionSignalingMode SignalingModeForClientName(string clientName)
        {
            RTCPeerConnectionSignalingMode ret = RTCPeerConnectionSignalingMode.Json;

            string[] substring = clientName.Split('-');
            if (substring.Length >= 2)
            {
                switch (substring.Last())
                {
                    case "dual":
                        ret = RTCPeerConnectionSignalingMode.Json;
                        break;

                    case "json":
                        ret = RTCPeerConnectionSignalingMode.Json; 
                        break;

                    default:
                        ret = RTCPeerConnectionSignalingMode.Sdp;
                        break;
                }
            }
            return ret;
        }

        public static Task<RTCMediaStreamTrackConfiguration> GetTrackConfigurationForCapabilities(RTCRtpCapabilities sourceCapabilities, RTCRtpCodecCapability preferredCodec)
        {
            if (preferredCodec == null)
                throw new ArgumentNullException(nameof(preferredCodec));

            return Task.Run(() =>
            {
                RTCRtpCapabilities capabilities = sourceCapabilities.Clone();
                RTCRtpParameters parameters = RTCSessionDescription.ConvertCapabilitiesToParameters(sourceCapabilities);

                if (parameters == null)
                    throw new NullReferenceException("Unexpected null return from RTCSessionDescription.ConvertCapabilitiesToParameters.");

                // scoope: move prefered codec to be first in the list
                {
                    var itemsToRemove = capabilities.Codecs.Where(x => x.PreferredPayloadType == preferredCodec.PreferredPayloadType).ToList();
                    if (itemsToRemove.Count > 0)
                    {
                        RTCRtpCodecCapability codecCapability = itemsToRemove.First();
                        if (codecCapability != null && capabilities.Codecs.IndexOf(codecCapability) > 0)
                        {
                            capabilities.Codecs.Remove(codecCapability);
                            capabilities.Codecs.Insert(0, codecCapability);
                        }
                    }
                }

                // scoope: move prefered codec to be first in the list
                {
                    var itemsToRemove = parameters.Codecs.Where(x => x.PayloadType == preferredCodec.PreferredPayloadType).ToList();
                    if (itemsToRemove.Count > 0)
                    {
                        RTCRtpCodecParameters codecParameters = itemsToRemove.First();
                        if (codecParameters != null && parameters.Codecs.IndexOf(codecParameters) > 0)
                        {
                            parameters.Codecs.Remove(codecParameters);
                            parameters.Codecs.Insert(0, codecParameters);
                        }
                    }
                }

                RTCMediaStreamTrackConfiguration configuration = new RTCMediaStreamTrackConfiguration()
                {
                    Capabilities = capabilities,
                    Parameters = parameters
                };
                return configuration;
            });
        }
    }
}
