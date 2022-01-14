using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network
{
    public class Analytics
    {
        public static void Log(string eventCategory, string eventAction, string guid)
        {
#if NET461
            var objValue = Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\DCS-SR-Standalone", "SRSAnalyticsOptOut",
                "FALSE");
#else
            var objValue = "FALSE";
#endif
            if (objValue == null || (string) objValue != "TRUE")
            {
//#if !DEBUG
                var http = new HttpClient()
                {
                    BaseAddress = new Uri("http://www.google-analytics.com/")
                };
                http.DefaultRequestHeaders.Add("User-Agent", "DCS-SRS");
                http.DefaultRequestHeaders.ExpectContinue = false;

                try
                {
                    var content =
                        new StringContent(
                            $"v=1&tid=UA-115685293-1&cid={guid}&t=event&ec={eventCategory}&ea={eventAction}&el={UpdaterChecker.VERSION}",
                            Encoding.ASCII, "application/x-www-form-urlencoded");
                    http.PostAsync("collect", content);
                }
                catch
                {
                }
//#endif
            }
        }
    }
}