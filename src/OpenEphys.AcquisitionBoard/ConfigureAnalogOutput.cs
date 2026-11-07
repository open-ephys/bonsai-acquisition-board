using System;
using System.ComponentModel;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Configures the analog output device of the acquisition board.
    /// </summary>
    /// <remarks>
    /// This configuration operator can be linked to a data IO operator, such as <see cref="AnalogOutput"/>,
    /// using a shared <c>DeviceName</c>.
    /// </remarks>
    [Description("Configures the analog output device of the acquisition board.")]
    public class ConfigureAnalogOutput : SingleDeviceFactory<AcquisitionBoardContextTask>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureAnalogOutput"/> class.
        /// </summary>
        public ConfigureAnalogOutput()
            : base(typeof(AnalogOut))
        {
        }

        /// <summary>
        /// Configures the analog output device.
        /// </summary>
        /// <remarks>
        /// This will schedule configuration actions to be applied by a <see cref="StartAcquisition"/> instance
        /// prior to data acquisition.
        /// </remarks>
        /// <param name="source">A sequence of <see cref="AcquisitionBoardContextTask"/> instances that holds configuration actions.</param>
        /// <returns>The original sequence modified by adding additional configuration actions required to configure the analog output device.</returns>
        public override IObservable<AcquisitionBoardContextTask> Process(IObservable<AcquisitionBoardContextTask> source)
        {
            var deviceName = DeviceName;
            var deviceAddress = DeviceAddress;
            return source.ConfigureAndLatchDevice(context =>
            {
                var device = context.GetDeviceContext(deviceAddress, DeviceType);
                return DeviceManager.RegisterDevice(deviceName, device, DeviceType);
            });
        }
    }

    static class AnalogOut
    {
        public const int ID = 0x10003;
        public const uint MinimumVersion = 1;

        public const uint NULLPARAM = 0;

        public const int ChannelCount = 16;

        public const ushort DacMidScale = 32768;

        public const double VoltsPerDivision = 5.0 / DacMidScale;

        internal class NameConverter : DeviceNameConverter
        {
            public NameConverter()
                : base(typeof(AnalogOut)) { }
        }
    }

    /// <summary>
    /// Specifies the analog sample representation.
    /// </summary>
    public enum AnalogIODataType
    {
        /// <summary>
        /// Unsigned 16-bit integer 
        /// </summary>
        U16,
        /// <summary>
        /// Twos-complement encoded signed 16-bit integer
        /// </summary>
        S16,
        /// <summary>
        /// 32-bit floating point voltage.
        /// </summary>
        Volts
    }
}
