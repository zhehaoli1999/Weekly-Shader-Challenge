// ===========================================================
// 2-hour shader challenge of Zhehao Li on 2022/05/22
//
// Reference: https://www.shadertoy.com/view/tltXWM#
// The main fbm color generation code belongs to above reference
// ===========================================================


// ============== noise part =================
// From https://www.shadertoy.com/view/tltXWM#
// number of octaves of fbm
#define NUM_NOISE_OCTAVES 10
#define PI 3.141592653

// Precision-adjusted variations of https://www.shadertoy.com/view/4djSRW
float hash(float p) { p = fract(p * 0.011); p *= p + 7.5; p *= p + p; return fract(p); }

float noise(vec3 x) {
    const vec3 step = vec3(110, 241, 171);
    vec3 i = floor(x);
    vec3 f = fract(x);
    float n = dot(i, step);
    vec3 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix( hash(n + dot(step, vec3(0, 0, 0))), hash(n + dot(step, vec3(1, 0, 0))), u.x),
                   mix( hash(n + dot(step, vec3(0, 1, 0))), hash(n + dot(step, vec3(1, 1, 0))), u.x), u.y),
               mix(mix( hash(n + dot(step, vec3(0, 0, 1))), hash(n + dot(step, vec3(1, 0, 1))), u.x),
                   mix( hash(n + dot(step, vec3(0, 1, 1))), hash(n + dot(step, vec3(1, 1, 1))), u.x), u.y), u.z);
}


// fbm code from: https://iquilezles.org/articles/fbm/
float fbm( vec3 x)
{   
    float H = 1.0;
    float G = exp2(-H);
    float f = 1.0;
    float a = 0.6;
    float t = 0.0;
    for( int i=0; i<NUM_NOISE_OCTAVES; i++ )
    {
        t += a*noise(f*x);
        f *= 2.0;
        f += 0.;
        a *= G;
    }
    return t;
}

float fbm2(vec3 x) {
	float v = 0.0;
	float a = 0.5;
	vec3 shift = vec3(100);
	for (int i = 0; i < NUM_NOISE_OCTAVES; ++i) {
		v += a * noise(x);
		x = x * 2.0 + shift;
		a *= 0.5;
	}
	return v;
}

// returns max of a single vec3
float max3 (vec3 v) {
  return max (max (v.x, v.y), v.z);
}

vec3 fbmColorSimple(vec3 point)
{
    return vec3(0.0);
}

vec3 fbmColor(vec3 point)
{
    vec3 sphere = vec3(0, 1., 6);
    vec3 r_to_c = point - sphere;
    float theta = iTime * 0.15;  
    mat3 rot = mat3(
        cos(theta), 0, sin(theta),	// column 1
        0, 1, 0,	                // column 2
        -sin(theta), 0, cos(theta)	// column 3
    );
    float alpha = PI / 3.;
    mat3 rot2 = mat3(
      cos(alpha), sin(alpha ),0 ,	                
      -sin(alpha), cos(alpha ),0,	
      0, 0, 1
    );
    r_to_c =  rot2 * rot * r_to_c; 
    point = sphere + r_to_c; 
    
    vec3 q = vec3(0.0);
    vec3 r = vec3(0.0);
	float v = 0.0;
    vec3 color = vec3(0.0);
    
    // calculate fbm noise (3 steps)
    q = vec3(fbm(point + 0.025*iTime), fbm(point), fbm(point));
    r = vec3(fbm(point + 1.0*q + 0.05*iTime), fbm(point + q), fbm(point + q));
    v = fbm(point + 5.0*r + iTime*0.005);
    
    // convert noise value into color
    // three colors: top - mid - bottom (mid being constructed by three colors)
    vec3 col_top = vec3(1.0, 1.0, 1.0);
    vec3 col_bot = vec3(0.0, 0.0, 0.0);
    vec3 col_mid1 = vec3(0.1, 0.4, 0.0);
    vec3 col_mid2 = vec3(0.3, 0.5, 0.6); //vec3(0.4, 0.4, 0.6);
    vec3 col_mid3 = vec3(0.2, 0.4, 0.9);
    
    // mix mid color based on intermediate results
    vec3 col_mid = mix(col_mid1, col_mid2, clamp(r, 0.0, 1.0));
    col_mid = mix(col_mid, col_mid3, clamp(q, 0.0, 1.0));

    // calculate pos (scaling betwen top and bot color) from v
    float pos = v * 2.0 - 1.0;
    color = mix(col_mid, col_top, clamp(pos, 0.0, 1.0));
    color = mix(color, col_bot, clamp(-pos, 0.0, 1.0));

    // clamp color to scale the highest r/g/b to 1.0
    color = color / max3(color);
      
    // create output color, increase light > 0.5 (and add a bit to dark areas)
    color = (clamp((0.4 * pow(v,3.) + pow(v,2.) + 0.5*v), 0.0, 1.0) * 0.9 + 0.1) * color;
    return color; 
}
//===============================================

//================= Ray Marching Part ===============

#define MAX_STEPS 100
#define MAX_DIST 1e10
#define EPS .01


float GetSceneDistance(vec3 point)
{
    vec4 sphere = vec4(0, 1., 6, 1.); // (xyz, radius)
  
    float sphere_dist = length(point - sphere.xyz)-sphere.w;
    float d = sphere_dist; 
    
    return d; 
}

float RayMarch(vec3 ray_origin, vec3 ray_dir)
{
    float d = 0.; 
    for(int i = 0; i < MAX_STEPS; i++)
    {
        vec3 p = ray_origin + ray_dir * d;
        float ds = GetSceneDistance(p); 
        d += ds; 
        if(d > MAX_DIST || ds < EPS) 
            break;  // hit object or out of scene
    }
    return d; 
}

vec3 GetNormal(vec3 point)
{
    float d = GetSceneDistance(point); 
    vec2 e = vec2(0.01, 0); 
    vec3 n = d - vec3(
        GetSceneDistance(point - e.xyy),
        GetSceneDistance(point - e.yxy),
        GetSceneDistance(point - e.yyx)
    );
    
    return normalize(n); 
}

float GetLight(vec3 point)
{    
    //point = rot * point; 
    vec3 light_pos = vec3(3, 5, -1); 
     
    vec3 to_light = normalize(light_pos - point); 
    vec3 normal = GetNormal(point); 
    
    float intensity = 0.6;
    float diffuse = intensity * clamp(dot(to_light, normal), 0., 1.); 
    
    float ambient = 0.01;
    float d = RayMarch(point+normal*2.*EPS, to_light);
    
    if (d < length(light_pos - point))
        diffuse *= 0.6;
    
    return diffuse + ambient;
}

// =============================================

void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    // Normalized pixel coordinates (from -0.5 to 0.5)
    vec2 uv = (fragCoord - .5*iResolution.xy)/iResolution.x;

    // Time varying pixel color
    vec3 ray_origin = vec3(0, 1, 0);
    vec3 ray_dir = normalize(vec3(uv.x, uv.y, 1.));
   
    float d = RayMarch(ray_origin, ray_dir);
    
    vec3 point = ray_origin + d * ray_dir; 
    
    float diffuse_light = GetLight(point); 
    
    vec3 col = fbmColor(point) * (diffuse_light);
    
    col = pow(col, vec3(0.4545)); // Gamma correction
    // Output to screen
    fragColor = vec4(col, 1.0);
}
