using System.Runtime.InteropServices;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Contains the rhythm frame counter whenever a Harp second counter
    /// is produced
    /// </summary>
    [ExpectedSampleRate(1)]
    public class HarpRhythmDataFrame : DataFrame
    {
        public unsafe HarpRhythmDataFrame(oni.Frame frame)
            : base(frame.Clock)
        {
            var payload = (RhythmHarpPayload*)frame.Data.ToPointer();
            HubClock = payload->HubClock;
            RhythmSampleCount = payload->RhythmSampleCount;
            HarpSecondCounter = payload->HarpSecondCounter;
        }

        /// <summary>
        /// Gets the Rhythm sample count corresponding to the Harp second counter.
        /// </summary>
        public uint RhythmSampleCount { get; }

        /// <summary>
        /// Gets the produced Harp second counter
        /// </summary>
        public uint HarpSecondCounter { get; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct RhythmHarpPayload
    {
        public ulong HubClock;
        public uint RhythmSampleCount;
        public uint HarpSecondCounter;
    }
}
