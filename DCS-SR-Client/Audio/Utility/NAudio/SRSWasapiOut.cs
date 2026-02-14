using System;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave.SampleProviders;

// ReSharper disable once CheckNamespace
namespace NAudio.Wave;

/// <summary>
///     Support for playback using Wasapi
/// </summary>
public class SRSWasapiOut : IWavePlayer, IWavePosition
{
    private readonly bool isUsingEventSync;
    private readonly MMDevice mmDevice;
    private readonly AudioClientShareMode shareMode;
    private readonly SynchronizationContext syncContext;
    private AudioClient audioClient;
    private int bufferFrameCount;
    private int bytesPerFrame;
    private bool dmoResamplerNeeded;
    private EventWaitHandle frameEventWaitHandle;
    private int latencyMilliseconds;
    private volatile PlaybackState playbackState;
    private Thread playThread;
    private byte[] readBuffer;
    private AudioRenderClient renderClient;
    private IWaveProvider sourceProvider;
    private bool windowsN;

    /// <summary>
    ///     WASAPI Out shared mode, defauult
    /// </summary>
    public SRSWasapiOut() :
        this(GetDefaultAudioEndpoint(), AudioClientShareMode.Shared, true, 200)
    {
    }

    /// <summary>
    ///     WASAPI Out using default audio endpoint
    /// </summary>
    /// <param name="shareMode">ShareMode - shared or exclusive</param>
    /// <param name="latency">Desired latency in milliseconds</param>
    public SRSWasapiOut(AudioClientShareMode shareMode, int latency) :
        this(GetDefaultAudioEndpoint(), shareMode, true, latency)
    {
    }

    /// <summary>
    ///     WASAPI Out using default audio endpoint
    /// </summary>
    /// <param name="shareMode">ShareMode - shared or exclusive</param>
    /// <param name="useEventSync">true if sync is done with event. false use sleep.</param>
    /// <param name="latency">Desired latency in milliseconds</param>
    public SRSWasapiOut(AudioClientShareMode shareMode, bool useEventSync, int latency) :
        this(GetDefaultAudioEndpoint(), shareMode, useEventSync, latency)
    {
    }

    /// <summary>
    ///     Creates a new WASAPI Output
    /// </summary>
    /// <param name="device">Device to use</param>
    /// <param name="shareMode"></param>
    /// <param name="useEventSync">true if sync is done with event. false use sleep.</param>
    /// <param name="latency">Desired latency in milliseconds</param>
    public SRSWasapiOut(MMDevice device, AudioClientShareMode shareMode, bool useEventSync, int latency)
    {
        audioClient = device.AudioClient;
        mmDevice = device;
        this.shareMode = shareMode;
        isUsingEventSync = useEventSync;
        latencyMilliseconds = latency;
        syncContext = SynchronizationContext.Current;
        OutputWaveFormat = audioClient.MixFormat; // allow the user to query the default format for shared mode streams
    }

    public SRSWasapiOut(MMDevice device, AudioClientShareMode shareMode, bool useEventSync, int latency, bool windowsN)
        : this(device, shareMode, useEventSync, latency)
    {
        this.windowsN = windowsN;
    }

    /// <summary>
    ///     Playback Stopped
    /// </summary>
    public event EventHandler<StoppedEventArgs> PlaybackStopped;

    /// <summary>
    ///     Gets a <see cref="Wave.WaveFormat" /> instance indicating the format the hardware is using.
    /// </summary>
    public WaveFormat OutputWaveFormat { get; private set; }

    #region IDisposable Members

    /// <summary>
    ///     Dispose
    /// </summary>
    public void Dispose()
    {
        if (audioClient != null)
        {
            Stop();

            audioClient.Dispose();
            audioClient = null;
            renderClient = null;
        }
    }

    #endregion

    /// <summary>
    ///     Gets the current position in bytes from the wave output device.
    ///     (n.b. this is not the same thing as the position within your reader
    ///     stream)
    /// </summary>
    /// <returns>Position in bytes</returns>
    public long GetPosition()
    {
        if (playbackState == PlaybackState.Stopped) return 0;
        return (long)audioClient.AudioClockClient.AdjustedPosition;
    }

    public static MMDevice GetDefaultAudioEndpoint()
    {
        if (Environment.OSVersion.Version.Major < 6)
            throw new NotSupportedException("WASAPI supported only on Windows Vista and above");
        var enumerator = new MMDeviceEnumerator();
        return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
    }

    private void PlayThread()
    {
        ResamplerDmoStream resamplerDmoStream = null;
        var playbackProvider = sourceProvider;
        Exception exception = null;
        WdlResamplingSampleProvider resamplerWdlStream = null;
        try
        {
            if (dmoResamplerNeeded)
            {
                if (!windowsN)
                {
                    resamplerDmoStream = new ResamplerDmoStream(sourceProvider, OutputWaveFormat);
                    playbackProvider = resamplerDmoStream;
                }
                else
                {
                    resamplerWdlStream = new WdlResamplingSampleProvider(sourceProvider.ToSampleProvider(),
                        OutputWaveFormat.SampleRate);
                    playbackProvider = resamplerWdlStream.ToWaveProvider();
                }
            }

            // fill a whole buffer
            bufferFrameCount = audioClient.BufferSize;
            bytesPerFrame = OutputWaveFormat.Channels * OutputWaveFormat.BitsPerSample / 8;
            readBuffer = new byte[bufferFrameCount * bytesPerFrame];
            FillBuffer(playbackProvider, bufferFrameCount);

            // Create WaitHandle for sync
            var waitHandles = new WaitHandle[] { frameEventWaitHandle };

            audioClient.Start();

            while (playbackState != PlaybackState.Stopped)
            {
                // If using Event Sync, Wait for notification from AudioClient or Sleep half latency
                var indexHandle = 0;
                if (isUsingEventSync)
                    indexHandle = WaitHandle.WaitAny(waitHandles, 3 * latencyMilliseconds, false);
                else
                    Thread.Sleep(latencyMilliseconds / 2);

                // If still playing and notification is ok
                if (playbackState == PlaybackState.Playing && indexHandle != WaitHandle.WaitTimeout)
                {
                    // See how much buffer space is available.
                    int numFramesPadding;
                    if (isUsingEventSync)
                        // In exclusive mode, always ask the max = bufferFrameCount = audioClient.BufferSize
                        numFramesPadding = shareMode == AudioClientShareMode.Shared ? audioClient.CurrentPadding : 0;
                    else
                        numFramesPadding = audioClient.CurrentPadding;
                    var numFramesAvailable = bufferFrameCount - numFramesPadding;
                    if (numFramesAvailable > 10) // see https://naudio.codeplex.com/workitem/16363
                        FillBuffer(playbackProvider, numFramesAvailable);
                }
            }

            Thread.Sleep(latencyMilliseconds / 2);
            audioClient.Stop();
            if (playbackState == PlaybackState.Stopped) audioClient.Reset();
        }
        catch (Exception e)
        {
            exception = e;
        }
        finally
        {
            if (resamplerDmoStream != null) resamplerDmoStream.Dispose();
            RaisePlaybackStopped(exception);
        }
    }

    private void RaisePlaybackStopped(Exception e)
    {
        var handler = PlaybackStopped;
        if (handler != null)
        {
            if (syncContext == null)
                handler(this, new StoppedEventArgs(e));
            else
                syncContext.Post(state => handler(this, new StoppedEventArgs(e)), null);
        }
    }

    private void FillBuffer(IWaveProvider playbackProvider, int frameCount)
    {
        var buffer = renderClient.GetBuffer(frameCount);
        var readLength = frameCount * bytesPerFrame;
        var read = playbackProvider.Read(readBuffer, 0, readLength);
        if (read == 0) playbackState = PlaybackState.Stopped;
        Marshal.Copy(readBuffer, 0, buffer, read);
        var actualFrameCount = read / bytesPerFrame;
        /*if (actualFrameCount != frameCount)
        {
            Debug.WriteLine(String.Format("WASAPI wanted {0} frames, supplied {1}", frameCount, actualFrameCount ));
        }*/
        renderClient.ReleaseBuffer(actualFrameCount, AudioClientBufferFlags.None);
    }

    private WaveFormat GetFallbackFormat()
    {
        var correctSampleRateFormat = audioClient.MixFormat;
        /*WaveFormat.CreateIeeeFloatWaveFormat(
        audioClient.MixFormat.SampleRate,
        audioClient.MixFormat.Channels);*/

        if (!audioClient.IsFormatSupported(shareMode, correctSampleRateFormat))
        {
            // Iterate from Worst to Best Format
            WaveFormatExtensible[] bestToWorstFormats =
            {
                new(
                    OutputWaveFormat.SampleRate, 32,
                    OutputWaveFormat.Channels),
                new(
                    OutputWaveFormat.SampleRate, 24,
                    OutputWaveFormat.Channels),
                new(
                    OutputWaveFormat.SampleRate, 16,
                    OutputWaveFormat.Channels)
            };

            // Check from best Format to worst format ( Float32, Int24, Int16 )
            for (var i = 0; i < bestToWorstFormats.Length; i++)
            {
                correctSampleRateFormat = bestToWorstFormats[i];
                if (audioClient.IsFormatSupported(shareMode, correctSampleRateFormat)) break;
                correctSampleRateFormat = null;
            }

            // If still null, then test on the PCM16, 2 channels
            if (correctSampleRateFormat == null)
            {
                // Last Last Last Chance (Thanks WASAPI)
                correctSampleRateFormat = new WaveFormatExtensible(OutputWaveFormat.SampleRate, 16, 2);
                if (!audioClient.IsFormatSupported(shareMode, correctSampleRateFormat))
                    throw new NotSupportedException("Can't find a supported format to use");
            }
        }

        return correctSampleRateFormat;
    }

    #region IWavePlayer Members

    /// <summary>
    ///     Begin Playback
    /// </summary>
    public void Play()
    {
        if (playbackState != PlaybackState.Playing)
        {
            if (playbackState == PlaybackState.Stopped)
            {
                playThread = new Thread(PlayThread);
                playbackState = PlaybackState.Playing;
                playThread.Start();
            }
            else
            {
                playbackState = PlaybackState.Playing;
            }
        }
    }

    /// <summary>
    ///     Stop playback and flush buffers
    /// </summary>
    public void Stop()
    {
        if (playbackState != PlaybackState.Stopped)
        {
            playbackState = PlaybackState.Stopped;
            playThread.Join();
            playThread = null;
        }
    }

    /// <summary>
    ///     Stop playback without flushing buffers
    /// </summary>
    public void Pause()
    {
        if (playbackState == PlaybackState.Playing) playbackState = PlaybackState.Paused;
    }

    /// <summary>
    ///     Initialize for playing the specified wave stream
    /// </summary>
    /// <param name="waveProvider">IWaveProvider to play</param>
    public void Init(IWaveProvider waveProvider)
    {
        long latencyRefTimes = latencyMilliseconds * 10000;
        OutputWaveFormat = waveProvider.WaveFormat;
        // first attempt uses the WaveFormat from the WaveStream
        WaveFormatExtensible closestSampleRateFormat;
        if (!audioClient.IsFormatSupported(shareMode, OutputWaveFormat, out closestSampleRateFormat))
        {
            // Use closesSampleRateFormat (in sharedMode, it equals usualy to the audioClient.MixFormat)
            // See documentation : http://msdn.microsoft.com/en-us/library/ms678737(VS.85).aspx 
            // They say : "In shared mode, the audio engine always supports the mix format"
            // The MixFormat is more likely to be a WaveFormatExtensible.
            if (closestSampleRateFormat == null)
                OutputWaveFormat = GetFallbackFormat();
            else
                OutputWaveFormat = closestSampleRateFormat;

            if (!windowsN)
                try
                {
                    // just check that we can make it.
                    using (new ResamplerDmoStream(waveProvider, OutputWaveFormat))
                    {
                    }
                }
                catch (Exception)
                {
                    // On Windows 10 some poorly coded drivers return a bad format in to closestSampleRateFormat
                    // In that case, try and fallback as if it provided no closest (e.g. force trying the mix format)
                    OutputWaveFormat = GetFallbackFormat();
                    try
                    {
                        using (new ResamplerDmoStream(waveProvider, OutputWaveFormat))
                        {
                        }
                    }
                    catch (Exception)
                    {
                        //still something wrong - assume windows N and DMO is broken in some way
                        windowsN = true;
                    }
                }

            dmoResamplerNeeded = true;
        }
        else
        {
            dmoResamplerNeeded = false;
        }

        sourceProvider = waveProvider;

        // If using EventSync, setup is specific with shareMode
        if (isUsingEventSync)
        {
            // Init Shared or Exclusive
            if (shareMode == AudioClientShareMode.Shared)
            {
                // With EventCallBack and Shared, both latencies must be set to 0 (update - not sure this is true anymore)
                // 
                audioClient.Initialize(shareMode, AudioClientStreamFlags.EventCallback, latencyRefTimes, 0,
                    OutputWaveFormat, Guid.Empty);

                // Windows 10 returns 0 from stream latency, resulting in maxing out CPU usage later
                var streamLatency = audioClient.StreamLatency;
                if (streamLatency != 0)
                    // Get back the effective latency from AudioClient
                    latencyMilliseconds = (int)(streamLatency / 10000);
            }
            else
            {
                // With EventCallBack and Exclusive, both latencies must equals
                audioClient.Initialize(shareMode, AudioClientStreamFlags.EventCallback, latencyRefTimes,
                    latencyRefTimes,
                    OutputWaveFormat, Guid.Empty);
            }

            // Create the Wait Event Handle
            frameEventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            audioClient.SetEventHandle(frameEventWaitHandle.SafeWaitHandle.DangerousGetHandle());
        }
        else
        {
            // Normal setup for both sharedMode
            audioClient.Initialize(shareMode, AudioClientStreamFlags.None, latencyRefTimes, 0,
                OutputWaveFormat, Guid.Empty);
        }

        // Get the RenderClient
        renderClient = audioClient.AudioRenderClient;
    }

    /// <summary>
    ///     Playback State
    /// </summary>
    public PlaybackState PlaybackState => playbackState;

    /// <summary>
    ///     Volume
    /// </summary>
    public float Volume
    {
        get => 1;
        set
        {
            // if (value < 0) throw new ArgumentOutOfRangeException("value", "Volume must be between 0.0 and 1.0");
            // if (value > 1) throw new ArgumentOutOfRangeException("value", "Volume must be between 0.0 and 1.0");
            // mmDevice.AudioEndpointVolume.MasterVolumeLevelScalar = value;
        }
    }

    /// <summary>
    ///     Retrieve the AudioStreamVolume object for this audio stream
    /// </summary>
    /// <remarks>
    ///     This returns the AudioStreamVolume object ONLY for shared audio streams.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     This is thrown when an exclusive audio stream is being used.
    /// </exception>
    public AudioStreamVolume AudioStreamVolume
    {
        get
        {
            if (shareMode == AudioClientShareMode.Exclusive)
                throw new InvalidOperationException("AudioStreamVolume is ONLY supported for shared audio streams.");
            return audioClient.AudioStreamVolume;
        }
    }

    #endregion
}