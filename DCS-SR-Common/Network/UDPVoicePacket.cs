using System;
using System.Text;
using System.Windows.Documents;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network
{
    /**
       * UDP PACKET LAYOUT
       *
       * - HEADER SEGMENT
       * UInt16 Packet Length - 2 bytes
       * UInt16 AudioPart1 Length - 2 bytes
       * UInt16 FrequencyPart Length - 2 bytes
       * - AUDIO SEGMENT
       * Bytes AudioPart1 - variable bytes
       * - FREQUENCY SEGMENT (one or multiple)
       * double Frequency - 8 bytes
       * byte Modulation - 1 byte
       * byte Encryption - 1 byte
       * radio Num - 1 byte
       * - FIXED SEGMENT
       * UInt UnitId - 4 bytes
       * UInt64 PacketId - 8 bytes
       * byte Retransmit / node / hop count - 1 byte
       * Bytes / ASCII String TRANSMISSION GUID - 22 bytes used for transmission relay
       * Bytes / ASCII String CLIENT GUID - 22 bytes
       */

    public class UDPVoicePacket
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static readonly int GuidLength = 22;

        public static readonly int PacketHeaderLength =
            sizeof(ushort) // UInt16 Packet Length - 2 bytes
            + sizeof(ushort) // UInt16 AudioPart1 Length - 2 bytes
            + sizeof(ushort); // UInt16 FrequencyPart Length - 2 bytes

        public static readonly int FrequencySegmentLength =
            sizeof(double) // double Frequency - 8 bytes
            + sizeof(byte) // byte Modulation - 1 byte
            + sizeof(byte) // byte Encryption - 1 byte
            + sizeof(byte); // Transmitting radio

        public static readonly int FixedPacketLength =
            sizeof(uint) // UInt UnitId - 4 bytes
            + sizeof(ulong) // UInt64 PacketId - 8 bytes
            + sizeof(byte) // Byte indicating number of hops for this message // default is 0
            + GuidLength  // Bytes / ASCII String Transmission GUID - 22 bytes
            + GuidLength; // Bytes / ASCII String GUID - 22 bytes

        // HEADER SEGMENT
        public ushort PacketLength { get; set; }

        // AUDIO SEGMENT
        public ushort AudioPart1Length { get; set; }
        public byte[] AudioPart1Bytes { get; set; }

        // FREQUENCY SEGMENT
        public double[] Frequencies { get; set; }

        /*
        AM = 0,
        FM = 1,
        INTERCOM = 2,
        DISABLED = 3,
        HAVEQUICK = 4,
        SATCOM = 5,
        MIDS = 6,*/
        public byte[] Modulations { get; set; }

        public byte[] Encryptions { get; set; }

        // FIXED SEGMENT
        public uint UnitId { get; set; }
        public byte[] GuidBytes { get; set; }
        public string Guid { get; set; }

        public byte[] OriginalClientGuidBytes { get; set; }
        public string OriginalClientGuid { get; set; }
        public ulong PacketNumber { get; set; }
        public byte[] TransmittingRadio { get; set; }
        //Number of times its been retransmitted - added to stop retransmission loop with sensible limit
        public byte RetransmissionCount { get; set; } = new byte();

        public byte[] EncodePacket()
        {
            // Calculate length of different packet segments for header
            var frequencyPartLength = Frequencies.Length * FrequencySegmentLength;

            var dynamicSegmentLength = AudioPart1Length + frequencyPartLength;
            var staticSegmentLength = PacketHeaderLength + FixedPacketLength;
            var totalPacketLength = staticSegmentLength + dynamicSegmentLength;

            PacketLength = (ushort)totalPacketLength;

            // Allocate memory for all combined packet segments
            var combinedBytes = new byte[totalPacketLength];

            /**
             * HEADER SEGMENT
             */

            // Total packet length
            var packetLength = BitConverter.GetBytes(Convert.ToUInt16(totalPacketLength));
            combinedBytes[0] = packetLength[0];
            combinedBytes[1] = packetLength[1];

            // Length of audio part
            var part1Size = BitConverter.GetBytes(Convert.ToUInt16(AudioPart1Length));
            combinedBytes[2] = part1Size[0];
            combinedBytes[3] = part1Size[1];

            // Length of frequencies part
            var freqSize = BitConverter.GetBytes(Convert.ToUInt16(frequencyPartLength));
            combinedBytes[4] = freqSize[0];
            combinedBytes[5] = freqSize[1];

            /**
             * AUDIO SEGMENT
             */

            // Copy segment after length headers
            Buffer.BlockCopy(AudioPart1Bytes, 0, combinedBytes, 6, AudioPart1Bytes.Length);

            /**
             * FREQUENCY SEGMENT
             */

            // Offset for frequency info, incremented by iteration below
            var frequencyOffset = PacketHeaderLength + AudioPart1Length;

            // Add freq/modulation/encryption info for each transmitted frequency
            for (var i = 0; i < Frequencies.Length; i++)
            {
                var freq = BitConverter.GetBytes(Frequencies[i]);

                // Radio frequency (double, 8 bytes)
                combinedBytes[frequencyOffset] = freq[0];
                combinedBytes[frequencyOffset + 1] = freq[1];
                combinedBytes[frequencyOffset + 2] = freq[2];
                combinedBytes[frequencyOffset + 3] = freq[3];
                combinedBytes[frequencyOffset + 4] = freq[4];
                combinedBytes[frequencyOffset + 5] = freq[5];
                combinedBytes[frequencyOffset + 6] = freq[6];
                combinedBytes[frequencyOffset + 7] = freq[7];

                // Radio that is transmitting (1 byte)
                combinedBytes[frequencyOffset + 8] = TransmittingRadio[i];

                // Radio modulation (1 byte), defaults to AM if not defined for all frequencies
                var mod = Modulations.Length > i ? Modulations[i] : (byte)4;
                combinedBytes[frequencyOffset + 9] = mod;

                // Radio encryption (1 byte), defaults to disabled if not defined for all frequencies
                var enc = Encryptions.Length > i ? Encryptions[i] : (byte)0;
                combinedBytes[frequencyOffset + 10] = enc;

                frequencyOffset += FrequencySegmentLength;
            }

            /**
             * FIXED SEGMENT
             */

            // Offset for fixed segment
            var fixedSegmentOffset = PacketHeaderLength + dynamicSegmentLength;

            // Unit ID (uint, 4 bytes)
            var unitId = BitConverter.GetBytes(UnitId);
            combinedBytes[fixedSegmentOffset] = unitId[0];
            combinedBytes[fixedSegmentOffset + 1] = unitId[1];
            combinedBytes[fixedSegmentOffset + 2] = unitId[2];
            combinedBytes[fixedSegmentOffset + 3] = unitId[3];

            // Packet ID (ulong, 8 bytes)
            var packetNumber = BitConverter.GetBytes(PacketNumber); //8 bytes
            combinedBytes[fixedSegmentOffset + 4] = packetNumber[0];
            combinedBytes[fixedSegmentOffset + 5] = packetNumber[1];
            combinedBytes[fixedSegmentOffset + 6] = packetNumber[2];
            combinedBytes[fixedSegmentOffset + 7] = packetNumber[3];
            combinedBytes[fixedSegmentOffset + 8] = packetNumber[4];
            combinedBytes[fixedSegmentOffset + 9] = packetNumber[5];
            combinedBytes[fixedSegmentOffset + 10] = packetNumber[6];
            combinedBytes[fixedSegmentOffset + 11] = packetNumber[7];

            // back before Transmission GUID
            combinedBytes[totalPacketLength - (GuidLength + GuidLength + 1)] = RetransmissionCount;

            //Copy Transmission nearly at the end - just before the clientGUID
            Buffer.BlockCopy(OriginalClientGuidBytes, 0, combinedBytes, totalPacketLength - (GuidLength + GuidLength), GuidLength);

            // Copy client GUID to end of packet
            Buffer.BlockCopy(GuidBytes, 0, combinedBytes, totalPacketLength - GuidLength, GuidLength);

            return combinedBytes;
        }

        public static UDPVoicePacket DecodeVoicePacket(byte[] encodedOpusAudio, bool decode = true)
        {
            try
            {
                // Last 22 bytes of packet are always the client GUID
                var receivingGuid = Encoding.ASCII.GetString(
                    encodedOpusAudio, encodedOpusAudio.Length - GuidLength, GuidLength);

                //Copy Transmission nearly at the end - just before the client GUID
                var transmissionBytes = new byte[GuidLength];

                //copy the raw bytes as we'll need them for retransmit
                Buffer.BlockCopy(encodedOpusAudio, encodedOpusAudio.Length - (GuidLength + GuidLength),
                    transmissionBytes, 0, GuidLength);

                var transmissionGuid = Encoding.ASCII.GetString(
                    encodedOpusAudio, encodedOpusAudio.Length - (GuidLength + GuidLength), GuidLength);

                //just before transmission GUID
                var retransmissionCount = encodedOpusAudio[encodedOpusAudio.Length - (GuidLength + GuidLength + 1)];

                var packetLength = BitConverter.ToUInt16(encodedOpusAudio, 0);

                var ecnAudio1 = BitConverter.ToUInt16(encodedOpusAudio, 2);

                var freqLength = BitConverter.ToUInt16(encodedOpusAudio, 4);
                var freqCount = freqLength / FrequencySegmentLength;

                byte[] part1 = null;

                if (decode)
                {
                    part1 = new byte[ecnAudio1];
                    Buffer.BlockCopy(encodedOpusAudio, 6, part1, 0, ecnAudio1);
                }

                var frequencies = new double[freqCount];
                var transmittingRadio = new byte[freqCount];
                var modulations = new byte[freqCount];
                var encryptions = new byte[freqCount];

                var frequencyOffset = PacketHeaderLength + ecnAudio1;
                for (var i = 0; i < freqCount; i++)
                {
                    frequencies[i] = BitConverter.ToDouble(encodedOpusAudio, frequencyOffset);
                    transmittingRadio[i] = encodedOpusAudio[frequencyOffset + 8];
                    modulations[i] = encodedOpusAudio[frequencyOffset + 9];
                    encryptions[i] = encodedOpusAudio[frequencyOffset + 10];

                    frequencyOffset += FrequencySegmentLength;
                }

                var unitId = BitConverter.ToUInt32(encodedOpusAudio, PacketHeaderLength + ecnAudio1 + freqLength);

                var packetNumber =
                    BitConverter.ToUInt64(encodedOpusAudio, PacketHeaderLength + ecnAudio1 + freqLength + 4);

                return new UDPVoicePacket
                {
                    Guid = receivingGuid,
                    AudioPart1Bytes = part1,
                    AudioPart1Length = ecnAudio1,
                    Frequencies = frequencies,
                    UnitId = unitId,
                    Encryptions = encryptions,
                    Modulations = modulations,
                    PacketNumber = packetNumber,
                    TransmittingRadio = transmittingRadio,
                    PacketLength = packetLength,
                    OriginalClientGuid = transmissionGuid,
                    OriginalClientGuidBytes = transmissionBytes,
                    RetransmissionCount = retransmissionCount
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unable to decode UDP Voice Packet");
            }

            return null;
        }
    }
}
