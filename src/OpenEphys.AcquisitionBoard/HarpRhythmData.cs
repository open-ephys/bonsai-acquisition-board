using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using Bonsai;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Produces a sequence of <see cref="HarpRhythmDataFrame"/> objects, each containing a Harp clock
    /// synchronization signal and the corresponding Rhythm device sample count.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This data IO operator must be linked to an appropriate configuration, such as a <see
    /// cref="ConfigureHarpRhythm"/>, using a shared <c>DeviceName</c>.
    /// </para>
    /// <para>
    /// Harp devices periodically broadcast the current Harp time, in seconds, through their clock
    /// synchronization output. When such a device is connected to the acquisition board, a frame is
    /// produced for each received Harp second, which can be used to align data from the acquisition board
    /// with data from Harp devices. No data is produced if no Harp device is connected.
    /// </para>
    /// </remarks>
    [Description("Produces a sequence of HarpRhythmDataFrame objects, each containing a Harp clock synchronization signal and the corresponding Rhythm device sample count.")]
    public class HarpRhythmData : Source<HarpRhythmDataFrame>
    {
        /// <inheritdoc cref = "SingleDeviceFactory{TContext}.DeviceName"/>
        [TypeConverter(typeof(HarpRhythm.NameConverter))]
        [Description(DeviceFactoryStrings.DeviceNameDescription)]
        [Category(DeviceFactoryStrings.ConfigurationCategory)]
        public string DeviceName { get; set; }

        /// <summary>
        /// Generates a sequence of <see cref="HarpRhythmDataFrame"/> objects, each of which contains the
        /// Rhythm device sample count at the moment a Harp second was received.
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
