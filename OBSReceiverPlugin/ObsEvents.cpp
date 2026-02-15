#include "ObsEvents.hpp"
#include <obs-module.h>



void on_hot_cue_event(const std::string& event) {
    // For now, just log the event. In a real implementation, this would likely
    // trigger some action in OBS.
    blog(LOG_INFO, "[hot-cue-mesh] received event: %s", event.c_str());
}
