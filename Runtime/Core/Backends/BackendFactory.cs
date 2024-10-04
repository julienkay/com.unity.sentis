using System;
using UnityEngine;

namespace Unity.Sentis
{
    public static class BackendFactory
    {
        public static IBackend CreateBackend(BackendType backendType)
        {
            switch (backendType)
            {
                case BackendType.GPUCompute:
                    return new GPUComputeBackend();
                case BackendType.GPUPixel:
                    return new GPUPixelBackend();
                default:
                    return new CPUBackend();
            }
        }
    }
}
