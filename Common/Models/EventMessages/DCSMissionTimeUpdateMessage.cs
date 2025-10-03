using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages
{
    public class DCSMissionTimeUpdateMessage
    {
        public DateTime CurrentMissionTime { get; }
        public DateTime StartMissionTime { get; }

        public DCSMissionTimeUpdateMessage(DateTime currentMissionTime, DateTime startMissionTime)
        {
            CurrentMissionTime = currentMissionTime;
            StartMissionTime = startMissionTime;
        }
    }
}