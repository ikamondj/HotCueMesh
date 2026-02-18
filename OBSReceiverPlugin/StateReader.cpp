#include "StateReader.hpp"

#include <atomic>
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

nlohmann::json build_obs_state_json() {
    // TODO: fill with real OBS state later
    return nlohmann::json{
        {"ok", true},
        {"note", "stub"}
    };
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
