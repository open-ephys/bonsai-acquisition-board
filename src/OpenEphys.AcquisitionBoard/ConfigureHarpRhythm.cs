using System;
using System.ComponentModel;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Configures the Harp synchronization device of the acquisition board.
    /// </summary>
    /// <remarks>
    /// The Harp synchronization device timestamps the clock synchronization signals received from a Harp
    /// device using the Rhythm device sample counter, allowing data acquired by the acquisition board to be
    /// aligned with data acquired by Harp devices. This configuration operator can be linked to a data IO
    /// operator, such as <see cref="HarpRhythmData"/>, using a shared <c>DeviceName</c>.
    /// </remarks>
    [Description("Configures the Harp synchronization device of the acquisition board.")]
    public class ConfigureHarpRhythm : SingleDeviceFactory<AcquisitionBoardContextTask>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureHarpRhythm"/> class.
        /// </summary>
        public ConfigureHarpRhythm()
            : base(typeof(HarpRhythm))
        {
        }

        /// <summary>
        /// Gets or sets the device enable state.
        /// </summary>
        /// <remarks>
        /// If set to true, a <see cref="HarpRhythmData"/> instance that is linked to this configuration will
        /// produce data. If set to false, it will not produce data.
        /// </remarks>
        [Category(ConfigurationCategory)]
        [Description("Specifies whether the Harp synchronization device is enabled.")]
        public bool Enable { get; set; } = false;

        /// <summary>
        /// Configures the Harp synchronization device.
        /// </summary>
        /// <remarks>
        /// This will schedule configuration actions to be applied by a <see cref="StartAcquisition"/> instance
        /// prior to data acquisition.
        /// </remarks>
        /// <param name="source">A sequence of <see cref="AcquisitionBoardContextTask"/> instances that holds configuration actions.</param>
        /// <returns>The original sequence modified by adding additional configuration actions required to configure the Harp synchronization device.</returns>
        public override IObservable<AcquisitionBoardContextTask> Process(IObservable<AcquisitionBoardContextTask> source)
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
