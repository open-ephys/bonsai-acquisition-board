using System.Runtime.InteropServices;
using OpenCV.Net;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Buffered data from the amplifier chips connected to the acquisition board headstage ports.
    /// </summary>
    [ExpectedSampleRate(30000)]
    public class HeadstageDataFrame : BufferedDataFrame
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HeadstageDataFrame"/> class.
        /// </summary>
        /// <param name="clock">An array of <see cref="DataFrame.Clock"/> values.</param>
        /// <param name="hubClock">An array of hub clock counter values.</param>
        /// <param name="sampleCount">An array of Rhythm device sample counter values.</param>
        /// <param name="amplifierData">An array of multi-channel amplifier data.</param>
        /// <param name="auxData">An array of multi-channel auxiliary data.</param>
        public HeadstageDataFrame(ulong[] clock, ulong[] hubClock, uint[] sampleCount, Mat amplifierData, Mat auxData)
            : base(clock, hubClock)
        {
            AmplifierData = amplifierData;
            AuxData = auxData;
            SampleCount = sampleCount;
        }

        /// <summary>
        /// Gets the buffered amplifier data array.
        /// </summary>
        /// <remarks>
        /// Each row corresponds to an amplifier channel and each column to a sample. Channels of all selected
        /// chips are concatenated in port order. Samples are represented as unsigned, offset binary encoded
        /// 16-bit integers.
        /// </remarks>
        public Mat AmplifierData { get; }

        /// <summary>
        /// Gets the buffered auxiliary data array.
        /// </summary>
        /// <remarks>
        /// Each selected chip contributes three rows, one per auxiliary input, and each column corresponds to
        /// a sample. Auxiliary inputs are sampled at one quarter of the amplifier sample rate, so this array
        /// has one quarter of the columns of <see cref="AmplifierData"/>.
        /// </remarks>
        public Mat AuxData { get; }

        /// <summary>
        /// Gets the buffered array of Rhythm device sample counter values.
        /// </summary>
        /// <remarks>
        /// This counter can be used to align these data with those from other Rhythm device data sources,
        /// such as <see cref="ExternalDataFrame"/> or <see cref="HarpRhythmDataFrame"/>.
        /// </remarks>
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
