using System;
using System.Diagnostics;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility.Speex
{
    // Freely inspired from Speex's JitterBuffer: https://gitlab.xiph.org/xiph/speexdsp/-/blob/main/libspeexdsp/jitter.c?ref_type=heads
    // BSD-3-Clause Copyright (C) 2002 Jean-Marc Valin
    internal class Jitter<T>
    {
        static readonly int MAX_BUFFER_SIZE = 200; /// Maximum number of packets in jitter buffer
        static readonly int MAX_BUFFERS = 3;
        static readonly int TOP_DELAY = 40;

        public enum Status
        {
            OK,
            Missing,
            Insertion,
        }

        class TimingBuffer
        {
            static readonly int MAX_TIMINGS = 40;
            internal int filled; /// Number of entries occupied in 'timings' and 'counts'
            internal int curr_count; /// Number of packet timings we got (including those we discarded)
            internal long[] timings = new long[MAX_TIMINGS]; /// Sorted list of all timings ('latest' packets first)
            internal int[] counts = new int[MAX_TIMINGS]; /// Order the packets were put in (will be used for short-term estimate)

            internal void Add(long timing)
            {
                // Discard packet that won't make it into the list because they're too early
                if (filled >= MAX_TIMINGS && timing >= timings[filled - 1])
                {
                    curr_count++;
                    return;
                }

                // Find where the timing info goes in the sorted list
                // #TODO: bisection instead of linear probe
                int pos = 0;
                while (pos < filled && timing >= timings[pos])
                {
                    pos++;
                }

                Debug.Assert(pos <= filled && pos < MAX_TIMINGS);

                // Shift everything so  we can perform the insertion.
                if (pos < filled)
                {
                    int move_size = filled - pos;
                    if (filled == MAX_TIMINGS)
                    {
                        move_size -= 1;
                    }
                    Array.Copy(timings, pos + 1, timings, pos, move_size);
                    Array.Copy(counts, pos + 1, counts, pos, move_size);
                }

                // Insert
                timings[pos] = timing;
                counts[pos] = curr_count;
                curr_count++;

                if (filled < MAX_TIMINGS)
                {
                    filled++;
                }
            }

            internal void Init()
            {
                filled = 0;
                curr_count = 0;
            }
        }

        internal class Packet
        {
            internal T Data; /// Payload
            internal long timestamp; /// Timestamp for the packet
            internal long span; /// Time covered by the packet (same units as timestamp)
        }

        long pointer_timestamp; /// Timestamp of what we will *get* next
        long last_returned_timestamp; /// Useful for getting the next packet with the same timestamp (for fragmented media)
        long next_stop; /// Estimated time the next get() will be called

        long buffered; /// Amount of data we think is still buffered by the application (timestamp units)

        Packet[] packets = new Packet[MAX_BUFFER_SIZE]; /// Packets stored in the buffer
        long[] arrival = new long[MAX_BUFFER_SIZE]; /// Packet arrival time (0 means it was late, even though it's a valid timestamp)

        int delay_step; /// Size of the steps when adjusting buffering (timestamp units)
        int concealment_size; /// Size of the packet loss concealment "units"
        bool reset_state; /// True if state was just reset
        int buffer_margin; /// How many frames we want to keep in the buffer (lower bound)
        int late_cutoff = 50; /// How late must a packet be for it not to be considered at all
        long interp_requested; /// An interpolation is requested by speex_jitter_update_delay()
        bool auto_adjust = true; /// Whether to automatically adjust the delay at any time

        TimingBuffer[] _tb = new TimingBuffer[MAX_BUFFERS]; /// Don't use those directly
        int[] timeBuffers = new int[MAX_BUFFERS]; /// Storing arrival time of latest frames so we can compute some stats
        int window_size; /// Total window over which the late frames are counted
        int subwindow_size; /// Sub-window size for faster computation
        int max_late_rate; /// Absolute maximum amount of late packets tolerable (in percent)
        int latency_tradeoff; /// Latency equivalent of losing one percent of packets
        long auto_tradeoff; /// Latency equivalent of losing one percent of packets (automatic default)

        int lost_count; /// Number of consecutive lost packets

        static int RoundDown(int x, int step)
        {
            return ((x < 0 ? (x - step + 1) : x) / step) * step;
        }

        static long RoundDown(long x, long step)
        {
            return ((x < 0 ? (x - step + 1) : x) / step) * step;
        }
        long ComputeOptDelay()
        {
            // Number of packet timings we have received (including those we didn't keep)
            var tot_count = 0;
            foreach (var timingBuffer in _tb)
            {
                tot_count += timingBuffer.curr_count;
            }

            if (tot_count == 0)
            {
                return 0;
            }

            // Compute cost for one lost packet.
            var late_factor = 0f;
            if (latency_tradeoff != 0)
            {
                late_factor = latency_tradeoff * 100f / tot_count;
            }
            else
            {
                late_factor = auto_tradeoff * window_size / tot_count;
            }

            long worst = 0;
            long best = 0;
            var late = 0;
            int best_cost = int.MaxValue;
            long opt = 0;
            var pos = new int[MAX_BUFFERS];
            var penalty_taken = false;
            for (var i = 0; i < TOP_DELAY; i++)
            {
                var next = -1;
                long latest = short.MaxValue;
                // Picck latest among all sub-windows
                for (var j = 0; j < MAX_BUFFERS; j++)
                {
                    if (pos[j] < _tb[j].filled && _tb[j].timings[pos[j]] < latest)
                    {
                        next = j;
                        latest = _tb[j].timings[pos[j]];
                    }
                }
                if (next != -1)
                {
                    if (i == 0)
                    {
                        worst = latest;
                    }
                    best = latest;
                    latest = RoundDown(latest, delay_step);
                    pos[next]++;

                    // Actual cost function that tells us how bad using this delay would be
                    var cost = (int)(-latest + late_factor * late);
                    if (cost < best_cost)
                    {
                        best_cost = cost;
                        opt = latest;
                    }
                }
                else
                {
                    break;
                }

                // For the next timing we will consider, there will be one maore late packet to count
                late++;

                // Two-frame penalyt if we're going to increase the amount of late frames (hysteresis)
                if (latest >= 0 && !penalty_taken)
                {
                    late += 4;
                    penalty_taken = true;
                }
            }

            var deltaT = best - worst;
            // This is a default "automatic latency tradeoff" when none is provided
            auto_tradeoff = 1 + deltaT / TOP_DELAY;

            // #FIXME: Compute a short-term estimate too and combine with the long-term one

            // Prevents reducing the buffer size when we haven't really had much data
            if (tot_count < TOP_DELAY && opt > 0)
            {
                return 0;
            }

            return opt;
        }

        public Jitter(int step_size)
        {
            delay_step = step_size;
            concealment_size = step_size;
            MaxLateRate = 4;
            Reset();
        }

        public void Reset()
        {
            for (var i = 0; i < packets.Length; i++)
            {
                packets[i] = null;
            }

            pointer_timestamp = 0;
            next_stop = 0;
            reset_state = true;
            lost_count = 0;
            buffered = 0;
            auto_tradeoff = 32000;

            for (int i = 0; i < _tb.Length; i++)
            {
                _tb[i] = new();
                timeBuffers[i] = i;
            }


        }

        /// <summary>
        ///  Take the following timing into consideration for future calculations
        /// </summary>
        /// <param name="timing"></param>
        void UpdateTimings(long timing)
        {
            timing = Math.Clamp(timing, short.MinValue + 1, short.MaxValue);

            // If the current sub-window is full, perform a rotation and discard oldest sub-window
            if (_tb[timeBuffers[0]].curr_count >= subwindow_size)
            {
                var tmp = timeBuffers[MAX_BUFFERS - 1];
                for (var i = MAX_BUFFERS - 1; i >= 1; i--)
                {
                    timeBuffers[i] = timeBuffers[i - 1];
                }
                timeBuffers[0] = tmp;
                _tb[timeBuffers[0]].Init();
            }

            _tb[timeBuffers[0]].Add(timing);
        }

        /// <summary>
        /// Compensate all timings when we do an adjustment of the buffering
        /// </summary>
        /// <param name="amount"></param>
        void ShiftTimings(long amount)
        {
            for (var i = 0; i < MAX_BUFFERS; i++)
            {
                var tb = _tb[timeBuffers[i]];
                for (var j = 0; j < tb.filled; j++)
                {
                    tb.timings[j] += amount;
                }
            }
        }

        /// <summary>
        /// Put one packet into the jitter buffer
        /// </summary>
        /// <param name="packet"></param>
        public void Put(Packet packet)
        {
            var late = false;
            if (!reset_state)
            {
                for (var i = 0; i < packets.Length; i++)
                {
                    // Make sure we don't discard a 'just-late' packet in case we want to paly it next (if we interpolate).
                    if (packets[i] != null && packets[i].timestamp + packets[i].span <= pointer_timestamp)
                    {
                        packets[i] = null;
                    }
                }

                // Check if packet is late (could still be useful though)
                if (packet.timestamp < next_stop)
                {
                    UpdateTimings(packet.timestamp - next_stop - buffer_margin);
                    late = true;
                }
            }

            // For some reason, the consumer has failed the last 20 fetches.
            // Make sure this packet is used to resync.
            if (lost_count > 20)
            {
                Reset();
            }

            // Only insert the packet if it's not hopelessly late (i.e. totally useless)
            if (reset_state || (packet.timestamp + packet.span + delay_step) >= pointer_timestamp)
            {
                // Find an empty slot in the buffer
                var i = 0;
                for(; i < packets.Length; ++i)
                {
                    if (packets[i] == null)
                    {
                        break;
                    }
                }

                // No place left in the buffer, need to make room for it by discarding the oldest packet
                if (i == packets.Length)
                {
                    var earliest = packets[0]?.timestamp;
                    i = 0;
                    for (var j = 1; j < packets.Length; j++)
                    {
                        if (packets[i] != null || packets[j]?.timestamp < earliest)
                        {
                            earliest = packets[j]?.timestamp;
                            i = j;
                        }
                    }

                    packets[i] = null;
                }

                // Copy packet in buffer.
                packets[i] = packet;
                arrival[i] = reset_state || late ? 0 : next_stop;
            }
        }

        /// <summary>
        /// Get one packet from the jitter buffer
        /// </summary>
        /// <param name="desired_span"></param>
        /// <returns></returns>
        public Status Get(int desired_span, out Packet outPacket)
        {
            // Syncing on the first call
            if (reset_state)
            {
                bool found = false;
                long oldest = 0;
                foreach (var packet in packets)
                {
                    if (packet != null && (!found || packet.timestamp < oldest))
                    {
                        oldest = packet.timestamp;
                        found = true;
                    }
                }

                if (found)
                {
                    reset_state = false;
                    pointer_timestamp = oldest;
                    next_stop = oldest;
                }
                else
                {
                    outPacket = new()
                    {
                        timestamp = 0,
                        span = interp_requested,
                    };
                    return Status.Missing;
                }
            }

            last_returned_timestamp = pointer_timestamp;
            if (interp_requested != 0)
            {
                outPacket = new()
                {
                    timestamp = pointer_timestamp,
                    span = interp_requested,
                };
                
                // Increment the pointer because it got decremented in the delay update
                pointer_timestamp += interp_requested;
                interp_requested = 0;
                buffered = outPacket.span - desired_span;
                return Status.Insertion;
            }

            // Searching for the packet that fits best.
            var i = 0;

            // Search for the buffer for a packet with the right timestamp and spanning the whole current chunk
            for (; i < packets.Length; i++)
            {
                var packet = packets[i];
                if (packet != null && packet.timestamp == pointer_timestamp && (packet.timestamp + packet.span) >= (pointer_timestamp + desired_span))
                {
                    break;
                }
            }

            // If no match, try for an 'older' packet that still spans (fully) the current chunk
            if (i == packets.Length)
            {
                for (; i < packets.Length; i++)
                {
                    var packet = packets[i];
                    if (packet != null && packet.timestamp <= pointer_timestamp && (packet.timestamp + packet.span) >= (pointer_timestamp + desired_span))
                    {
                        break;
                    }
                }
            }

            // If still no match, try for an 'older' packet that spans part of the current chunk
            if (i == packets.Length)
            {
                for (; i < packets.Length; i++)
                {
                    var packet = packets[i];
                    if (packet != null && packet.timestamp <= pointer_timestamp && (packet.timestamp + packet.span) >= pointer_timestamp)
                    {
                        break;
                    }
                }
            }

            // If still no match, try for earliest packet possible
            if (i == packets.Length)
            {
                var besti = 0;
                Packet bestCandidate = null;
                for (; i < packets.Length; i++)
                {
                    var packet = packets[i];
                    if (packet != null && packet.timestamp < (pointer_timestamp + desired_span) && packet.timestamp >= pointer_timestamp)
                    {
                        if (bestCandidate == null || packet.timestamp < bestCandidate.timestamp || (packet.timestamp == bestCandidate.timestamp && packet.span >= bestCandidate.span))
                        {
                            besti = i;
                            bestCandidate = packet;

                        }
                    }
                }

                if (bestCandidate != null)
                {
                    i = besti;
                }
            }

            // If we foudn something
            if (i != packets.Length)
            {
                // We (obviously) haven't lost this packet
                lost_count = 0;

                // In this case, 0 isn't as a valid timestamp
                if (arrival[i] != 0)
                {
                    UpdateTimings(packets[i].timestamp - arrival[i] - buffer_margin);
                }

                // Copy packet
                outPacket = packets[i];
                packets[i] = null;
                last_returned_timestamp = outPacket.timestamp;

                // Point to the end of the current packet
                pointer_timestamp = outPacket.timestamp + outPacket.span;
                buffered = outPacket.span - desired_span;

                return Status.OK;
            }

            // If we haven't found anything worth returning
            lost_count++;
            var opt = ComputeOptDelay();

            // Should we force an increase in the buffer or just do normal interpolation?
            if (opt < 0)
            {
                // Need to increase buffering
                // Shift histogram to compensate
                ShiftTimings(-opt);

                outPacket = new()
                {
                    timestamp = pointer_timestamp,
                    span = -opt,
                };
                buffered = outPacket.span - desired_span;
                return Status.Insertion;
            }

            // Normal packet loss
            desired_span = RoundDown(desired_span, concealment_size);
            outPacket = new()
            {
                timestamp = pointer_timestamp,
                span = desired_span,
            };

            pointer_timestamp += outPacket.span;
            buffered = outPacket.span - desired_span;
            return Status.Missing;
        }

        long _UpdateDelay()
        {
            var opt = ComputeOptDelay();
            if (opt != 0)
            {
                ShiftTimings(-opt);
                pointer_timestamp += opt;
                if (opt < 0)
                {
                    interp_requested = -opt;
                }
            }

            return opt;
        }

        public long UpdateDelay()
        {
            // If the programmer calls UpdateDelaay() directly,
            // automatically disable auto-adjustment
            auto_adjust = false;
            return _UpdateDelay();
        }

        public long PointerTimestamp
        {
            get { return pointer_timestamp; }
        }

        public void Tick()
        {
            // Automatically adjust the buffering delay if requested
            if (auto_adjust)
                _UpdateDelay();

            if (buffered >= 0)
            {
                next_stop = pointer_timestamp - buffered;
            }
            else
            {
                next_stop = pointer_timestamp;
                Debug.Assert(buffered >= 0, $"Jitter sees negative buffering, your code might be broken. Value is {buffered}");
            }
        }

        public void RemainingSpan(long rem)
        {
            // Automatically adjust the buffering delay if requested
            if (auto_adjust)
                _UpdateDelay();

            Debug.Assert(buffered >= 0, $"Jitter sees negative buffering, your code might be broken. Value is {buffered}");
            next_stop = pointer_timestamp - rem;
        }

        public bool Available
        {
            get
            {
                foreach (var packet in packets)
                {
                    if (packet != null && packet.timestamp >= pointer_timestamp)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int MaxLateRate
        {
            get { return max_late_rate; }
            set
            {
                max_late_rate = value;
                window_size = 100 * TOP_DELAY / max_late_rate;
                subwindow_size = window_size / MAX_BUFFERS;
            }
        }

        public int DelayStep
        {
            get { return delay_step; }
            set { delay_step = value; }
        }
    }
}
