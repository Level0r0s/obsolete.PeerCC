using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using org.ortc;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using org.ortc.adapter;

namespace PeerConnectionClient.Media_Extension
{
    public class RTCPeerConnectionHealthStats
    {
    }
    public class MediaStreamEvent
    {
        public MediaStream Stream;
    }

    public class RTCMediaStreamConstraints
    {
        public Boolean audioEnabled;
        public Boolean videoEnabled;
    }
    public class Media
    {
        private IList<MediaDeviceInfo> AudioCaptureDevices { get;  set; } = new List<MediaDeviceInfo>();
        private IList<MediaDeviceInfo> AudioPlaybackDevices { get; set; } = new List<MediaDeviceInfo>();
        private IList<MediaDeviceInfo> VideoDevices { get; set; } = new List<MediaDeviceInfo>();

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        private MediaDevice _audioCaptureDevice;
        private MediaDevice _audioPlaybackDevice;
        private MediaDevice _videoDevice;
        private int _preferredFrameWidth;
        private int _preferredFrameHeight;
        private int _preferredFPS;

        public delegate void OnMediaCaptureDeviceFoundDelegate(MediaDevice param0);

        public event OnMediaCaptureDeviceFoundDelegate OnAudioCaptureDeviceFound;
        public event OnMediaCaptureDeviceFoundDelegate OnVideoCaptureDeviceFound;

        public static Media CreateMedia()
        {
            var ret = new Media();

            return ret;
        }

        public static IAsyncOperation<Media> CreateMediaAsync()
        {
            return Task.Run(() => CreateMedia()).AsAsyncOperation();
        }

        /*public IAsyncOperation<MediaStream> GetUserMedia(RTCMediaStreamConstraints mediaStreamConstraints)
        {
            Task<MediaStream> t = Task.Run(() =>
            {
                var constraints = Helper.MakeConstraints(mediaStreamConstraints.audioEnabled, null,
                    MediaDeviceKind.AudioInput, _audioCaptureDevice);
                constraints = Helper.MakeConstraints(mediaStreamConstraints.videoEnabled, constraints,
                    MediaDeviceKind.VideoInput, _videoDevice);
                if (constraints.Video != null && constraints.Video.Advanced.Count > 0)
                {
                    MediaTrackConstraintSet constraintSet = constraints.Video.Advanced.First();
                    constraintSet.Width = new ConstrainLong();
                    constraintSet.Width.Value = _preferredFrameWidth;
                    constraintSet.Height = new ConstrainLong();
                    constraintSet.Height.Value = _preferredFrameHeight;
                    constraintSet.FrameRate = new ConstrainDouble();
                    constraintSet.FrameRate.Value = _preferredFPS;
                }

                Task<IList<MediaStreamTrack>> task = MediaDevices.GetUserMedia(constraints).AsTask();
                return task.ContinueWith(temp => new MediaStream(temp.Result));
            });

            return t.AsAsyncOperation();
        }*/

        public IAsyncOperation<IList<MediaStreamTrack>> GetUserMedia(RTCMediaStreamConstraints mediaStreamConstraints)
        {
            Task<IList<MediaStreamTrack>> t = Task.Run(() =>
            {
                var constraints = Helper.MakeConstraints(mediaStreamConstraints.audioEnabled, null,
                    MediaDeviceKind.AudioInput, _audioCaptureDevice);
                constraints = Helper.MakeConstraints(mediaStreamConstraints.videoEnabled, constraints,
                    MediaDeviceKind.VideoInput, _videoDevice);
                if (constraints.Video != null && constraints.Video.Advanced.Count > 0)
                {
                    MediaTrackConstraintSet constraintSet = constraints.Video.Advanced.First();
                    constraintSet.Width = new ConstrainLong();
                    constraintSet.Width.Value = _preferredFrameWidth;
                    constraintSet.Height = new ConstrainLong();
                    constraintSet.Height.Value = _preferredFrameHeight;
                    constraintSet.FrameRate = new ConstrainDouble();
                    constraintSet.FrameRate.Value = _preferredFPS;
                }

                Task<IList<MediaStreamTrack>> task = MediaDevices.GetUserMedia(constraints).AsTask();
                return task.Result;
            });

            return t.AsAsyncOperation();
        }
        public IMediaSource CreateMediaSource(MediaStreamTrack track, string id)
        {
            return track?.CreateMediaSource();
        }

        public IAsyncOperation<bool> EnumerateAudioVideoCaptureDevices()
        {
            return Task.Run(async () =>
            {
                var devices = await MediaDevices.EnumerateDevices();

                var audioCaptureList = Helper.Filter(MediaDeviceKind.AudioInput, devices);
                var audioPlaybackList = Helper.Filter(MediaDeviceKind.AudioOutput, devices);
                var videoList = Helper.Filter(MediaDeviceKind.VideoInput, devices);

                //_audioCaptureDevices = audioCaptureList;
                //_videoDevices = videoList;

                using (var @lock = new AutoLock(_lock))
                {
                    @lock.WaitAsync().Wait();
                    AudioCaptureDevices = audioCaptureList;
                    AudioPlaybackDevices = audioPlaybackList;
                    VideoDevices = videoList;
                }

                /*await Task.Run(() =>
                {
                    foreach (var info in audioCaptureList)
                    {
                        OnAudioCaptureDeviceFound?.Invoke(new MediaDevice(info.DeviceId, info.Label));
                    }
                    foreach (var info in videoList)
                    {
                        OnVideoCaptureDeviceFound?.Invoke(new MediaDevice(info.DeviceId, info.Label));
                    }
                });*/

                return devices.Count > 0;
            }).AsAsyncOperation();
        }

        //public IList<MediaDevice> GetAudioCaptureDevices();
        //public IList<MediaDevice> GetVideoCaptureDevices();
        public static void OnAppSuspending()
        {
            MediaDevices.OnAppSuspending();
        }

        public void SelectAudioCaptureDevice(MediaDevice device)
        {
            using (var @lock = new AutoLock(_lock))
            {
                @lock.WaitAsync().Wait();
                _audioCaptureDevice = device;
            }
        }

        public void SelectVideoDevice(MediaDevice device)
        {
            using (var @lock = new AutoLock(_lock))
            {
                @lock.WaitAsync().Wait();
                _videoDevice = device;
            }
        }

        //public void SetDisplayOrientation(DisplayOrientations display_orientation);
        public bool SelectAudioPlayoutDevice(MediaDevice device)
        {
            using (var @lock = new AutoLock(_lock))
            {
                @lock.WaitAsync().Wait();
                _audioPlaybackDevice = device;
            }
            return true;
        }

        public IList<MediaDevice> GetAudioPlayoutDevices()
        {
            var contentAsync = MediaDevices.EnumerateDevices();
            contentAsync.AsTask().Wait();
            var devices = contentAsync.GetResults();

            var audioPlaybackList = Helper.Filter(MediaDeviceKind.AudioOutput, devices);

            using (var @lock = new AutoLock(_lock))
            {
                @lock.WaitAsync().Wait();
                AudioPlaybackDevices = audioPlaybackList;
            }

            AudioPlaybackDevices = audioPlaybackList;

            return Helper.ToMediaDevices(audioPlaybackList);
        }

        public void SetPreferredVideoCaptureFormat(int frameWidth, int frameHeight, int fps)
        {
            using (var @lock = new AutoLock(_lock))
            {
                @lock.WaitAsync().Wait();
                _preferredFrameWidth = frameWidth;
                _preferredFrameHeight = frameHeight;
                _preferredFPS = fps;
            }
        }
    }
}