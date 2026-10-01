using System;
using System.Reactive.Disposables;
using System.Text;
using oni;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Represents a combination of a BNO and a I2C EEPROM in a SPI headstage
    /// </summary>
    internal class SPIHeadstageI2CDevices : ConfigureDetachableBno055
    {
        static HeadstageIds GetDeviceIdFromEeprom(DeviceContext device)
        {
            var eeprom = new I2CRegisterContext(device, I2CRawDevice.EEPROM_I2C_ADDR);
            var identifier = eeprom.ReadBytes(0, 4, true);
            if (Encoding.ASCII.GetString(identifier) != "OESH")
                return HeadstageIds.Unknown;
            var version = eeprom.ReadByte(4, true);
            if (version != 1 && version != 2) return HeadstageIds.Unknown;
            var id = eeprom.ReadBytes(8, 4, true);
            return (HeadstageIds)BitConverter.ToUInt32(id, 0);
        }

        void SetBNOOrientation(HeadstageIds headstageId)
        {
            switch (headstageId)
            {
                case HeadstageIds.LowProfile64Hirose:
                    AxisMap = Bno055AxisMap.XYZ;
                    AxisSign = Bno055AxisSign.Default;
                    break;
                case HeadstageIds.Omnetics32:
                    AxisMap = Bno055AxisMap.XZY;
                    AxisSign = Bno055AxisSign.MirrorX;
                    break;
                case HeadstageIds.Omnetics16Bipolar:
                    AxisMap = Bno055AxisMap.XZY;
                    AxisSign = Bno055AxisSign.MirrorX;
                    break;
                default:
                    AxisMap = Bno055AxisMap.XYZ;
                    AxisSign = Bno055AxisSign.Default;
                    break;
            }
        }

        public override IObservable<Tctx> Process<Tctx>(IObservable<Tctx> source)
        {
            // NB: In the acq board we follow this pattern, if we use these headstages
            // somewhere else we should follow this structure
            var deviceAddress = DeviceAddress + 1;
            return base.Process(source).ConfigureAndLatchController(context =>
            {
                var device = context.GetDeviceContext(deviceAddress, typeof(I2CRawDevice));
                HeadstageIds detectedHeadstage = detectedHeadstage = HeadstageIds.LowProfile64Hirose; ;
                try
                {
                    var i2cPresent = device.ReadRegister(I2CRawDevice.I2C_BUS_READY);
                    if (i2cPresent != 0)
                    {
                        detectedHeadstage = GetDeviceIdFromEeprom(device);
                        Console.WriteLine("Detected headstage id " + detectedHeadstage);
                    }
                }
                catch (ONIException ex) when (ex.Number == -5)
                {
                    // just ignore this error here
                }
                SetBNOOrientation(detectedHeadstage);
                return Disposable.Empty;
            });
        }

        enum HeadstageIds : uint
        {
            Unknown = 0,
            LowProfile64Hirose = 0x80000001,
            Omnetics32 = 0x80000002,

            Omnetics16Bipolar = 0x80000003
        }
    }

    static class I2CRawDevice
    {
        public const int ID = 34;
        public const uint MinimumVersion = 1;

        public const uint I2C_BUS_READY = 0x1 + (1 << 15);

        public const uint EEPROM_I2C_ADDR = 0x50;
    }
}
