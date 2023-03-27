#define RADIX 8
#define BUCKET_SIZE 256 // 2 ^ RADIX
#define DATA_BLOCK_SIZE 32 
#define BLOCK_SIZE 256 // DATA_BLOCK_SIZE * 8
#define THREADS_PER_BLOCK 1024
#define WARP_SIZE 32

#define XSHIFT  20
#define YSHIFT  10
#define ZSHIFT  0

#define MAX_FLOAT 0x7F7FFFFF

struct AABB
{
    float3 min;
    float _dummy0;
    float3 max;
    float _dummy1;
};
