using System.Text;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Tests.Network;

[TestClass]
public class UDPVoicePacketTests
{
    [TestMethod]
    public void EncodeInitialVoicePacket()
    {
        var udpVoicePacket = new UDPVoicePacket
        {
            GuidBytes = Encoding.ASCII.GetBytes("ufYS_WlLVkmFPjqCgxz6GA"),
            AudioPart1Bytes = new byte[] { 0, 1, 2, 3, 4, 5 },
            AudioPart1Length = 6,
            Frequencies = new double[] { 100 },
            UnitId = 1,
            Encryptions = new byte[] { 0 },
            Modulations = new byte[] { 4 },
            OriginalClientGuidBytes = Encoding.ASCII.GetBytes("ufYS_WlLVkmFPjqCgxz6GA"),
            PacketNumber = 1,
            RetransmissionCount = (byte)4u
        };

        var encodedUdpVoicePacket = udpVoicePacket.EncodePacket();

        Assert.AreEqual(79, udpVoicePacket.PacketLength);
        Assert.HasCount(79, encodedUdpVoicePacket);

        var expectedEncodedUdpVoicePacket = new byte[79]
        {
            // Total packet length
            79, 0,
            // Length of audio part
            6, 0,
            // Length of frequencies part
            10, 0,
            // Audio part
            0, 1, 2, 3, 4, 5,
            // Radio frequency #1
            0, 0, 0, 0, 0, 0, 89, 64,
            // Radio modulation #1
            4,
            // Radio encryption #1
            0,
            // Unit ID
            1, 0, 0, 0,
            // Packet ID
            1, 0, 0, 0, 0, 0, 0, 0,
            //Retransmission Count,
            4,
            // Transmission GUID
            117, 102, 89, 83, 95, 87, 108, 76, 86, 107, 109, 70, 80, 106, 113, 67, 103, 120, 122, 54, 71, 65,
            // Client GUID
            117, 102, 89, 83, 95, 87, 108, 76, 86, 107, 109, 70, 80, 106, 113, 67, 103, 120, 122, 54, 71, 65
        };

        for (var i = 0; i < expectedEncodedUdpVoicePacket.Length; i++)
            Assert.AreEqual(expectedEncodedUdpVoicePacket[i], encodedUdpVoicePacket[i]);
        CollectionAssert.AreEqual(expectedEncodedUdpVoicePacket, encodedUdpVoicePacket);
    }

    [TestMethod]
    public void DecodeInitialVoicePacket()
    {
        var encodedUdpVoicePacket = new byte[79]
        {
            // Total packet length
            79, 0,
            // Length of audio part
            6, 0,
            // Length of frequencies part
            10, 0,
            // Audio part
            0, 1, 2, 3, 4, 5,
            // Radio frequency #1
            0, 0, 0, 0, 0, 0, 89, 64,
            // Radio modulation #1
            4,
            // Radio encryption #1
            0,
            // Unit ID
            1, 0, 0, 0,
            // Packet ID
            1, 0, 0, 0, 0, 0, 0, 0,
            //Retransmission Count,
            231,
            // Transmission GUID
            117, 102, 89, 83, 95, 87, 108, 76, 86, 107, 109, 70, 80, 106, 113, 67, 103, 120, 122, 54, 71, 65,
            // Client GUID
            117, 102, 89, 83, 95, 87, 108, 76, 86, 107, 109, 70, 80, 106, 113, 67, 103, 120, 122, 54, 71, 65
        };

        var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(encodedUdpVoicePacket);

        Assert.AreEqual("ufYS_WlLVkmFPjqCgxz6GA", udpVoicePacket.Guid);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5 }, udpVoicePacket.AudioPart1Bytes);
        Assert.AreEqual(6, udpVoicePacket.AudioPart1Length);
        CollectionAssert.AreEqual(new double[] { 100 }, udpVoicePacket.Frequencies);
        Assert.AreEqual((uint)1, udpVoicePacket.UnitId);
        CollectionAssert.AreEqual(new byte[] { 0 }, udpVoicePacket.Encryptions);
        CollectionAssert.AreEqual(new byte[] { 4 }, udpVoicePacket.Modulations);
        Assert.AreEqual((ulong)1, udpVoicePacket.PacketNumber);
        Assert.AreEqual((ushort)79, udpVoicePacket.PacketLength);
        Assert.AreEqual((byte)231, udpVoicePacket.RetransmissionCount);
        Assert.AreEqual("ufYS_WlLVkmFPjqCgxz6GA", udpVoicePacket.OriginalClientGuid);
    }

    [TestMethod]
    public void EncodeMultipleFrequencyVoicePacket()
    {
        var udpVoicePacket = new UDPVoicePacket
        {
            GuidBytes = Encoding.ASCII.GetBytes("ufYS_WlLVkmFPjqCgxz6GA"),
            AudioPart1Bytes = new byte[] { 0, 1, 2, 3, 4, 5 },
            AudioPart1Length = 6,
            Frequencies = new double[] { 251000000, 30000000, 251000000 },
            UnitId = 1,
            Encryptions = new byte[] { 0, 0, 1 },
            Modulations = new byte[] { 0, 1, 0 },
            PacketNumber = 1,
            OriginalClientGuidBytes = Encoding.ASCII.GetBytes("ufYS_WlLVkmFPjqCgxz6GA"),
            RetransmissionCount = (byte)254u
        };

        var encodedUdpVoicePacket = udpVoicePacket.EncodePacket();

        Assert.AreEqual(99, udpVoicePacket.PacketLength);
        Assert.HasCount(99, encodedUdpVoicePacket);

        var expectedEncodedUdpVoicePacket = new byte[99]
        {
            // Total packet length
            99, 0,
            // Length of audio part
            6, 0,
            // Length of frequencies part
            30, 0,
            // Audio part
            0, 1, 2, 3, 4, 5,
            // Radio frequency #1
            0, 0, 0, 128, 233, 235, 173, 65,
            // Radio modulation #1
            0,
            // Radio encryption #1
            0,
            // Radio frequency #2
            0, 0, 0, 0, 56, 156, 124, 65,
            // Radio modulation #2
            1,
            // Radio encryption #2
            0,
            // Radio frequency #3
            0, 0, 0, 128, 233, 235, 173, 65,
            // Radio modulation #3
            0,
            // Radio encryption #3
            1,
            // Unit ID
            1, 0, 0, 0,
            // Packet ID
            1, 0, 0, 0, 0, 0, 0, 0,
            //Retransmission Count
            254,
            // Transmission GUID
            117, 102, 89, 83, 95, 87, 108, 76, 86, 107, 109, 70, 80, 106, 113, 67, 103, 120, 122, 54, 71, 65,
            // Client GUID
            117, 102, 89, 83, 95, 87, 108, 76, 86, 107, 109, 70, 80, 106, 113, 67, 103, 120, 122, 54, 71, 65
        };

        CollectionAssert.AreEqual(expectedEncodedUdpVoicePacket, encodedUdpVoicePacket);
    }

    [TestMethod]
    public void DecodeMultipleFrequencyVoicePacket()
    {
        var encodedUdpVoicePacket = new byte[99]
        {
            // Total packet length
            99, 0,
            // Length of audio part
            6, 0,
            // Length of frequencies part
            30, 0,
            // Audio part
            0, 1, 2, 3, 4, 5,
            // Radio frequency #1
            0, 0, 0, 128, 233, 235, 173, 65,
            // Radio modulation #1
            0,
            // Radio encryption #1
            0,
            // Radio frequency #2
            0, 0, 0, 0, 56, 156, 124, 65,
            // Radio modulation #2
            1,
            // Radio encryption #2
            0,
            // Radio frequency #3
            0, 0, 0, 128, 233, 235, 173, 65,
            // Radio modulation #3
            0,
            // Radio encryption #3
            1,
            // Unit ID
            1, 0, 0, 0,
            // Packet ID
            1, 0, 0, 0, 0, 0, 0, 0,
            254,
            // Transmission GUID
            117, 102, 89, 83, 95, 87, 108, 76, 86, 107, 109, 70, 80, 106, 113, 67, 103, 120, 122, 54, 71, 65,
            // Client GUID
            117, 102, 89, 83, 95, 87, 108, 76, 86, 107, 109, 70, 80, 106, 113, 67, 103, 120, 122, 54, 71, 65
        };

        var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(encodedUdpVoicePacket);

        Assert.AreEqual("ufYS_WlLVkmFPjqCgxz6GA", udpVoicePacket.Guid);
        Assert.AreEqual("ufYS_WlLVkmFPjqCgxz6GA", udpVoicePacket.OriginalClientGuid);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5 }, udpVoicePacket.AudioPart1Bytes);
        Assert.AreEqual(6, udpVoicePacket.AudioPart1Length);
        CollectionAssert.AreEqual(new double[] { 251000000, 30000000, 251000000 }, udpVoicePacket.Frequencies);
        Assert.AreEqual((uint)1, udpVoicePacket.UnitId);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 1 }, udpVoicePacket.Encryptions);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 0 }, udpVoicePacket.Modulations);
        Assert.AreEqual((ulong)1, udpVoicePacket.PacketNumber);
        Assert.AreEqual((ushort)99, udpVoicePacket.PacketLength);

        Assert.AreEqual((byte)254, udpVoicePacket.RetransmissionCount);
    }
}