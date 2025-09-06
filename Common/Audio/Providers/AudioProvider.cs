using System;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

public abstract class AudioProvider
{
    public static readonly int SILENCE_PAD = 200;

    protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    protected OpusDecoder _decoder;

    protected AudioProvider()
    {
        _decoder = OpusDecoder.Create(Constants.OUTPUT_SAMPLE_RATE, 1);
        _decoder.ForwardErrorCorrection = false;
        _decoder.MaxDataBytes = Constants.OUTPUT_SAMPLE_RATE * 4;
    }

    public long LastUpdate { get; set; }

    //is it a new transmission?
    protected bool LikelyNewTransmission()
    {
        //400 ms since last update
        var now = DateTime.Now.Ticks;

        return TimeSpan.FromTicks(now - LastUpdate) > JitterBufferProviderInterface.JITTER_MS;
    }

    public abstract int AddClientAudioSamples(ClientAudio audio);


    //destructor to clear up opus
    ~AudioProvider()
    {
        _decoder?.Dispose();
        _decoder = null;
    }
}