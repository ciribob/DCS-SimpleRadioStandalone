using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;

internal class JsonDCSPropertiesResolver
{
    public static void StripDCSIgnored(JsonTypeInfo jsonTypeInfo)
    {
        if (jsonTypeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        if (jsonTypeInfo.Properties is List<JsonPropertyInfo>)
        {
            (jsonTypeInfo.Properties as List<JsonPropertyInfo>).RemoveAll(prop => Attribute.IsDefined(prop.PropertyType, typeof(JsonDCSIgnoreSerializationAttribute)));
        }
        else
        {
            int i = jsonTypeInfo.Properties.Count - 1;
            while (i > -1)
            {
                var prop = jsonTypeInfo.Properties[i];
                if (Attribute.IsDefined(prop.PropertyType, typeof(JsonDCSIgnoreSerializationAttribute)))
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