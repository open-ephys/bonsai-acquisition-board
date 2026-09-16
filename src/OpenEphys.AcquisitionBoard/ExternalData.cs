using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Bonsai;
using Bonsai.Reactive;
using OpenEphys.Onix1;
using OpenCV.Net;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Represents an operator that acquires external analog and digital data from the acquisition board.
    /// </summary>
    [Description("Acquires external analog and digital data from the acquisition board.")]
    public class ExternalData : Source<ExternalDataFrame>
    {
        /// <inheritdoc cref = "SingleDeviceFactory.DeviceName"/>
        [TypeConverter(typeof(RhythmDevice.NameConverter))]
        [Description("The name of the acquisition board device to acquire data from.")]
        public string DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the buffer size.
        /// </summary>
        /// <remarks>
        /// This property determines the number of samples that are collected
        /// before data is propagated.
        /// </remarks>
        [Description("Number of samples that are collected before data is propagated.")]
        public int BufferSize { get; set; } = 32;
        public override IObservable<ExternalDataFrame> Generate()
        {
            var bufferSize = BufferSize;
            return DeviceManager.GetDevice(DeviceName).SelectMany(
                deviceInfo =>
                {
                    var rhythmInfo = (RhythmDeviceInfo)deviceInfo;
                    var chipIds = rhythmInfo.ChipIds;
                    var frameData = rhythmInfo.Context.GetDeviceFrames(rhythmInfo.DeviceAddress);
                    return Observable.Create<ExternalDataFrame>(observer =>
                    {
                        int activeStreams = 0;
                        foreach (var chipId in chipIds)
                        {
                            try
                            {
                                activeStreams += HeadstageData.GetStreamsPerChip(chipId);
                            }
                            catch (InvalidEnumArgumentException ex)
                            {
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
