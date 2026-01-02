using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

public class NetworkMessage
{
    public enum MessageType
    {
        UPDATE, //META Data update - No Radio Information
        PING,
        SYNC,
        RADIO_UPDATE, //Only received server side
        SERVER_SETTINGS,
        CLIENT_DISCONNECT, // Client disconnected
        VERSION_MISMATCH,
        EXTERNAL_AWACS_MODE_PASSWORD, // Received server side to "authenticate"/pick side for external AWACS mode
        EXTERNAL_AWACS_MODE_DISCONNECT // Received server side on "voluntary" disconnect by the client (without closing the server connection)
        ,
        GATEWAY_CLIENT_FULL_UPDATE,
        GATEWAY_CLIENT_METADATA_UPDATE,
        GATEWAY_CLIENT_DISCONNECT
    }

    private static readonly JsonSerializerOptions JsonSerializerSettings = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        {
            Modifiers = { JsonNetworkPropertiesResolver.StripNetworkIgnored } // strip out things not required for the TCP sync
        },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IncludeFields = true,
    };

    public SRClientBase Client { get; set; }

    public MessageType MsgType { get; set; }

    public List<SRClientBase> Clients { get; set; }

    public Dictionary<string, string> ServerSettings { get; set; }

    public string ExternalAWACSModePassword { get; set; }

    public string Version { get; set; }

    public string Encode()
    {
        Version = UpdaterChecker.VERSION;
        return JsonSerializer.Serialize(this, JsonSerializerSettings) + "\n";
    }
}