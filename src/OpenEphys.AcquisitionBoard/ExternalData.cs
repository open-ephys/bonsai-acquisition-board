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
    /// Produces a sequence of <see cref="ExternalDataFrame"/> objects with data from the analog and
    /// digital inputs of the acquisition board.
    /// </summary>
    /// <remarks>
    /// This data IO operator must be linked to an appropriate configuration, such as a <see
    /// cref="ConfigureRhythmDevice"/>, using a shared <c>DeviceName</c>. The analog and digital inputs are
    /// sampled synchronously with the headstage amplifier data, and the number of samples in each frame is
    /// determined by <see cref="ConfigureRhythmDevice.BufferSize"/>.
    /// </remarks>
    [Description("Produces a sequence of ExternalDataFrame objects with data from the analog and digital inputs of the acquisition board.")]
    public class ExternalData : Source<ExternalDataFrame>
    {
        /// <inheritdoc cref = "SingleDeviceFactory{TContext}.DeviceName"/>
        [TypeConverter(typeof(RhythmDevice.NameConverter))]
        [Description(DeviceFactoryStrings.DeviceNameDescription)]
        [Category(DeviceFactoryStrings.ConfigurationCategory)]
        public string DeviceName { get; set; }

        /// <summary>
        /// Generates a sequence of <see cref="ExternalDataFrame"/> objects, each of which is a buffered set
        /// of analog and digital input samples.
        /// </summary>
        /// <returns>A sequence of <see cref="ExternalDataFrame"/> objects.</returns>
        public override IObservable<ExternalDataFrame> Generate()
        {
            return DeviceManager.GetDevice(DeviceName).SelectMany(
                deviceInfo =>
                {
                    var rhythmInfo = (RhythmDeviceInfo)deviceInfo;
                    var chipIds = rhythmInfo.ChipIds;
                    var frameData = rhythmInfo.Context.GetDeviceFrames(rhythmInfo.DeviceAddress);
                    var bufferSize = rhythmInfo.BufferSize;
                    return Observable.Create<ExternalDataFrame>(observer =>
                    {
                        int activeStreams = 0;
                        foreach (var chipId in chipIds)
                        {
                            if (chipId.HasValue)
                            {
                                activeStreams += HeadstageData.GetStreamsPerChip(chipId.Value);
                            }
                        }
                        int adcOffset = 36 * activeStreams * sizeof(ushort);

                        TTLState[] ttlBuffer = new TTLState[bufferSize];
                        ushort[,] adcBuffer = new ushort[RhythmDevice.AdcChannels, bufferSize];
                        ulong[] clockBuffer = new ulong[bufferSize];
                        ulong[] hubClockBuffer = new ulong[bufferSize];
                        uint[] sampleCountBuffer = new uint[bufferSize];
                        int sampleIndex = 0;
                        var frameObserver = Observer.Create<oni.Frame>(frame =>
                        {
                            unsafe
                            {
                                var payload = (RhythmPayload*)frame.Data.ToPointer();
                                clockBuffer[sampleIndex] = frame.Clock;
                                hubClockBuffer[sampleIndex] = payload->HubClock;
                                sampleCountBuffer[sampleIndex] = payload->SampleCount;

                                var frameData = (ushort*)((byte*)payload + sizeof(RhythmPayload) + adcOffset);
                                for (int channel = 0; channel < RhythmDevice.AdcChannels; channel++)
                                {
                                    adcBuffer[channel, sampleIndex] = frameData[channel];
                                }
                                ttlBuffer[sampleIndex] = (TTLState)frameData[RhythmDevice.AdcChannels];
                            }
                            if (++sampleIndex >= bufferSize)
                            {
                                var adcData = Mat.FromArray(adcBuffer);
                                var externalDataFrame = new ExternalDataFrame(clockBuffer, hubClockBuffer, sampleCountBuffer, adcData, ttlBuffer);
                                observer.OnNext(externalDataFrame);
                                ttlBuffer = new TTLState[bufferSize];
                                adcBuffer = new ushort[RhythmDevice.AdcChannels, bufferSize];
                                clockBuffer = new ulong[bufferSize];
                                hubClockBuffer = new ulong[bufferSize];
                                sampleCountBuffer = new uint[bufferSize];
                                sampleIndex = 0;
                            }

                        }, observer.OnError, observer.OnCompleted);
                        return frameData.Subscribe(frameObserver);
                    });
                });
        }
    }
}
