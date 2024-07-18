# Create an engine to run a model

To run a model, you need to create a worker. A worker is the engine that breaks the model down into executable tasks and schedules the tasks to run on a backend, usually the GPU or CPU.

A worker is an instance of an [`IWorker`](xref:Unity.Sentis.IWorker) object.

## Create a Worker

Use [`WorkerFactory.CreateWorker`](xref:Unity.Sentis.ModelAssetExtensions.CreateWorker(Unity.Sentis.ModelAsset,Unity.Sentis.DeviceType,System.Boolean)) to create a worker. You must specify a backend type, which tells Sentis where to run the worker and a [runtime model](import-a-model-file.md#create-a-runtime-model).

For example, the following code creates a worker that runs on the GPU using Sentis compute shaders.

```
using UnityEngine;
using Unity.Sentis;

public class CreateWorker : MonoBehaviour
{
    ModelAsset modelAsset;
    Model runtimeModel;
    IWorker worker;

    void Start()
    {
        runtimeModel = ModelLoader.Load(modelAsset);
        worker = WorkerFactory.CreateWorker(BackendType.GPUCompute, runtimeModel);
    }
}
```

## Backend types

Sentis provides CPU and GPU backend types. To understand how Sentis executes operations using the different backends, refer to [How Sentis runs a model](how-sentis-runs-a-model.md).

If a backend type doesn't support a Sentis layer in a model, the worker will assert. Refer to [Supported ONNX operators](supported-operators.md) for more information.

Among the backend types, `BackendType.GPUCompute`, `BackendType.GPUCommandBuffer`, and `BackendType.CPU` are the fastest. Only resort to using `BackendType.GPUPixel` if the platform does not support compute shaders. To check if your runtime platform supports compute shaders, use [SystemInfo.supportsComputeShaders](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/SystemInfo-supportsComputeShaders.html).

If you use `BackendType.CPU` with WebGL, Burst compiles to WebAssembly code which might be slow. Refer to [Getting started with WebGL development](https://docs.unity3d.com/Documentation/Manual/webgl-gettingstarted.html) for more information.

The speed of model execution depends on the platform's support for multithreading in Burst or its full support for compute shaders. You can [profile a model](profile-a-model.md) to understand the performance of a model.

## Additional resources

- [Create a runtime model](import-a-model-file.md#create-a-runtime-model)
- [How Sentis runs a model](how-sentis-runs-a-model.md)
- [Supported ONNX operators](supported-operators.md)
- [Run a model](run-a-model.md)
