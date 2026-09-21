using System;
using System.ComponentModel;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    internal class ConfigureTTLOut : SingleDeviceFactory
    {
        public ConfigureTTLOut()
            : base(typeof(TTLOut))
        {
        }

        /// <summary>
        /// Gets or sets the output mode for TTL output.
        /// </summary>
        ///<remarks>
        /// If set to true, the TTL output will be set immediately when the value is received.
        /// If set to false, the TTL output will be synchronized with the acquisition board sample rate
        ///</remarks>
        [Category(ConfigurationCategory)]
        [Description("Specifies whether the TTL output is set immediately or synchronized with the acquisition board sample rate.")]
        public bool ImediateMode { get; set; } = false;

        public override IObservable<TContext> Process<TContext>(IObservable<TContext> source)
        {
            var immediateMode = ImediateMode;
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
    /// Specifies the state of the acquisition board's digital input pins.
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
