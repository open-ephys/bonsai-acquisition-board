using System;
using System.ComponentModel;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Configures the TTL output device of the acquisition board.
    /// </summary>
    /// <remarks>
    /// This configuration operator can be linked to a data IO operator, such as <see cref="TTLOutput"/>,
    /// using a shared <c>DeviceName</c>.
    /// </remarks>
    [Description("Configures the TTL output device of the acquisition board.")]
    public class ConfigureTTLOut : SingleDeviceFactory<AcquisitionBoardContextTask>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureTTLOut"/> class.
        /// </summary>
        public ConfigureTTLOut()
            : base(typeof(TTLOut))
        {
        }

        /// <summary>
        /// Gets or sets a value specifying whether TTL outputs are updated immediately.
        /// </summary>
        /// <remarks>
        /// If set to true, the TTL output pins are updated as soon as a new value is received. If set to
        /// false, updates are synchronized with the acquisition board sample clock.
        /// </remarks>
        [Category(ConfigurationCategory)]
        [Description("Specifies whether the TTL output is set immediately or synchronized with the acquisition board sample rate.")]
        public bool ImmediateMode { get; set; } = false;

        /// <summary>
        /// Configures the TTL output device.
        /// </summary>
        /// <remarks>
        /// This will schedule configuration actions to be applied by a <see cref="StartAcquisition"/> instance
        /// prior to data acquisition.
        /// </remarks>
        /// <param name="source">A sequence of <see cref="AcquisitionBoardContextTask"/> instances that holds configuration actions.</param>
        /// <returns>The original sequence modified by adding additional configuration actions required to configure the TTL output device.</returns>
        public override IObservable<AcquisitionBoardContextTask> Process(IObservable<AcquisitionBoardContextTask> source)
        {
            var immediateMode = ImmediateMode;
            var deviceName = DeviceName;
            var deviceAddress = DeviceAddress;
            return source.ConfigureAndLatchDevice(context =>
            {
                var device = context.GetDeviceContext(deviceAddress, DeviceType);
                device.WriteRegister(TTLOut.MODE, immediateMode ? 1u : 0u);
                return DeviceManager.RegisterDevice(deviceName, device, DeviceType);
            });
        }
    }

    static class TTLOut
    {
        public const int ID = 0x10002;
        public const uint MinimumVersion = 1;

        public const uint NULLPARAM = 0;

        public const uint MODE = 1;

        internal class NameConverter : DeviceNameConverter
        {
            public NameConverter()
                : base(typeof(TTLOut)) { }
        }
    }

    /// <summary>
    /// Specifies the state of the acquisition board's digital (TTL) input or output pins.
    /// </summary>
    [Flags]
    public enum TTLState : ushort
    {
        /// <summary>
        /// Specifies that pin 0 is high.
        /// </summary>
        Pin0 = 0x1,
        /// <summary>
        /// Specifies that pin 1 is high.
        /// </summary>
        Pin1 = 0x2,
        /// <summary>
        /// Specifies that pin 2 is high.
        /// </summary>
        Pin2 = 0x4,
        /// <summary>
        /// Specifies that pin 3 is high.
        /// </summary>
        Pin3 = 0x8,
        /// <summary>
        /// Specifies that pin 4 is high.
        /// </summary>
        Pin4 = 0x10,
        /// <summary>
        /// Specifies that pin 5 is high.
        /// </summary>
        Pin5 = 0x20,
        /// <summary>
        /// Specifies that pin 6 is high.
        /// </summary>
        Pin6 = 0x40,
        /// <summary>
        /// Specifies that pin 7 is high.
        /// </summary>
        Pin7 = 0x80,
        /// <summary>
        /// Specifies that pin 8 is high.
        /// </summary>
        Pin8 = 0x100,
        /// <summary>
        /// Specifies that pin 9 is high.
        /// </summary>
        Pin9 = 0x200,
        /// <summary>
        /// Specifies that pin 10 is high.
        /// </summary>
        Pin10 = 0x400,
        /// <summary>
        /// Specifies that pin 11 is high.
        /// </summary>
        Pin11 = 0x800,
        /// <summary>
        /// Specifies that pin 12 is high.
        /// </summary>
        Pin12 = 0x1000,
        /// <summary>
        /// Specifies that pin 13 is high.
        /// </summary>
        Pin13 = 0x2000,
        /// <summary>
        /// Specifies that pin 14 is high.
        /// </summary>
        Pin14 = 0x4000,
        /// <summary>
        /// Specifies that pin 15 is high.
        /// </summary>
        Pin15 = 0x8000,

    }
}
