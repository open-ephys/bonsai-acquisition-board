using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using Bonsai;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Provides an abstract base class for the configuration operators of the Open Ephys Acquisition Board models.
    /// </summary>
    /// <remarks>
    /// Each Acquisition Board model has its own configuration operator, derived from this class, which configures
    /// all the devices of that model as a group. Use the operator that matches the connected hardware:
    /// <see cref="ConfigureAcquisitionBoardGen2"/> or <see cref="ConfigureAcquisitionBoardGen3"/>.
    /// </remarks>
    public abstract class AcquisitionBoardFactory : MultiDeviceFactory<AcquisitionBoardContextTask>
    {
        const uint HEARTBEAT_ADDRESS = 0;
        const uint MEM_USAGE_ADDR = 1;
        const uint CONTROL_ADDR = 254u;

        const uint RHYTHM_ADDR = 0x0101;
        const uint TTLOUT_ADDR = 0x0102;
        const uint ANALOGOUT_ADDR = 0x0103;
        const uint HARP_ADDR = 0x0104;

        internal AcquisitionBoardFactory()
        {
            PortControl.DeviceAddress = CONTROL_ADDR;
            Heartbeat.DeviceAddress = HEARTBEAT_ADDRESS;
            Rhythm.DeviceAddress = RHYTHM_ADDR;
            MemoryMonitor.DeviceAddress = MEM_USAGE_ADDR;
            MemoryMonitor.Enable = true;
            TTLOutput.DeviceAddress = TTLOUT_ADDR;
            AnalogOutput.DeviceAddress = ANALOGOUT_ADDR;
            HarpRhythm.DeviceAddress = HARP_ADDR;
            HarpRhythm.Enable = true;
        }

        // NB: implemented by each model with a field initializer, so that it already exists when the
        // MultiDeviceFactory constructor enumerates GetDevices to name the devices
        internal abstract ConfigurePortController PortControl { get; }

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

        /// <summary>
        /// Gets the devices that only exist on some models, such as the I2C devices of the headstage ports.
        /// </summary>
        internal virtual IEnumerable<IDeviceConfiguration<AcquisitionBoardContextTask>> GetModelDevices()
        {
            return Enumerable.Empty<IDeviceConfiguration<AcquisitionBoardContextTask>>();
        }

        internal override IEnumerable<IDeviceConfiguration<AcquisitionBoardContextTask>> GetDevices()
        {
            yield return PortControl;
            yield return Heartbeat;

            foreach (var device in GetModelDevices())
            {
                yield return device;
            }

            yield return Rhythm;
            yield return MemoryMonitor;
            yield return TTLOutput;
            yield return AnalogOutput;
            yield return HarpRhythm;
        }
    }
}
