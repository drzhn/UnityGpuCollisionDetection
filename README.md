# Unity GPU collision detection
GPU collision detection prototype written in Unity3D+HLSL.

32.000 spheres, smooth 60 fps.

Implemented article from GPU Gems 3:

[https://developer.nvidia.com/gpugems/gpugems3/part-v-physics-simulation/chapter-32-broad-phase-collision-detection-cuda](https://developer.nvidia.com/gpugems/gpugems3/part-v-physics-simulation/chapter-32-broad-phase-collision-detection-cuda)

![Demo](https://github.com/drzhn/UnityGpuCollisionDetection/blob/master/ice_video_20230330-175441%20-%20Trim.gif?raw=true)

**WARNING**: for GPU sorting I implemented RadixSort where I used new HLSL wave intrinsics for scan stage. So it's obligation to run this project on Nvidia GPUs because of lane size equal to 32. 
