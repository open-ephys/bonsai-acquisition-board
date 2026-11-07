using System.Runtime.InteropServices;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// A Harp clock synchronization signal and the corresponding Rhythm device sample count.
    /// </summary>
    [ExpectedSampleRate(1)]
    public class HarpRhythmDataFrame : DataFrame
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HarpRhythmDataFrame"/> class.
        /// </summary>
        /// <param name="frame">
        /// A frame produced by the Harp synchronization device of an acquisition board.
        /// </param>
        public unsafe HarpRhythmDataFrame(oni.Frame frame)
            : base(frame.Clock)
        {
            var payload = (RhythmHarpPayload*)frame.Data.ToPointer();
            HubClock = payload->HubClock;
            RhythmSampleCount = payload->RhythmSampleCount;
            HarpSecondCounter = payload->HarpSecondCounter;
        }

        /// <summary>
        /// Gets the Rhythm device sample count at the moment <see cref="HarpSecondCounter"/> was received.
        /// </summary>
        /// <remarks>
        /// This value can be matched against the <c>SampleCount</c> of a <see cref="HeadstageDataFrame"/> or
        /// <see cref="ExternalDataFrame"/> to align acquisition board data with Harp time.
        /// </remarks>
        public uint RhythmSampleCount { get; }

        /// <summary>
        /// Gets the Harp time, in whole seconds, received from the Harp clock synchronization signal.
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
