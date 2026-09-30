using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using Bonsai;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Produces a sequence of Harp clock synchronization signals
    /// produced alongside the acquisition data from the Rhythm device
    /// </summary>
    /// <remarks>
    /// Only produces data when a Harp device is connected to the Acquisition Board.
    /// </remarks>
    [Description("Produces a sequence of Harp clock synchronization signals produced" +
        " alongside the acquisition data from the Rhythm device only when a Harp device is connected" +
        " to the Acquisition Board")]
    public class HarpRhythmData : Source<HarpRhythmDataFrame>
    {
        /// <inheritdoc cref = "SingleDeviceFactory.DeviceName"/>
        [TypeConverter(typeof(HarpRhythm.NameConverter))]
        [Description(SingleDeviceFactory.DeviceNameDescription)]
        [Category(DeviceFactory.ConfigurationCategory)]
        public string DeviceName { get; set; }

        /// <summary>
        /// Generates a sequence of <see cref="HarpRhythmDataFrame"/> objects containing the
        /// Harp clock synchronization signals produced alongside the Rhythm device
        /// sample counter on the moment of each Harp second
        /// </summary>
        /// <returns>A sequence of <see cref="HarpRhythmDataFrame"/> objects.</returns>
        public override IObservable<HarpRhythmDataFrame> Generate()
        {
            return DeviceManager.GetDevice(DeviceName).SelectMany(deviceInfo =>
            {
                var device = deviceInfo.GetDeviceContext(typeof(HarpRhythm));
                return deviceInfo.Context
                    .GetDeviceFrames(device.Address)
                    .Select(frame => new HarpRhythmDataFrame(frame));
            });
        }
    }
}
