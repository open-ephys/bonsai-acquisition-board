using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Bonsai;
using OpenEphys.Onix1;
using Rhythm.Net;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Configures the Rhythm device of the acquisition board.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Rhythm device controls the Intan RHD2000-series amplifier chips on the headstages connected to the
    /// acquisition board ports, as well as the analog and digital inputs of the board, which are sampled
    /// synchronously with the amplifier data.
    /// </para>
    /// <para>
    /// During configuration, the connected amplifier chips are detected and the optimal cable delay for each
    /// port is computed automatically, after which an ADC calibration is performed on all chips.
    /// </para>
    /// <para>
    /// This configuration operator can be linked to data IO operators, such as <see cref="HeadstageData"/>
    /// and <see cref="ExternalData"/>, using a shared <c>DeviceName</c>.
    /// </para>
    /// </remarks>
    [Description("Configures the Rhythm device of the acquisition board.")]
    public class ConfigureRhythmDevice : SingleDeviceFactory<AcquisitionBoardContextTask>
    {
        const string PortsCategory = "Port Configuration";
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureRhythmDevice"/> class.
        /// </summary>
        public ConfigureRhythmDevice() :
            base(typeof(RhythmDevice))
        {
        }
        const uint initLen = 64;

        const int ReadSize = 24 * 1024;

        BehaviorSubject<bool> boardLed = new(true);
        BehaviorSubject<uint> clockDivider = new(0);

        /// <summary>
        /// Gets or sets a value indicating the state of the on-board LEDs.
        /// </summary>
        /// <value><c>true</c> if the LEDs are enabled; otherwise, <c>false</c>.</value>
        [Category(AcquisitionCategory)]
        [Description("Specifies whether the on-board LEDs are enabled.")]
        public bool BoardLeds
        {
            get
            {
                return boardLed.Value;
            }
            set
            {
                boardLed.OnNext(value);
            }
        }

        /// <summary>
        /// Gets or sets the clock divider for the sample clock output
        /// of the acquisition board.
        /// </summary>
        [Category(AcquisitionCategory)]
        [Description("Clock divider for the sample clock output of the acquisition board.")]
        [Range(1, 65535)]
        public uint ClockDivider
        {
            get
            {
                return clockDivider.Value + 1;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "ClockDivider must be >= 1.");
                }
                clockDivider.OnNext(value - 1);
            }
        }

        /// <summary>
        /// Gets or sets the per-channel sampling rate of the amplifier.
        /// </summary>
        /// <value>A <see cref="AmplifierSampleRate"/> value defining the sampling frequency.</value>
        [Category(ConfigurationCategory)]
        [Description("The per-channel sampling rate.")]
        public AmplifierSampleRate SampleRate { get; set; } = AmplifierSampleRate.SampleRate30000Hz;

        /// <summary>
        /// Gets or sets a value indicating whether external fast settle mode is enabled on channel 0.
        /// </summary>
        /// <value><c>true</c> if external fast settle is enabled; otherwise, <c>false</c>.</value>
        [Category(ConfigurationCategory)]
        [Description("Specifies whether the external fast settle channel (channel 0) is enabled.")]
        public bool ExternalFastSettleEnabled { get; set; }

        /// <summary>
        /// Gets or sets the lower cutoff bandwidth for the on-board analog amplifier filter.
        /// </summary>
        /// <value>The lower cutoff frequency in Hertz (Hz).</value>
        [Category(ConfigurationCategory)]
        [Description("The lower bandwidth of the amplifier on-board analog filter (Hz).")]
        public double LowerBandwidth { get; set; }

        /// <summary>
        /// Gets or sets the upper cutoff bandwidth for the on-board analog amplifier filter.
        /// </summary>
        /// <value>The upper cutoff frequency in Hertz (Hz).</value>
        [Category(ConfigurationCategory)]
        [Description("The upper bandwidth of the amplifier on-board analog filter (Hz).")]
        public double UpperBandwidth { get; set; }

        /// <summary>
        /// Gets or sets the cutoff frequency used by the DSP offset removal filter.
        /// </summary>
        /// <value>The high-pass filter cutoff frequency in Hertz (Hz).</value>
        [Category(ConfigurationCategory)]
        [Description("The cutoff frequency of the DSP offset removal filter (Hz).")]
        public double DspCutoffFrequency { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the DSP offset removal filter is active.
        /// </summary>
        /// <value><c>true</c> if DSP offset removal is enabled; otherwise, <c>false</c>.</value>
        [Category(ConfigurationCategory)]
        [Description("Specifies whether the DSP offset removal filter is enabled.")]
        public bool DspEnabled { get; set; }

        /// <summary>
        /// Gets or sets the buffer size.
        /// </summary>
        /// <remarks>
        /// This property determines the number of samples that are collected
        /// before data is propagated.
        /// It is a multiple of 4 to be able to accommodate
        /// the aux data which is sampled at fs/4.
        /// </remarks>
        [Description("Number of samples that are collected before data is propagated.")]
        [Category(ConfigurationCategory)]
        public int BufferSize
        {
            get => bufferSize;
            set => bufferSize = 4 * ((value + 3) / 4);
        }

        /// <summary>
        /// Gets or sets the chip identifier connected to port A1.
        /// </summary>
        /// <remarks>Use <see langword="null"/> when no chip is connected to port A1.</remarks>
        [Category(PortsCategory)]
        [Description("The chip connected to port A1.")]
        public Rhd2000ChipId? PortA1Chip { get; set; } = null;

        /// <summary>
        /// Gets or sets the chip identifier connected to port A2.
        /// </summary>
        /// <remarks>
        /// Use <see langword="null"/> when no chip is connected to port A2.
        /// </remarks>
        [Category(PortsCategory)]
        [Description("The chip connected to port A2.")]
        public Rhd2000ChipId? PortA2Chip { get; set; } = null;

        /// <summary>
        /// Gets or sets the SPI cable delay for port A.
        /// </summary>
        /// <remarks>
        /// The SPI cable delay for port A in clock cycles.
        /// </remarks>
        [Category(PortsCategory)]
        [Description("The SPI cable delay for port A.")]
        public uint PortACableDelay { get; set; } = 0;



        /// <summary>
        /// Gets or sets the chip identifier connected to port B1.
        /// </summary>
        /// <remarks>Use <see langword="null"/> when no chip is connected to port B1.</remarks>
        [Category(PortsCategory)]
        [Description("The chip connected to port B1.")]
        public Rhd2000ChipId? PortB1Chip { get; set; } = null;

        /// <summary>
        /// Gets or sets the chip identifier connected to port B2.
        /// </summary>
        /// <remarks>
        /// Use <see langword="null"/> when no chip is connected to port B2.
        /// </remarks>
        [Category(PortsCategory)]
        [Description("The chip connected to port B2.")]
        public Rhd2000ChipId? PortB2Chip { get; set; } = null;

        /// <summary>
        /// Gets or sets the SPI cable delay for port B.
        /// </summary>
        /// <remarks>
        /// The SPI cable delay for port B in clock cycles.
        /// </remarks>
        [Category(PortsCategory)]
        [Description("The SPI cable delay for port B.")]
        public uint PortBCableDelay { get; set; } = 0;

        /// <summary>
        /// Gets or sets the chip identifier connected to port C1.
        /// </summary>
        /// <remarks>Use <see langword="null"/> when no chip is connected to port C1.</remarks>
        [Category(PortsCategory)]
        [Description("The chip connected to port C1.")]
        public Rhd2000ChipId? PortC1Chip { get; set; } = null;

        /// <summary>
        /// Gets or sets the chip identifier connected to port C2.
        /// </summary>
        /// <remarks>
        /// Use <see langword="null"/> when no chip is connected to port C2.
        /// </remarks>
        [Category(PortsCategory)]
        [Description("The chip connected to port C2.")]
        public Rhd2000ChipId? PortC2Chip { get; set; } = null;

        /// <summary>
        /// Gets or sets the SPI cable delay for port C.
        /// </summary>
        /// <remarks>
        /// The SPI cable delay for port C in clock cycles.
        /// </remarks>
        [Category(PortsCategory)]
        [Description("The SPI cable delay for port C.")]
        public uint PortCCableDelay { get; set; } = 0;

        /// <summary>
        /// Gets or sets the chip identifier connected to port D1.
        /// </summary>
        /// <remarks>Use <see langword="null"/> when no chip is connected to port D1.</remarks>
        [Category(PortsCategory)]
        [Description("The chip connected to port D1.")]
        public Rhd2000ChipId? PortD1Chip { get; set; } = null;

        /// <summary>
        /// Gets or sets the chip identifier connected to port D2.
        /// </summary>
        /// <remarks>
        /// Use <see langword="null"/> when no chip is connected to port D2.
        /// </remarks>
        [Category(PortsCategory)]
        [Description("The chip connected to port D2.")]
        public Rhd2000ChipId? PortD2Chip { get; set; } = null;

        /// <summary>
        /// Gets or sets the SPI cable delay for port D.
        /// </summary>
        /// <remarks>
        /// The SPI cable delay for port D in clock cycles.
        /// </remarks>
        [Category(PortsCategory)]
        [Description("The SPI cable delay for port D.")]
        public uint PortDCableDelay { get; set; } = 0;


        int bufferSize = 32;


        /// <summary>
        /// Configures the Rhythm device.
        /// </summary>
        /// <remarks>
        /// This will schedule configuration actions to be applied by a <see cref="StartAcquisition"/> instance
        /// prior to data acquisition.
        /// </remarks>
        /// <param name="source">A sequence of <see cref="AcquisitionBoardContextTask"/> instances that holds configuration actions.</param>
        /// <returns>The original sequence modified by adding additional configuration actions required to configure the Rhythm device.</returns>
        public override IObservable<AcquisitionBoardContextTask> Process(IObservable<AcquisitionBoardContextTask> source)
        {
            RhythmBoard board = null;
            var deviceAddress = DeviceAddress;
            var deviceName = DeviceName;
            var buffersize = BufferSize;
            var chipIds = new Rhd2000ChipId?[8] { PortA1Chip, PortA2Chip, PortB1Chip, PortB2Chip, PortC1Chip, PortC2Chip, PortD1Chip, PortD2Chip };
            var portDelays = new uint[4] { PortACableDelay, PortBCableDelay, PortCCableDelay, PortDCableDelay };
            return source
                .ConfigureAndLatchDevice(context =>
                {
                    var device = context.GetDeviceContext(deviceAddress, DeviceType);
                    board = new RhythmBoard(device);
                    board.Initialize();
                    var ledSubscription = boardLed.Subscribe(value => board.SetBoardLeds(value));
                    var dividerSubscription = clockDivider.Subscribe(value => board.SetClockDivider(value));
                    UploadCommonCommands(board);
                    ChangeSampleRate(board);
                    // NB : We need the reset so the initialization commands
                    // are into effect before the scan
                    context.Reset();
                    var deviceInfo = new RhythmDeviceInfo(device, DeviceType, chipIds, buffersize);
                    return new CompositeDisposable(
                        ledSubscription,
                        dividerSubscription,
                        DeviceManager.RegisterDevice(deviceName, deviceInfo)
                        );
                }).ConfigureDirectDevice(context =>
                {
                    SetCableDelays(board, portDelays);
                    board.SetContinuousRunMode(true);
                    return Disposable.Empty;
                }).ConfigureCalibration(deviceAddress, true,
                frames => ReadChipIds(frames, chipIds),
                context =>
                {
                    board.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 0);
                    board.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 0);
                    board.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 0);
                    board.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 0);

                    return Disposable.Create(() =>
                    {
                        UpdateRegisterConfiguration(board, false);
                        board.EnableExternalFastSettle(ExternalFastSettleEnabled);
                        EnableConnectedAmplifiers(board, chipIds);
                    });
                });
        }

        static IObservable<oni.Frame> ReadChipIds(IObservable<oni.Frame> frames, Rhd2000ChipId?[] chipIds)
        {
            const int adcChannels = 35;
            var data = new ushort[RhythmBoard.MAX_DATA_STREAMS, adcChannels, initLen];
            var frameIndex = 0;
            return frames
                .Take((int)initLen)
                .Do(frame =>
                {
                    var frameData = frame.GetData<ushort>();
                    for (int result = 0; result < adcChannels; result++)
                    {
                        for (int stream = 0; stream < RhythmBoard.MAX_DATA_STREAMS; stream++)
                        {
                            data[stream, result, frameIndex] = frameData[10 + result * RhythmBoard.MAX_DATA_STREAMS + stream];
                        }
                    }
                    frameIndex++;
                },
                // NB: an exception here terminates this sequence with an error, which is forwarded by ConfigureCalibration
                () => VerifyChipIds(data, chipIds));
        }


        void ChangeSampleRate(RhythmBoard board)
        {
            board.SetSampleRate(SampleRate);


            // Set up an RHD2000 register object using this sample rate to
            // optimize MUX-related register settings.
            var sampleRate = board.GetSampleRate();
            Rhd2000Registers chipRegisters = new Rhd2000Registers(sampleRate);
            var commandList = new List<int>();


            // For the AuxCmd3 slot, we will create three command sequences.  All sequences
            // will configure and read back the RHD2000 chip registers, but one sequence will
            // also run ADC calibration.  Another sequence will enable amplifier 'fast settle'.

            // Before generating register configuration command sequences, set amplifier
            // bandwidth parameters.
            chipRegisters.SetDspCutoffFreq(DspCutoffFrequency);
            chipRegisters.SetLowerBandwidth(LowerBandwidth);
            chipRegisters.SetUpperBandwidth(UpperBandwidth);
            chipRegisters.EnableDsp(DspEnabled);

            // Upload version with ADC calibration to AuxCmd3 RAM Bank 0.
            var sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, true);
            board.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);

            // Upload version with no ADC calibration to AuxCmd3 RAM Bank 1.
            sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, false);
            board.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 1);
            board.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);

            // Upload version with fast settle enabled to AuxCmd3 RAM Bank 2.
            chipRegisters.SetFastSettle(true);
            sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, false);
            board.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 2);
            board.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);
            chipRegisters.SetFastSettle(false);

            UpdateRegisterConfiguration(board, false);
        }

        /// <summary>
        /// Uploads command lists not related to sample rate, so they only need to be set once.
        /// </summary>
        static void UploadCommonCommands(RhythmBoard board)
        {
            // Set up an RHD2000 register object using this sample rate to
            // optimize MUX-related register settings.
            var sampleRate = board.GetSampleRate();
            Rhd2000Registers chipRegisters = new Rhd2000Registers(sampleRate);
            var commandList = new List<int>();

            // Create a command list for the AuxCmd1 slot.  This command sequence will create a 250 Hz,
            // zero-amplitude sine wave (i.e., a flatline).  We will change this when we want to perform
            // impedance testing.
            var sequenceLength = chipRegisters.CreateCommandListZcheckDac(commandList, 250, 0);
            board.UploadCommandList(commandList, AuxCmdSlot.AuxCmd1, 0);
            board.SelectAuxCommandLength(AuxCmdSlot.AuxCmd1, 0, sequenceLength - 1);
            board.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd1, 0);
            board.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd1, 0);
            board.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd1, 0);
            board.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd1, 0);

            // Next, we'll create a command list for the AuxCmd2 slot.  This command sequence
            // will sample the temperature sensor and other auxiliary ADC inputs.
            sequenceLength = chipRegisters.CreateCommandListTempSensor(commandList);
            board.UploadCommandList(commandList, AuxCmdSlot.AuxCmd2, 0);
            board.SelectAuxCommandLength(AuxCmdSlot.AuxCmd2, 0, sequenceLength - 1);
            board.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd2, 0);
            board.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd2, 0);
            board.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd2, 0);
            board.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd2, 0);
        }

        static void UpdateRegisterConfiguration(RhythmBoard board, bool fastSettle)
        {
            board.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            board.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            board.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            board.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
        }

        static void EnableConnectedAmplifiers(RhythmBoard board, Rhd2000ChipId?[] chipIds)
        {
            int activeStream = 0;
            for (int chipIdx = 0; chipIdx < chipIds.Length; ++chipIdx)
            {
                var chipId = chipIds[chipIdx];
                if (chipId.HasValue)
                {
                    board.EnableDataStream(activeStream, true);
                    board.SetDataSource(activeStream, (BoardDataSource)chipIdx);
                    if (chipId.Value == Rhd2000ChipId.Rhd2164)
                    {
                        board.EnableDataStream(activeStream + 1, true);
                        board.SetDataSource(activeStream + 1, (BoardDataSource)(chipIdx + BoardDataSource.PortA1Ddr));
                        activeStream += 2;
                    }
                    else activeStream++;
                }
            }

            // Disable data streams where no chips are present.
            for (; activeStream < RhythmBoard.MAX_DATA_STREAMS; activeStream++)
            {
                board.EnableDataStream(activeStream, false);
            }
        }

        static void SetCableDelays(RhythmBoard board, uint[] portDelays)
        {
            board.SetCableDelay(BoardPort.PortA, (int)portDelays[0]);
            board.SetCableDelay(BoardPort.PortB, (int)portDelays[1]);
            board.SetCableDelay(BoardPort.PortC, (int)portDelays[2]);
            board.SetCableDelay(BoardPort.PortD, (int)portDelays[3]);
        }
        static Rhd2000ChipId? ReadDeviceId(ushort[,,] data, int stream)
        {
            // First, check ROM registers 32-36 to verify that they hold 'INTAN', and
            // the initial chip name ROM registers 24-26 that hold 'RHD'.
            // This is just used to verify that we are getting good data over the SPI
            // communication channel.

            var intanChipPresent = (
                (char)data[stream, 2, 32] == 'I' &&
                (char)data[stream, 2, 33] == 'N' &&
                (char)data[stream, 2, 34] == 'T' &&
                (char)data[stream, 2, 35] == 'A' &&
                (char)data[stream, 2, 36] == 'N' &&
                (char)data[stream, 2, 24] == 'R' &&
                (char)data[stream, 2, 25] == 'H' &&
                (char)data[stream, 2, 26] == 'D');

            // If the SPI communication is bad, return null. Otherwise, return the Intan
            // chip ID number stored in ROM register 63.
            if (!intanChipPresent)
            {
                return null;
            }

            var chipId = (Rhd2000ChipId)data[stream, 2, 19]; // chip ID (Register 63)
            if (!Enum.IsDefined(typeof(Rhd2000ChipId), chipId))
            {
                return null;
            }

            if (chipId == Rhd2000ChipId.Rhd2164)
            {
                var register59ValueA = data[stream, 2, 23];
                var register59ValueB = data[stream + RhythmBoard.MAX_DATA_STREAMS / 2, 2, 28];
                if (register59ValueA != RhythmDevice.REGISTER_59_MISO_A && register59ValueB != RhythmDevice.REGISTER_59_MISO_B)
                {
                    throw new InvalidOperationException($"Invalid MISO configuration for RHD2164 on data stream {stream}.");
                }
            }
            return chipId;
        }

        static void VerifyChipIds(ushort[,,] data, Rhd2000ChipId?[] expected)
        {
            for (int chipIdx = 0; chipIdx < expected.Length; chipIdx++)
            {
                var id = ReadDeviceId(data, chipIdx);
                if (id != expected[chipIdx])
                {
                    throw new InvalidOperationException(
                        $"Detected chip ID on port slot {chipIdx} ({(id.HasValue ? id.Value.ToString() : "none")}) does not match the configured chip ({(expected[chipIdx].HasValue ? expected[chipIdx].Value.ToString() : "none")}).");
                }
            }
        }

    }

    static class RhythmDevice
    {
        public const int ID = 65537;
        public const uint MinimumVersion = 1;

        internal const uint ENABLE = 0;
        internal const uint MODE = 1;
        internal const uint MAX_TIMESTEP = 2;
        internal const uint CABLE_DELAY = 3;
        internal const uint AUXCMD_BANK_1 = 4;
        internal const uint AUXCMD_BANK_2 = 5;
        internal const uint AUXCMD_BANK_3 = 6;
        internal const uint MAX_AUXCMD_INDEX_1 = 7;
        internal const uint MAX_AUXCMD_INDEX_2 = 8;
        internal const uint MAX_AUXCMD_INDEX_3 = 9;
        internal const uint LOOP_AUXCMD_INDEX_1 = 10;
        internal const uint LOOP_AUXCMD_INDEX_2 = 11;
        internal const uint LOOP_AUXCMD_INDEX_3 = 12;
        internal const uint DATA_STREAM_1_8_SEL = 13;
        internal const uint DATA_STREAM_9_16_SEL = 14;
        internal const uint DATA_STREAM_EN = 15;
        internal const uint EXTERNAL_FAST_SETTLE = 16;
        internal const uint EXTERNAL_DIGOUT_A = 17;
        internal const uint EXTERNAL_DIGOUT_B = 18;
        internal const uint EXTERNAL_DIGOUT_C = 19;
        internal const uint EXTERNAL_DIGOUT_D = 20;
        internal const uint SYNC_CLKOUT_DIVIDE = 21;
        internal const uint DAC_CTL = 22;
        internal const uint DAC_SEL_1 = 23;
        internal const uint DAC_SEL_2 = 24;
        internal const uint DAC_SEL_3 = 25;
        internal const uint DAC_SEL_4 = 26;
        internal const uint DAC_SEL_5 = 27;
        internal const uint DAC_SEL_6 = 28;
        internal const uint DAC_SEL_7 = 29;
        internal const uint DAC_SEL_8 = 30;
        internal const uint DAC_THRESH_1 = 31;
        internal const uint DAC_THRESH_2 = 32;
        internal const uint DAC_THRESH_3 = 33;
        internal const uint DAC_THRESH_4 = 34;
        internal const uint DAC_THRESH_5 = 35;
        internal const uint DAC_THRESH_6 = 36;
        internal const uint DAC_THRESH_7 = 37;
        internal const uint DAC_THRESH_8 = 38;
        internal const uint HPF = 39;
        internal const uint SPI_RUNNING = 40;

        internal const uint COMMAND_MEM_BASE = 0x4000;

        internal const ulong MAGIC_NUMBER = 0xc691199927021942;

        internal const int REGISTER_59_MISO_A = 53;
        internal const int REGISTER_59_MISO_B = 58;

        public const int AdcChannels = 8;

        internal class NameConverter : DeviceNameConverter
        {
            public NameConverter()
                : base(typeof(RhythmDevice)) { }
        }
    }

    class RhythmDeviceInfo : DeviceInfo
    {
        public RhythmDeviceInfo(DeviceContext device, Type deviceType, Rhd2000ChipId?[] chipIds, int bufferSize)
            : base(device, deviceType)
        {
            ChipIds = chipIds;
            BufferSize = bufferSize;
        }

        public Rhd2000ChipId?[] ChipIds { get; }

        public int BufferSize { get; }
    }

    /// <summary>
    /// Identifies the RHD2000 chip and the value of the chip ID register.
    /// </summary>
    public enum Rhd2000ChipId
    {
        /// <summary>
        /// Represents a RHD2132 chip, which has 32 single-channel amplifier channels.
        /// </summary>
        Rhd2132 = 1,
        /// <summary>
        /// Represents a RHD2216 chip, which has 16 dual-channel amplifier channels.
        /// </summary>
        Rhd2216 = 2,
        /// <summary>
        /// Represents a RHD2164 chip, which has 64 single-channel amplifier channels.
        /// </summary>
        Rhd2164 = 4
    }


}
