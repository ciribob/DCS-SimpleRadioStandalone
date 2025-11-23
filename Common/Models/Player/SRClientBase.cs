using System;
using System.Net;
using System.Text.Json.Serialization;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

public class SRClientBase : PropertyChangedBaseClass
{
    private int _coalition;
    [JsonIgnore] private float _lineOfSightLoss; // 0.0 is NO Loss therefore Full line of sight

    private string _name = "";

    //TODO move all these references out to a different class!
    //These should not be here / the model doubled up on

    // Used by server client list to display last frequency client transmitted on
    [JsonIgnore] private string _transmittingFrequency;

    public string ClientGuid { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            if (value == null || value == "") value = "---";

            if (_name != value)
            {
                _name = value;
                NotifyPropertyChanged();
            }
        }
    }

    public int Coalition
    {
        get => _coalition;
        set
        {
            _coalition = value;
            NotifyPropertyChanged();
        }
    }

    public bool AllowRecord { get; set; }

    public int Seat { get; set; }

    public PlayerRadioInfoBase RadioInfo { get; set; }

    public LatLngPosition LatLngPosition { get; set; }

    [JsonIgnore] public string AllowRecordingStatus => AllowRecord ? "R" : "-";

    [JsonIgnore] public bool Muted { get; set; }

    [JsonIgnore] public IPEndPoint VoipPort { get; set; }


    [JsonIgnore]
    public float LineOfSightLoss
    {
        get
        {
            if (_lineOfSightLoss == 0) return 0;
            if (LatLngPosition?.lat == 0 && LatLngPosition?.lat == 0) return 0;
            return _lineOfSightLoss;
        }
        set => _lineOfSightLoss = value;
    }

    [JsonIgnore]
    public string TransmittingFrequency
    {
        get => _transmittingFrequency;
        set
        {
            if (_transmittingFrequency != value)
            {
                _transmittingFrequency = value;
                NotifyPropertyChanged();
            }
        }
    }

    // Used by server client list to remove last frequency client transmitted on after threshold
    [JsonIgnore] public DateTime LastTransmissionReceived { get; set; }

    [JsonIgnore] public Guid ClientSession { get; set; }
    public bool Gateway { get; set; } = false;

    public int DISEntityId { get; set; } = -1;
    public bool GatewayClient { get; set; }

    public override string ToString()
    {
        string side;

        if (Coalition == 1)
            side = "Red";
        else if (Coalition == 2)
            side = "Blue";
        else
            side = "Spectator";
        return Name == ""
            ? "Unknown"
            : Name + " - " + side + " LOS Loss " + _lineOfSightLoss + " Pos" + LatLngPosition;
    }

    public bool MetaDataEquals(SRClientBase other, bool usePosition)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other.GetType() != GetType()) return false;

        if (usePosition && !LatLngPosition.Equals(other.LatLngPosition)) return false;

        return Coalition == other.Coalition
               && Seat == other.Seat
               && Name == other.Name
               //RadioInfo is ignored!
               && AllowRecord == other.AllowRecord
               && ClientGuid == other.ClientGuid;
    }

    public SRClientBase DeepClone()
    {
        var copy = new SRClientBase
        {
            RadioInfo = RadioInfo?.DeepClone(),
            LatLngPosition = LatLngPosition?.DeepClone(),
            Name = Name,
            AllowRecord = AllowRecord,
            Seat = Seat,
            ClientGuid = ClientGuid,
            Coalition = Coalition,
            GatewayClient = GatewayClient
        };

        return copy;
    }
}