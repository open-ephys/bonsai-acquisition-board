using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Bonsai;
using OpenCV.Net;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Produces a sequence of <see cref="HeadstageDataFrame"/> objects with data from the amplifier chips
    /// connected to the acquisition board headstage ports.
    /// </summary>
    /// <remarks>
    /// This data IO operator must be linked to an appropriate configuration, such as a <see
    /// cref="ConfigureRhythmDevice"/>, using a shared <c>DeviceName</c>. The number of samples in each frame
    /// is determined by <see cref="ConfigureRhythmDevice.BufferSize"/>.
    /// </remarks>
    [Description("Produces a sequence of HeadstageDataFrame objects with data from the amplifier chips connected to the acquisition board headstage ports.")]
    public class HeadstageData : Source<HeadstageDataFrame>
    {
        /// <inheritdoc cref = "SingleDeviceFactory{TContext}.DeviceName"/>
        [TypeConverter(typeof(RhythmDevice.NameConverter))]
        [Description(DeviceFactoryStrings.DeviceNameDescription)]
        [Category(DeviceFactoryStrings.ConfigurationCategory)]
        public string DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the headstage ports to acquire data from.
        /// </summary>
        /// <remarks>
        /// Each headstage port (A to D) can hold up to two amplifier chips (e.g. <see
        /// cref="HeadstagePort.PortA1"/> and <see cref="HeadstagePort.PortA2"/>) when in dual MISO mode, or a
        /// single one when in MISO + I2C mode. Multiple ports can be combined, in which case the channels of
        /// all selected chips are concatenated in port order. If <see cref="HeadstagePort.AllConnected"/> is
        /// selected, data is acquired from every chip detected during configuration. Selecting a port with
        /// no detected chip will result in an error.
        /// </remarks>
        [Description("The headstage port or ports to acquire data from.")]
        [Category(DeviceFactoryStrings.ConfigurationCategory)]
        public HeadstagePort Port { get; set; } = HeadstagePort.AllConnected;

        /// <summary>
        /// Generates a sequence of <see cref="HeadstageDataFrame"/> objects, each of which is a buffered set
        /// of multichannel samples from the selected headstage ports.
        /// </summary>
        /// <returns>A sequence of <see cref="HeadstageDataFrame"/> objects.</returns>
        public override IObservable<HeadstageDataFrame> Generate()
        {
            return DeviceManager.GetDevice(DeviceName).SelectMany(
                deviceInfo =>
                {
                    var selectedPort = Port;
                    var rhythmInfo = (RhythmDeviceInfo)deviceInfo;
                    var chipIds = rhythmInfo.ChipIds;
                    var frameData = rhythmInfo.Context.GetDeviceFrames(rhythmInfo.DeviceAddress);
                    var amplifierBufferSize = rhythmInfo.BufferSize;
                    var auxBufferSize = rhythmInfo.BufferSize / 4;
                    return Observable.Create<HeadstageDataFrame>(observer =>
                    {

                        // Parse for AllConnected
                        HeadstagePort effectivePort = selectedPort;
                        if (selectedPort.HasFlag(HeadstagePort.AllConnected))
                        {
                            effectivePort &= ~HeadstagePort.AllConnected;
                            for (int i = 0; i < chipIds.Length; i++)
                            {
                                if (chipIds[i].HasValue)
                                {
                                    effectivePort |= (HeadstagePort)(1 << i);
                                }
                            }
                        }

                        // Precalculate matrix sizes
                        int totalStreams = 0;
                        int totalAuxRows = 0;
                        int totalAuxIndexes = 0;
                        int totalAmplifierRows = 0;

                        for (int i = 0; i < chipIds.Length; i++)
                        {
                            var chipId = chipIds[i];
                            var currentPort = (HeadstagePort)(1 << i);
                            bool isPortSelected = effectivePort.HasFlag(currentPort);
                            if (!chipId.HasValue)
                            {
                                if (isPortSelected)
                                {
                                    throw new InvalidOperationException($"Headstage port {currentPort} is selected but no chip is connected.");
                                }
                                continue;
                            }

                            totalStreams += GetStreamsPerChip(chipId.Value);
                            if (isPortSelected)
                            {
                                totalAuxRows += 3;
                                totalAuxIndexes += 1;
                                totalAmplifierRows += GetNumChannelsPerChip(chipId.Value);
                            }
                        }
                        int[] auxOffsets = new int[totalAuxIndexes];
                        int[] amplifierOffsets = new int[totalAmplifierRows];
                        int currentStream = 0;
                        int auxIndex = 0;
                        int amplifierIndex = 0;

                        // index array build
                        for (int i = 0; i < chipIds.Length; i++)
                        {
                            var chipId = chipIds[i];

                            if (chipId.HasValue)
                            {
                                var currentPort = (HeadstagePort)(1 << i);
                                var isPortSelected = effectivePort.HasFlag(currentPort);
                                var streamChannels = GetNumChannelsPerStream(chipId.Value);
                                var numStreams = GetStreamsPerChip(chipId.Value);
                                if (isPortSelected)
                                {
                                    // NB : Only AuxCmd2
                                    auxOffsets[auxIndex++] = totalStreams + currentStream;
                                    for (int s = 0; s < numStreams; s++)
                                    {
                                        for (int c = 0; c < streamChannels; c++)
                                        {
                                            amplifierOffsets[amplifierIndex++] = (3 + c) * totalStreams + (currentStream + s);
                                        }
                                    }
                                }
                                currentStream += numStreams;
                            }
                        }
                        ushort[,] auxBuffer = new ushort[totalAuxRows, auxBufferSize];
                        ushort[,] amplifierBuffer = new ushort[totalAmplifierRows, amplifierBufferSize];
                        ulong[] clockBuffer = new ulong[amplifierBufferSize];
                        uint[] sampleCountBuffer = new uint[amplifierBufferSize];
                        ulong[] hubClockBuffer = new ulong[amplifierBufferSize];
                        int sampleIndex = 0;
                        var frameObserver = Observer.Create<oni.Frame>(frame =>
                        {
                            unsafe
                            {
                                var payload = (RhythmPayload*)frame.Data.ToPointer();
                                CheckMagic(payload->Magic);
                                clockBuffer[sampleIndex] = frame.Clock;
                                hubClockBuffer[sampleIndex] = payload->HubClock;
                                sampleCountBuffer[sampleIndex] = payload->SampleCount;

                                var frameData = (ushort*)((byte*)payload + sizeof(RhythmPayload));
                                var auxCmd = ((payload->SampleCount + 3) % 4);
                                if (auxCmd != 3)
                                {
                                    for (int i = 0; i < totalAuxIndexes; i++)
                                    {
                                        auxBuffer[i * 3 + auxCmd, sampleIndex / 4] = frameData[auxOffsets[i]];
                                    }
                                }
                                for (int i = 0; i < totalAmplifierRows; i++)
                                {
                                    amplifierBuffer[i, sampleIndex] = frameData[amplifierOffsets[i]];
                                }
                            }
                            if (++sampleIndex >= amplifierBufferSize)
                            {
                                var amplifierData = Mat.FromArray(amplifierBuffer);
                                var auxData = Mat.FromArray(auxBuffer);

                                var headstageFrame = new HeadstageDataFrame(clockBuffer, hubClockBuffer, sampleCountBuffer, amplifierData, auxData);
                                observer.OnNext(headstageFrame);
                                auxBuffer = new ushort[totalAuxRows, auxBufferSize];
                                amplifierBuffer = new ushort[totalAmplifierRows, amplifierBufferSize];
                                clockBuffer = new ulong[amplifierBufferSize];
                                sampleCountBuffer = new uint[amplifierBufferSize];
                                hubClockBuffer = new ulong[amplifierBufferSize];
                                sampleIndex = 0;
                            }

                        }, observer.OnError, observer.OnCompleted);
                        return frameData.Subscribe(frameObserver);
                    });
                });
        }

        /// <summary>
        /// Checks the magic number of the Rhythm data frame to ensure it is valid.
        /// </summary>
        /// <param name="magic">The magic number to check.</param>
        /// <exception cref="InvalidOperationException">Thrown if the magic number is invalid.</exception>
        static void CheckMagic(ulong magic)
        {
            if (magic != RhythmDevice.MAGIC_NUMBER)
            {
                throw new InvalidOperationException("Invalid Rhythm data frame");
            }
        }

        internal static int GetStreamsPerChip(Rhd2000ChipId chipId)
        {
            return chipId switch
            {
                Rhd2000ChipId.Rhd2164 => 2,
                Rhd2000ChipId.Rhd2132 => 1,
                Rhd2000ChipId.Rhd2216 => 1,
                _ => throw new InvalidEnumArgumentException("Invalid chip ID: " + chipId),
            };
        }

        internal static int GetNumChannelsPerChip(Rhd2000ChipId chipId)
        {
            return chipId switch
            {
                Rhd2000ChipId.Rhd2164 => 64,
                Rhd2000ChipId.Rhd2132 => 32,
                Rhd2000ChipId.Rhd2216 => 16,
                _ => throw new InvalidEnumArgumentException("Invalid chip ID: " + chipId),
            };
        }

        internal static int GetNumChannelsPerStream(Rhd2000ChipId chipId)
        {
            return chipId switch
            {
                Rhd2000ChipId.Rhd2164 => 32,
                Rhd2000ChipId.Rhd2132 => 32,
                Rhd2000ChipId.Rhd2216 => 16,
                _ => throw new InvalidEnumArgumentException("Invalid chip ID: " + chipId),
            };
        }
    }

    /// <summary>
    /// Specifies the headstage ports that can be selected for data acquisition.
    /// </summary>
    [Flags]
    public enum HeadstagePort
    {
        /// <summary>
        /// Indicates that no headstage ports are selected.
        /// </summary>
        None = 0,
        /// <summary>
        /// Indicates that headstage port A1 is selected.
        /// </summary>
        A1 = 1 << 0,
        /// <summary>
        /// Indicates that headstage port A2 is selected.
        /// </summary>
        A2 = 1 << 1,
        /// <summary>
        /// Indicates that headstage port B1 is selected.
        /// </summary>
        B1 = 1 << 2,
        /// <summary>
        /// Indicates that headstage port B2 is selected.
        /// </summary>
        B2 = 1 << 3,
        /// <summary>
        /// Indicates that headstage port C1 is selected.
        /// </summary>
        C1 = 1 << 4,
        /// <summary>
        /// Indicates that headstage port C2 is selected.
        /// </summary>
        C2 = 1 << 5,
        /// <summary>
        /// Indicates that headstage port D1 is selected.
        /// </summary>
        D1 = 1 << 6,
        /// <summary>
        /// Indicates that headstage port D2 is selected.
        /// </summary>
        D2 = 1 << 7,
        /// <summary>
        /// Indicates that all headstage ports are selected.
        /// </summary>
        All = A1 | A2 | B1 | B2 | C1 | C2 | D1 | D2,
        /// <summary>
        /// Indicates that all connected headstage ports are selected.
        /// </summary>
        AllConnected = 1 << 16
    }
}
