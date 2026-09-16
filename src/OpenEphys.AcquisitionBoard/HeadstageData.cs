using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Bonsai;
using OpenEphys.Onix1;
using OpenCV.Net;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Produces a sequence of <see cref="HeadstageDataFrame"/> values from a specified acquisition board headstage port.
    /// </summary>
    [Description("Produces a sequence of AcquisitionBoardHeadstageDataFrame values from a specified acquisition board headstage port.")]
    public class HeadstageData : Source<HeadstageDataFrame>
    {
        /// <inheritdoc cref = "SingleDeviceFactory.DeviceName"/>
        [TypeConverter(typeof(RhythmDevice.NameConverter))]
        [Description("The name of the acquisition board device to acquire data from.")]
        public string DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the headstage port to acquire data from.
        /// </summary>
        [Description("The headstage port or ports to acquire data from.")]
        public HeadstagePort Port { get; set; } = HeadstagePort.AllConnected;

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
        public int BufferSize
        {
            get => bufferSize;
            set => bufferSize = 4*((value+3)/4);
        }

        int bufferSize = 32;
        public override IObservable<HeadstageDataFrame> Generate()
        {
            var amplifierBufferSize = bufferSize;
            var auxBufferSize = bufferSize/4;
            return DeviceManager.GetDevice(DeviceName).SelectMany(
                deviceInfo =>
                {
                    var selectedPort = Port;
                    var rhythmInfo = (RhythmDeviceInfo)deviceInfo;
                    var chipIds = rhythmInfo.ChipIds;
                    var frameData = rhythmInfo.Context.GetDeviceFrames(rhythmInfo.DeviceAddress);
                    return Observable.Create<HeadstageDataFrame>(observer =>
                    {

                        // Parse for AllConnected
                        HeadstagePort effectivePort = selectedPort;
                        if (selectedPort.HasFlag(HeadstagePort.AllConnected))
                        {
                            effectivePort &= ~HeadstagePort.AllConnected;
                            for (int i = 0; i < chipIds.Length; i++)
                            {
                                if (chipIds[i] != RhythmDevice.RhdChipId.None)
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
                            if (chipId == RhythmDevice.RhdChipId.None)
                            {
                                if (isPortSelected)
                                {
                                    throw new InvalidOperationException($"Headstage port {currentPort} is selected but no chip is connected.");
                                }
                                continue;
                            }

                            totalStreams += GetStreamsPerChip(chipId);
                            if (isPortSelected)
                            {
                                totalAuxRows += 3;
                                totalAuxIndexes += 1;
                                totalAmplifierRows += GetNumChannelsPerChip(chipId);
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

                            if (chipId != RhythmDevice.RhdChipId.None)
                            {
                                var currentPort = (HeadstagePort)(1 << i);
                                var isPortSelected = effectivePort.HasFlag(currentPort);
                                var streamChannels = GetNumChannelsPerStream(chipId);
                                var numStreams = GetStreamsPerChip(chipId);
                                if (isPortSelected)
                                {
                                    // NB : Only AuxCmd2
                                    auxOffsets[auxIndex++] = totalStreams + currentStream ;
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
                                var auxCmd = ((payload->SampleCount+3)%4);
                                if (auxCmd != 3)
                                {
                                    for (int i = 0; i < totalAuxIndexes; i++)
                                    {
                                        auxBuffer[i*3+auxCmd, sampleIndex/4] = frameData[auxOffsets[i]];
                                    }
                                }
                                for (int i = 0; i < totalAmplifierRows; i++)
                                {
                                    amplifierBuffer[i, sampleIndex] = frameData[amplifierOffsets[i]];
                                }
                            }
                            if(++sampleIndex >= amplifierBufferSize)
                            {
                                var amplifierData = Mat.FromArray(amplifierBuffer);
                                var auxData = Mat.FromArray(auxBuffer);

                                var headstageFrame = new HeadstageDataFrame(clockBuffer, hubClockBuffer, sampleCountBuffer, amplifierData, auxData);
                                observer.OnNext(headstageFrame);
                                auxBuffer = new ushort[totalAuxRows, auxBufferSize];
                                amplifierBuffer = new ushort[totalAmplifierRows, amplifierBufferSize];
                                clockBuffer = new ulong[amplifierBufferSize];
                                sampleCountBuffer = new uint [amplifierBufferSize];
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

        internal static int GetStreamsPerChip(RhythmDevice.RhdChipId chipId)
        {
            return chipId switch
            {
                RhythmDevice.RhdChipId.Rhd2164 => 2,
                RhythmDevice.RhdChipId.Rhd2132 => 1,
                RhythmDevice.RhdChipId.Rhd2216 => 1,
                _ => throw new InvalidEnumArgumentException("Invalid chip ID: " + chipId),
            };
        }

        internal static int GetNumChannelsPerChip(RhythmDevice.RhdChipId chipId)
        {
            return chipId switch
            {
                RhythmDevice.RhdChipId.Rhd2164 => 64,
                RhythmDevice.RhdChipId.Rhd2132 => 32,
                RhythmDevice.RhdChipId.Rhd2216 => 16,
                _ => throw new InvalidEnumArgumentException("Invalid chip ID: " + chipId),
            };
        }

        internal static int GetNumChannelsPerStream(RhythmDevice.RhdChipId chipId)
        {
            return chipId switch
            {
                RhythmDevice.RhdChipId.Rhd2164 => 32,
                RhythmDevice.RhdChipId.Rhd2132 => 32,
                RhythmDevice.RhdChipId.Rhd2216 => 16,
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
        PortA1 = 1 << 0,
        /// <summary>
        /// Indicates that headstage port A2 is selected.
        /// </summary>
        PortA2 = 1 << 1,
        /// <summary>
        /// Indicates that headstage port B1 is selected.
        /// </summary>
        PortB1 = 1 << 2,
        /// <summary>
        /// Indicates that headstage port B2 is selected.
        /// </summary>
        PortB2 = 1 << 3,
        /// <summary>
        /// Indicates that headstage port C1 is selected.
        /// </summary>
        PortC1 = 1 << 4,
        /// <summary>
        /// Indicates that headstage port C2 is selected.
        /// </summary>
        PortC2 = 1 << 5,
        /// <summary>
        /// Indicates that headstage port D1 is selected.
        /// </summary>
        PortD1 = 1 << 6,
        /// <summary>
        /// Indicates that headstage port D2 is selected.
        /// </summary>
        PortD2 = 1 << 7,
        /// <summary>
        /// Indicates that all headstage ports are selected.
        /// </summary>
        All = PortA1 | PortA2 | PortB1 | PortB2 | PortC1 | PortC2 | PortD1 | PortD2,
        /// <summary>
        /// Indicates that all connected headstage ports are selected.
        /// </summary>
        AllConnected = 1 <<16
    }
}
