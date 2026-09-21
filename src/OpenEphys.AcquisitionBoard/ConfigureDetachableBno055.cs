using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Disposables;
using oni;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    internal class ConfigureDetachableBno055 : Onix1.SingleDeviceFactory
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureBno055"/> class.
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
        /// If set to true, a <see cref="Bno055Data"/> instance that is linked to this configuration will produce data. If set to false,
        /// it will not produce data.
        /// </remarks>
        [Category(ConfigurationCategory)]
        [Description("Specifies whether the Bno055 device is enabled.")]
        public bool Enable { get; set; } = true;

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
        /// Gets the connected status of the device
        /// </summary>
        public bool IsConnected { get; private set; } = false;

        /// <summary>
        /// Configures a Bosch Bno055 9-axis IMU device.
        /// </summary>
        /// <remarks>
        /// This will schedule configuration actions to be applied by a <see cref="StartAcquisition"/> instance
        /// prior to data acquisition.
        /// </remarks>
        /// <param name="source">A sequence of <see cref="AcquisitionBoardContextTask"/> instances that holds configuration actions.</param>
        /// <returns>The original sequence modified by adding additional configuration actions required to configure a Bno055 device.</returns>
        public override IObservable<Tctx> Process<Tctx>(IObservable<Tctx> source)
        {
            var deviceName = DeviceName;
            var deviceAddress = DeviceAddress;
            return source
                .ConfigureAndLatchController(context =>
                {
                    Console.WriteLine("Conf bno " + DeviceAddress);
                    var device = context.GetDeviceContext(deviceAddress, DeviceType);
                    try
                    {
                        uint val;
                        Stopwatch timeout = Stopwatch.StartNew();
                        do
                        {
                            val = device.ReadRegister(DetachableBno055.STATUS);
                        } while (val != (uint)DetachableBno055.StatusValues.SCANNING && timeout.Elapsed < detectTimeout);
                        if (val == (uint)DetachableBno055.StatusValues.PRESENT)
                        {
                            IsConnected = true;
                            Console.WriteLine("Detected bno at " + device.Address);
                        }
                    }
                    catch (ONIException ex) when (ex.Number == -5)
                    {
                        IsConnected = false;
                        Console.WriteLine("Erro read bno " + device.Address);
                    }


                    return Disposable.Create(() => { IsConnected = false; });
                })
                .ConfigureAndLatchDevice(context =>
            {
                if (IsConnected)
                {
                    var device = context.GetDeviceContext(deviceAddress, DeviceType);
                    device.WriteRegister(DetachableBno055.ENABLE, Enable ? 1u : 0);
                    device.WriteRegister(DetachableBno055.AXIS_MAP, ((uint)AxisSign << 8) | (uint)AxisMap);
                    return DeviceManager.RegisterDevice(deviceName, device, DeviceType);
                }
                else
                {
                    return Disposable.Empty;
                }
            });
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
        public const uint STATUS = 0x2; //Read only. "00" for no BNO present, "01" for BNO detected, "10" for BNO scan in progress

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
