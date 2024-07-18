using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Sentis
{
    /// <summary>
    /// An interface that provides methods for converting custom tensor data to `BurstTensorData`.
    /// </summary>
    public interface IConvertibleToBurstTensorData
    {
        /// <summary>
        /// Implement this method to convert to `BurstTensorData`.
        /// </summary>
        /// <param name="dstCount">The number of elements.</param>
        /// <returns>Converted `BurstTensorData`.</returns>
        BurstTensorData ConvertToBurstTensorData(int dstCount);
    }

    /// <summary>
    /// An interface that provides Job system dependency fences for the memory resource.
    /// </summary>
    public interface IDependableMemoryResource
    {
        /// <summary>
        /// A read fence job handle. You can use `fence` as a `dependsOn` argument when you schedule a job that reads data. The job will start when the tensor data is ready for read access.
        /// </summary>
        Unity.Jobs.JobHandle fence { get; set; }
        /// <summary>
        /// A write fence job handle. You can use `reuse` as a `dependsOn` argument when you schedule a job that reads data. The job will start when the tensor data is ready for write access.
        /// </summary>
        Unity.Jobs.JobHandle reuse { get; set; }
        /// <summary>
        /// The raw memory pointer for the resource.
        /// </summary>
        unsafe void* rawPtr { get; }
    }

    /// <summary>
    /// Represents Burst-specific internal data storage for a `Tensor`.
    /// </summary>
    public class BurstTensorData : ITensorData, IDependableMemoryResource, IConvertibleToComputeTensorData, IReadableTensorData
    {
        JobHandle m_ReadFence;
        JobHandle m_WriteFence;
        NativeTensorArray m_Array;
        int m_Count;
        bool m_SafeToDispose = true;

        /// <inheritdoc/>
        public BackendType backendType => BackendType.CPU;
        /// <inheritdoc/>
        public int maxCapacity => m_Count;
        /// <summary>
        /// The `NativeTensorArray` managed array containing the `Tensor` data.
        /// </summary>
        public NativeTensorArray array => m_Array;

        /// <inheritdoc/>
        public JobHandle fence { get { return m_ReadFence; } set { m_ReadFence = value; m_WriteFence = value; m_SafeToDispose = false; } }
        /// <inheritdoc/>
        public JobHandle reuse { get { return m_WriteFence; } set { m_WriteFence = JobHandle.CombineDependencies(value, m_WriteFence); m_SafeToDispose = false; } }

        /// <inheritdoc/>
        public unsafe void* rawPtr => m_Array.AddressAt<float>(0);

        /// <summary>
        /// Initializes and returns an instance of `BurstTensorData`, and allocates storage for a tensor with the shape of `shape`.
        /// </summary>
        /// <param name="count">The number of elements.</param>
        /// <param name="clearOnInit">Whether to zero the data on allocation. The default value is `false`.</param>
        public BurstTensorData(int count, bool clearOnInit = false)
        {
            m_Count = count;
            if (m_Count == 0)
                return;
            m_Array = new NativeTensorArray(m_Count, clearOnInit);
        }

        /// <summary>
        /// Initializes and returns an instance of `BurstTensorData` from a `NativeTensorArray`.
        /// </summary>
        /// <param name="data">The elements of the tensor data as a `NativeTensorArray`.</param>
        public BurstTensorData(NativeTensorArray data)
        {
            if (data == null)
            {
                m_Count = 0; m_Array = null;
                return;
            }
            m_Count = data.Length;
            m_Array = data;
        }

        /// <inheritdoc/>
        public ITensorData Clone()
        {
            if (m_Count == 0)
                return new BurstTensorData(0);
            var data = new NativeTensorArray(m_Count);
            NativeTensorArray.Copy(m_Array, data);
            return new BurstTensorData(data);
        }

        /// <summary>
        /// Finalizes the `BurstTensorData`.
        /// </summary>
        ~BurstTensorData()
        {
            if (!m_SafeToDispose)
                D.LogWarning($"Found unreferenced, but undisposed BurstTensorData that potentially participates in an unfinished job and might lead to hazardous memory overwrites");
        }

        /// <summary>
        /// Disposes of the `BurstTensorData` and any associated memory.
        /// </summary>
        public void Dispose()
        {
            // It isn't safe to Complete jobs from a finalizer thread, so
            if (Thread.CurrentThread == CPUBackend.MainThread)
                CompleteAllPendingOperations();
        }

        /// <inheritdoc/>
        public void CompleteAllPendingOperations()
        {
            fence.Complete();
            reuse.Complete();
            m_SafeToDispose = true;
        }

        /// <summary>
        /// Uploads data to internal storage.
        /// </summary>
        /// <param name="data">The data to upload as a native array.</param>
        /// <param name="srcCount">The number of elements to upload.</param>
        /// <typeparam name="T">The data type of the elements.</typeparam>
        public void Upload<T>(NativeArray<T> data, int srcCount) where T : unmanaged
        {
            CompleteAllPendingOperations();

            var numItemToCopy = srcCount;
            var numItemAvailableInData = data.Length;
            Assert.IsTrue(numItemToCopy <= numItemAvailableInData);

            NativeTensorArray.Copy(data, 0, m_Array, 0, numItemToCopy);
        }

        /// <summary>
        /// Returns data from internal storage.
        /// </summary>
        /// <param name="dstCount">The number of elements to download.</param>
        /// <typeparam name="T">The data type of the elements.</typeparam>
        /// <returns>The downloaded data as a native array.</returns>
        public NativeArray<T> Download<T>(int dstCount) where T : unmanaged
        {
            // Download() as optimization gives direct access to the internal buffer
            // thus need to prepare internal buffer for potential writes
            CompleteAllPendingOperations();

            var downloadCount = dstCount;
            Logger.AssertIsTrue(m_Count >= downloadCount, "BurstTensorData.Download.ValueError: cannot download {0} items from tensor of size {1}", downloadCount, m_Count);

            var dest = new NativeArray<T>(downloadCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            NativeTensorArray.Copy(m_Array, 0, dest, 0, downloadCount);
            return dest;
        }

        #if UNITY_2023_2_OR_NEWER
        /// <inheritdoc/>
        public async Awaitable<NativeArray<T>> DownloadAsync<T>(int dstCount) where T : unmanaged
        {
            while (!fence.IsCompleted)
            {
                await Awaitable.NextFrameAsync();
            }
            CompleteAllPendingOperations();
            var dest = new NativeArray<T>(dstCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeTensorArray.Copy(m_Array, 0, dest, 0, dstCount);
            return dest;
        }
        #endif

        /// <inheritdoc/>
        public T Get<T>(int index) where T : unmanaged
        {
            CompleteAllPendingOperations();
            return m_Array.Get<T>(index);
        }

        /// <inheritdoc/>
        public void Set<T>(int index, T value) where T : unmanaged
        {
            CompleteAllPendingOperations();
            m_Array.Set<T>(index, value);
        }

        /// <inheritdoc/>
        public ReadOnlySpan<T> ToReadOnlySpan<T>(int dstCount) where T : unmanaged
        {
            if (m_Count == 0)
                return ReadOnlySpan<T>.Empty;
            CompleteAllPendingOperations();
            return m_Array.AsReadOnlySpan<T>(dstCount);
        }

        /// <inheritdoc/>
        public NativeArray<T>.ReadOnly GetReadOnlyNativeArrayHandle<T>(int dstCount) where T : unmanaged
        {
            if (m_Count == 0)
                return new NativeArray<T>.ReadOnly();
            CompleteAllPendingOperations();
            return m_Array.GetReadOnlyNativeArrayHandle<T>(dstCount);
        }

        /// <inheritdoc/>
        public T[] ToArray<T>(int dstCount) where T : unmanaged
        {
            if (m_Count == 0)
                return Array.Empty<T>();
            CompleteAllPendingOperations();
            return m_Array.ToArray<T>(dstCount);
        }

        /// <inheritdoc/>
        public ComputeTensorData ConvertToComputeTensorData(int count)
        {
            CompleteAllPendingOperations();

            var output = new ComputeTensorData(count);
            if (count == 0)
                return output;

            output.buffer.SetData(array.GetNativeArrayHandle<float>(), 0, 0, count);

            return output;
        }

        /// <inheritdoc/>
        public bool IsReadbackRequestDone()
        {
            if (!fence.IsCompleted)
                return false;
            CompleteAllPendingOperations();
            return true;
        }

        /// <inheritdoc/>
        public void ReadbackRequest() {}

        /// <summary>
        /// Returns a string that represents the `BurstTensorData`.
        /// </summary>
        /// <returns>The string summary of the `BurstTensorData`.</returns>
        public override string ToString()
        {
            return string.Format("(CPU burst: [{0}], uploaded: {1})", m_Array?.Length, m_Count);
        }

        /// <summary>
        /// Moves a tensor into memory on the CPU backend device.
        /// </summary>
        /// <param name="X">The `Tensor` to move to the CPU.</param>
        /// <param name="clearOnInit">Whether to initialize the backend data. The default value is `true`.</param>
        /// <returns>The pinned `BurstTensorData`.</returns>
        public static BurstTensorData Pin(Tensor X, bool clearOnInit = false)
        {
            var onDevice = X.dataOnBackend;
            if (onDevice == null)
            {
                X.AttachToDevice(new BurstTensorData(X.count, clearOnInit));
                return X.dataOnBackend as BurstTensorData;
            }

            if (onDevice is BurstTensorData)
                return onDevice as BurstTensorData;

            BurstTensorData dataOnBackend;
            if (onDevice is IConvertibleToBurstTensorData asConvertible)
            {
                dataOnBackend = asConvertible.ConvertToBurstTensorData(X.count);
            }
            else
            {
                dataOnBackend = new BurstTensorData(X.count, clearOnInit: false);
                dataOnBackend.Upload<int>(onDevice.Download<int>(X.count), X.count);
            }
            X.AttachToDevice(dataOnBackend);

            return X.dataOnBackend as BurstTensorData;
        }
    }
}
