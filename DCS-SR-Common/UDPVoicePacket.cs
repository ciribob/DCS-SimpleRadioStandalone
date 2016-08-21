using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common
{
    /**
       * UDP PACKET LAYOUT
       * UInt16 AudioPart1 Length - 2 bytes
       * UInt16 AudioPart2 Length - 2 bytes
       * Bytes AudioPart1 - variable bytes
       * Bytes AudioPart2 - variable bytes
       * Double Frequency Length - 8 bytes
       * byte Modulation Length - 1 byte
       * byte Encryption Length - 1 byte
       * UInt UnitId Length - 4 bytes
       * Byte[] / Ascii String GUID length - 22
       */
    public class UDPVoicePacket
    {
        public static readonly int GuidLength = 22;

        public static readonly int FixedPacketLength =
            sizeof(UInt16)
            + sizeof(UInt16)
            + sizeof(double)
            + sizeof(int)
            + sizeof(byte)
            + sizeof(byte)
            + sizeof(uint) + GuidLength;

        public UInt16 AudioPart1Length { get; set; }
        public byte[] AudioPart1Bytes { get; set; }

        public UInt16 AudioPart2Length { get; set; }
        public byte[] AudioPart2Bytes { get; set; }

        public double Frequency { get; set; }
        public byte Modulation { get; set; } //0 - AM, 1 - FM, 2- Intercom, 3 - disabled
        public byte Encryption { get; set; }
        public uint UnitId { get; set; }
        public byte[] GuidBytes { get; set; }
        public String Guid { get; set; }
      

        public byte[] EncodePacket()
        {

            //2 * int16 at the start giving the two segments
            //
            var combinedLength = AudioPart1Length + AudioPart2Length + 4; //calculate first part of packet length + 4 for 2* int16
            var combinedBytes = new byte[combinedLength+FixedPacketLength];

            byte[] part1Size = BitConverter.GetBytes(Convert.ToUInt16(AudioPart1Bytes.Length));
            combinedBytes[0] = part1Size[0];
            combinedBytes[1] = part1Size[1];

            byte[] part2Size = BitConverter.GetBytes(Convert.ToUInt16(AudioPart2Bytes.Length));
            combinedBytes[2] = part2Size[0];
            combinedBytes[3] = part2Size[1];

            //copy audio segments after we've added the two length heads
            Buffer.BlockCopy(AudioPart1Bytes, 0, combinedBytes, 4, AudioPart1Bytes.Length); // copy audio
            Buffer.BlockCopy(AudioPart2Bytes, 0, combinedBytes, AudioPart1Bytes.Length + 4, AudioPart2Bytes.Length); // copy audio

            var freq = BitConverter.GetBytes(Frequency); //8 bytes

            combinedBytes[combinedLength] = freq[0];
            combinedBytes[combinedLength + 1] = freq[1];
            combinedBytes[combinedLength + 2] = freq[2];
            combinedBytes[combinedLength + 3] = freq[3];
            combinedBytes[combinedLength + 4] = freq[4];
            combinedBytes[combinedLength + 5] = freq[5];
            combinedBytes[combinedLength + 6] = freq[6];
            combinedBytes[combinedLength + 7] = freq[7];

            //modulation
            combinedBytes[combinedLength + 8] = (byte)Modulation; //1 byte;

            //encryption
            combinedBytes[combinedLength + 9] = (byte)Encryption; //1 byte;

            //unit Id
            var unitId = BitConverter.GetBytes(UnitId); //4 bytes
            combinedBytes[combinedLength + 10] = unitId[0];
            combinedBytes[combinedLength + 11] = unitId[1];
            combinedBytes[combinedLength + 12] = unitId[2];
            combinedBytes[combinedLength + 13] = unitId[3];

            Buffer.BlockCopy(GuidBytes, 0, combinedBytes, combinedLength + FixedPacketLength - GuidLength, GuidLength);
            // copy short guid

            return combinedBytes;
        }

        public static UDPVoicePacket DecodeVoicePacket(byte[] encodedOpusAudio, bool decode = true)
        {
            //last 22 bytes are guid!
            var recievingGuid = Encoding.ASCII.GetString(
                encodedOpusAudio, encodedOpusAudio.Length - GuidLength, GuidLength);

            var ecnAudio1 = BitConverter.ToUInt16(encodedOpusAudio, 0);
            var ecnAudio2 = BitConverter.ToUInt16(encodedOpusAudio, 2);

            byte[] part1 = null;
            byte[] part2 = null;

            if (decode)
            {
                part1 = new byte[ecnAudio1];
                Buffer.BlockCopy(encodedOpusAudio, 4, part1, 0, ecnAudio1);

                part2 = new byte[ecnAudio2];
                Buffer.BlockCopy(encodedOpusAudio, 4 + ecnAudio1, part2, 0, ecnAudio2);
            }
         
            var frequency = BitConverter.ToDouble(encodedOpusAudio,
                ecnAudio1+ ecnAudio2+4);

            //after frequency and audio
            var modulation = (byte)encodedOpusAudio[ecnAudio1 + ecnAudio2 + 4 + 8];

            var encryption = (byte)encodedOpusAudio[ecnAudio1 + ecnAudio2 + 4 + 8 + 1 ];

            var unitId = BitConverter.ToUInt32(encodedOpusAudio, ecnAudio1 + ecnAudio2 + 4 + 8 + 1 + 1);

            return new UDPVoicePacket()
            {
                Guid = recievingGuid,
                AudioPart1Bytes = part1,
                AudioPart1Length = ecnAudio1,
                AudioPart2Bytes = part2,
                AudioPart2Length = ecnAudio2,
                Frequency = frequency,
                UnitId = unitId,
                Encryption = encryption,
                Modulation = modulation
            };

        }
    }
}
