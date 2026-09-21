using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Bonsai;
using OpenEphys.Onix1;
using Rhythm.Net;

namespace OpenEphys.AcquisitionBoard
{
    internal class ConfigureRhythmDevice : AcquisitionBoardSingleDeviceFactory
    {
        public ConfigureRhythmDevice() :
            base(typeof(RhythmDevice))
        {
        }
        const uint initLen = 64;



        BehaviorSubject<bool> boardLed = new(true);

        /// <summary>
        /// Gets or sets a value indicating the state of the on-board LEDs.
        /// </summary>
        /// <value><c>true</c> if the LEDs are enabled; otherwise, <c>false</c>.</value>
        [Category(AcquisitionCategory)]
        [Description("Board LED status")]
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
        /// Gets or sets the index of the board to open.
        /// </summary>
        /// <value>
        /// An integer representing the target board index, ranging from -1 to 100.
        /// Set to -1 to automatically open the first available board.
        /// </value>
        [Category(ConfigurationCategory)]
        [Range(-1, 100)]
        [Description("The index of the board to open (-1 to open the first available board).")]
        public int BoardIndex { get; set; } = -1;

        /// <summary>
        /// Gets or sets the per-channel sampling rate of the amplifier.
        /// </summary>
        /// <value>A <see cref="AmplifierSampleRate"/> value defining the sampling frequency.</value>
        [Category(ConfigurationCategory)]
        [Description("The per-channel sampling rate.")]
        public AmplifierSampleRate SampleRate { get; set; }

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
        /// It is a multiple of 4 to be able to accomodate
        /// the aux data which is sampled at fs/4.
        /// </remarks>
        [Description("Number of samples that are collected before data is propagated.")]
        [Category(ConfigurationCategory)]
        public int BufferSize
        {
            get => bufferSize;
            set => bufferSize = 4 * ((value + 3) / 4);
        }

        int bufferSize = 32;


        public override IObservable<AcquisitionBoardContextTask> Process(IObservable<AcquisitionBoardContextTask> source)
        {
            RhythmBoard board = null;
            var deviceAddress = DeviceAddress;
            var deviceName = DeviceName;
            var buffersize = BufferSize;
            return source
                .ConfigureAndLatchDevice(context =>
                {
                    var device = context.GetDeviceContext(deviceAddress, DeviceType);
                    board = new RhythmBoard(device);
                    board.Initialize();
                    var ledSubscription = boardLed.Subscribe(value => board.SetBoardLeds(value));
                    UploadCommonCommands(board);
                    ChangeSampleRate(board);
                    // NB : We need the reset so the initialization commands
                    // are into effect before the scan
                    context.Reset();
                    Console.WriteLine($"Connected streams : {board.GetNumEnabledDataStreams()}");
                    var chipIds = ScanConnectedAmplifiers(board, context, deviceAddress);
                    var deviceInfo = new RhythmDeviceInfo(device, DeviceType, chipIds, buffersize);
                    return new CompositeDisposable(
                        ledSubscription,
                        DeviceManager.RegisterDevice(deviceName, deviceInfo)
                        );
                }).ConfigureDirectDevice(context =>
                {
                    RunCalibration(board, context);
                    board.EnableExternalFastSettle(ExternalFastSettleEnabled);
                    board.SetContinuousRunMode(true);
                    return Disposable.Empty;
                });
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

        static void RunCalibration(RhythmBoard board, AcquisitionBoardContextTask context)
        {
            // Select RAM Bank 0 for AuxCmd3 initially, so the ADC is calibrated.
            board.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 0);

            // Since our longest command sequence is 60 commands, we run the SPI
            // interface for 60 samples 
            board.SetMaxTimeStep(initLen);
            board.SetContinuousRunMode(false);

            // Run ADC calibration command sequence
            using (var acquisitionScope = context.StartTemporaryAcquisition())
            {
                // Ignore frames until the command sequence has completed.
                // the command sequence might be smaller than the BlockReadSize
                // so we rely on the buffer and just discard all frames until
                // the Rhythm state machine stops running.
                while (board.IsRunning()) ;
            }

            // Now that ADC calibration has been performed, we switch to the command sequence
            // that does not execute ADC calibration.
            UpdateRegisterConfiguration(board, false);
        }

        static void UpdateRegisterConfiguration(RhythmBoard board, bool fastSettle)
        {
            board.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            board.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            board.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            board.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
        }

        static RhythmDevice.RhdChipId[] ScanConnectedAmplifiers(RhythmBoard board, AcquisitionBoardContextTask context, uint deviceAddress)
        {

            // Select RAM Bank 0 for AuxCmd3
            board.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 0);
            board.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 0);

            //We run this for long enough to get enough buffers in the
            board.SetMaxTimeStep(128 * initLen);
            board.SetContinuousRunMode(false);

            // Run SPI command sequence at all 16 possible FPGA MISO delay settings
            // to find optimum delay for each SPI interface cable.
            var maxNumChips = 8;
            var chipId = new RhythmDevice.RhdChipId[maxNumChips];
            var optimumDelays = new int[maxNumChips];
            var secondDelays = new int[maxNumChips];
            var goodDelayCounts = new int[maxNumChips];
            for (int i = 0; i < optimumDelays.Length; i++)
            {
                optimumDelays[i] = -1;
                secondDelays[i] = -1;
                goodDelayCounts[i] = 0;
            }

            for (int delay = 0; delay < 16; delay++)
            {
                Console.WriteLine("Testing MISO delay setting {0}...", delay);
                board.SetCableDelay(BoardPort.PortA, delay);
                board.SetCableDelay(BoardPort.PortB, delay);
                board.SetCableDelay(BoardPort.PortC, delay);
                board.SetCableDelay(BoardPort.PortD, delay);

                const int adcChannels = 35;
                ushort[,,] data = new ushort[RhythmBoard.MAX_DATA_STREAMS, adcChannels, initLen];
                // Run SPI command sequence
                using (var acquisitionScope = context.StartTemporaryAcquisition())
                {
                    for (int frameIndex = 0; frameIndex < initLen; frameIndex++)
                    {
                        acquisitionScope.ProcessNextFrame(frame =>
                        {
                            var frameData = frame.GetData<ushort>();
                            for (int result = 0; result < adcChannels; result++)
                            {
                                for (int stream = 0; stream < RhythmBoard.MAX_DATA_STREAMS; stream++)
                                {
                                    data[stream, result, frameIndex] = frameData[10 + result * RhythmBoard.MAX_DATA_STREAMS + stream];
                                }
                            }
                        }, deviceAddress);
                    }
                }

                // Read the Intan chip ID number from each RHD2000 chip found.
                // Record delay settings that yield good communication with the chip.
                for (int chipIdx = 0; chipIdx < chipId.Length; chipIdx++)
                {
                    var id = ReadDeviceId(data, chipIdx);
                    if (id > 0)
                    {
                        chipId[chipIdx] = id;
                        goodDelayCounts[chipIdx]++;
                        switch (goodDelayCounts[chipIdx])
                        {
                            case 1: optimumDelays[chipIdx] = delay; break;
                            case 2: secondDelays[chipIdx] = delay; break;
                            case 3: optimumDelays[chipIdx] = secondDelays[chipIdx]; break;
                        }
                    }
                }
            }

            // Now that we know which RHD2000 amplifier chips are plugged into each SPI port,
            // add up the total number of amplifier channels on each port and calculate the number
            // of data streams necessary to convey this data over the USB interface.
            var numStreamsRequired = 0;
            var rhd2216ChipPresent = false;
            for (int chipIdx = 0; chipIdx < chipId.Length; ++chipIdx)
            {
                switch (chipId[chipIdx])
                {
                    case RhythmDevice.RhdChipId.Rhd2216:
                        numStreamsRequired++;
                        rhd2216ChipPresent = true;
                        break;
                    case RhythmDevice.RhdChipId.Rhd2132:
                        numStreamsRequired++;
                        break;
                    case RhythmDevice.RhdChipId.Rhd2164:
                        numStreamsRequired += 2;
                        break;
                    default:
                        break;
                }
            }

            // Reconfigure USB data streams in consecutive order to accommodate all connected chips.
            int activeStream = 0;
            for (int chipIdx = 0; chipIdx < chipId.Length; ++chipIdx)
            {
                if (chipId[chipIdx] > 0)
                {
                    Console.WriteLine("Found hardware device with chip ID {0} on SPI port {1}.", chipId[chipIdx], chipIdx);
                    board.EnableDataStream(activeStream, true);
                    board.SetDataSource(activeStream, (BoardDataSource)chipIdx);
                    if (chipId[chipIdx] == RhythmDevice.RhdChipId.Rhd2164)
                    {
                        board.EnableDataStream(activeStream + 1, true);
                        board.SetDataSource(activeStream + 1, (BoardDataSource)(chipIdx + BoardDataSource.PortA1Ddr));
                        activeStream += 2;
                    }
                    else activeStream++;
                }
                else optimumDelays[chipIdx] = 0;
            }

            // Now, disable data streams where we did not find chips present.
            for (; activeStream < RhythmBoard.MAX_DATA_STREAMS; activeStream++)
            {
                board.EnableDataStream(activeStream, false);
            }
            // Set cable delay settings that yield good communication with each
            // RHD2000 chip.
            var optimumDelayA = Math.Max(optimumDelays[0], optimumDelays[1]);
            var optimumDelayB = Math.Max(optimumDelays[2], optimumDelays[3]);
            var optimumDelayC = Math.Max(optimumDelays[4], optimumDelays[5]);
            var optimumDelayD = Math.Max(optimumDelays[6], optimumDelays[7]);
            board.SetCableDelay(BoardPort.PortA, optimumDelayA);
            board.SetCableDelay(BoardPort.PortB, optimumDelayB);
            board.SetCableDelay(BoardPort.PortC, optimumDelayC);
            board.SetCableDelay(BoardPort.PortD, optimumDelayD);
            return chipId;
        }

        static RhythmDevice.RhdChipId ReadDeviceId(ushort[,,] data, int stream)
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

            // If the SPI communication is bad, return -1.  Otherwise, return the Intan
            // chip ID number stored in ROM regstier 63.
            if (!intanChipPresent)
            {
                return RhythmDevice.RhdChipId.None;
            }
            else
            {
                RhythmDevice.RhdChipId chipId = (RhythmDevice.RhdChipId)data[stream, 2, 19]; // chip ID (Register 63)
                if (chipId == RhythmDevice.RhdChipId.Rhd2164)
                {
                    var register59ValueA = data[stream, 2, 23];
                    var register59ValueB = data[stream + RhythmBoard.MAX_DATA_STREAMS / 2, 2, 28];
                    if (register59ValueA != RhythmDevice.REGISTER_59_MISO_A && register59ValueB != RhythmDevice.REGISTER_59_MISO_B)
                    {
                        return RhythmDevice.RhdChipId.None; // Invalid MISO configuration for RHD2164
                    }
                }
                return chipId;
            }
        }

    }

    public static class RhythmDevice
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

        internal enum RhdChipId : int
        {
            None = 0,
            Rhd2132 = 1,
            Rhd2216 = 2,
            Rhd2164 = 4
        }
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
        public RhythmDeviceInfo(DeviceContext device, Type deviceType, RhythmDevice.RhdChipId[] chipIds, int bufferSize)
            : base(device, deviceType)
        {
            ChipIds = chipIds;
            BufferSize = bufferSize;
        }

        public RhythmDevice.RhdChipId[] ChipIds { get; }

        public int BufferSize { get; }
    }


}
