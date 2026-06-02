namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Voice;

// SyncedServerSettings.GlobalFrequencies is populated from the server-pushed
// GLOBAL_LOBBY_FREQUENCIES setting and is delivered without signing, PKI, or
// any per-client opt-out (see SyncedServerSettings.Decode and the
// SYNC / UPDATE / RADIO_UPDATE / SERVER_SETTINGS handlers in
// TCPClientHandler). A malicious or compromised server can therefore name an
// arbitrary frequency as "global" at any moment. Treating that list as
// authoritative on the receiver let the server (1) strip the per-packet
// encryption byte before the strict-encryption gate evaluated it and (2)
// bypass the LOS / in-range / blocked-radio realism gates per-frequency.
//
// The policy enforced here is: the client treats GlobalFrequencies as a
// display-only hint. The per-packet encryption byte and the realism gates
// must always be evaluated against the sender's intended values.
public static class GlobalFrequencyPolicy
{
    public static bool ShouldStripReceiverEncryption() => false;

    public static bool ShouldBypassRealismGates() => false;
}
