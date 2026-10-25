// Small noise helpers shared by the sky and planet shaders (Built-in Render Pipeline).
#ifndef RUMRAKET_NOISE_INCLUDED
#define RUMRAKET_NOISE_INCLUDED

float rk_hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float3 rk_hash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

// Smooth value noise, 0..1
float rk_noise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = rk_hash13(i);
    float n100 = rk_hash13(i + float3(1, 0, 0));
    float n010 = rk_hash13(i + float3(0, 1, 0));
    float n110 = rk_hash13(i + float3(1, 1, 0));
    float n001 = rk_hash13(i + float3(0, 0, 1));
    float n101 = rk_hash13(i + float3(1, 0, 1));
    float n011 = rk_hash13(i + float3(0, 1, 1));
    float n111 = rk_hash13(i + float3(1, 1, 1));

    return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
}

// Layered noise, roughly 0..0.97 with the middle around 0.48
float rk_fbm(float3 p)
{
    float sum = 0.0;
    float amp = 0.5;
    for (int k = 0; k < 5; k++)
    {
        sum += amp * rk_noise(p);
        p = p * 2.02 + 17.3;
        amp *= 0.5;
    }
    return sum;
}

#endif
