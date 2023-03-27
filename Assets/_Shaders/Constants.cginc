#define RADIX 8
#define BUCKET_SIZE 256 // 2 ^ RADIX
#define BLOCK_SIZE 32
#define THREADS_PER_BLOCK 1024
#define WARP_SIZE 32

#define MAX_FLOAT 0x7F7FFFFF

struct AABB
{
    float3 min;
    float _dummy0;
    float3 max;
    float _dummy1;
};
