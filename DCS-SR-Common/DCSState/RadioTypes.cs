using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState
{
    public enum Radios
    {
        Unknown,
        Intercom,
        ARI1063,
        ANARC131,
        ANARC134,
        ANARC150,
        ANARC159,
        ANARC164,
        ANARC182,
        ANARC186,
        ANARC210,
        ANARC222,
        ANARC27,
        ANARC51BX,
        ARI232591,
        Baklan5,
        FR22,
        FR24,
        FuG16ZY,
        INTERCOM,
        JADRO1A,
        JADRO1I,
        KTR953HF,
        KY197A,
        MIDS,
        R800,
        R800L14,
        R828,
        R832,
        R832M,
        R852,
        R862,
        R863,
        R864,
        R1155,
        RSI6K,
        RSIU4V,
        RTA42ABENDIX,
        SCR522,
        SCR522A,
        SPU9SW,
        SRT651N,
        SUNAIRASB850,
        T1154,
        TRAP138A,
        TRC9600PR4G,
        TRTERA7000,
        TRTERA7200,
        UHFTRA6031,
        VTVU740,
        VHF20B,
        VHFUHFExpansion,
        XT6013,
        XT6313D,
    }

    public static class DefaultRadioInformation
    {
        private static readonly Dictionary<Radios, RadioValues> RadioDefaults = new Dictionary<Radios, RadioValues>()
        {
            // Currently asssuming 50ohms for all radios when converting from uV to dbm, in addition to links below https://dpdproductions.com/pages/military-radio-reference-list was used for reference
            // Power information was used from above source if only minimums given in specific documentation and above documentation provided a specific power (i.e not a range of powers) 
            {Radios.Unknown, new RadioValues(40) },
            {Radios.Intercom, new RadioValues(0) }, // Dummy value for intercoms
            {Radios.ANARC131, new RadioValues(40) }, // possible sensitivity info https://apps.dtic.mil/sti/pdfs/ADA033368.pdf
            {Radios.ANARC134, new RadioValues(46, -88) }, // https://www.liberatedmanuals.com/TM-11-5821-277-20.pdf
            {Radios.ANARC150, new RadioValues(40, -113) }, // https://en.wikipedia.org/wiki/AN/PRC-150
            {Radios.ANARC159, new RadioValues(40, -96)}, // https://www.columbiaelectronics.com/an_arc_159_v__uhf_transceiver.htm
            {Radios.ANARC164, new RadioValues(40, -91)}, // https://tsc-60.cellmail.com/tsc-60/TSC-118/rtn_ncs_products_arc164_pdf.pdf
            {Radios.ANARC182, new RadioValues(44, -110) }, // https://www.columbiaelectronics.com/an_arc_182_v__vhf_uhf_radio_set.htm, used AM parameters - modulation changes based on frequency with differing power outputs and sensitivity
            {Radios.ANARC186, new RadioValues(40, -97) }, // https://www.columbiaelectronics.com/an_arc_186_v__radio_set.htm TODO: create ANARC186FM due to tx power and rx sensitivity differences
            {Radios.ANARC210, RadioDefaults[Radios.Unknown] }, // http://static6.arrow.com/aropdfconversion/11eaff76e8fc9a992ea6cb5163f1efddd95392b2/arc-210integratedcommsystemswhitepaper.pdf.pdf, unable to find sensitivity information
            {Radios.ANARC222, RadioDefaults[Radios.Unknown] }, // Having trouble finding SINGCARS configuration details for F16 Blk. 50 in relation to sensitivity, default values are likely a close approximation
            {Radios.ANARC27,  new RadioValues(39)}, // https://web.archive.org/web/20100706140322/http://www.nj7p.org/cgi-bin/millist2?mode=normal&name=AN%2FARC-27
            {Radios.ANARC51BX, new RadioValues(44)}, // No (good) documentation found yet 
            {Radios.ARI1063, RadioDefaults[Radios.Unknown] }, // No documentation found yet
            {Radios.ARI232591, RadioDefaults[Radios.Unknown] }, // No documentation found yet
            {Radios.Baklan5, new RadioValues(37) }, // https://www.an2flyers.org/avionics.html, would prefer a better source
            {Radios.FR22, RadioDefaults[Radios.Unknown]}, // Info from DCS manual, power is 10w UHF or 20w VHF - using lower power for the time being
            {Radios.FR24, new RadioValues(35) }, // Info from DCS manual
            {Radios.FuG16ZY, new RadioValues(40, -77) }, // https://www.esr.se/images/articles-pyssel/TME_11-227-2.pdf, document notes rated Tx power of 10w, testing shows 5w. Using 10w value for now.
            {Radios.JADRO1A, new RadioValues(47, -93) }, // https://fccid.io/ANATEL/01011-13-08760/Manual-Radio-HF/2908D16B-1059-472A-922E-9C9947678865
            {Radios.JADRO1I, new RadioValues(47, -97) }, // https://fccid.io/ANATEL/01011-13-08760/Manual-Radio-HF/2908D16B-1059-472A-922E-9C9947678865
            {Radios.KTR953HF, new RadioValues(46, -97) }, // http://shop.avionics.co.nz/ktr953
            {Radios.KY197A, new RadioValues(40, -100) }, // https://www.seaerospace.com/sales/product/BendixKing/KY-197A
            {Radios.MIDS, RadioDefaults[Radios.ANARC210]}, // I assume MIDS communication will share the same values as the  AN/ARC-210
            {Radios.R1155, new RadioValues(24, -86)  }, // http://www.vq5x79.f2s.com/greenradio/Wireless21a.html, would prefer a better source
            {Radios.R800, new RadioValues(38)}, // http://www.radionic.ru/book/export/html/744, would prefer a better source
            {Radios.R800L14, RadioDefaults[Radios.Unknown]}, // No documentation found yet
            {Radios.R828, RadioDefaults[Radios.Unknown]}, // https://pandia.ru/text/80/087/37141.php, would prefer a better source
            {Radios.R832, RadioDefaults[Radios.R832]}, // Assume similar to R832M
            {Radios.R832M, new RadioValues(42)}, // https://www.armedconflicts.com/Aviacionnaya-UKV-DMV-radiostanciya-R-832M-t80222, would prefer a better source
            {Radios.R852, new RadioValues(40, -93)}, // https://lektsii.com/2-129490.html, would prefer a better source - assuming 10W Tx power
            {Radios.R862, new RadioValues(44, -97)}, // https://radiotract.ru/radiostancia-r-862m.html, would prefer a better source
            {Radios.R863, new RadioValues(40, -97) }, // https://radiotract.ru/radiostancia-r-863m.html, would prefer a better source
            {Radios.R864, new RadioValues(50, -93)}, // http://www.radioscanner.ru/trx/military/r-864/, would prefer a better source
            {Radios.RSI6K, new RadioValues(37)}, // https://archives.nato.int/uploads/r/null/1/1/116423/SG_253_1_FINAL_ENG_PDP.pdf, would prefer a better source
            {Radios.RSIU4V, new RadioValues(39) }, // https://www.armedconflicts.com/Aviacionnaya-UKV-radiostanciya-R-801-RSIU-4B-t80161, would prefer better source (note that RSIU4V == R801V)
            {Radios.RTA42ABENDIX, RadioDefaults[Radios.Unknown]}, // No documentation found yet
            {Radios.SCR522, RadioDefaults[Radios.SCR522A]}, // Assume values will be similar to SCR522A
            {Radios.SCR522A, new RadioValues(40, -94)}, // http://www.radiomanual.info/schemi/Surplus_NATO/SCR-522A_SCR-542A_serv_AN16-40SCR522-3_1952.pdf, other sources provide different transmit power values
            {Radios.SPU9SW, RadioDefaults[Radios.Unknown]}, // Fairly certain this is just an intercom?
            {Radios.SRT651N, new RadioValues(40, -103)}, // https://www.yumpu.com/en/document/read/41736902/v-uhf-airborne-radio-systems-srt-651-e-a-se-008-v2-12-
            {Radios.SUNAIRASB850, new RadioValues(46 ,-97)}, // https://www.sunairelectronics.com/workspace/uploads/asb-850_10-1-1981_2nd-1314220642.pdf
            {Radios.T1154, new RadioValues(46)}, // http://www.shopingathome.com/1154%20Transmitter.htm, would prefer a better source
            {Radios.TRAP138A, RadioDefaults[Radios.Unknown]}, // No documentation found yet
            {Radios.TRC9600PR4G, RadioDefaults[Radios.Unknown]}, // No documentation found yet
            {Radios.TRTERA7000, RadioDefaults[Radios.Unknown]}, // No documentation found yet
            {Radios.TRTERA7200, RadioDefaults[Radios.Unknown]}, // No documentation found yet
            {Radios.UHFTRA6031, RadioDefaults[Radios.Unknown]}, // No documentation found yet
            {Radios.VHF20B, new RadioValues(43) }, // https://www.seaerospace.com/sales/product/Collins%20Aerospace/VHF-20B
            {Radios.VHFUHFExpansion, RadioDefaults[Radios.Unknown]}, // Standard Expansion Radios
            {Radios.VTVU740, RadioDefaults[Radios.Unknown]}, // No documentation found yet
            {Radios.XT6013, RadioDefaults[Radios.Unknown]}, // No documentation found yet
            {Radios.XT6313D, RadioDefaults[Radios.Unknown]}, // No documentation found yet
        };

        private static readonly Dictionary<string, Radios[]> AircraftDefaults = new Dictionary<string, Radios[]>()
        {
            {"UH-1H", new [] { Radios.Intercom, Radios.ANARC131, Radios.ANARC51BX, Radios.ANARC134 } },
            {"Ka-50", new [] {Radios.R800L14, Radios.R828, Radios.SPU9SW } },
            {"Mi-8MT", new [] { Radios.Intercom, Radios.R863, Radios.JADRO1A, Radios.R828 } },
            {"Mi-24P", new [] { Radios.Intercom, Radios.R863, Radios.R828, Radios.JADRO1I, Radios.R852} },
            {"Yak-52", new [] { Radios.Intercom, Radios.Baklan5, Radios.ANARC186, Radios.ANARC164} },
            {"FA-18C_hornet", new [] { Radios.ANARC210, Radios.ANARC210, Radios.MIDS, Radios.MIDS} },
            {"F-86F Sabre", new [] { Radios.ANARC27, Radios.ANARC186, Radios.ANARC164 } },
            {"MiG-15bis", new [] {Radios.RSI6K, Radios.ANARC186, Radios.ANARC164 } },
            {"MiG-19P", new [] {Radios.RSIU4V, Radios.ANARC186, Radios.ANARC164 } },
            {"MiG-21Bis", new [] {Radios.R832, Radios.ANARC186, Radios.ANARC164 } },
            {"F-5E-3", new [] {Radios.ANARC164, Radios.ANARC186, Radios.ANARC164} },
            {"FW-190D9", new [] {Radios.FuG16ZY, Radios.ANARC186, Radios.ANARC164} },
            {"FW-190A8", AircraftDefaults["FW-190D9"] },
            {"Bf-109K-4", AircraftDefaults["FW-190D9"] },
            {"C-101EB", new [] { Radios.Intercom, Radios.ANARC164, Radios.ANARC134} },
            {"C-101CC", new [] { Radios.Intercom, Radios.VTVU740, Radios.VHF20B} },
            {"MB-339A", new [] { Radios.Intercom, Radios.ANARC150, Radios.SRT651N } },
            {"MB-339APAN", AircraftDefaults["MB-339A"] },
            {"Hawk", new [] { Radios.ANARC164, Radios.ARI232591} },
            {"Christen Eagle II", new [] { Radios.Intercom, Radios.KY197A, Radios.ANARC186, Radios.ANARC164 } },
            {"M-2000C", new []  {Radios.TRTERA7000, Radios.TRTERA7200 } },
            {"JF-17", new [] { Radios.Unknown, Radios.Unknown, Radios.VHFUHFExpansion} },
            {"AV8BNA", new [] { Radios.ANARC210, Radios.ANARC210, Radios.ANARC210 } },
            {"AJS37", new [] { Radios.FR22, Radios.FR24, Radios.ANARC164} },
            {"A-10A", new [] { Radios.ANARC186, Radios.ANARC164, Radios.ANARC186} },
            {"A-4E-C", new [] { Radios.ANARC51BX, Radios.ANARC186, Radios.ANARC186} }, //TODO: Radio 3 updated to 186FM when added
            {"PUCARA", new [] { Radios.Intercom, Radios.SUNAIRASB850, Radios.RTA42ABENDIX} },
            {"T-45", new [] {Radios.Intercom, Radios.ANARC182, Radios.ANARC182} },
            {"A-29B", new [] { Radios.XT6013, Radios.XT6313D, Radios.KTR953HF } },
            {"F-15C", new [] { Radios.ANARC164, Radios.ANARC164, Radios.ANARC186} },
            {"MiG-29A", new [] { Radios.R862, Radios.ANARC186, Radios.ANARC164} },
            {"MiG-29S", AircraftDefaults["MiG-29A"]},
            {"MiG-29G", AircraftDefaults["MiG-29A"]},
            {"Su-27", new [] {Radios.R800, Radios.R864, Radios.ANARC164 } },
            {"Su-33", AircraftDefaults["Su-27"] },
            {"Su-25", new [] { Radios.R862, Radios.R828, Radios.ANARC164 } },
            {"Su-25T", AircraftDefaults["Su-25"]},
            {"F-16C_50", new [] { Radios.ANARC164, Radios.ANARC222} },
            {"SA342M", new [] { Radios.Intercom, Radios.TRAP138A, Radios.UHFTRA6031, Radios.TRC9600PR4G} },
            {"SA342L", AircraftDefaults["SA342M"] },
            {"SA342Mistral", AircraftDefaults["SA342M"]},
            {"SA342Minigun", AircraftDefaults["SA342M"]},
            {"L-39C", new [] { Radios.Intercom, Radios.R832M, Radios.ANARC186, Radios.ANARC164} },
            {"L-39ZA", AircraftDefaults["L-39C"] },
            {"F-14B", new [] { Radios.Intercom, Radios.ANARC159, Radios.ANARC182 } },
            {"F-14A-135-GR", AircraftDefaults["F-14B"] },
            {"A-10C", new [] { Radios.ANARC186, Radios.ANARC164, Radios.ANARC186 } },
            {"A-10C_2", AircraftDefaults["A-10C_2"] },
            {"P-51D", new [] {Radios.SCR522A, Radios.ANARC186, Radios.ANARC164 } },
            {"P-51D-30-NA", AircraftDefaults["P-51D"] },
            {"TF-51D", AircraftDefaults["P-51D"]},
            {"P-47D-30", new [] {Radios.SCR522, Radios.ANARC186, Radios.ANARC164 } },
            {"P-47D-30bl1", AircraftDefaults["P-47D-30"] },
            {"P-47D-40", AircraftDefaults["P-47D-30"] },
            {"SpitfireLFMkIX", new [] {Radios.ARI1063, Radios.ANARC186, Radios.ANARC164 } },
            {"SpitfireLFMkIXCW", AircraftDefaults["SpitfireLFMkIX"] },
            {"MosquitoFBMkVI", new [] { Radios.Intercom, Radios.SCR522A, Radios.R1155, Radios.T1154, Radios.ANARC210} },
        };

        public static Dictionary<Radios, RadioValues> GetRadioDefaults()
        {
            return RadioDefaults;
        }

        public static Dictionary<string, Radios[]> GetAircraftDefaults()
        {
            return AircraftDefaults;
        }
    }
}
