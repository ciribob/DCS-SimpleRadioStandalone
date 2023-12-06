using System;
using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NAudio.Utils;
using NAudio.Wave;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio
{
    public class JitterBufferProviderInterface 
    {
        private readonly CircularFloatBuffer _circularBuffer;

        public static readonly int MAXIMUM_BUFFER_SIZE_MS = 2500;

        private readonly float[] _silence = new float[AudioManager.OUTPUT_SEGMENT_FRAMES]; 

        private readonly LinkedList<JitterBufferAudio> _bufferedAudio = new LinkedList<JitterBufferAudio>();

        private ulong _lastRead; // gives current index - unsigned as it'll loops eventually

        private readonly object _lock = new object();

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        //  private const int INITIAL_DELAY_MS = 200;
        //   private long _delayedUntil = -1; //holds audio for a period of time

        private DeJitteredTransmission lastTransmission;

        private float[] returnBuffer;

        public JitterBufferProviderInterface(WaveFormat waveFormat)
        {
            WaveFormat = waveFormat;

            _circularBuffer = new CircularFloatBuffer(AudioManager.OUTPUT_SAMPLE_RATE * 3);//3 seconds worth of audio

            Array.Clear(_silence, 0, _silence.Length);
        }

        public WaveFormat WaveFormat { get; }

        public DeJitteredTransmission Read(int count)
        {
          //  int now = Environment.TickCount;

            returnBuffer = BufferHelpers.Ensure(returnBuffer, count);

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
                    read = read + _circularBuffer.Read(returnBuffer,  read, count - read);

                    if (read < count)
                    {
                        //now read in from the jitterbuffer
                        if (_bufferedAudio.Count == 0)
                        {
                            //goes to a mixer so we just return what we've read which could be 0!
                            //Mixer Handles this OK
                            break;
                            //
                            // zero the end of the buffer
                            //      Array.Clear(buffer, offset + read, count - read);
                            //     read = count;
                            //  Console.WriteLine("Buffer Empty");
                        }
                        else
                        {
                            var audio = _bufferedAudio.First.Value;
                            //no Pop?
                            _bufferedAudio.RemoveFirst();

                            lastTransmission = new DeJitteredTransmission()
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
                                Encryption = audio.Encryption

                            };

                            if (_lastRead == 0)
                                _lastRead = audio.PacketNumber;
                            else
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

                                        for (var i = 0; i < (int)fill; i++)
                                        {
                                            _circularBuffer.Write(_silence, 0, _silence.Length);
                                        }
                                    }
                                  
                                }

                                _lastRead = audio.PacketNumber;
                            }

                            _circularBuffer.Write(audio.Audio, 0, audio.Audio.Length);
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
                lastTransmission.PCMMonoAudio = returnBuffer;
            }
            else
            {
                lastTransmission.PCMMonoAudio = null;
            }
          
            return lastTransmission;
        }

        public void AddSamples(JitterBufferAudio jitterBufferAudio)
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
                    var time = _bufferedAudio.Count * AudioManager.OUTPUT_AUDIO_LENGTH_MS; // this isnt quite true as there can be padding audio but good enough

                    if (time > MAXIMUM_BUFFER_SIZE_MS)
                    {
                        _bufferedAudio.Clear();
                        Logger.Warn($"Cleared Audio buffer - length was {time} ms");
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
                        {
                            //discard! Duplicate packet
                            return;
                        }

                        if (jitterBufferAudio.PacketNumber < it.Value.PacketNumber)
                        {
                            _bufferedAudio.AddBefore(it, jitterBufferAudio);
                            return;
                        }

                        if ((jitterBufferAudio.PacketNumber > it.Value.PacketNumber) &&
                            ((next == null) || (jitterBufferAudio.PacketNumber < next.Value.PacketNumber)))
                        {
                            _bufferedAudio.AddAfter(it, jitterBufferAudio);
                            return;
                        }

                        it = next;
                    }
                }
            }
        }
    }
}