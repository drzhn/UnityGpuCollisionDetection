public static class Constants
{
    public const int THREADS_PER_BLOCK = 1024;
    
    public const int DATA_BLOCK_SIZE = 32;
    public const int BLOCK_SIZE = DATA_BLOCK_SIZE * 8;
    
    public const int DATA_ARRAY_COUNT = THREADS_PER_BLOCK * BLOCK_SIZE;

    public const int RADIX = 8;
    public const int BUCKET_SIZE = 1 << 8;
}