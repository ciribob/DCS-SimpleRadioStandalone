namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network;

public class UDPInterfaceCommand
{
    public enum UDPCommandType
    {
        FREQUENCY_DELTA = 0,
        ACTIVE_RADIO = 1,
        TOGGLE_GUARD = 2,
        CHANNEL_UP = 3,
        CHANNEL_DOWN = 4,
        SET_VOLUME = 5,
        TRANSPONDER_POWER = 6,
        TRANSPONDER_M1_CODE = 7,
        TRANSPONDER_M3_CODE = 8,
        TRANSPONDER_M4 = 9,
        TRANSPONDER_IDENT = 10,
        GUARD = 11, // SET guard
        FREQUENCY_SET = 12,
        TRANSPONDER_M2_CODE = 13,
        TOGGLE_RADIO_RETRANSMIT = 14,
        RADIO_RETRANSMIT = 15,
        TOGGLE_GLOBAL_SIMULTANEOUS_TRANSMISSION = 16,
        GLOBAL_SIMULTANEOUS_TRANSMISSION = 17,
        TOGGLE_SIMULTANEOUS_RADIO = 18,
        SIMULTANEOUS_RADIO = 19,
        INTERCOM_CHANNEL = 20
        // INTERCOM_VOLUME = SET_VOLUME (5) RadioId = 0
    }

    public int RadioId { get; set; }
    public double Frequency { get; set; }
    public UDPCommandType Command { get; set; }
    public float Volume { get; set; }

    public bool Enabled { get; set; }

    public int Code { get; set; }
}