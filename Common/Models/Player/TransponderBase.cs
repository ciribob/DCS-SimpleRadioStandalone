using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

public class TransponderBase
{
    public enum IFFStatus
    {
        OFF = 0,
        NORMAL = 1,
        IDENT = 2
    }

    /**
     * *  -- IFF_STATUS:  OFF = 0,  NORMAL = 1 , or IDENT = 2 (IDENT means Blink on ATCAWACS)
     * -- M1:-1 = off, any other number on
     * -- M3: -1 = OFF, any other number on
     * -- M4: 1 = ON or 0 = OFF
     * -- IFF STATUS{"control":1,"expansion":false,"mode1":51,"mode3":7700,"mode4":1,"status":2}
     */
    public int Mode1 { get; set; } = -1;

    public int Mode3 { get; set; } = -1;
    public bool Mode4 { get; set; }

    public IFFStatus Status { get; set; } = IFFStatus.OFF;

    protected bool Equals(TransponderBase other)
    {
        return Mode1 == other.Mode1 && Mode3 == other.Mode3 && Mode4 == other.Mode4 && Status == other.Status;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((TransponderBase)obj);
    }

    public override int GetHashCode() => HashCode.Combine(Mode1, Mode3, Mode4, Status);

    public TransponderBase DeepClone()
    {
        return new TransponderBase
        {
            Mode1 = Mode1,
            Mode3 = Mode3,
            Mode4 = Mode4,
            Status = Status
        };
    }
}