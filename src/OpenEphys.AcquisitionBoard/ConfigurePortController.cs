using System;
using System.ComponentModel;
using System.Reactive.Disposables;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Configures the headstage ports of an Open Ephys Acquisition Board.
    /// </summary>
    /// <remarks>
    /// Each headstage port can operate in one of two modes:
    /// <list type="bullet">
    /// <item><description>Dual MISO (default): both data lines of the port are used by the Rhythm controller,
    /// allowing two acquisition chips per port. I2C is disabled.</description></item>
    /// <item><description>MISO + I2C: a single data line is used by the Rhythm controller, allowing a single
    /// acquisition chip, and the other is used for I2C, enabling I2C devices such as a Bno055.</description></item>
    /// </list>
    /// When a port is in dual MISO mode, the gateware controllers for its I2C devices are disabled and any
    /// attempt to access them will fail.
    /// </remarks>
    internal class ConfigurePortController : SingleDeviceFactory<AcquisitionBoardContextTask>
    {
        public ConfigurePortController()
            : base(typeof(PortController))
        {
        }

        [Category(ConfigurationCategory)]
        [Description("Specifies whether port A is in MISO + I2C mode (true) or dual MISO mode (false).")]
        public bool PortAI2CEnabled { get; set; } = false;

        [Category(ConfigurationCategory)]
        [Description("Specifies whether port B is in MISO + I2C mode (true) or dual MISO mode (false).")]
        public bool PortBI2CEnabled { get; set; } = false;

        [Category(ConfigurationCategory)]
        [Description("Specifies whether port C is in MISO + I2C mode (true) or dual MISO mode (false).")]
        public bool PortCI2CEnabled { get; set; } = false;

        [Category(ConfigurationCategory)]
        [Description("Specifies whether port D is in MISO + I2C mode (true) or dual MISO mode (false).")]
        public bool PortDI2CEnabled { get; set; } = false;

        /// <summary>
        /// Gets the gateware version of the connected acquisition board.
        /// </summary>
        public string GatewareVersion { get; private set; }

        public override IObservable<AcquisitionBoardContextTask> Process(IObservable<AcquisitionBoardContextTask> source)
        {
            var deviceAddress = DeviceAddress;
            var i2cEnable = new[] { PortAI2CEnabled, PortBI2CEnabled, PortCI2CEnabled, PortDI2CEnabled };
            return source.ConfigureAndLatchController(context =>
            {
                var version = GetGatewareVersion(context, deviceAddress);
                if (version < new Version(2, 0, 0))
                {
                    throw new InvalidOperationException($"The connected Rhythm board has gateware version {version}, which is not supported. Please update the board gateware to version 2.0.0 or later.");
                }
                GatewareVersion = CreateGatewareVersionString(version);

                uint val = 0;
                for (int i = 0; i < i2cEnable.Length; i++)
                {
                    if (i2cEnable[i])
                    {
                        val |= 1u << i;
                    }
                }
                context.WriteRegister(deviceAddress, PortController.ENABLE_I2C, val);
                return Disposable.Empty;
            });
        }

        static Version GetGatewareVersion(ContextTask context, uint deviceAddress)
        {
            uint val = (uint)context.ReadRegister(deviceAddress, PortController.GATEWARE_VERSION);

            int patch = (int)(val & 0xFF);
            int minor = (int)((val >> 8) & 0xFF);
            int major = (int)((val >> 16) & 0xFF);
            int rc = (int)((val >> 24) & 0xFF);

            return new Version(major, minor, patch, rc);
        }

        static string CreateGatewareVersionString(Version version)
        {
            string versionString = $"v{version.Major}.{version.Minor}.{version.Build}";
            int rc = version.Revision;

            if (rc != 0)
            {
                string tag;
                switch (rc & 0xC0)
                {
                    case 0x80:
                        tag = "-rc";
                        break;
                    case 0xC0:
                        tag = "-experimental";
                        break;
                    default:
                        tag = "-beta";
                        break;
                }

                int rcNumber = rc & 0x3F;
                versionString += $"{tag}{rcNumber}";
            }

            return versionString;
        }
    }

    static class PortController
    {
        public const uint GATEWARE_VERSION = 2; // Read only. Bits 7-0: patch, 15-8: minor, 23-16: major, 31-24: release candidate tag
        public const uint ENABLE_I2C = 0x00001001; // Bit i sets the mode of port i (0 = A, 1 = B, ...): 0 = dual MISO, 1 = MISO + I2C
    }
}
