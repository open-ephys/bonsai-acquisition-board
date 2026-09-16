using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCV.Net;
using OpenEphys.Onix1;
using System.Runtime.InteropServices;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Represents a data frame produced by an acquisition board headstage collection.
    /// </summary>
    public class HeadstageDataFrame : BufferedDataFrame
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HeadstageDataFrame"/> class.
        /// </summary>
        /// <param name="clock">The clock values for the data frame.</param>
        /// <param name="hubClock">The hub clock values for the data frame.</param>
        /// <param name="sampleCount">The frame count values for the data frame.</param>
        /// <param name="amplifierData">The amplifier data matrix.</param>
        /// <param name="auxData">The auxiliary data matrix.</param>
        public HeadstageDataFrame(ulong[] clock, ulong[] hubClock, uint[] sampleCount, Mat amplifierData, Mat auxData)
            : base(clock, hubClock)
        {
            AmplifierData = amplifierData;
            AuxData = auxData;
            SampleCount = sampleCount;
        }
        /// <summary>
        /// Gets the amplifier data matrix for the data frame.
        /// </summary>
        public Mat AmplifierData { get; }
        /// <summary>
        /// Gets the auxiliary data matrix for the data frame.
        /// </summary>
        public Mat AuxData { get; }

        /// <summary>
        /// Gets the sample count values for the data frame.
        /// </summary>
        public uint[] SampleCount { get; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct RhythmPayload
    {
        public ulong HubClock;
        public ulong Magic;
        public uint SampleCount;

        // NB : Actual data sits beyond this, with variable size
    }
}
