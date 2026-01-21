// ImHex pattern for Prefab Type 11 (water)
struct Vector3 {
    float x;
    float y;
    float z;
};

u32 PrefabType @ 0x00;
Vector3 StartPos @ 0x04;
Vector3 EndPos @ 0x10;
u32 Unknown_0x1C @ 0x1C; // unknown
float Culling_distance @ 0x20;
u32 Bitmask_at_0x24 @ 0x24;
u8 View_distance_ratio @ 0x28; // seems to always be 100(%)
u8 Lod_distance_ratio @ 0x29; // seems to always be 100(%)
u8 byte_at_0x2A @ 0x2A; // unknown
u8 Flag @ 0x2B; // River type?
u32 u32_at_0x2F_id__or_more_flags_maybe @ 0x2F; // unknown data
float float_at_0x33 @ 0x33; // unknown data
float float_at_0x37 @ 0x37; // unknown data
float quaternion_at_0x3B[0x04] @ 0x3B; // unknown data, might not be a quaternion

float Unknown_at_0x4B @ 0x4B;
float Unknown_at_0x4F @ 0x4F;
float Param_scale_at_0x53 @ 0x53;
float Z_offset_at_0x57 @ 0x57;
float Unknown_at_0x5B @ 0x5B; // unknown data
float Tile_step_at_0x5F @ 0x5F;
float Texture_scale_at_0x63[0x02] @ 0x63; // uv-scale

u32 ArrayCount1 @ 0x6B;
float Unknown_Depth_Maybe @ 0x6F;
float Speed @ 0x73;
u32 ArrayCount2 @ 0x77;
Vector3 SegmentDataPoints[ArrayCount1] @ 0x7B;
Vector3 BorderDataPoints[ArrayCount2] @ 0x7B + (ArrayCount1 * 12);