using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCV.Net;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Represents a data frame produced by an acquisition board external data collection.
    /// </summary>
    public class ExternalDataFrame : BufferedDataFrame
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalDataFrame"/> class.
        /// </summary>
        /// <param name="clock">The clock values for the data frame.</param>
        /// <param name="hubClock">The hub clock values for the data frame.</param>
        /// <param name="sampleCount">The frame count values for the data frame.</param>
        /// <param name="analogData">The analog data matrix.</param>
        /// <param name="digitalIn">The digital input state array for the data frame.</param>
        public ExternalDataFrame(ulong[] clock, ulong[] hubClock, uint[] sampleCount, Mat analogData, TTLState[] digitalIn)
            : base(clock, hubClock)
        {
            AnalogData = analogData;
            DigitalIn = digitalIn;
            SampleCount = sampleCount;
        }

        /// <summary>
        /// Gets the analog data matrix for the data frame.
        /// </summary>
        public Mat AnalogData { get; }

        /// <summary>
        /// Gets the digital input state array for the data frame.
        /// </summary>
        public TTLState[] DigitalIn { get; }

        /// <summary>
        /// Gets the sample count values for the data frame.
        /// </summary>
        public uint[] SampleCount { get; }
    }
}
