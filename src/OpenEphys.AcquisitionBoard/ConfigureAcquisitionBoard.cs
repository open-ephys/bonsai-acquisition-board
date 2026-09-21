using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Xml.Serialization;
using Bonsai;
using OpenEphys.Onix1;
using Rhythm.Net;

namespace OpenEphys.AcquisitionBoard
{
    internal class ConfigureAcquisitionBoard : Onix1.MultiDeviceFactory
    {



        const uint HEARTBEAT_ADDRESS = 0;
        const uint MEM_USAGE_ADDR = 1;
        const uint BNO_BASE_ADDR = 2u;
        const uint I2C_BASE_ADDR = 3u;
        const uint CONTROL_ADDR = 254u;

        const uint RHYTHM_ADDR = 0x0101;
        const uint ENABLE_I2C_REG = 0x00001001;

        readonly ConfigureHeartbeat heartbeat = new();

        ReadOnlyCollection<SPIHeadstageI2CDevices> bnoArray = new ReadOnlyCollection<SPIHeadstageI2CDevices>(new[]
        {
            new SPIHeadstageI2CDevices(),
            new SPIHeadstageI2CDevices(),
            new SPIHeadstageI2CDevices(),
            new SPIHeadstageI2CDevices()
        });
        readonly ConfigureRhythmDevice rhythm = new();

        readonly ConfigureMemoryMonitor memoryMonitor = new();

        /// <inheritdoc cref="ConfigureRhythmDevice.BoardLeds"/>
        [Category(DeviceFactory.AcquisitionCategory)]
        [Description("Board LED status")]
        public bool BoardLeds
        {
            get => rhythm.BoardLeds;
            set => rhythm.BoardLeds = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.BoardIndex"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Range(-1, 100)]
        [Description("The index of the board to open (-1 to open the first available board).")]
        public int BoardIndex
        {
            get => rhythm.BoardIndex;
            set => rhythm.BoardIndex = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.SampleRate"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Description("The per-channel sampling rate.")]
        public AmplifierSampleRate SampleRate
        {
            get => rhythm.SampleRate;
            set => rhythm.SampleRate = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.ExternalFastSettleEnabled"/>
        [Category(DeviceFactory.AcquisitionCategory)]
        [Description("Specifies whether the external fast settle channel (channel 0) is enabled.")]
        public bool ExternalFastSettleEnabled
        {
            get => rhythm.ExternalFastSettleEnabled;
            set => rhythm.ExternalFastSettleEnabled = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.LowerBandwidth"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Description("The lower bandwidth of the amplifier on-board analog filter (Hz).")]
        public double LowerBandwidth
        {
            get => rhythm.LowerBandwidth;
            set => rhythm.LowerBandwidth = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.UpperBandwidth"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Description("The upper bandwidth of the amplifier on-board analog filter (Hz).")]
        public double UpperBandwidth
        {
            get => rhythm.UpperBandwidth;
            set => rhythm.UpperBandwidth = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.DspCutoffFrequency"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Description("The cutoff frequency of the DSP offset removal filter (Hz).")]
        public double DspCutoffFrequency
        {
            get => rhythm.DspCutoffFrequency;
            set => rhythm.DspCutoffFrequency = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.GatewareVersion"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Externalizable(false)]
        [ReadOnly(true)]
        [XmlIgnore]
        public string GatewareVersion { get; private set; }

        /// <inheritdoc cref="ConfigureRhythmDevice.DspEnabled"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Description("Specifies whether the DSP offset removal filter is enabled.")]
        public bool DspEnabled
        {
            get => rhythm.DspEnabled;
            set => rhythm.DspEnabled = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.BufferSize"/>
        [Description("Number of samples that are collected before data is propagated.")]
        [Category(ConfigurationCategory)]
        public int BufferSize
        {
            get => rhythm.BufferSize;
            set => rhythm.BufferSize = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureAcquisitionBoard"/> class.
        /// </summary>
        public ConfigureAcquisitionBoard() : base()
        {
            heartbeat.DeviceAddress = 0;
            for (int i = 0; i < bnoArray.Count; i++)
            {
                bnoArray[i].DeviceAddress = (uint)(BNO_BASE_ADDR + i * 2);
            }
            rhythm.DeviceAddress = RHYTHM_ADDR;
            memoryMonitor.DeviceAddress = MEM_USAGE_ADDR;
            memoryMonitor.Enable = true;
        }

        internal override void UpdateDeviceNames()
        {
            int bnoCount = 0;
            foreach (var device in GetDevices())
            {
                if (device.DeviceType == typeof(DetachableBno055))
                {
                    device.DeviceName = GetFullDeviceName("Bno055_" + (char)('A' + bnoCount++));
                }
                else
                {
                    device.DeviceName = GetFullDeviceName(device.DeviceType.Name);
                }
            }
        }

        static void SetI2cMode(ContextTask context, bool[] bnoEnable)
        {
            uint val = 0;
            for (int i = 0; i < bnoEnable.Length; i++)
            {
                if (bnoEnable[i])
                {
                    val |= (1u << i);
                }
            }
            context.WriteRegister(CONTROL_ADDR, ENABLE_I2C_REG, val);
        }

        internal override IEnumerable<IDeviceConfiguration> GetDevices()
        {
            yield return heartbeat;
            for (int i = 0; i < bnoArray.Count; i++)
            {
                yield return bnoArray[i];
            }
            yield return rhythm;
            yield return memoryMonitor;
        }

        public override IObservable<TContext> Process<TContext>(IObservable<TContext> source)
        {
            return base.Process(source.ConfigureAndLatchController(context =>
            {
                var version = GetGatewareVersion(context);
                if (version < new Version(2, 0, 0))
                {
                    throw new InvalidOperationException($"The connected Rhythm board has gateware version {version}, which is not supported. Please update the board gateware to version 2.0.0 or later.");
                }
                GatewareVersion = CreateGatewareVersionString(version);
                Console.WriteLine("Init i2c");
                SetI2cMode(context, Enumerable.Repeat(true, bnoArray.Count).ToArray());
                context.Reset();
                return Disposable.Empty;

            })).ConfigureAndLatchController(context =>
            {
                bool[] bnoEnable = new bool[bnoArray.Count];
                for (int i = 0; i < bnoEnable.Length; i++)
                {
                    bnoEnable[i] = bnoArray[i].IsConnected;
                }
                SetI2cMode(context, bnoEnable);
                Console.WriteLine("Set i2c " + bnoEnable);
                return Disposable.Empty;
            });
        }

        static Version GetGatewareVersion(ContextTask context)
        {
            uint val = (uint)context.ReadRegister(CONTROL_ADDR, 2);

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
}
