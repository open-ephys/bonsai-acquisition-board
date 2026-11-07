using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using Bonsai;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Sends digital output data to the TTL output pins of the acquisition board.
    /// </summary>
    /// <remarks>
    /// This data IO operator must be linked to an appropriate configuration, such as a <see
    /// cref="ConfigureTTLOut"/>, using a shared <c>DeviceName</c>. Whether the outputs are updated
    /// immediately or synchronized with the acquisition board sample clock is determined by <see
    /// cref="ConfigureTTLOut.ImmediateMode"/>.
    /// </remarks>
    [Description("Sends digital output data to the TTL output pins of the acquisition board.")]
    public class TTLOutput : Sink<TTLState>
    {
        /// <inheritdoc cref = "SingleDeviceFactory{TContext}.DeviceName"/>
        [TypeConverter(typeof(TTLOut.NameConverter))]
        [Description(DeviceFactoryStrings.DeviceNameDescription)]
        [Category(DeviceFactoryStrings.ConfigurationCategory)]
        public string DeviceName { get; set; }

        /// <summary>
        /// Updates the state of the TTL output pins.
        /// </summary>
        /// <param name="source">A sequence of <see cref="TTLState"/> values indicating the state of the
        /// acquisition board's TTL output pins.</param>
        /// <returns>A sequence that is identical to <paramref name="source"/>.</returns>
        public override IObservable<TTLState> Process(IObservable<TTLState> source)
        {
            return DeviceManager.GetDevice(DeviceName).SelectMany(deviceInfo =>
            {
                var device = deviceInfo.GetDeviceContext(typeof(TTLOut));
                return source.Do(value => device.Write((uint)value));
            });
        }
    }
}
