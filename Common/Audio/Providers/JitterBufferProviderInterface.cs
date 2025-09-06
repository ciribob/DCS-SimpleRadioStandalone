using System;
using System.Buffers;
using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility;
using NAudio.Utils;
using NAudio.Wave;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

internal class JitterBufferProviderInterface
{

    public static readonly int MAXIMUM_BUFFER_SIZE_MS = 2500;
    public static readonly TimeSpan JITTER_MS = TimeSpan.FromMilliseconds(400);

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    

    private readonly LinkedList<JitterBufferAudio> _bufferedAudio = new();
    private readonly CircularFloatBuffer _circularBuffer;

    private readonly object _lock = new();

    private readonly float[] _silence = new float[Constants.OUTPUT_SEGMENT_FRAMES];

    private ulong _lastRead; // gives current index - unsigned as it'll loops eventually
    private long _lastPacketTicks = 0;

    //  private const int INITIAL_DELAY_MS = 200;
    //   private long _delayedUntil = -1; //holds audio for a period of time

    private DeJitteredTransmission lastTransmission;

    internal JitterBufferProviderInterface(WaveFormat waveFormat)
    {
        WaveFormat = waveFormat;

        _circularBuffer = new CircularFloatBuffer(Constants.OUTPUT_SAMPLE_RATE * 3); //3 seconds worth of audio

        Array.Clear(_silence, 0, _silence.Length);
    }

    public WaveFormat WaveFormat { get; }

    private static readonly ArrayPool<float> PCMPool = ArrayPool<float>.Shared;

    internal ref DeJitteredTransmission Read(int count)
    {
        //  int now = Environment.TickCount;
        var now = DateTime.Now.Ticks;
        var timeSinceLastDequeue = TimeSpan.FromTicks(now - _lastPacketTicks);

        var pcmBuffer = PCMPool.Rent(count);
        pcmBuffer.AsSpan(0, count).Clear();

        //other implementation of waiting
        //            if(_delayedUntil > now)
        //            {
        //                //wait
        //                return 0;
        //            }

        var read = 0;
        lock (_lock)
        {
            //need to return read equal to count

            //do while loop
            //break when read == count
            //each time round increment read
            //read becomes read + last Read

            do
            {
                read = read + _circularBuffer.Read(pcmBuffer, read, count - read);

                if (read < count)
                {
                    if (_bufferedAudio.Count > 0)
                    {
                        //
                        // zero the end of the buffer
                        //      Array.Clear(buffer, offset + read, count - read);
                        //     read = count;
                        //  Console.WriteLine("Buffer Empty");
                        var audio = _bufferedAudio.First.Value;
                        //no Pop?
                        _bufferedAudio.RemoveFirst();

                        lastTransmission = new DeJitteredTransmission
                        {
                            Modulation = audio.Modulation,
                            Frequency = audio.Frequency,
                            Decryptable = audio.Decryptable,
                            IsSecondary = audio.IsSecondary,
                            ReceivedRadio = audio.ReceivedRadio,
                            Volume = audio.Volume,
                            NoAudioEffects = audio.NoAudioEffects,
                            Guid = audio.Guid,
                            OriginalClientGuid = audio.OriginalClientGuid,
                            Encryption = audio.Encryption,
                            ReceivingPower = audio.ReceivingPower,
                            LineOfSightLoss = audio.LineOfSightLoss,
                            Ambient = audio.Ambient,
                        };

                        if (_lastRead > 0)
                        {
                            //TODO deal with looping packet number
                            if (_lastRead + 1 < audio.PacketNumber)
                            {
                                //fill with missing silence - will only add max of 5x Packet length but it could be a bunch of missing?
                                var missing = audio.PacketNumber - (_lastRead + 1);

                                // packet number is always discontinuous at the start of a transmission if you didnt receive a transmission for a while i.e different radio channel
                                // if the gap is more than 4 assume its just a new transmission

                                if (missing <= 4)
                                {
                                    var fill = Math.Min(missing, 4);

                                    for (var i = 0; i < (int)fill; i++) _circularBuffer.Write(_silence, 0, _silence.Length);
                                }
                            }
                        }

                        _lastRead = audio.PacketNumber;
                        _circularBuffer.Write(audio.Audio, 0, audio.AudioLength);
                        audio.Dispose();
                        _lastPacketTicks = now;
                    }
                    else if (timeSinceLastDequeue < JITTER_MS)
                    {
                        // Starvation.
                        // When that happens, allow for 400ms over,
                        // giving some buffer for new packets to arrive.
                        // Latency can be high, given the packets have to go first to the server
                        // then to the clients.
                        // If two clients have about 80ms ping to the server, there's already a 160ms
                        // transmission delay between the two.
                        _circularBuffer.Write(_silence, 0, Math.Min(count - read, _silence.Length));

                    }
                    else
                    {
                        // Full starvation, early out.
                        break;
                    }
                    
                }
            } while (read < count);

//                if (read == 0)
//                {
//                    _delayedUntil = Environment.TickCount + INITIAL_DELAY_MS;
//                }
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
        lock (_lock)
        {
            //re-order if we can or discard

            //add to linked list
            //add front to back
            if (_bufferedAudio.Count == 0)
            {
                _bufferedAudio.AddFirst(jitterBufferAudio);
            }
            else if (jitterBufferAudio.PacketNumber > _lastRead)
            {
                //TODO CHECK THIS
                var time = _bufferedAudio.Count *
                           Constants
                               .OUTPUT_AUDIO_LENGTH_MS; // this isnt quite true as there can be padding audio but good enough

                var timeOverBudget = time - MAXIMUM_BUFFER_SIZE_MS;
                if (timeOverBudget > 0)
                {
                    Logger.Warn($"Skipping Audio buffer - length was {time} ms, {timeOverBudget} ms will be skipped.");
                    // Compute how many packet we can ditch.
                    var toSkip = timeOverBudget / Constants.OUTPUT_AUDIO_LENGTH_MS;
                    for (; toSkip > 0 && _bufferedAudio.Count > 0; --toSkip)
                    {
                        _bufferedAudio.First.Value.Dispose();
                        _bufferedAudio.RemoveFirst();
                    }
                        

                    _lastRead = 0;
                }

                for (var it = _bufferedAudio.First; it != null;)
                {
                    //iterate list
                    //if packetNumber == curentItem
                    // discard
                    //else if packetNumber < _currentItem
                    //add before
                    //else if packetNumber > _currentItem
                    //add before

                    //if not added - add to end?

                    var next = it.Next;

                    if (it.Value.PacketNumber == jitterBufferAudio.PacketNumber)
                        //discard! Duplicate packet
                        return;

                    if (jitterBufferAudio.PacketNumber < it.Value.PacketNumber)
                    {
                        _bufferedAudio.AddBefore(it, jitterBufferAudio);
                        return;
                    }

                    if (jitterBufferAudio.PacketNumber > it.Value.PacketNumber &&
                        (next == null || jitterBufferAudio.PacketNumber < next.Value.PacketNumber))
                    {
                        _bufferedAudio.AddAfter(it, jitterBufferAudio);
                        return;
                    }

                    it = next;
                }
            }
        }
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