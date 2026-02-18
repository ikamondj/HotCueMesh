#include "StateReader.hpp"

#include <obs-module.h>

#include <atomic>
#include <array>
#include <memory>
#include <mutex>
#include <thread>

#include <httplib.h>
#include <nlohmann/json.hpp>

namespace {
std::mutex g_mu;
std::unique_ptr<httplib::Server> g_srv;
std::unique_ptr<std::thread> g_thr;
std::atomic<bool> g_running{false};

struct SourceFlagName {
    uint32_t flag;
    const char *name;
};

constexpr std::array<SourceFlagName, 17> kSourceFlagNames{{
    {OBS_SOURCE_VIDEO, "OBS_SOURCE_VIDEO"},
    {OBS_SOURCE_AUDIO, "OBS_SOURCE_AUDIO"},
    {OBS_SOURCE_ASYNC, "OBS_SOURCE_ASYNC"},
    {OBS_SOURCE_CUSTOM_DRAW, "OBS_SOURCE_CUSTOM_DRAW"},
    {OBS_SOURCE_INTERACTION, "OBS_SOURCE_INTERACTION"},
    {OBS_SOURCE_COMPOSITE, "OBS_SOURCE_COMPOSITE"},
    {OBS_SOURCE_DO_NOT_DUPLICATE, "OBS_SOURCE_DO_NOT_DUPLICATE"},
    {OBS_SOURCE_DEPRECATED, "OBS_SOURCE_DEPRECATED"},
    {OBS_SOURCE_DO_NOT_SELF_MONITOR, "OBS_SOURCE_DO_NOT_SELF_MONITOR"},
    {OBS_SOURCE_CAP_DISABLED, "OBS_SOURCE_CAP_DISABLED"},
    {OBS_SOURCE_MONITOR_BY_DEFAULT, "OBS_SOURCE_MONITOR_BY_DEFAULT"},
    {OBS_SOURCE_SUBMIX, "OBS_SOURCE_SUBMIX"},
    {OBS_SOURCE_CONTROLLABLE_MEDIA, "OBS_SOURCE_CONTROLLABLE_MEDIA"},
    {OBS_SOURCE_CEA_708, "OBS_SOURCE_CEA_708"},
    {OBS_SOURCE_SRGB, "OBS_SOURCE_SRGB"},
    {OBS_SOURCE_CAP_DONT_SHOW_PROPERTIES, "OBS_SOURCE_CAP_DONT_SHOW_PROPERTIES"},
    {OBS_SOURCE_REQUIRES_CANVAS, "OBS_SOURCE_REQUIRES_CANVAS"},
}};

inline const char *safe_source_name(const obs_source_t *source) {
    const char *name = source ? obs_source_get_name(source) : nullptr;
    return name ? name : "";
}

nlohmann::json::array_t build_source_flags(uint32_t flags) {
    nlohmann::json::array_t source_flags;
    source_flags.reserve(kSourceFlagNames.size());

    for (const auto &entry : kSourceFlagNames) {
        if (flags & entry.flag) {
            source_flags.emplace_back(entry.name);
        }
    }

    return source_flags;
}

struct FilterEnumData {
    nlohmann::json::array_t *filters;
};

void enum_source_filter(obs_source_t *, obs_source_t *filter, void *param) {
    auto *data = static_cast<FilterEnumData *>(param);
    if (!data || !data->filters || !filter) {
        return;
    }

    nlohmann::json filter_json;
    filter_json["name"] = safe_source_name(filter);
    filter_json["enabled"] = obs_source_enabled(filter);
    data->filters->emplace_back(std::move(filter_json));
}

nlohmann::json::array_t build_source_filters(obs_source_t *source) {
    nlohmann::json::array_t filters;
    if (!source) {
        return filters;
    }

    FilterEnumData data{&filters};
    obs_source_enum_filters(source, enum_source_filter, &data);
    return filters;
}

struct SceneItemEnumData {
    nlohmann::json::array_t *sources;
};

bool enum_scene_item(obs_scene_t *, obs_sceneitem_t *item, void *param) {
    auto *data = static_cast<SceneItemEnumData *>(param);
    if (!data || !data->sources || !item) {
        return true;
    }

    obs_source_t *source = obs_sceneitem_get_source(item);
    if (!source) {
        return true;
    }

    nlohmann::json source_json;
    source_json["name"] = safe_source_name(source);
    source_json["sourceFlags"] = build_source_flags(obs_source_get_output_flags(source));
    source_json["filters"] = build_source_filters(source);
    source_json["visible"] = obs_sceneitem_visible(item);

    data->sources->emplace_back(std::move(source_json));
    return true;
}

struct SceneEnumData {
    nlohmann::json::array_t *scenes;
};

bool enum_scene(void *param, obs_source_t *scene_source) {
    auto *data = static_cast<SceneEnumData *>(param);
    if (!data || !data->scenes || !scene_source) {
        return true;
    }

    // Groups are scene-like internals; top-level response should contain scenes.
    if (obs_source_is_group(scene_source)) {
        return true;
    }

    obs_scene_t *scene = obs_scene_from_source(scene_source);
    if (!scene) {
        return true;
    }

    nlohmann::json scene_json;
    scene_json["name"] = safe_source_name(scene_source);

    nlohmann::json::array_t sources;
    SceneItemEnumData scene_item_data{&sources};
    obs_scene_enum_items(scene, enum_scene_item, &scene_item_data);
    scene_json["sources"] = std::move(sources);

    data->scenes->emplace_back(std::move(scene_json));
    return true;
}

nlohmann::json build_obs_state_json() {
    nlohmann::json::array_t scenes;
    SceneEnumData data{&scenes};
    obs_enum_scenes(enum_scene, &data);

    nlohmann::json result;
    result["scenes"] = std::move(scenes);
    return result;
}
} // namespace

void start_state_reader_server(int port) {
    std::lock_guard<std::mutex> lk(g_mu);
    if (g_running.load()) return;

    g_srv = std::make_unique<httplib::Server>();

    g_srv->Get("/obsState", [](const httplib::Request&, httplib::Response& res) {
        nlohmann::json j = build_obs_state_json();
        res.set_content(j.dump(), "application/json");
        res.status = 200;
    });

    // Optional: quick healthcheck (handy for debugging)
    g_srv->Get("/health", [](const httplib::Request&, httplib::Response& res) {
        res.set_content("ok", "text/plain");
        res.status = 200;
    });

    g_running.store(true);
    g_thr = std::make_unique<std::thread>([port]() {
        // listen() blocks until stop() is called
        g_srv->listen("0.0.0.0", port);

        // If listen returns (stop called or error), mark not running.
        g_running.store(false);
    });
}

void stop_state_reader_server() {
    std::unique_ptr<std::thread> thr;
    std::unique_ptr<httplib::Server> srv;

    {
        std::lock_guard<std::mutex> lk(g_mu);
        if (!g_running.load()) {
            // Still clean up if partially started
            if (g_thr && g_thr->joinable()) {
                thr = std::move(g_thr);
            } else {
                g_thr.reset();
            }
            g_srv.reset();
            return;
        }

        srv = std::move(g_srv);
        thr = std::move(g_thr);
        g_running.store(false);
    }

    if (srv) srv->stop();
    if (thr && thr->joinable()) thr->join();
}
