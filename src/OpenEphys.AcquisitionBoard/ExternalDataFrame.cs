using OpenCV.Net;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Buffered data from the analog and digital inputs of the acquisition board.
    /// </summary>
    [ExpectedSampleRate(30000)]
    public class ExternalDataFrame : BufferedDataFrame
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalDataFrame"/> class.
        /// </summary>
        /// <param name="clock">An array of <see cref="DataFrame.Clock"/> values.</param>
        /// <param name="hubClock">An array of hub clock counter values.</param>
        /// <param name="sampleCount">An array of Rhythm device sample counter values.</param>
        /// <param name="analogData">An array of multi-channel analog input data.</param>
        /// <param name="digitalIn">An array of digital input states.</param>
        public ExternalDataFrame(ulong[] clock, ulong[] hubClock, uint[] sampleCount, Mat analogData, TTLState[] digitalIn)
            : base(clock, hubClock)
        {
            AnalogData = analogData;
            DigitalIn = digitalIn;
            SampleCount = sampleCount;
        }

        /// <summary>
        /// Gets the buffered analog input data array.
        /// </summary>
        /// <remarks>
        /// Each of the 8 rows corresponds to an analog input channel of the acquisition board and each column
        /// to a sample. Samples are represented as raw, unsigned 16-bit ADC values.
        /// </remarks>
        public Mat AnalogData { get; }

        /// <summary>
        /// Gets the buffered array of digital input states.
        /// </summary>
        /// <remarks>
        /// Each element holds the state of the digital input pins of the acquisition board at a given
        /// sample.
        /// </remarks>
        public TTLState[] DigitalIn { get; }

        /// <summary>
        /// Gets the buffered array of Rhythm device sample counter values.
        /// </summary>
        /// <remarks>
        /// This counter can be used to align these data with those from other Rhythm device data sources,
        /// such as <see cref="HeadstageDataFrame"/> or <see cref="HarpRhythmDataFrame"/>.
        /// </remarks>
        public uint[] SampleCount { get; }
    }
}
