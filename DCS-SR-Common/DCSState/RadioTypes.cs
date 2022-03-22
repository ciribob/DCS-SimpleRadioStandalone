using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState
{
    // TODO: the enums aren't really necessary and can be replaced by strings
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
        ANARC201D,
        ANARC210,
        ANARC220,
        ANARC222,
        ANARC27,
        ANARC51BX,
        ARI232591,
        Baklan5,
        FR22,
        FR24,
        FuG16ZY,
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
        public static readonly Dictionary<string, RadioValues> RadioDefaults = new Dictionary<string, RadioValues>()
        {
            // Currently asssuming 50ohms for all radios when converting from uV to dbm, in addition to links below https://dpdproductions.com/pages/military-radio-reference-list was used for reference
            // Power information was used from above source if only minimums given in specific documentation and above documentation provided a specific power (i.e not a range of powers) 
            {Radios.Unknown.ToString(), new RadioValues(40) },
            {Radios.Intercom.ToString(), new RadioValues(0) }, // Dummy value for intercoms
            {Radios.ANARC131.ToString(), new RadioValues(40) }, // possible sensitivity info https://apps.dtic.mil/sti/pdfs/ADA033368.pdf
            {Radios.ANARC134.ToString(), new RadioValues(46, -88) }, // https://www.liberatedmanuals.com/TM-11-5821-277-20.pdf
            {Radios.ANARC150.ToString(), new RadioValues(40) }, // Proving difficult to find information for - produced by MagnaVox
            {Radios.ANARC159.ToString(), new RadioValues(40, -96)}, // https://www.columbiaelectronics.com/an_arc_159_v__uhf_transceiver.htm
            {Radios.ANARC164.ToString(), new RadioValues(40, -91)}, // https://tsc-60.cellmail.com/tsc-60/TSC-118/rtn_ncs_products_arc164_pdf.pdf
            {Radios.ANARC182.ToString(), new RadioValues(44, -110) }, // https://www.columbiaelectronics.com/an_arc_182_v__vhf_uhf_radio_set.htm, used AM parameters - modulation changes based on frequency with differing power outputs and sensitivity
            {Radios.ANARC186.ToString(), new RadioValues(40, -97) }, // https://www.columbiaelectronics.com/an_arc_186_v__radio_set.htm TODO: create ANARC186FM due to tx power and rx sensitivity differences
            {Radios.ANARC201D.ToString(), new RadioValues(40) }, // https://www.l3harris.com/sites/default/files/2020-11/cs-tcom-an-arc-201d-sincgars-airborne-radio-datasheet.pdf
            {Radios.ANARC210.ToString(), new RadioValues(40) }, // http://static6.arrow.com/aropdfconversion/11eaff76e8fc9a992ea6cb5163f1efddd95392b2/arc-210integratedcommsystemswhitepaper.pdf.pdf, unable to find sensitivity information
            {Radios.ANARC220.ToString(), new RadioValues(50) }, // https://dpdproductions.com/pages/military-radio-reference-list
            {Radios.ANARC222.ToString(), new RadioValues(40) }, // Having trouble finding SINGCARS configuration details for F16 Blk. 50 in relation to sensitivity, default values are likely a close approximation
            {Radios.ANARC27.ToString(),  new RadioValues(39)}, // https://web.archive.org/web/20100706140322/http://www.nj7p.org/cgi-bin/millist2?mode=normal&name=AN%2FARC-27
            {Radios.ANARC51BX.ToString(), new RadioValues(44)}, // No (good) documentation found yet 
            {Radios.ARI1063.ToString(), new RadioValues(40) }, // No documentation found yet
            {Radios.ARI232591.ToString(), new RadioValues(40) }, // No documentation found yet
            {Radios.Baklan5.ToString(), new RadioValues(37) }, // https://www.an2flyers.org/avionics.html, would prefer a better source
            {Radios.FR22.ToString(), new RadioValues(40)}, // Info from DCS manual, power is 10w UHF or 20w VHF - using lower power for the time being
            {Radios.FR24.ToString(), new RadioValues(35) }, // Info from DCS manual
            {Radios.FuG16ZY.ToString(), new RadioValues(40, -77) }, // https://www.esr.se/images/articles-pyssel/TME_11-227-2.pdf, document notes rated Tx power of 10w, testing shows 5w. Using 10w value for now.
            {Radios.JADRO1A.ToString(), new RadioValues(47, -93) }, // https://fccid.io/ANATEL/01011-13-08760/Manual-Radio-HF/2908D16B-1059-472A-922E-9C9947678865
            {Radios.JADRO1I.ToString(), new RadioValues(47, -97) }, // https://fccid.io/ANATEL/01011-13-08760/Manual-Radio-HF/2908D16B-1059-472A-922E-9C9947678865
            {Radios.KTR953HF.ToString(), new RadioValues(46, -97) }, // http://shop.avionics.co.nz/ktr953
            {Radios.KY197A.ToString(), new RadioValues(40, -100) }, // https://www.seaerospace.com/sales/product/BendixKing/KY-197A
            {Radios.MIDS.ToString(), new RadioValues(40)}, // I assume MIDS communication will share the same values as the  AN/ARC-210
            {Radios.R1155.ToString(), new RadioValues(24, -86)  }, // http://www.vq5x79.f2s.com/greenradio/Wireless21a.html, would prefer a better source
            {Radios.R800.ToString(), new RadioValues(38)}, // http://www.radionic.ru/book/export/html/744, would prefer a better source
            {Radios.R800L14.ToString(), new RadioValues(40)}, // No documentation found yet
            {Radios.R828.ToString(), new RadioValues(40)}, // https://pandia.ru/text/80/087/37141.php, would prefer a better source
            {Radios.R832.ToString(), new RadioValues(42)}, // Assume similar to R832M
            {Radios.R832M.ToString(), new RadioValues(42)}, // https://www.armedconflicts.com/Aviacionnaya-UKV-DMV-radiostanciya-R-832M-t80222, would prefer a better source
            {Radios.R852.ToString(), new RadioValues(40, -93)}, // https://lektsii.com/2-129490.html, would prefer a better source - assuming 10W Tx power
            {Radios.R862.ToString(), new RadioValues(44, -97)}, // https://radiotract.ru/radiostancia-r-862m.html, would prefer a better source
            {Radios.R863.ToString(), new RadioValues(40, -97) }, // https://radiotract.ru/radiostancia-r-863m.html, would prefer a better source
            {Radios.R864.ToString(), new RadioValues(50, -93)}, // http://www.radioscanner.ru/trx/military/r-864/, would prefer a better source
            {Radios.RSI6K.ToString(), new RadioValues(37)}, // https://archives.nato.int/uploads/r/null/1/1/116423/SG_253_1_FINAL_ENG_PDP.pdf, would prefer a better source
            {Radios.RSIU4V.ToString(), new RadioValues(39) }, // https://www.armedconflicts.com/Aviacionnaya-UKV-radiostanciya-R-801-RSIU-4B-t80161, would prefer better source (note that RSIU4V == R801V)
            {Radios.RTA42ABENDIX.ToString(), new RadioValues(40)}, // No documentation found yet
            {Radios.SCR522.ToString(), new RadioValues(40, -94)}, // Assume values will be similar to SCR522A
            {Radios.SCR522A.ToString(), new RadioValues(40, -94)}, // http://www.radiomanual.info/schemi/Surplus_NATO/SCR-522A_SCR-542A_serv_AN16-40SCR522-3_1952.pdf, other sources provide different transmit power values
            {Radios.SPU9SW.ToString(), new RadioValues(40)}, // Fairly certain this is just an intercom?
            {Radios.SRT651N.ToString(), new RadioValues(40, -103)}, // https://www.yumpu.com/en/document/read/41736902/v-uhf-airborne-radio-systems-srt-651-e-a-se-008-v2-12-
            {Radios.SUNAIRASB850.ToString(), new RadioValues(46 ,-97)}, // https://www.sunairelectronics.com/workspace/uploads/asb-850_10-1-1981_2nd-1314220642.pdf
            {Radios.T1154.ToString(), new RadioValues(46)}, // http://www.shopingathome.com/1154%20Transmitter.htm, would prefer a better source
            {Radios.TRAP138A.ToString(), new RadioValues(40)}, // No documentation found yet
            {Radios.TRC9600PR4G.ToString(), new RadioValues(40)}, // No documentation found yet
            {Radios.TRTERA7000.ToString(), new RadioValues(40)}, // No documentation found yet
            {Radios.TRTERA7200.ToString(), new RadioValues(40)}, // No documentation found yet
            {Radios.UHFTRA6031.ToString(), new RadioValues(40)}, // No documentation found yet
            {Radios.VHF20B.ToString(), new RadioValues(43) }, // https://www.seaerospace.com/sales/product/Collins%20Aerospace/VHF-20B
            {Radios.VHFUHFExpansion.ToString(), new RadioValues(40)}, // Standard Expansion Radios
            {Radios.VTVU740.ToString(), new RadioValues(40)}, // No documentation found yet
            {Radios.XT6013.ToString(), new RadioValues(40)}, // No documentation found yet
            {Radios.XT6313D.ToString(), new RadioValues(40)}, // No documentation found yet
        };

        public static readonly Dictionary<string, string[]> AircraftDefaults = new Dictionary<string, string[]>()
        {
            {"External AWACS", new [] { Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.Unknown.ToString(),
                                       Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.Unknown.ToString()} },
            {"CA", new [] { Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.Unknown.ToString(),
                                       Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.Unknown.ToString()} },
            {"AH-64D_BLK_II", new []  { Radios.Intercom.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString(), Radios.ANARC201D.ToString()} }
            {"UH-1H", new [] { Radios.Intercom.ToString(), Radios.ANARC131.ToString(), Radios.ANARC51BX.ToString(), Radios.ANARC134.ToString() } },
            {"Ka-50", new [] {Radios.R800L14.ToString(), Radios.R828.ToString(), Radios.SPU9SW.ToString() } },
            {"Mi-8MT", new [] { Radios.Intercom.ToString(), Radios.R863.ToString(), Radios.JADRO1A.ToString(), Radios.R828.ToString() } },
            {"Mi-24P", new [] { Radios.Intercom.ToString(), Radios.R863.ToString(), Radios.R828.ToString(), Radios.JADRO1I.ToString(), Radios.R852.ToString()} },
            {"Yak-52", new [] { Radios.Intercom.ToString(), Radios.Baklan5.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString()} },
            {"FA-18C_hornet", new [] { Radios.ANARC210.ToString(), Radios.ANARC210.ToString(), Radios.MIDS.ToString(), Radios.MIDS.ToString()} },
            {"F-86F Sabre", new [] { Radios.ANARC27.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() } },
            {"MiG-15bis", new [] {Radios.RSI6K.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() } },
            {"MiG-19P", new [] {Radios.RSIU4V.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() } },
            {"MiG-21Bis", new [] {Radios.R832.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() } },
            {"F-5E-3", new [] {Radios.ANARC164.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString()} },
            {"FW-190D9", new [] {Radios.FuG16ZY.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString()} },
            {"FW-190A8", new [] {Radios.FuG16ZY.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString()} },
            {"Bf-109K-4", new [] {Radios.FuG16ZY.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString()} },
            {"C-101EB", new [] { Radios.Intercom.ToString(), Radios.ANARC164.ToString(), Radios.ANARC134.ToString()} },
            {"C-101CC", new [] { Radios.Intercom.ToString(), Radios.VTVU740.ToString(), Radios.VHF20B.ToString()} },
            {"MB-339A", new [] { Radios.Intercom.ToString(), Radios.ANARC150.ToString(), Radios.SRT651N.ToString() } },
            {"MB-339APAN", new [] { Radios.Intercom.ToString(), Radios.ANARC150.ToString(), Radios.SRT651N.ToString() } },
            {"Hawk", new [] { Radios.ANARC164.ToString(), Radios.ARI232591.ToString()} },
            {"Christen Eagle II", new [] { Radios.Intercom.ToString(), Radios.KY197A.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() } },
            {"M-2000C", new []  {Radios.TRTERA7000.ToString(), Radios.TRTERA7200.ToString() } },
            {"JF-17", new [] { Radios.Unknown.ToString(), Radios.Unknown.ToString(), Radios.VHFUHFExpansion.ToString()} },
            {"AV8BNA", new [] { Radios.ANARC210.ToString(), Radios.ANARC210.ToString(), Radios.ANARC210.ToString() } },
            {"AJS37", new [] { Radios.FR22.ToString(), Radios.FR24.ToString(), Radios.ANARC164.ToString()} },
            {"A-10A", new [] { Radios.ANARC186.ToString(), Radios.ANARC164.ToString(), Radios.ANARC186.ToString()} },
            {"A-4E-C", new [] { Radios.ANARC51BX.ToString(), Radios.ANARC186.ToString(), Radios.ANARC186.ToString()} }, //TODO: Radio 3 updated to 186FM when added
            {"PUCARA", new [] { Radios.Intercom.ToString(), Radios.SUNAIRASB850.ToString(), Radios.RTA42ABENDIX.ToString()} },
            {"T-45", new [] {Radios.Intercom.ToString(), Radios.ANARC182.ToString(), Radios.ANARC182.ToString()} },
            {"A-29B", new [] { Radios.XT6013.ToString(), Radios.XT6313D.ToString(), Radios.KTR953HF.ToString() } },
            {"F-15C", new [] { Radios.ANARC164.ToString(), Radios.ANARC164.ToString(), Radios.ANARC186.ToString()} },
            {"MiG-29A", new [] { Radios.R862.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString()} },
            {"MiG-29S", new [] { Radios.R862.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString()} },
            {"MiG-29G", new [] { Radios.R862.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString()} },
            {"Su-27", new [] {Radios.R800.ToString(), Radios.R864.ToString(), Radios.ANARC164.ToString() } },
            {"Su-33", new [] {Radios.R800.ToString(), Radios.R864.ToString(), Radios.ANARC164.ToString() } },
            {"Su-25", new [] { Radios.R862.ToString(), Radios.R828.ToString(), Radios.ANARC164.ToString() } },
            {"Su-25T", new [] { Radios.R862.ToString(), Radios.R828.ToString(), Radios.ANARC164.ToString() } },
            {"F-16C_50", new [] { Radios.ANARC164.ToString(), Radios.ANARC222.ToString()} },
            {"SA342M", new [] { Radios.Intercom.ToString(), Radios.TRAP138A.ToString(), Radios.UHFTRA6031.ToString(), Radios.TRC9600PR4G.ToString()} },
            {"SA342L", new [] { Radios.Intercom.ToString(), Radios.TRAP138A.ToString(), Radios.UHFTRA6031.ToString(), Radios.TRC9600PR4G.ToString()} },
            {"SA342Mistral", new [] { Radios.Intercom.ToString(), Radios.TRAP138A.ToString(), Radios.UHFTRA6031.ToString(), Radios.TRC9600PR4G.ToString()}},
            {"SA342Minigun", new [] { Radios.Intercom.ToString(), Radios.TRAP138A.ToString(), Radios.UHFTRA6031.ToString(), Radios.TRC9600PR4G.ToString()}},
            {"L-39C", new [] { Radios.Intercom.ToString(), Radios.R832M.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString()} },
            {"L-39ZA", new [] { Radios.Intercom.ToString(), Radios.R832M.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString()} },
            {"F-14B", new [] { Radios.Intercom.ToString(), Radios.ANARC159.ToString(), Radios.ANARC182.ToString() } },
            {"F-14A-135-GR", new [] { Radios.Intercom.ToString(), Radios.ANARC159.ToString(), Radios.ANARC182.ToString() }  },
            {"A-10C", new [] { Radios.ANARC186.ToString(), Radios.ANARC164.ToString(), Radios.ANARC186.ToString() } },
            {"A-10C_2", new [] { Radios.ANARC186.ToString(), Radios.ANARC164.ToString(), Radios.ANARC186.ToString() } },
            {"P-51D", new [] {Radios.SCR522A.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() } },
            {"P-51D-30-NA", new [] {Radios.SCR522A.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() } },
            {"TF-51D", new [] {Radios.SCR522A.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() }},
            {"P-47D-30", new [] {Radios.SCR522.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() } },
            {"P-47D-30bl1", new [] {Radios.SCR522.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() } },
            {"P-47D-40", new [] {Radios.SCR522.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() } },
            {"SpitfireLFMkIX", new [] {Radios.ARI1063.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() } },
            {"SpitfireLFMkIXCW", new [] {Radios.ARI1063.ToString(), Radios.ANARC186.ToString(), Radios.ANARC164.ToString() }  },
            {"MosquitoFBMkVI", new [] { Radios.Intercom.ToString(), Radios.SCR522A.ToString(), Radios.R1155.ToString(), Radios.T1154.ToString(), Radios.ANARC210.ToString()} },
        };

        public static Dictionary<string, RadioValues> GetRadioDefaults()
        {
            return RadioDefaults;
        }

        public static Dictionary<string, string[]> GetAircraftDefaults()
        {
            return AircraftDefaults;
        }
    }
}
