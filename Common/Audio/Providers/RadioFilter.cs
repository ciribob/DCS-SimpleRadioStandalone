using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

public class RadioFilter : ISampleProvider
{
    public static readonly float CLIPPING_MAX = 4000 / 32768f;
    public static readonly float CLIPPING_MIN = -CLIPPING_MAX;

    private class RadioModel
    {
        public DeferredSourceProvider TxSource { get; } = new DeferredSourceProvider();

        public ISampleProvider TxEffectProvider { get; set; }

        public RadioModel(Models.Dto.RadioModel dtoPreset)
        {
            TxEffectProvider = dtoPreset.TxEffect.ToSampleProvider(TxSource);
        }
    }

    private RadioModel Arc210 { get; } = new RadioModel(DefaultRadioModels.BuildArc210());

    public RadioFilter(ISampleProvider sampleProvider)
    {
        Arc210.TxSource.Source = sampleProvider;
    }

    public WaveFormat WaveFormat => Arc210.TxEffectProvider.WaveFormat;

    public int Read(float[] buffer, int offset, int sampleCount)
    {
        return Arc210.TxEffectProvider.Read(buffer, offset, sampleCount);
    }
}