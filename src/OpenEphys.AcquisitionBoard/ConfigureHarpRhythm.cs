using System;
using System.ComponentModel;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    internal class ConfigureHarpRhythm : SingleDeviceFactory
    {
        public ConfigureHarpRhythm()
            : base(typeof(HarpRhythm))
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether the Harp Rhythm device is enabled.
        /// </summary>
        [Category(ConfigurationCategory)]
        [Description("Specifies whether the Harp Rhythm device is enabled.")]
        public bool Enable { get; set; } = false;
        public override IObservable<TContext> Process<TContext>(IObservable<TContext> source)
        {
            var deviceName = DeviceName;
            var deviceAddress = DeviceAddress;
            return source.ConfigureAndLatchDevice(context =>
            {
                var device = context.GetDeviceContext(deviceAddress, DeviceType);
                device.WriteRegister(HarpRhythm.ENABLE, Enable ? 1u : 0u);
                return DeviceManager.RegisterDevice(deviceName, device, DeviceType);
            });
        }
    }

    static class HarpRhythm
    {
        public const int ID = 0x10004;
        public const uint MinimumVersion = 1;
        public const uint ENABLE = 0;
        internal class NameConverter : DeviceNameConverter
        {
            public NameConverter()
                : base(typeof(HarpRhythm)) { }
        }
    }
}
