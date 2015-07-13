using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using webrtc_winrt_api;

namespace PeerConnectionClient.Utilities
{
    class SdpUtils
    {
        public static bool SelectCodecs(ref string sdp, CodecInfo audioCodec, CodecInfo videoCodec)
        {
            if (audioCodec == null)
                return false;
            if (videoCodec == null)
                return false;
            Regex mfdRegex = new Regex("\r\nm=audio.*RTP.*?( .\\d*)+\r\n");
            Match mfdMatch = mfdRegex.Match(sdp);
            List<string> mfdListToErase = new List<string>(); //mdf = media format descriptor
            for (int groupCtr = 1/*Group 0 is whole match*/; groupCtr < mfdMatch.Groups.Count; groupCtr++)
                for (int captureCtr = 0; captureCtr < mfdMatch.Groups[groupCtr].Captures.Count; captureCtr++)
                    mfdListToErase.Add(mfdMatch.Groups[groupCtr].Captures[captureCtr].Value.TrimStart());

            mfdRegex = new Regex("\r\nm=video.*RTP.*?( .\\d*)+\r\n");
            mfdMatch = mfdRegex.Match(sdp);
            for (int groupCtr = 1/*Group 0 is whole match*/; groupCtr < mfdMatch.Groups.Count; groupCtr++)
                for (int captureCtr = 0; captureCtr < mfdMatch.Groups[groupCtr].Captures.Count; captureCtr++)
                    mfdListToErase.Add(mfdMatch.Groups[groupCtr].Captures[captureCtr].Value.TrimStart());

            if (!mfdListToErase.Remove(audioCodec.Id.ToString()))
                return false;
            if (!mfdListToErase.Remove(videoCodec.Id.ToString()))
                return false;

            //Alter audio entry
            Regex audioRegex = new Regex("\r\n(m=audio.*RTP.*?)( .\\d*)+");
            sdp = audioRegex.Replace(sdp, "\r\n$1 " + audioCodec.Id);

            //Alter video entry
            Regex videoRegex = new Regex("\r\n(m=video.*RTP.*?)( .\\d*)+");
            sdp = videoRegex.Replace(sdp, "\r\n$1 " + videoCodec.Id);

            //Remove associated rtp mapping, format parameters, feedback parameters
            Regex removeOtherMdfs = new Regex("a=(rtpmap|fmtp|rtcp-fb):(" + String.Join("|", mfdListToErase) + ") .*\r\n");
            sdp = removeOtherMdfs.Replace(sdp, "");

            return true;
        }
    }
}
