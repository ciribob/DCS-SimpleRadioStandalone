using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility.Speex;
using NAudio.Wave;
using NLog;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

internal class JitterBufferProviderInterface
{
    public static readonly TimeSpan JITTER_MS = TimeSpan.FromMilliseconds(400);

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private Jitter<JitterBufferAudio>.Packet _packet = null;
    private readonly Jitter<JitterBufferAudio> _jitter = new(Constants.OUTPUT_SEGMENT_FRAMES);


    private readonly Lock _lock = new();

    private DeJitteredTransmission lastTransmission;

    internal JitterBufferProviderInterface(WaveFormat waveFormat)
    {
        WaveFormat = waveFormat;
    }

    public WaveFormat WaveFormat { get; }

    private static readonly ArrayPool<float> PCMPool = ArrayPool<float>.Shared;

    Jitter<JitterBufferAudio>.Status Get(int span, out Jitter<JitterBufferAudio>.Packet packet)
    {
        // #TODO: lock (_lock) with C# 13
        using (_lock.EnterScope())
        {
            return _jitter.Get(span, out packet);
        }
    }

    void Put(Jitter<JitterBufferAudio>.Packet packet)
    {
        // #TODO: lock (_lock) with C# 13
        using (_lock.EnterScope())
        {
            _jitter.Put(packet);
        }
    }

    void Tick()
    {
        // #TODO: lock (_lock) with C# 13
        using (_lock.EnterScope())
        {
            _jitter.Tick();
        }
    }

    bool Available()
    {
        using (_lock.EnterScope())
        {
            return _jitter.Available;
        }
    }

    bool Hydrate()
    {
        // Hydrate the packet from the jitter buffer.
        while (_packet == null && Available())
        {
            var status = Get(Constants.OUTPUT_SEGMENT_FRAMES, out _packet);
            switch (status)
            {
                case Jitter<JitterBufferAudio>.Status.OK:
                    lastTransmission = new DeJitteredTransmission
                    {
                        Modulation = _packet.Data.Modulation,
                        Frequency = _packet.Data.Frequency,
                        Decryptable = _packet.Data.Decryptable,
                        IsSecondary = _packet.Data.IsSecondary,
                        ReceivedRadio = _packet.Data.ReceivedRadio,
                        Volume = _packet.Data.Volume,
                        NoAudioEffects = _packet.Data.NoAudioEffects,
                        Guid = _packet.Data.Guid,
                        OriginalClientGuid = _packet.Data.OriginalClientGuid,
                        Encryption = _packet.Data.Encryption,
                        ReceivingPower = _packet.Data.ReceivingPower,
                        LineOfSightLoss = _packet.Data.LineOfSightLoss,
                        Ambient = _packet.Data.Ambient,
                    };

                    break;
                case Jitter<JitterBufferAudio>.Status.Missing:
                    _packet = null;
                    break;
                default: break;
            }
        }

        Tick();
        return _packet != null;
    }

    int Read(Span<float> buffer)
    {
        var read = 0;
        var desired = buffer.Length;
        if (_packet != null)
        {
            // How much data from the current packet?
            var fromBuffer = Math.Min(desired, (int)_packet.span);
            if (fromBuffer > 0)
            {
                if (_packet.Data != null)
                {
                    // Want to read <fromBuffer> bytes from packet.
                    var startIndex = (int)(_packet.Data.Audio.Length - _packet.span);
                    var source = _packet.Data.Audio.AsSpan().Slice(startIndex, fromBuffer);
                    source.CopyTo(buffer);
                    _packet.span -= source.Length;
                    read += source.Length;
                }
                else
                {
                    // 'fake' packet (insertion case).
                    // 'fake' read (keep silence)
                    _packet.span -= fromBuffer;
                    read += fromBuffer;
                }

                if (_packet.span == 0)
                {
                    // Drained everything available, need to rehydrate.
                    _packet = null;
                }
            }
        }

        return read;
    }

    internal ref DeJitteredTransmission Read(int count)
    {
        var pcmBuffer = PCMPool.Rent(count);
        var pcmSpan = pcmBuffer.AsSpan(0, count);
        pcmSpan.Clear();
        var read = 0;

        while (read < count)
        {
            var pcmSlice = pcmSpan.Slice(read);
            read += Read(pcmSlice);

            if (!Hydrate())
            {
                // Starved.
                break;
            }
        }


        lastTransmission.PCMAudioLength = read;

        if (read > 0)
        {
            lastTransmission.PCMMonoAudio = pcmBuffer;
        }

        else
        {
            lastTransmission.PCMMonoAudio = null;
            PCMPool.Return(pcmBuffer);
        }


        return ref lastTransmission;
    }

    internal void AddSamples(JitterBufferAudio jitterBufferAudio)
    {
        Debug.Assert(jitterBufferAudio.Audio.Length == Constants.OUTPUT_SEGMENT_FRAMES);
        var timestamp = jitterBufferAudio.PacketNumber * (ulong)Constants.OUTPUT_SEGMENT_FRAMES;
        Put(new()
        {
            Data = jitterBufferAudio,
            timestamp = (long)timestamp,
            span = jitterBufferAudio.Audio.Length,
        });
    }

    internal void Dispose(ref DeJitteredTransmission transmission)
    {
        if (transmission.PCMMonoAudio != null)
        {
            PCMPool.Return(transmission.PCMMonoAudio);
            transmission.PCMMonoAudio = null;
            transmission.PCMAudioLength = 0;
        }
    }
}