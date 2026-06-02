using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Voice;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Tests.Network.Voice;

// Regression tests for the server-pushed GLOBAL_LOBBY_FREQUENCIES trust
// downgrade in UDPClientAudioProcessor.UdpAudioDecode. Before this fix the
// receiver rewrote udpVoicePacket.Encryptions[i] to 0 and bypassed the
// LOS / in-range / blocked-radio realism gates whenever a frequency appeared
// in SyncedServerSettings.GlobalFrequencies, allowing a malicious or
// compromised server to silently strip per-packet encryption and bypass
// realism gates by naming arbitrary frequencies "global" at any time.
[TestClass]
public class GlobalFrequencyPolicyTests
{
    [TestMethod]
    public void GlobalFrequenciesAreDisplayOnly_DoNotStripReceiverEncryption()
    {
        Assert.IsFalse(GlobalFrequencyPolicy.ShouldStripReceiverEncryption(),
            "GLOBAL_LOBBY_FREQUENCIES is server-pushed and unauthenticated. " +
            "The receiver must never rewrite the per-packet encryption byte.");
    }

    [TestMethod]
    public void GlobalFrequenciesAreDisplayOnly_DoNotBypassRealismGates()
    {
        Assert.IsFalse(GlobalFrequencyPolicy.ShouldBypassRealismGates(),
            "GLOBAL_LOBBY_FREQUENCIES is server-pushed and unauthenticated. " +
            "The receiver must never bypass LOS / in-range / blocked-radio gates " +
            "for a server-named frequency.");
    }

    [TestMethod]
    public void ServerPushedGlobalFrequencyDoesNotMakeEncryptedTrafficDecryptableOnCleartextReceiver()
    {
        // Simulate the receiver-side decision UDPClientAudioProcessor.UdpAudioDecode
        // performs after SyncedServerSettings.Decode has applied an attacker-pushed
        // GLOBAL_LOBBY_FREQUENCIES delta. Even though 243 MHz now appears in
        // SyncedServerSettings.GlobalFrequencies, the patched receiver must keep
        // the sender's encryption byte intact and let CanHearTransmission refuse.

        var serverSettings = new SyncedServerSettings();
        serverSettings.Decode(new Dictionary<string, string>
        {
            { ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES.ToString(), "243.0" },
            { ServerSettingsKeys.STRICT_RADIO_ENCRYPTION.ToString(), "true" },
        });
        CollectionAssert.Contains(serverSettings.GlobalFrequencies, 243e6);

        var senderEncryptionKey = (byte)0xAB;
        const double frequency = 243e6;
        var radioInfo = new PlayerRadioInfoBase { unitId = 1 };
        radioInfo.radios[1] = new RadioBase
        {
            freq = frequency,
            modulation = Modulation.AM,
            enc = false,
            encKey = 0,
        };

        var packetEncryption = senderEncryptionKey;
        var globalFrequency = serverSettings.GlobalFrequencies.Contains(frequency);
        if (globalFrequency && GlobalFrequencyPolicy.ShouldStripReceiverEncryption())
            packetEncryption = 0;

        var radio = radioInfo.CanHearTransmission(
            frequency,
            Modulation.AM,
            packetEncryption,
            strictEncryption: serverSettings.GetSettingAsBool(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION),
            sendingUnitId: 2,
            blockedRadios: new List<int>(),
            out _,
            out var decryptable);

        Assert.IsNotNull(radio, "Receiver should still see the matching radio.");
        Assert.AreEqual(senderEncryptionKey, packetEncryption,
            "Receiver must not rewrite the per-packet encryption byte for a server-named global frequency.");
        Assert.IsFalse(decryptable,
            "A cleartext receiver must NOT treat encrypted traffic as decryptable " +
            "just because the server named the frequency global.");
    }

    [TestMethod]
    public void ServerPushedGlobalFrequencyDoesNotBypassBlockedRadioRealismGate()
    {
        // Simulate the LOS / in-range / blocked-radio realism gate evaluated in
        // UDPClientAudioProcessor.UdpAudioDecode. With hasLineOfSight=false,
        // inRange=false, and the receiving radio explicitly in the local
        // blockedRadios set, the gate must be false even when 243 MHz appears
        // in the server-pushed GlobalFrequencies list.

        var serverSettings = new SyncedServerSettings();
        serverSettings.Decode(new Dictionary<string, string>
        {
            { ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES.ToString(), "243.0" },
        });

        const double frequency = 243e6;
        const bool hasLineOfSight = false;
        const bool inRange = false;
        var receivedOn = 1;
        var blockedRadios = new List<int> { receivedOn };
        var radioModulation = Modulation.AM;
        var globalFrequency = serverSettings.GlobalFrequencies.Contains(frequency);

        var realismGate =
            radioModulation == Modulation.INTERCOM
            || radioModulation == Modulation.MIDS
            || (globalFrequency && GlobalFrequencyPolicy.ShouldBypassRealismGates())
            || (hasLineOfSight && inRange && !blockedRadios.Contains(receivedOn));

        Assert.IsFalse(realismGate,
            "The realism gate must remain closed when LOS/range fail and the " +
            "radio is locally blocked, regardless of GLOBAL_LOBBY_FREQUENCIES.");
    }
}
