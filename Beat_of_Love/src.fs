// ==========================================================
// "Beat of Love"
// -- A lovely toon-style beating heart for anniversary of love at 2022/05/08
// 
// Written by Rrrrichard(Zhehao Li) 
// in a "2-hour shader challenge" on 2022/05/08 
// (Actually used about 3 hours)
//
// Copyright Zhehao Li, 2022 
// You cannot use this work in any commercial product, 
// website or project. You cannot sell this Work 
// and you cannot mint NFTs of it.
// 
// ==========================================================

#define MAX_STEPS 300
//#define MAX_DIST 10. --> amazing effect with the shape of SDF
#define MAX_DIST 1e10
#define EPS .01


float smoothAbs(float x, float alpha)
{
    // softmax for soften sharp features 
    float t1 = exp(alpha *x);
    float t2 = exp(-alpha *x);
    return x*(t1 - t2) / (t1+t2);
}

float spike(float x, float center, float sigma)
{
    // a Guass normal function, not used 
    return 1. / sigma * exp(- pow((x-center)/sigma, 2.));
}

float GetSceneDistance(vec3 point, out int obj)
{
    vec4 sphere = vec4(0, 1.5, 6, 1.3); // (xyz, radius)
    float bounce_freq = 9.;
    sphere.w += 0.1*sin(bounce_freq * iTime);
    
    point = vec3(
        0.95 * point.x, 
        //1.1*point.y - abs(point.x) *(25. - point.x) / 50.,
        1.1 *point.y - smoothAbs(point.x, 6.) *(25. - point.x) / 50.,
        0.8 * point.z);  
        
    float sphere_dist = length(point - sphere.xyz)-sphere.w;
   
    float ground_dist =  10. - point.z; // distance to background wall 
    
    float d =min(sphere_dist, ground_dist);
    
    // distance to center 
    float c= (point.x*point.x + point.y*point.y);
    
    float eps = 0.01;
    if( abs(sphere_dist - d) < eps)
        obj = 1; // heart 
    else
        obj = 0; // wall
        
    if(obj == 0)
    {
        //point.z += 0.01* sin(dist_to_center);
        //d+= 0.1*pow(0.99, c) *sin(0.3*c- bounce_freq * iTime);
        
        d+= 0.3*pow(0.95, c) *
                sin(0.3*c- bounce_freq * iTime);
        
        //d+= 10. * spike(c, 1., 0.4);
     }
        
    
    return d; 
}

float RayMarch(vec3 ray_origin, vec3 ray_dir, out int obj)
{
    float d = 0.; 
    for(int i = 0; i < MAX_STEPS; i++)
    {
        vec3 p = ray_origin + ray_dir * d;
        float ds = GetSceneDistance(p, obj); 
        d += ds; 
        if(d > MAX_DIST || ds < EPS)
            break;  // hit object or out of scene
    }
    return d; 
}

vec3 GetNormal(vec3 point)
{
    int obj_nouse;
    float d = GetSceneDistance(point, obj_nouse); 
    vec2 e = vec2(0.01, 0); 
    vec3 n = d - vec3(
        GetSceneDistance(point - e.xyy, obj_nouse),
        GetSceneDistance(point - e.yxy,  obj_nouse),
        GetSceneDistance(point - e.yyx, obj_nouse)
    );
    
    return normalize(n); 
}

float smoothStep(float edge0, float edge1, float x)
{
    
    if (x < edge0)
        return 0.;
    if (x > edge1)
        return 1.;
    if(edge1 == edge0)
        return 0.; 
    x = (x - edge0) / (edge1 - edge0);
    return x*x * (3.-2.*x);
}

vec3 GetLight(bool is_ground, vec3 point, vec3 light_pos, vec3 light_col, vec3 camera_pos)
{    
    vec3 to_light = normalize(light_pos - point); 
    vec3 normal = GetNormal(point); 
        
    //======== 1. diffuse ==========
    bool in_shadow = false; 
    float NdotL = dot(to_light, normal);
    float diffuse = NdotL > 0. ? 1. : 0.;
    if(is_ground)
    {
        float NdotL_smooth = smoothStep(0.4, 1.0, NdotL);
        diffuse = NdotL > 0.4 ? 1.0 : 0.5;
    }
        
    // Shoot a ray towards light 
    int obj_nouse;
    float d = RayMarch(point+normal*2.*EPS, to_light, obj_nouse);
    
    if(is_ground)
        diffuse *= 0.4;
    // Shadow 
    if (d < length(light_pos - point))
    {
        in_shadow = true;
        diffuse *= 0.3;
    }        
    
    // ======== 2. ambient ===========
    float ambient = 0.3; 
    if (is_ground)
         ambient = 0.1;
    
    // ======== 3. specular ===========
    vec3 to_camera = normalize(camera_pos - point);
    vec3 half_vector = normalize(to_camera + to_light);
    float NdotH = dot(normal, half_vector);
    
    float glossiness = 20.; 
    if (is_ground)
        glossiness = 2.;
    float specular = pow(NdotH, glossiness * glossiness);
    float specular_smooth = smoothStep(0.005, 0.01, specular);
    
    // ======== 4. rim light ===========
    float scale = 0.2;
    float rimDot = pow(NdotL, scale)* (1. - clamp(dot(to_camera, normal), 0., 1.));
    float rimAmount = 0.616; 
    if (is_ground)
        rimAmount = 0.1;
    float rim = 0.5 *  smoothStep(rimAmount - 0.01, rimAmount+0.01, rimDot);
    
    // ======== Final light ==========
    float intensity = 0.8; 
    
    float sum = 0.;
    
    if(in_shadow)
        if(is_ground)
            sum = (diffuse + ambient + 0.5 *(specular_smooth ));
            // not add rim here
        else
            sum = (diffuse + ambient);
    else
        sum = (diffuse + ambient + specular_smooth + rim);
    
    vec3 light = intensity * sum * light_col ;
    
    return light;
}

void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    // Normalized pixel coordinates (from -0.5 to 0.5)
    vec2 uv = (fragCoord - .5*iResolution.xy)/iResolution.x;

    // Time varying pixel color
    vec3 ray_origin = vec3(0., 1.5, -1);
    vec3 ray_dir = normalize(vec3(uv.x, uv.y, 1.));
    
    // light
    vec3 light_pos = vec3(0.8, 5., -2); 
    
    vec3 light_col = vec3(255.,25.,53.) / 255.;
    //vec3 light_col = vec3(240.,139.,113.) / 255.; lovely pink-yellow
    //vec3 light_col = vec3(240.,179.,113.) / 255.; lovely white-yellow
    //vec3 ground_col = vec3(120.,19.,27.) / 255.;
    vec3 ground_col = vec3(250,59.,103.) / 255.;
    //vec3 ground_col = vec3(150,59.,203.) / 255.; a little purple
    
    float spin = 1.;
    float range = 10.;
    vec3 light_pos2 = light_pos + range* vec3(sin(spin *iTime), cos(spin *iTime), 0.);
    
    // 
    int obj = 0; 
    float d = RayMarch(ray_origin, ray_dir, obj);
    
    vec3 point = ray_origin + d * ray_dir; 
   
    vec3 col; 
    if(obj == 1)
        col = GetLight(false, point, light_pos, light_col, ray_origin);
    else
        col = GetLight(true, point, light_pos, ground_col, ray_origin);
    
    col = pow(col, vec3(0.4545)); // Gamma correction
    
    // Output to screen
    fragColor = vec4(col,1.0);
}