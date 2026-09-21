using System;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    internal class ConfigureAnalogOutput : SingleDeviceFactory
    {
        public ConfigureAnalogOutput()
            : base(typeof(AnalogOut))
        {
        }

        public override IObservable<TContext> Process<TContext>(IObservable<TContext> source)
        {
            throw new NotImplementedException();
        }
    }

    static class AnalogOut
    {
        public const int ID = 0x10002;
        public const uint MinimumVersion = 1;

        public const uint NULLPARAM = 0;

        public const uint MODE = 1;

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
