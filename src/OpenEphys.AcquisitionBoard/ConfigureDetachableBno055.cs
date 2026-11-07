using System;
using System.ComponentModel;
using System.Diagnostics;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Configures a Bosch Bno055 9-axis inertial measurement unit (IMU) connected to an acquisition board
    /// headstage port.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A detachable Bno055 is comprised of a controller in the acquisition board gateware and the Bno055
    /// chip itself, which is located on the headstage and might not be physically connected. The controller
    /// polls the chip through the I2C lines of the headstage port, so it is only available while the port is
    /// in MISO + I2C mode.
    /// </para>
    /// <para>
    /// This configuration operator can be linked to a data IO operator, such as <see cref="Bno055Data"/>,
    /// using a shared <c>DeviceName</c>.
    /// </para>
    /// </remarks>
    [Description("Configures a Bno055 9-axis IMU connected to an acquisition board headstage port.")]
    public class ConfigureDetachableBno055 : SingleDeviceFactory<ContextTask>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureDetachableBno055"/> class.
        /// </summary>
        public ConfigureDetachableBno055()
            : base(typeof(DetachableBno055))
        {
        }

        readonly TimeSpan detectTimeout = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Gets or sets the device enable state.
        /// </summary>
        /// <remarks>
        /// If set to true, the Bno055 controller in the acquisition board will send the data it produces to
        /// the host computer, and a <see cref="Bno055Data"/> instance that is linked to this configuration
        /// will produce data. If set to false, the controller will not send data to the host.
        /// </remarks>
        [Category(ConfigurationCategory)]
        [Description("Specifies whether the Bno055 device is enabled.")]
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Gets or sets the axis map that will be applied during configuration.
        /// </summary>
        /// <remarks>
        /// This value can be changed to compensate for the Bno055's mounting orientation.
        /// Specifically, this value can be set to rotate the Bno055's coordinate system compared
        /// to the default orientation presented on page 24 of the Bno055 datasheet.
        /// </remarks>
        [Category(ConfigurationCategory)]
        [Description("Specifies the axis map that will be applied during configuration.")]
        public Bno055AxisMap AxisMap { get; set; } = Bno055AxisMap.XYZ;

        /// <summary>
        /// Gets or sets the axis sign that will be applied during configuration.
        /// </summary>
        /// <remarks>
        /// This value can be changed to compensate for the Bno055's mounting orientation.
        /// Specifically, this value can be set to mirror specific axes in the Bno055's coordinate
        /// system compared to the default orientation presented on page 24 of the Bno055 datasheet.
        /// </remarks>
        [Category(ConfigurationCategory)]
        [Description("Specifies axis sign that will be applied during configuration.")]
        public Bno055AxisSign AxisSign { get; set; } = Bno055AxisSign.Default;

        /// <summary>
        /// Configures a Bosch Bno055 9-axis IMU device.
        /// </summary>
        /// <remarks>
        /// This will schedule configuration actions to be applied by a <see cref="StartAcquisition"/> instance
        /// prior to data acquisition.
        /// <para>
        /// A detachable Bno055 is comprised of a controller in the acquisition board gateware and the Bno055
        /// chip itself, which is located on the headstage and might not be physically connected. The
        /// controller only exists while its headstage port is in MISO + I2C mode; in dual MISO mode it is
        /// disabled and any attempt to access it will fail.
        /// </para>
        /// <para>
        /// When the controller is present, it scans the port for the Bno055 chip after each reset. If the
        /// device is enabled and the chip is not detected, data from this device would be missing or invalid,
        /// so an exception is thrown unless <see cref="CreateContext.ValidationLevel"/> is <see
        /// cref="ValidationLevel.Permissive"/>, in which case a warning is produced instead and configuration
        /// proceeds.
        /// </para>
        /// </remarks>
        /// <param name="source">A sequence of <see cref="ContextTask"/> instances that holds configuration actions.</param>
        /// <returns>The original sequence modified by adding additional configuration actions required to configure a Bno055 device.</returns>
        public override IObservable<ContextTask> Process(IObservable<ContextTask> source)
        {
            var deviceName = DeviceName;
            var deviceAddress = DeviceAddress;
            var enable = Enable;
            var axisMap = AxisMap;
            var axisSign = AxisSign;
            return source.ConfigureAndLatchDevice(context =>
            {
                var device = context.GetDeviceContext(deviceAddress, DeviceType);
                if (enable && !IsChipPresent(device))
                {
                    ContextHelper.Validate(ValidationLevel.Permissive, new InvalidOperationException(
                        $"No Bno055 chip was detected for device '{deviceName}' at address {deviceAddress}. Confirm " +
                        $"that a headstage containing a Bno055 is connected to the corresponding port, or disable this device."));
                }

                device.WriteRegister(DetachableBno055.ENABLE, enable ? 1u : 0);
                device.WriteRegister(DetachableBno055.AXIS_MAP, ((uint)axisSign << 8) | (uint)axisMap);
                return DeviceManager.RegisterDevice(deviceName, device, DeviceType);
            });
        }

        // NB: waits for the controller to finish scanning the port for the Bno055 chip
        bool IsChipPresent(DeviceContext device)
        {
            uint val;
            var timeout = Stopwatch.StartNew();
            do
            {
                val = device.ReadRegister(DetachableBno055.STATUS);
            } while (val == (uint)DetachableBno055.StatusValues.SCANNING && timeout.Elapsed < detectTimeout);
            return val == (uint)DetachableBno055.StatusValues.PRESENT;
        }
    }

    [EquivalentDataSource(typeof(Bno055))]
    static class DetachableBno055
    {
        public const int ID = 9;
        public const uint MinimumVersion = 2;

        // managed registers
        public const uint ENABLE = 0x0; // Enable or disable the data output stream
        public const uint AXIS_MAP = 0x1; //Axis map for the BNO device. Bits 7-0: AXIS_MAP, BITS 15-7 AXIS_SIGN
        public const uint STATUS = 0x2; //Read only. Bno055 chip scan result: "00" for no chip present, "01" for chip detected, "10" for scan in progress

        internal enum StatusValues : uint
        {
            NOT_PRESENT = 0,
            PRESENT = 1,
            SCANNING = 2
        }

        internal class NameConverter : DeviceNameConverter
        {
            public NameConverter()
                : base(typeof(DetachableBno055))
            {
            }
        }
    }
}
