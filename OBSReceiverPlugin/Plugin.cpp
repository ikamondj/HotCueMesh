// hot-cue-mesh.cpp
#include <obs-module.h>

#include <string>
#include <thread>

OBS_DECLARE_MODULE()
OBS_MODULE_USE_DEFAULT_LOCALE("hot-cue-mesh", "en-US")



const char* obs_module_name(void)
{
    return "Hot Cue Mesh";
}

const char* obs_module_description(void)
{
    return "Receives events from virtual DJ hot cues.";
}

static float g_accum = 0.0f;

static void tick_callback(void *param, float seconds)
{

}

bool obs_module_load(void)
{
    blog(LOG_INFO, "[hot-cue-mesh] module loaded");

    obs_add_tick_callback(tick_callback, nullptr);
    return true;
}



void obs_module_unload(void)
{
    // Stop timer
    obs_remove_tick_callback(tick_callback, nullptr);

    blog(LOG_INFO, "[hot-cue-mesh] module unloaded");
}
