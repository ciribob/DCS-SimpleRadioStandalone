namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;

public class SRSClientStatus
{
    public bool Connected { get; set; }
    public string ClientIP { get; set; }
    public string SRSGuid { get; set; }
}