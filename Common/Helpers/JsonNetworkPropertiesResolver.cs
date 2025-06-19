using System;
using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;

internal class JsonNetworkPropertiesResolver
{
    public static void StripNetworkIgnored(JsonTypeInfo jsonTypeInfo)
    {
        if (jsonTypeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        if (jsonTypeInfo.Properties is List<JsonPropertyInfo>)
        {
            (jsonTypeInfo.Properties as List<JsonPropertyInfo>).RemoveAll(prop => Attribute.IsDefined(prop.PropertyType, typeof(JsonNetworkIgnoreSerializationAttribute)));
        }
        else
        {
            int i = jsonTypeInfo.Properties.Count - 1;
            while (i > -1)
            {
                var prop = jsonTypeInfo.Properties[i];
                if (Attribute.IsDefined(prop.PropertyType, typeof(JsonNetworkIgnoreSerializationAttribute)))
                {
                    jsonTypeInfo.Properties.RemoveAt(i);
                }
                else
                {
                    i--;
                }
            }
        }
    }
}