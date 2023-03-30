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

inline uint mod2(uint v)
{
    return v & 1;
}

uint CalculateCellType(uint x, uint y, uint z)
{
    return mod2(x) + mod2(y) * 2 + mod2(z) * 4;
}

float3 ClampBounds(float3 position)
{
    return float3(
        clamp(position.x, 0.001, 40),
        clamp(position.y, 0.001, 1024),
        clamp(position.z, 0.001, 40)
    );
}