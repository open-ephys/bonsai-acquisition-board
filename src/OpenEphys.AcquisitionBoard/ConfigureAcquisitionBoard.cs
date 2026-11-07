using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using Bonsai;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Configures an Open Ephys Acquisition Board.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Open Ephys Acquisition Board is a system for acquiring electrophysiology data from headstages
    /// based on Intan RHD2000-series amplifier chips. It provides the following features:
    /// <list type="bullet">
    /// <item><description>4x headstage ports (A to D), each of which can operate in dual MISO mode, supporting
    /// two amplifier chips, or MISO + I2C mode, supporting a single amplifier chip and I2C devices such as a
    /// Bno055 9-axis IMU.</description></item>
    /// <item><description>Analog and digital inputs sampled synchronously with the amplifier
    /// data.</description></item>
    /// <item><description>Analog and digital (TTL) outputs.</description></item>
    /// <item><description>A Harp clock synchronization input.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This operator configures all devices on the acquisition board as a group and should be
    /// placed directly after a <see cref="CreateContext"/> operator. Data from each device can then be
    /// acquired using the corresponding data IO operator, such as <see cref="HeadstageData"/> or <see
    /// cref="ExternalData"/>, linked to the device configuration through a shared <c>DeviceName</c>.
    /// </para>
    /// </remarks>
    [Description("Configures an Open Ephys Acquisition Board.")]
    public class ConfigureAcquisitionBoard : MultiDeviceFactory<AcquisitionBoardContextTask>
    {
        readonly ConfigurePortController PortControl = new();

        const uint HEARTBEAT_ADDRESS = 0;
        const uint MEM_USAGE_ADDR = 1;
        const uint BNO_BASE_ADDR = 2u;
        const uint CONTROL_ADDR = 254u;

        const uint RHYTHM_ADDR = 0x0101;
        const uint TTLOUT_ADDR = 0x0102;
        const uint ANALOGOUT_ADDR = 0x0103;
        const uint HARP_ADDR = 0x0104;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureAcquisitionBoard"/> class.
        /// </summary>
        public ConfigureAcquisitionBoard()
        {
            PortControl.DeviceAddress = CONTROL_ADDR;
            Heartbeat.DeviceAddress = HEARTBEAT_ADDRESS;
            var bno055Devices = Bno055Devices;
            for (int i = 0; i < bno055Devices.Length; i++)
            {
                bno055Devices[i].DeviceAddress = (uint)(BNO_BASE_ADDR + i * 2);
            }
            Rhythm.DeviceAddress = RHYTHM_ADDR;
            MemoryMonitor.DeviceAddress = MEM_USAGE_ADDR;
            MemoryMonitor.Enable = true;
            TTLOutput.DeviceAddress = TTLOUT_ADDR;
            AnalogOutput.DeviceAddress = ANALOGOUT_ADDR;
            HarpRhythm.DeviceAddress = HARP_ADDR;
            HarpRhythm.Enable = true;
        }

        /// <summary>
        /// Gets or sets the Rhythm device configuration.
        /// </summary>
        /// <remarks>
        /// The Rhythm device controls the headstage amplifier chips connected to the acquisition board ports,
        /// as well as the analog and digital inputs of the board.
        /// </remarks>
        [TypeConverter(typeof(SingleDeviceFactoryConverter))]
        [Description("Specifies the configuration for the Rhythm device in the acquisition board.")]
        [Category(DevicesCategory)]
        public ConfigureRhythmDevice Rhythm { get; set; } = new();

        /// <summary>
        /// Gets or sets the Bno055 device configuration for port A.
        /// </summary>
        /// <remarks>
        /// This configuration is only applied when <see cref="PortAI2CEnabled"/> is true. Otherwise, the
        /// Bno055 controller for this port is disabled and this device is not available.
        /// </remarks>
        [TypeConverter(typeof(SingleDeviceFactoryConverter))]
        [Description("Specifies the configuration for the Bno055 device in port A. Only applied when port A is in MISO + I2C mode.")]
        [Category(DevicesCategory)]
        public ConfigureDetachableBno055 Bno055A { get; set; } = new();

        /// <summary>
        /// Gets or sets the Bno055 device configuration for port B.
        /// </summary>
        /// <remarks>
        /// This configuration is only applied when <see cref="PortBI2CEnabled"/> is true. Otherwise, the
        /// Bno055 controller for this port is disabled and this device is not available.
        /// </remarks>
        [TypeConverter(typeof(SingleDeviceFactoryConverter))]
        [Description("Specifies the configuration for the Bno055 device in port B. Only applied when port B is in MISO + I2C mode.")]
        [Category(DevicesCategory)]
        public ConfigureDetachableBno055 Bno055B { get; set; } = new();

        /// <summary>
        /// Gets or sets the Bno055 device configuration for port C.
        /// </summary>
        /// <remarks>
        /// This configuration is only applied when <see cref="PortCI2CEnabled"/> is true. Otherwise, the
        /// Bno055 controller for this port is disabled and this device is not available.
        /// </remarks>
        [TypeConverter(typeof(SingleDeviceFactoryConverter))]
        [Description("Specifies the configuration for the Bno055 device in port C. Only applied when port C is in MISO + I2C mode.")]
        [Category(DevicesCategory)]
        public ConfigureDetachableBno055 Bno055C { get; set; } = new();

        /// <summary>
        /// Gets or sets the Bno055 device configuration for port D.
        /// </summary>
        /// <remarks>
        /// This configuration is only applied when <see cref="PortDI2CEnabled"/> is true. Otherwise, the
        /// Bno055 controller for this port is disabled and this device is not available.
        /// </remarks>
        [TypeConverter(typeof(SingleDeviceFactoryConverter))]
        [Description("Specifies the configuration for the Bno055 device in port D. Only applied when port D is in MISO + I2C mode.")]
        [Category(DevicesCategory)]
        public ConfigureDetachableBno055 Bno055D { get; set; } = new();

        /// <summary>
        /// Gets or sets the TTL output device configuration.
        /// </summary>
        [TypeConverter(typeof(SingleDeviceFactoryConverter))]
        [Description("Specifies the configuration for the TTL output device in the acquisition board.")]
        [Category(DevicesCategory)]
        public ConfigureTTLOut TTLOutput { get; set; } = new();

        /// <summary>
        /// Gets or sets the analog output device configuration.
        /// </summary>
        [TypeConverter(typeof(SingleDeviceFactoryConverter))]
        [Description("Specifies the configuration for the analog output device in the acquisition board.")]
        [Category(DevicesCategory)]
        public ConfigureAnalogOutput AnalogOutput { get; set; } = new();

        /// <summary>
        /// Gets or sets the Harp synchronization device configuration.
        /// </summary>
        [TypeConverter(typeof(SingleDeviceFactoryConverter))]
        [Description("Specifies the configuration for the Harp synchronization device in the acquisition board.")]
        [Category(DevicesCategory)]
        public ConfigureHarpRhythm HarpRhythm { get; set; } = new();

        /// <summary>
        /// Gets or sets the heartbeat device configuration.
        /// </summary>
        [TypeConverter(typeof(SingleDeviceFactoryConverter))]
        [Description("Specifies the configuration for the heartbeat device in the acquisition board.")]
        [Category(DevicesCategory)]
        public ConfigureHeartbeat Heartbeat { get; set; } = new();

        /// <summary>
        /// Gets or sets the hardware memory monitor configuration.
        /// </summary>
        [TypeConverter(typeof(SingleDeviceFactoryConverter))]
        [Description("Specifies the configuration for the memory monitor device in the acquisition board.")]
        [Category(DevicesCategory)]
        public ConfigureMemoryMonitor MemoryMonitor { get; set; } = new();

        /// <summary>
        /// Gets or sets a value specifying whether port A is in MISO + I2C mode.
        /// </summary>
        /// <remarks>
        /// If true, port A is in MISO + I2C mode, which allows a single acquisition chip and I2C devices
        /// such as <see cref="Bno055A"/>. If false (default), port A is in dual MISO mode, which allows two
        /// acquisition chips but disables all I2C devices on the port.
        /// </remarks>
        [Description("Specifies whether port A is in MISO + I2C mode (true) or dual MISO mode (false).")]
        [Category(ConfigurationCategory)]
        public bool PortAI2CEnabled
        {
            get => PortControl.PortAI2CEnabled;
            set => PortControl.PortAI2CEnabled = value;
        }

        /// <summary>
        /// Gets or sets a value specifying whether port B is in MISO + I2C mode.
        /// </summary>
        /// <remarks>
        /// If true, port B is in MISO + I2C mode, which allows a single acquisition chip and I2C devices
        /// such as <see cref="Bno055B"/>. If false (default), port B is in dual MISO mode, which allows two
        /// acquisition chips but disables all I2C devices on the port.
        /// </remarks>
        [Description("Specifies whether port B is in MISO + I2C mode (true) or dual MISO mode (false).")]
        [Category(ConfigurationCategory)]
        public bool PortBI2CEnabled
        {
            get => PortControl.PortBI2CEnabled;
            set => PortControl.PortBI2CEnabled = value;
        }

        /// <summary>
        /// Gets or sets a value specifying whether port C is in MISO + I2C mode.
        /// </summary>
        /// <remarks>
        /// If true, port C is in MISO + I2C mode, which allows a single acquisition chip and I2C devices
        /// such as <see cref="Bno055C"/>. If false (default), port C is in dual MISO mode, which allows two
        /// acquisition chips but disables all I2C devices on the port.
        /// </remarks>
        [Description("Specifies whether port C is in MISO + I2C mode (true) or dual MISO mode (false).")]
        [Category(ConfigurationCategory)]
        public bool PortCI2CEnabled
        {
            get => PortControl.PortCI2CEnabled;
            set => PortControl.PortCI2CEnabled = value;
        }

        /// <summary>
        /// Gets or sets a value specifying whether port D is in MISO + I2C mode.
        /// </summary>
        /// <remarks>
        /// If true, port D is in MISO + I2C mode, which allows a single acquisition chip and I2C devices
        /// such as <see cref="Bno055D"/>. If false (default), port D is in dual MISO mode, which allows two
        /// acquisition chips but disables all I2C devices on the port.
        /// </remarks>
        [Description("Specifies whether port D is in MISO + I2C mode (true) or dual MISO mode (false).")]
        [Category(ConfigurationCategory)]
        public bool PortDI2CEnabled
        {
            get => PortControl.PortDI2CEnabled;
            set => PortControl.PortDI2CEnabled = value;
        }

        /// <summary>
        /// Gets the gateware version of the connected acquisition board.
        /// </summary>
        /// <remarks>
        /// This value is read from the hardware during configuration and is only available after the
        /// workflow has started.
        /// </remarks>
        [Description("The gateware version of the connected acquisition board.")]
        [Category(ConfigurationCategory)]
        [Externalizable(false)]
        [ReadOnly(true)]
        [XmlIgnore]
        public string GatewareVersion => PortControl.GatewareVersion;

        ConfigureDetachableBno055[] Bno055Devices => new[] { Bno055A, Bno055B, Bno055C, Bno055D };

        internal override void UpdateDeviceNames()
        {
            foreach (var device in GetDevices())
            {
                if (device.DeviceType != typeof(DetachableBno055))
                {
                    device.DeviceName = GetFullDeviceName(device.DeviceType.Name);
                }
            }

            // NB: name every Bno055 after its port, including those not returned by GetDevices
            var bno055Devices = Bno055Devices;
            for (int i = 0; i < bno055Devices.Length; i++)
            {
                bno055Devices[i].DeviceName = GetFullDeviceName("Bno055_" + (char)('A' + i));
            }
        }

        internal override IEnumerable<IDeviceConfiguration<AcquisitionBoardContextTask>> GetDevices()
        {
            yield return PortControl;
            yield return Heartbeat;

            // NB: a Bno055 controller only exists while its port is in MISO + I2C mode
            if (PortAI2CEnabled) yield return Bno055A;
            if (PortBI2CEnabled) yield return Bno055B;
            if (PortCI2CEnabled) yield return Bno055C;
            if (PortDI2CEnabled) yield return Bno055D;

            yield return Rhythm;
            yield return MemoryMonitor;
            yield return TTLOutput;
            yield return AnalogOutput;
            yield return HarpRhythm;
        }
    }
}
