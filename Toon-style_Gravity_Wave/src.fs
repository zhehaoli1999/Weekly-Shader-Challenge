// ==========================================================
// Written by Rrrrichard(Zhehao Li) 
// in a "2-hour shader challenge" on 2022/05/02 
// ==========================================================

#define MAX_STEPS 300
//#define MAX_DIST 10. --> amazing effect with the shape of SDF
#define MAX_DIST 1e10
#define EPS .001

float GetSceneDistance(vec3 point, out int obj)
{
    vec4 sphere = vec4(0, 1.5, 6, 1.); // (xyz, radius)
    vec4 sphere2 = vec4(0., 1.5, 6, 0.38); 
    vec4 sphere3 = vec4(0., 1.5, 6, 0.1);
    vec4 sphere4 = vec4(0., 1.5, 6, 0.2);
    
    float rotate_r2 = 2.0;
    float rotate_r3 = 1.4;
    float rotate_r4 = 1.7;
    
    float bounce_frequen = 4.;
    sphere += vec4(0., 0.1 *sin(bounce_frequen * iTime), 0., 0.);
    
    sphere2 += vec4(rotate_r2 *sin(iTime), 
                    0.8*sin(iTime),
                    rotate_r2 *cos(iTime),
                    0.);
                    
    float delta = 1.5;
    sphere3 += vec4(rotate_r3 *sin(iTime+delta), 
                    0.5*sin(iTime+delta),
                    rotate_r3 *cos(iTime+delta),
                    0.);
    sphere4 += vec4(rotate_r4 *sin(iTime+2.*delta), 
                    -0.6*sin(iTime+2.*delta),
                    rotate_r4 *cos(iTime+2.*delta),
                    0.);
    
    float sphere_dist = length(point - sphere.xyz)-sphere.w;
    float sphere_dist2 = length(point - sphere2.xyz)-sphere2.w;
    float sphere_dist3 = length(point - sphere3.xyz)-sphere3.w;
    float sphere_dist4 = length(point - sphere4.xyz)-sphere4.w;
    float ground_dist =  point.y;
    
    float t = length(point.xz - sphere.xz);
    float t2 = length(point.xyz - sphere2.xyz);
    float t3 = length(point.xyz - sphere3.xyz);
    float t4 = length(point.xyz - sphere4.xyz);
    bool is_background_obj = false;
    if (t < 15.)
    {
        float wave = 0.2 *pow(0.95, t)*  sin( 4. *pow(0.98, t) * t - bounce_frequen * iTime);
        float wave2 = -10.  *pow(sphere2.w, 3.) * 0.15 *pow(0.8, t)*  sin(4. * t2);
        float wave3 = -5.  *pow(sphere3.w, 3.) *0.15 *pow(0.8, t)*  sin(4. * t3);
        float wave4 = -5.  *pow(sphere4.w, 3.)*0.15 *pow(0.8, t)*  sin(4. * t4);
        //ground_dist += (wave);
        ground_dist += (wave + wave2 + wave3 + wave4);
    }
    //else if (t <20.)
      //  ground_dist =  point.y;
    else
        is_background_obj = true;
    
    
    float d = 
            min(
            min(
            min(
            min(sphere_dist, ground_dist),
            sphere_dist2),
            sphere_dist3),
            sphere_dist4);
    //float d = sphere_dist;
    
    float eps = 0.01;
    if( abs(sphere_dist - d) < eps)
        obj = 1;
    else if( abs(sphere_dist2 - d) < eps)
        obj = 2;
    else if( abs(sphere_dist3 - d) < eps)
        obj = 3;
    else if( abs(sphere_dist4 - d) < eps)
        obj = 4;
    else
        obj = 0; 
        
    if(is_background_obj)
        obj = -1;
    
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

// Toon-style shading
// Reference: https://roystan.net/articles/toon-shader.html
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
    
    // Shadow 
    if (d < length(light_pos - point))
    {
        in_shadow = true;
        diffuse *= 0.;
    }        
    
    // ======== 2. ambient ===========
    float ambient = 0.3; 
    
    // ======== 3. specular ===========
    vec3 to_camera = normalize(camera_pos - point);
    vec3 half_vector = normalize(to_camera + to_light);
    float NdotH = dot(normal, half_vector);
    
    float glossiness = 20.; 
    float specular = pow(NdotH, glossiness * glossiness);
    float specular_smooth = smoothStep(0.005, 0.01, specular);
    
    // ======== 4. rim light ===========
    float scale = 0.2;
    if (is_ground)
        scale = 0.3;
    float rimDot = pow(NdotL, scale)* (1. - clamp(dot(to_camera, normal), 0., 1.));
    float rimAmount = 0.616; 
    float rim = 0.5 *  smoothStep(rimAmount - 0.01, rimAmount+0.01, rimDot);
    
    // ======== Final light ==========
    float intensity = 0.8; 
    
    float sum = 0.;
    if(in_shadow)
        sum = 1.1 * (diffuse + ambient);
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
    vec3 ray_origin = vec3(0, 1.5, -1);
    vec3 ray_dir = normalize(vec3(uv.x, uv.y, 1.));
    
    // light
    vec3 light_pos = vec3(3, 8, -1); 
    vec3 light_col = vec3(60.,179.,113.) / 255.;
    //vec3 ground_col = vec3(120.,19.,27.) / 255.;
    vec3 ground_col = vec3(105,26.,235.) / 255.;
    vec3 light_col2 = vec3(10.,149.,237.) / 255.;
    vec3 light_col3 = vec3(200.,100.,37.) / 255.;
    vec3 light_col4 = vec3(100.,149.,27.) / 255.;
    
    float spin = 4.;
    float range = 2.;
    vec3 light_pos2 = light_pos + range* vec3(sin(spin*iTime), cos(spin *iTime), cos(spin*iTime));
    
    // 
    int obj = 0; 
    float d = RayMarch(ray_origin, ray_dir, obj);
    
    vec3 point = ray_origin + d * ray_dir; 
   
    vec3 col; 
    if(obj == 1)
        col = GetLight(false, point, light_pos2, light_col, ray_origin);
    else if(obj == 2)
        col = GetLight(false, point, light_pos2, light_col2, ray_origin);
    else if(obj == 3)
        col = GetLight(false, point, light_pos2, light_col3, ray_origin);
    else if(obj == 4)
        col = GetLight(false, point, light_pos2, light_col4, ray_origin);
    else if(obj==-1)
        col = ground_col * 0.1;
    else
        col = GetLight(true, point, light_pos, ground_col, ray_origin);
    
    col = pow(col, vec3(0.4545)); // Gamma correction
    
    // Output to screen
    fragColor = vec4(col,1.0);
}