using System;
using System.ComponentModel;
using System.Reactive.Linq;
using Bonsai;
using OpenCV.Net;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Sends analog output data to the acquisition board's DAC outputs.
    /// </summary>
    [Description("Sends analog output data to the acquisition board's DAC outputs.")]
    public class AnalogOutput : Sink<Mat>
    {
        /// <inheritdoc cref = "SingleDeviceFactory.DeviceName"/>
        [TypeConverter(typeof(AnalogOut.NameConverter))]
        [Description(SingleDeviceFactory.DeviceNameDescription)]
        [Category(DeviceFactory.ConfigurationCategory)]
        public string DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the data type used to represent analog samples.
        /// </summary>
        /// <remarks>
        /// If <see cref="AnalogIODataType.U16"/> is selected, each DAC value is represented by an
        /// unsigned 16-bit integer, with 0 V corresponding to the mid-scale value. If <see
        /// cref="AnalogIODataType.S16"/> is selected, each DAC value is represented by a signed,
        /// twos-complement encoded 16-bit integer. In both cases, the output voltage always
        /// corresponds to the DAC's fixed &#177;5 V range. When <see cref="AnalogIODataType.Volts"/>
        /// is selected, 32-bit floating point voltages between -5 and 5 volts are sent directly to
        /// the DACs.
        /// </remarks>
        [Description("The data type used to represent analog samples.")]
        [Category(DeviceFactory.ConfigurationCategory)]
        public AnalogIODataType DataType { get; set; } = AnalogIODataType.U16;

        /// <summary>
        /// Send a matrix of samples to all enabled analog outputs.
        /// </summary>
        /// <remarks>
        /// If a matrix contains multiple samples, they will be written to hardware as quickly as
        /// communication allows. The data within each input matrix must have <see cref="Depth.U16"/> when
        /// <c>DataType</c> is set to <see cref="AnalogIODataType.U16"/>, <see cref="Depth.S16"/> when
        /// <c>DataType</c> is set to <see cref="AnalogIODataType.S16"/>, or <see cref="Depth.F32"/>
        /// when <c>DataType</c> is set to <see cref="AnalogIODataType.Volts"/>.
        /// </remarks>
        /// <param name="source"> A sequence of 16xN sample matrices containing the analog data to write to
        /// channels 0 to 15.</param>
        /// <returns> A sequence of 16xN sample matrices containing the analog data that were written to
        /// channels 0 to 15.</returns>
        public override unsafe IObservable<Mat> Process(IObservable<Mat> source)
        {
            var dataType = DataType;
            return DeviceManager.GetDevice(DeviceName).SelectMany(deviceInfo =>
            {
                var bufferSize = 0;
                var scaleBuffer = default(Mat);
                var transposeBuffer = default(Mat);
                var device = deviceInfo.GetDeviceContext(typeof(AnalogOut));
                return source.Do(data =>
                {
                    if (dataType == AnalogIODataType.U16 && data.Depth != Depth.U16 ||
                        dataType == AnalogIODataType.S16 && data.Depth != Depth.S16 ||
                        dataType == AnalogIODataType.Volts && data.Depth != Depth.F32)
                    {
                        ThrowDataTypeException(data.Depth);
                    }

                    AssertChannelCount(data.Rows);
                    if (bufferSize != data.Cols)
                    {
                        bufferSize = data.Cols;
                        transposeBuffer = bufferSize > 1
                            ? new Mat(data.Cols, data.Rows, data.Depth, 1)
                            : null;
                        if (dataType == AnalogIODataType.Volts)
                        {
                            scaleBuffer = transposeBuffer != null
                                ? new Mat(data.Cols, data.Rows, Depth.U16, 1)
                                : new Mat(data.Rows, data.Cols, Depth.U16, 1);
                        }
                        else scaleBuffer = null;
                    }

                    var outputBuffer = data;
                    if (transposeBuffer != null)
                    {
                        CV.Transpose(outputBuffer, transposeBuffer);
                        outputBuffer = transposeBuffer;
                    }

                    if (scaleBuffer != null)
                    {
                        CV.ConvertScale(outputBuffer, scaleBuffer, 1 / AnalogOut.VoltsPerDivision, AnalogOut.DacMidScale);
                        outputBuffer = scaleBuffer;
                    }
                    else if (dataType == AnalogIODataType.S16)
                    {
                        // twos-complement to offset binary
                        const short Mask = -32768;
                        CV.XorS(outputBuffer, new Scalar(Mask, 0, 0), outputBuffer);
                    }

                    var dataSize = outputBuffer.Step * outputBuffer.Rows;
                    device.Write(outputBuffer.Data, dataSize);
                });
            });
        }

        /// <summary>
        /// Send a 16-element array of values to update all enabled analog outputs.
        /// </summary>
        /// <remarks>
        /// This overload should be used when <c>DataType</c> is set to <see
        /// cref="AnalogIODataType.U16"/> and values represent the raw DAC codes, with the mid-scale
        /// value corresponding to 0 V.
        /// </remarks>
        /// <param name="source"> A sequence of 16x1 element arrays each containing the analog data to write
        /// to channels 0 to 15.</param>
        /// <returns> A sequence of 16x1 element arrays each containing the analog data to write to channels 0
        /// to 15.</returns>
        public IObservable<ushort[]> Process(IObservable<ushort[]> source)
        {
            if (DataType != AnalogIODataType.U16)
                ThrowDataTypeException(Depth.U16);

            return DeviceManager.GetDevice(DeviceName).SelectMany(deviceInfo =>
            {
                var device = deviceInfo.GetDeviceContext(typeof(AnalogOut));
                return source.Do(data =>
                {
                    AssertChannelCount(data.Length);
                    device.Write(data);
                });
            });
        }

        /// <summary>
        /// Send a 16-element array of values to update all enabled analog outputs.
        /// </summary>
        /// <remarks>
        /// This overload should be used when <c>DataType</c> is set to <see
        /// cref="AnalogIODataType.S16"/> and values should be within -32,768 to 32,767, which
        /// correspond to -5.0 to 5.0 volts.
        /// </remarks>
        /// <param name="source"> A sequence of 16x1 element arrays each containing the analog data to write
        /// to channels 0 to 15.</param>
        /// <returns> A sequence of 16x1 element arrays each containing the analog data to write to channels 0
        /// to 15.</returns>
        public IObservable<short[]> Process(IObservable<short[]> source)
        {
            if (DataType != AnalogIODataType.S16)
                ThrowDataTypeException(Depth.S16);

            return DeviceManager.GetDevice(DeviceName).SelectMany(deviceInfo =>
            {
                var device = deviceInfo.GetDeviceContext(typeof(AnalogOut));
                return source.Do(data =>
                {
                    AssertChannelCount(data.Length);
                    var samples = new ushort[data.Length];
                    for (int i = 0; i < samples.Length; i++)
                    {
                        const short Mask = -32768;
                        samples[i] = unchecked((ushort)(data[i] ^ Mask)); // twos-complement to offset binary
                    }

                    device.Write(samples);
                });
            });
        }

        /// <summary>
        /// Send a 16-element array of values to update all enabled analog outputs.
        /// </summary>
        /// <remarks>
        /// This overload should be used when <c>DataType</c> is set to <see
        /// cref="AnalogIODataType.Volts"/> and values should be within -5.0 to 5.0 volts.
        /// </remarks>
        /// <param name="source"> A sequence of 16x1 element arrays each containing the analog data to write
        /// to channels 0 to 15.</param>
        /// <returns> A sequence of 16x1 element arrays each containing the analog data to write to channels 0
        /// to 15.</returns>
        public IObservable<float[]> Process(IObservable<float[]> source)
        {
            if (DataType != AnalogIODataType.Volts)
                ThrowDataTypeException(Depth.F32);

            return DeviceManager.GetDevice(DeviceName).SelectMany(deviceInfo =>
            {
                var device = deviceInfo.GetDeviceContext(typeof(AnalogOut));
                var divisionsPerVolt = 1 / AnalogOut.VoltsPerDivision;
                return source.Do(data =>
                {
                    AssertChannelCount(data.Length);
                    var samples = new ushort[data.Length];
                    for (int i = 0; i < samples.Length; i++)
                    {
                        samples[i] = (ushort)(data[i] * divisionsPerVolt + AnalogOut.DacMidScale);
                    }

                    device.Write(samples);
                });
            });
        }

        static void AssertChannelCount(int channels)
        {
            if (channels != AnalogOut.ChannelCount)
            {
                throw new InvalidOperationException(
                    $"The input data must have exactly {AnalogOut.ChannelCount} channels."
                );
            }
        }

        static void ThrowDataTypeException(Depth depth)
        {
            throw new InvalidOperationException(
                $"Invalid input data type '{depth}' for the specified analog IO configuration."
            );
        }
    }
}
