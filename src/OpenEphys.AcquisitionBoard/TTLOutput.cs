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
    [Description("Sends digital output data to the TTL output pins of the acquisition board.")]
    public class TTLOutput : Sink<TTLState>
    {

        /// <inheritdoc cref = "SingleDeviceFactory.DeviceName"/>
        [TypeConverter(typeof(TTLOut.NameConverter))]
        [Description(SingleDeviceFactory.DeviceNameDescription)]
        [Category(DeviceFactory.ConfigurationCategory)]
        public string DeviceName { get; set; }

        /// <summary>
        /// Updates the digital output port state.
        /// </summary>
        /// <param name="source"> A sequence of <see cref="TTLState"/> values indicating the state of the breakout board's 8 digital output pins</param>
        /// <returns> A sequence that is identical to <paramref name="source"/>.</returns>
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
