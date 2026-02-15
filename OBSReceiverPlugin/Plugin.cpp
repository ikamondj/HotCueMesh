// hot-cue-mesh.cpp
#ifdef _WIN32
#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "Ws2_32.lib")
#endif

#include <obs-module.h>

#include <atomic>
#include <mutex>
#include <string>
#include <thread>
#include "Channel.hpp"
#include "ObsEvents.hpp"

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
static constexpr unsigned short kHotCueTcpPort = 7779;
static std::atomic<bool> g_listener_stop{false};
static std::thread g_listener_thread;
#ifdef _WIN32
static std::mutex g_socket_mutex;
static SOCKET g_listener_socket = INVALID_SOCKET;
static SOCKET g_client_socket = INVALID_SOCKET;
#endif

StringChannel g_event_channel;

static void tick_callback(void *param, float seconds)
{
	    std::string event;
    while (g_event_channel.pop(event)) {
	on_hot_cue_event(event);
    }
}


bool obs_module_load(void)
{
    blog(LOG_INFO, "[hot-cue-mesh] module loaded");

    obs_add_tick_callback(tick_callback, nullptr);

    if (g_listener_thread.joinable()) {
#ifdef _WIN32
        g_listener_stop.store(true, std::memory_order_release);
        {
            std::lock_guard<std::mutex> lock(g_socket_mutex);
            if (g_client_socket != INVALID_SOCKET) {
                shutdown(g_client_socket, SD_BOTH);
                closesocket(g_client_socket);
                g_client_socket = INVALID_SOCKET;
            }
            if (g_listener_socket != INVALID_SOCKET) {
                closesocket(g_listener_socket);
                g_listener_socket = INVALID_SOCKET;
            }
        }
#endif
        g_listener_thread.join();
    }
    g_listener_stop.store(false, std::memory_order_release);

    g_listener_thread = std::thread([]() {
#ifdef _WIN32
        WSADATA wsa_data{};
        if (WSAStartup(MAKEWORD(2, 2), &wsa_data) != 0) {
            blog(LOG_ERROR, "[hot-cue-mesh] WSAStartup failed");
            return;
        }

        const SOCKET listener = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        if (listener == INVALID_SOCKET) {
            blog(LOG_ERROR, "[hot-cue-mesh] socket() failed");
            WSACleanup();
            return;
        }
        {
            std::lock_guard<std::mutex> lock(g_socket_mutex);
            g_listener_socket = listener;
        }

        const BOOL reuse_addr = TRUE;
        setsockopt(listener, SOL_SOCKET, SO_REUSEADDR,
                   reinterpret_cast<const char*>(&reuse_addr), sizeof(reuse_addr));

        sockaddr_in addr{};
        addr.sin_family = AF_INET;
        addr.sin_port = htons(kHotCueTcpPort);
        addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);

        if (bind(listener, reinterpret_cast<const sockaddr*>(&addr), sizeof(addr)) == SOCKET_ERROR) {
            blog(LOG_ERROR, "[hot-cue-mesh] bind() failed on 127.0.0.1:%u", kHotCueTcpPort);
            closesocket(listener);
            {
                std::lock_guard<std::mutex> lock(g_socket_mutex);
                if (g_listener_socket == listener) {
                    g_listener_socket = INVALID_SOCKET;
                }
            }
            WSACleanup();
            return;
        }

        if (listen(listener, SOMAXCONN) == SOCKET_ERROR) {
            blog(LOG_ERROR, "[hot-cue-mesh] listen() failed");
            closesocket(listener);
            {
                std::lock_guard<std::mutex> lock(g_socket_mutex);
                if (g_listener_socket == listener) {
                    g_listener_socket = INVALID_SOCKET;
                }
            }
            WSACleanup();
            return;
        }

        blog(LOG_INFO, "[hot-cue-mesh] listening for hot cue events on 127.0.0.1:%u", kHotCueTcpPort);

        char buffer[4096];
        while (!g_listener_stop.load(std::memory_order_acquire)) {
            const SOCKET client = accept(listener, nullptr, nullptr);
            if (client == INVALID_SOCKET) {
                if (g_listener_stop.load(std::memory_order_acquire)) {
                    break;
                }
                continue;
            }
            {
                std::lock_guard<std::mutex> lock(g_socket_mutex);
                g_client_socket = client;
            }

            const DWORD recv_timeout_ms = 250;
            setsockopt(client, SOL_SOCKET, SO_RCVTIMEO,
                       reinterpret_cast<const char*>(&recv_timeout_ms), sizeof(recv_timeout_ms));

            std::string pending_line;
            pending_line.reserve(256);

            while (!g_listener_stop.load(std::memory_order_acquire)) {
                const int bytes_read = recv(client, buffer, sizeof(buffer), 0);
                if (bytes_read == 0) {
                    break;
                }
                if (bytes_read < 0) {
                    const int err = WSAGetLastError();
                    if (err == WSAETIMEDOUT && !g_listener_stop.load(std::memory_order_acquire)) {
                        continue;
                    }
                    break;
                }

                size_t segment_start = 0;
                for (int i = 0; i < bytes_read; ++i) {
                    if (buffer[i] != '\n') {
                        continue;
                    }

                    pending_line.append(buffer + segment_start, static_cast<size_t>(i) - segment_start);
                    if (!pending_line.empty() && pending_line.back() == '\r') {
                        pending_line.pop_back();
                    }

                    if (!pending_line.empty() && !g_event_channel.push(std::move(pending_line))) {
                        g_listener_stop.store(true, std::memory_order_release);
                        break;
                    }

                    pending_line.clear();
                    segment_start = static_cast<size_t>(i) + 1;
                }

                if (g_listener_stop.load(std::memory_order_acquire)) {
                    break;
                }

                if (segment_start < static_cast<size_t>(bytes_read)) {
                    pending_line.append(buffer + segment_start, static_cast<size_t>(bytes_read) - segment_start);
                }
            }

            if (!g_listener_stop.load(std::memory_order_acquire) && !pending_line.empty()) {
                if (pending_line.back() == '\r') {
                    pending_line.pop_back();
                }
                if (!pending_line.empty() && !g_event_channel.push(std::move(pending_line))) {
                    g_listener_stop.store(true, std::memory_order_release);
                }
            }

            {
                std::lock_guard<std::mutex> lock(g_socket_mutex);
                if (g_client_socket == client) {
                    closesocket(client);
                    g_client_socket = INVALID_SOCKET;
                }
            }
        }
        {
            std::lock_guard<std::mutex> lock(g_socket_mutex);
            if (g_listener_socket != INVALID_SOCKET) {
                closesocket(g_listener_socket);
                g_listener_socket = INVALID_SOCKET;
            }
        }
        WSACleanup();
#else
        blog(LOG_WARNING, "[hot-cue-mesh] TCP listener thread is only implemented for Windows builds");
#endif
    });

    return true;
}



void obs_module_unload(void)
{
    // Stop timer
    obs_remove_tick_callback(tick_callback, nullptr);
#ifdef _WIN32
    g_listener_stop.store(true, std::memory_order_release);
    {
        std::lock_guard<std::mutex> lock(g_socket_mutex);
        if (g_client_socket != INVALID_SOCKET) {
            shutdown(g_client_socket, SD_BOTH);
            closesocket(g_client_socket);
            g_client_socket = INVALID_SOCKET;
        }
        if (g_listener_socket != INVALID_SOCKET) {
            closesocket(g_listener_socket);
            g_listener_socket = INVALID_SOCKET;
        }
    }
#endif
    if (g_listener_thread.joinable()) {
        g_listener_thread.join();
    }

    blog(LOG_INFO, "[hot-cue-mesh] module unloaded");
}
